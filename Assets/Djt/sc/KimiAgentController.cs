using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class KimiAgentController : MonoBehaviour
{
    private string apiUrl = "http://localhost:5000/process";
    private RbtAction3D rbtAction;

    private bool isWaitingForResponse = false; // 防止重复请求

    public Vector2 mapRange = new Vector2(-20, 20);      // 地图边界范围
    public Vector2 mapPickRange = new Vector2(-8, 8);    // 资源区范围

    public Transform agentTransform;
    public int carriedObjectCount = 0;

    public GameObject emo_happy, emo_sad;
    RbtAbsorb rbtAbsorb;

    private void Start()
    {
        rbtAction = GetComponent<RbtAction3D>();
        rbtAbsorb = GetComponent<RbtAbsorb>();
    }

    private void Update()
    {
        // 每帧检查是否可以发起新请求
        if (rbtAction != null && rbtAction.isComplete && !isWaitingForResponse)
        {
            
            StartCoroutine(RequestActionFromKimi());
        }

        // 更新携带方块数量
        if (rbtAbsorb != null)
        {
            carriedObjectCount = rbtAbsorb.ObjHasAbsorbed.Count;
        }
    }

    [System.Serializable]
    public class EnvironmentData
    {
        public string agent_position;
        public string map_range;
        public string pick_map_range;
        public string current_goal;
        public int current_cube_num;
    }

    [System.Serializable]
    public class AIResponse
    {
        public string actionType;
        public string targetPosition;
    }

    IEnumerator RequestActionFromKimi()
    {
        isWaitingForResponse = true;

        Vector3 mapVector3 = new Vector3(mapRange.x, -12f, mapRange.y);
        Vector2 agentPos2D = new Vector2(agentTransform.position.x, agentTransform.position.z);

        var env = new EnvironmentData
        {
            agent_position = agentPos2D.ToString("F2"),
            map_range = mapVector3.ToString("F0"),
            pick_map_range = mapPickRange.ToString("F0"),
            current_goal = "拾取与建造，避免重复位置",
            current_cube_num = carriedObjectCount
        };

        string json = JsonUtility.ToJson(env);

        UnityWebRequest req = new UnityWebRequest(apiUrl, "POST");
        byte[] jsonToSend = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var result = JsonUtility.FromJson<AIResponse>(req.downloadHandler.text);
            if (result.actionType == "move")
            {
                string posStr = result.targetPosition.Trim('(', ')');
                string[] parts = posStr.Split(',');


                //吸收
                rbtAction.StartCoroutine(rbtAction.AbsorbAndReset(rbtAction.maxSorbNum));

                //放置
                if (rbtAbsorb.ObjHasAbsorbed.Count > 0 && Mathf.Abs(transform.position.x) > 4 && Mathf.Abs(transform.position.z) > 4)
                {
                    Debug.Log("开始自动放置物体");
                    int i = Random.Range(1, 8);

                    rbtAbsorb.DropObjectFromSorb(i); // 从吸收点放置物体

                    //rbtAction.DropObjectFromSorb();
                }

                if (parts.Length == 2 &&
                    float.TryParse(parts[0], out float x) &&
                    float.TryParse(parts[1], out float z))
                {
                    Vector3 target = new Vector3(x, agentTransform.position.y, z);
                    AIAction action = new AIAction();
                    action.ACinit("move", target);
                    while (Mathf.Abs(target.x) > 200.0f) target.x /= 10.0f;
                    while (Mathf.Abs(target.y) > 200.0f) target.x /= 10.0f;
                    while (Mathf.Abs(target.z) > 200.0f) target.x /= 10.0f;
                    rbtAction.ExecuteAction(action);
                    Debug.Log($"✅ 执行 Kimi 指令移动到: {target}");
                }
                else
                {
                    Debug.LogWarning("⚠️ Kimi 返回的坐标格式错误: " + result.targetPosition);
                }
            }
            else
            {
                Debug.LogWarning("⚠️ Kimi 返回未知动作: " + result.actionType);
            }
        }
        else
        {
            Debug.LogError("❌ Kimi 请求失败: " + req.error);
        }

        isWaitingForResponse = false;
    }
}
