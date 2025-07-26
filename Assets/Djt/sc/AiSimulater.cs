using UnityEngine;
using System.Collections;
using TMPro;
using static UnityEngine.GraphicsBuffer;

public class AiSimulater : MonoBehaviour
{
    public Vector2 mapRange = new Vector2(-20, 20);      // 地图边界范围
    public Vector2 mapPickRange = new Vector2(-8, 8);    // 资源区范围

    public Transform agentTransform;
    public int carriedObjectCount = 0;

    public GameObject emo_happy, emo_sad;
    RbtAction3D rbtAction; // 机器人执行控制脚本
    RbtAbsorb rbtAbsorb; // 机器人吸收控制脚本
    //public bool isWaitingForAIResponse = false;
    bool canSorb = false;

    private void Start()
    {
        rbtAction = GetComponent<RbtAction3D>();
        rbtAbsorb = GetComponent<RbtAbsorb>();
    }

    private void Update()
    {
        carriedObjectCount = rbtAbsorb.ObjHasAbsorbed.Count; // 更新携带物品数量
        if (rbtAction.isComplete)
        {
            if (canSorb)
            {
                rbtAction.StartCoroutine(rbtAction.AbsorbAndReset(rbtAction.maxSorbNum)); // 启动吸收过程并在完成后恢复操作
                canSorb = false; // 吸收后不能立即再次吸收

            }

            RequestNextMove();
        }
        if (rbtAction.isComplete == true)
            Debug.Log("isComplete: " + rbtAction.isComplete);
    }

    /// <summary>
    /// 根据当前状态智能决策下一步移动位置
    /// </summary>
    private void RequestNextMove()
    {
 

        // 情绪显示：根据携带物品数量
        if (carriedObjectCount >= 3)
        {
            emo_happy.SetActive(true);
            emo_sad.SetActive(false);
        }
        else
        {
            emo_happy.SetActive(false);
            emo_sad.SetActive(true);
        }

        Vector3 nextTarget;

        // 决策逻辑
        if (carriedObjectCount == 0)
        {
            // 没有物体 → 移动到资源区
            nextTarget = GetRandomPositionInPickZone();
        }
        else
        {
            // 有物体 → 移动到外围放置
            nextTarget = GetRandomPositionInOuterZone();
        }


        if (rbtAbsorb.ObjHasAbsorbed.Count > 0 && Mathf.Abs(transform.position.x) > 4 && Mathf.Abs(transform.position.z) > 4)
        {
            Debug.Log("开始自动放置物体");
            int i = Random.Range(1, 8);

            rbtAbsorb.DropObjectFromSorb(i); // 从吸收点放置物体

            //rbtAction.DropObjectFromSorb();
        }

        AIAction action = new AIAction();
        action.ACinit("move", nextTarget);
        rbtAction.ExecuteAction(action);
        canSorb=true; // 移动后可以吸收物体
    }

    /// <summary>
    /// 获取资源区内的随机位置
    /// </summary>
    private Vector3 GetRandomPositionInPickZone()
    {
        float x = Random.Range(mapPickRange.x, mapPickRange.y);
        float z = Random.Range(mapPickRange.x, mapPickRange.y);
        return new Vector3(x, agentTransform.position.y,z);
    }

    /// <summary>
    /// 获取外围区内的随机位置（避开资源区）
    /// </summary>
    private Vector3 GetRandomPositionInOuterZone()
    {
        float x = Random.Range(mapRange.x, mapRange.y);
        float z = Random.Range(mapRange.x, mapRange.y);

        // 确保超出资源区边界
        if (Mathf.Abs(x) < mapPickRange.y)
            x += Mathf.Sign(x) * (mapPickRange.y + 2);
        if (Mathf.Abs(z) < mapPickRange.y)
            z += Mathf.Sign(z) * (mapPickRange.y + 2);

        return new Vector3(x, agentTransform.position.y, z);
    }

    /// <summary>
    /// 移动到目标位置
    /// </summary>

}
