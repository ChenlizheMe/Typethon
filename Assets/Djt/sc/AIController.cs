using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIController : MonoBehaviour
{
    public EnvironmentInfo environmentInfo; // 环境信息
    private AIAgent aiAgent; // AI 代理，用于请求 AI 指令
    private RbtAction3D robotAction; // 机器人执行控制脚本
    private RbtAbsorb rbtAbsorb; // 机器人吸收控制脚本
    public bool isWaitingForAIResponse = false; // 是否正在等待AI反馈
    public bool isFollowAi = false; // 是否跟随AI指令执行操作

    private void Awake()
    {
        // 初始化
        aiAgent = GetComponent<AIAgent>(); // 获取 AIAgent
        robotAction = GetComponent<RbtAction3D>(); // 获取 RbtAction3D
        rbtAbsorb = GetComponent<RbtAbsorb>(); // 获取 RbtAbsorb
    }

    private void Update()
    {
        Debug.Log("isComplete为：" + robotAction.isComplete);
        // 当 isComplete 为 true 时请求 AI 指令
        if (robotAction.isComplete && !isWaitingForAIResponse)
        {
            RequestNextActionFromAI(); // 请求下一条指令
        }
    }

    // 请求 AI 获取下一条指令
    private void RequestNextActionFromAI()
    {
        isWaitingForAIResponse = true; // 设置为等待 AI 响应

        Debug.Log("开始尝试吸收。。。");
        robotAction.ExecuteAction(new AIAction
        {
            actionType = "absorb",
            targetPosition = rbtAbsorb.absorptionPoint.position // 吸收点位置
        });

        if (rbtAbsorb.ObjHasAbsorbed.Count > 0 && Mathf.Abs(transform.position.x) > 8 && Mathf.Abs(transform.position.y) > 8)
        {
            Debug.Log("开始自动放置物体");
            int i = Random.Range(1, 8);
            while (i > 0)
            {
               rbtAbsorb.DropObjectFromSorb(5); // 从吸收点放置物体
                i--;
            }
           // robotAction.DropObjectFromSorb();
        }
        environmentInfo.UpdateEnvironmentInfo(); // 更新环境信息
        aiAgent.RequestActionFromAI(environmentInfo); // 请求 AI 提供下一条指令
    }

    // 接收 AI 返回的指令并分发给机器人执行
    public void OnReceivedAIAction(AIAction action)
    {
        if (action != null)
        {
            // 执行机器人动作
            Debug.Log($"AI指令：{action.actionType}，目标位置：{action.targetPosition}");
            robotAction.ExecuteAction(action); // 分发给机器人执行
        }
        else
        {
            Debug.LogWarning("AI 返回的指令为空！");
        }

        isWaitingForAIResponse = false; // 完成 AI 指令获取
    }
}
