using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;

public class AIAgent : MonoBehaviour
{
    private string apiUrl = "http://127.0.0.1:5000/process";  // Flask API URL
    private RbtAction3D robotAction; // 机器人执行控制脚本
    AIController AIC;

    private void Start()
    {
        // 获取 RbtAction3D 组件
        robotAction = GetComponent<RbtAction3D>();
        AIC = GetComponent<AIController>();
        if (robotAction == null)
        {
            Debug.LogError("未找到 RbtAction3D 脚本！");
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
            // 将环境信息构建为 JSON 数据
            var jsonContent = new
            {
                agent_position = new { x = environmentInfo.agentPosition.x, y = environmentInfo.agentPosition.y, z = environmentInfo.agentPosition.z },
                map_range = new { x = environmentInfo.mapRange.x, y = environmentInfo.mapRange.y },
                //pick_map_range = environmentInfo.pickMapRange,
                current_goal = environmentInfo.currentGoal,
                current_cube_num = environmentInfo.cubeNum,

            };

            // 序列化 JSON 内容
            string json = JsonConvert.SerializeObject(jsonContent);
            Debug.Log("构建的环境json:" + json);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // 发送 POST 请求
            var task = client.PostAsync(apiUrl, content);
            while (!task.IsCompleted) yield return null;

            HttpResponseMessage response = task.Result;
            if (response.IsSuccessStatusCode)
            {
                string result = response.Content.ReadAsStringAsync().Result;
                Debug.Log("AI Response: " + result);  // 打印出返回结果
                AIC.isWaitingForAIResponse = false;

                // 解析响应结果
                // 假设返回的是一个JSON字符串，里面有一个"action"字段包含指令
                var jsonResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(result);

                // 提取AI回复的内容
                string actionType = jsonResponse["actionType"].ToString();
                Vector3 targetPosition = Vector3.zero;

                if (actionType == "move")
                {
                    Debug.Log("提取到move指令，开始执行");
                    // 解析 "move, (x, y)" 格式的指令
                    string positionStr = jsonResponse["targetPosition"].ToString().Trim('(', ')');
                    string[] positionParts = positionStr.Split(',');

                    if (positionParts.Length == 2)
                    {
                        float x = float.Parse(positionParts[0].Trim());
                        float y = float.Parse(positionParts[1].Trim());
                        targetPosition = new Vector3(Mathf.Abs( x%50), 0f, Mathf.Abs(y %50));
                    }
                }

                // 创建并执行 AI 指令
                AIAction action = new AIAction();
                action.ACinit(actionType, targetPosition);
                robotAction.ExecuteAction(action);
            }
            else
            {
                Debug.LogError("请求失败，状态码: " + response.StatusCode);
            }
        }
    }


    private void HandleAIResponse(string response)
    {
        // 解析 AI 返回的指令
        if (!string.IsNullOrEmpty(response))
        {
            string actionType = string.Empty;
            Vector3 targetPosition = Vector3.zero;

            // 解析指令，例如 "move, (x, y)"
            if (response.StartsWith("move"))
            {
                string positionStr = response.Substring("move, ".Length).Trim();
                positionStr = positionStr.Trim('(', ')');  // 去掉括号
                string[] positionParts = positionStr.Split(',');

                if (positionParts.Length == 2)
                {
                    // 将字符串分割成坐标
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

            // 创建并执行 AI 指令
            AIAction action = new AIAction();
            action.ACinit(actionType, targetPosition);
            robotAction.ExecuteAction(action);
        }
    }
}
