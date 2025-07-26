using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;

public class AIAgent : MonoBehaviour
{
    private string apiUrl = "http://127.0.0.1:5000/process";  // Flask API URL
    private RbtAction3D robotAction; // ������ִ�п��ƽű�
    AIController AIC;

    private void Start()
    {
        // ��ȡ RbtAction3D ���
        robotAction = GetComponent<RbtAction3D>();
        AIC = GetComponent<AIController>();
        if (robotAction == null)
        {
            Debug.LogError("δ�ҵ� RbtAction3D �ű���");
        }
    }

    public void RequestActionFromAI(EnvironmentInfo environmentInfo)
    {
        StartCoroutine(SendRequestToAI(environmentInfo));
    }

    private IEnumerator SendRequestToAI(EnvironmentInfo environmentInfo)
    {
        using (HttpClient client = new HttpClient())
        {
            // ��������Ϣ����Ϊ JSON ����
            var jsonContent = new
            {
                agent_position = new { x = environmentInfo.agentPosition.x, y = environmentInfo.agentPosition.y, z = environmentInfo.agentPosition.z },
                map_range = new { x = environmentInfo.mapRange.x, y = environmentInfo.mapRange.y },
                //pick_map_range = environmentInfo.pickMapRange,
                current_goal = environmentInfo.currentGoal,
                current_cube_num = environmentInfo.cubeNum,

            };

            // ���л� JSON ����
            string json = JsonConvert.SerializeObject(jsonContent);
            Debug.Log("�����Ļ���json:" + json);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // ���� POST ����
            var task = client.PostAsync(apiUrl, content);
            while (!task.IsCompleted) yield return null;

            HttpResponseMessage response = task.Result;
            if (response.IsSuccessStatusCode)
            {
                string result = response.Content.ReadAsStringAsync().Result;
                Debug.Log("AI Response: " + result);  // ��ӡ�����ؽ��
                AIC.isWaitingForAIResponse = false;

                // ������Ӧ���
                // ���践�ص���һ��JSON�ַ�����������һ��"action"�ֶΰ���ָ��
                var jsonResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(result);

                // ��ȡAI�ظ�������
                string actionType = jsonResponse["actionType"].ToString();
                Vector3 targetPosition = Vector3.zero;

                if (actionType == "move")
                {
                    Debug.Log("��ȡ��moveָ���ʼִ��");
                    // ���� "move, (x, y)" ��ʽ��ָ��
                    string positionStr = jsonResponse["targetPosition"].ToString().Trim('(', ')');
                    string[] positionParts = positionStr.Split(',');

                    if (positionParts.Length == 2)
                    {
                        float x = float.Parse(positionParts[0].Trim());
                        float y = float.Parse(positionParts[1].Trim());
                        targetPosition = new Vector3(Mathf.Abs( x%50), 0f, Mathf.Abs(y %50));
                    }
                }

                // ������ִ�� AI ָ��
                AIAction action = new AIAction();
                action.ACinit(actionType, targetPosition);
                robotAction.ExecuteAction(action);
            }
            else
            {
                Debug.LogError("����ʧ�ܣ�״̬��: " + response.StatusCode);
            }
        }
    }


    private void HandleAIResponse(string response)
    {
        // ���� AI ���ص�ָ��
        if (!string.IsNullOrEmpty(response))
        {
            string actionType = string.Empty;
            Vector3 targetPosition = Vector3.zero;

            // ����ָ����� "move, (x, y)"
            if (response.StartsWith("move"))
            {
                string positionStr = response.Substring("move, ".Length).Trim();
                positionStr = positionStr.Trim('(', ')');  // ȥ������
                string[] positionParts = positionStr.Split(',');

                if (positionParts.Length == 2)
                {
                    // ���ַ����ָ������
                    float x = float.Parse(positionParts[0].Trim());
                    float y = float.Parse(positionParts[1].Trim());
                    targetPosition = new Vector3(x, 0f, y);
                    actionType = "move";
                }
            }
            else if (response == "pick")
            {
                actionType = "pick";
            }
            else if (response == "build")
            {
                actionType = "build";
            }
            else if (response == "throw")
            {
                actionType = "throw";
            }

            // ������ִ�� AI ָ��
            AIAction action = new AIAction();
            action.ACinit(actionType, targetPosition);
            robotAction.ExecuteAction(action);
        }
    }
}
