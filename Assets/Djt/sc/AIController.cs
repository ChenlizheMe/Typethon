using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIController : MonoBehaviour
{
    public EnvironmentInfo environmentInfo; // ������Ϣ
    private AIAgent aiAgent; // AI ������������ AI ָ��
    private RbtAction3D robotAction; // ������ִ�п��ƽű�
    private RbtAbsorb rbtAbsorb; // ���������տ��ƽű�
    public bool isWaitingForAIResponse = false; // �Ƿ����ڵȴ�AI����
    public bool isFollowAi = false; // �Ƿ����AIָ��ִ�в���

    private void Awake()
    {
        // ��ʼ��
        aiAgent = GetComponent<AIAgent>(); // ��ȡ AIAgent
        robotAction = GetComponent<RbtAction3D>(); // ��ȡ RbtAction3D
        rbtAbsorb = GetComponent<RbtAbsorb>(); // ��ȡ RbtAbsorb
    }

    private void Update()
    {
        Debug.Log("isCompleteΪ��" + robotAction.isComplete);
        // �� isComplete Ϊ true ʱ���� AI ָ��
        if (robotAction.isComplete && !isWaitingForAIResponse)
        {
            RequestNextActionFromAI(); // ������һ��ָ��
        }
    }

    // ���� AI ��ȡ��һ��ָ��
    private void RequestNextActionFromAI()
    {
        isWaitingForAIResponse = true; // ����Ϊ�ȴ� AI ��Ӧ

        Debug.Log("��ʼ�������ա�����");
        robotAction.ExecuteAction(new AIAction
        {
            actionType = "absorb",
            targetPosition = rbtAbsorb.absorptionPoint.position // ���յ�λ��
        });

        if (rbtAbsorb.ObjHasAbsorbed.Count > 0 && Mathf.Abs(transform.position.x) > 8 && Mathf.Abs(transform.position.y) > 8)
        {
            Debug.Log("��ʼ�Զ���������");
            int i = Random.Range(1, 8);
            while (i > 0)
            {
               rbtAbsorb.DropObjectFromSorb(5); // �����յ��������
                i--;
            }
           // robotAction.DropObjectFromSorb();
        }
        environmentInfo.UpdateEnvironmentInfo(); // ���»�����Ϣ
        aiAgent.RequestActionFromAI(environmentInfo); // ���� AI �ṩ��һ��ָ��
    }

    // ���� AI ���ص�ָ��ַ���������ִ��
    public void OnReceivedAIAction(AIAction action)
    {
        if (action != null)
        {
            // ִ�л����˶���
            Debug.Log($"AIָ�{action.actionType}��Ŀ��λ�ã�{action.targetPosition}");
            robotAction.ExecuteAction(action); // �ַ���������ִ��
        }
        else
        {
            Debug.LogWarning("AI ���ص�ָ��Ϊ�գ�");
        }

        isWaitingForAIResponse = false; // ��� AI ָ���ȡ
    }
}
