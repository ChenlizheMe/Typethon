using UnityEngine;
using System.Collections;
using TMPro;
using static UnityEngine.GraphicsBuffer;

public class AiSimulater : MonoBehaviour
{
    public Vector2 mapRange = new Vector2(-20, 20);      // ��ͼ�߽緶Χ
    public Vector2 mapPickRange = new Vector2(-8, 8);    // ��Դ����Χ

    public Transform agentTransform;
    public int carriedObjectCount = 0;

    public GameObject emo_happy, emo_sad;
    RbtAction3D rbtAction; // ������ִ�п��ƽű�
    RbtAbsorb rbtAbsorb; // ���������տ��ƽű�
    //public bool isWaitingForAIResponse = false;
    bool canSorb = false;

    private void Start()
    {
        rbtAction = GetComponent<RbtAction3D>();
        rbtAbsorb = GetComponent<RbtAbsorb>();
    }

    private void Update()
    {
        carriedObjectCount = rbtAbsorb.ObjHasAbsorbed.Count; // ����Я����Ʒ����
        if (rbtAction.isComplete)
        {
            if (canSorb)
            {
                rbtAction.StartCoroutine(rbtAction.AbsorbAndReset(rbtAction.maxSorbNum)); // �������չ��̲�����ɺ�ָ�����
                canSorb = false; // ���պ��������ٴ�����

            }

            RequestNextMove();
        }
        if (rbtAction.isComplete == true)
            Debug.Log("isComplete: " + rbtAction.isComplete);
    }

    /// <summary>
    /// ���ݵ�ǰ״̬���ܾ�����һ���ƶ�λ��
    /// </summary>
    private void RequestNextMove()
    {
 

        // ������ʾ������Я����Ʒ����
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

        // �����߼�
        if (carriedObjectCount == 0)
        {
            // û������ �� �ƶ�����Դ��
            nextTarget = GetRandomPositionInPickZone();
        }
        else
        {
            // ������ �� �ƶ�����Χ����
            nextTarget = GetRandomPositionInOuterZone();
        }


        if (rbtAbsorb.ObjHasAbsorbed.Count > 0 && Mathf.Abs(transform.position.x) > 4 && Mathf.Abs(transform.position.z) > 4)
        {
            Debug.Log("��ʼ�Զ���������");
            int i = Random.Range(1, 8);

            rbtAbsorb.DropObjectFromSorb(i); // �����յ��������

            //rbtAction.DropObjectFromSorb();
        }

        AIAction action = new AIAction();
        action.ACinit("move", nextTarget);
        rbtAction.ExecuteAction(action);
        canSorb=true; // �ƶ��������������
    }

    /// <summary>
    /// ��ȡ��Դ���ڵ����λ��
    /// </summary>
    private Vector3 GetRandomPositionInPickZone()
    {
        float x = Random.Range(mapPickRange.x, mapPickRange.y);
        float z = Random.Range(mapPickRange.x, mapPickRange.y);
        return new Vector3(x, agentTransform.position.y,z);
    }

    /// <summary>
    /// ��ȡ��Χ���ڵ����λ�ã��ܿ���Դ����
    /// </summary>
    private Vector3 GetRandomPositionInOuterZone()
    {
        float x = Random.Range(mapRange.x, mapRange.y);
        float z = Random.Range(mapRange.x, mapRange.y);

        // ȷ��������Դ���߽�
        if (Mathf.Abs(x) < mapPickRange.y)
            x += Mathf.Sign(x) * (mapPickRange.y + 2);
        if (Mathf.Abs(z) < mapPickRange.y)
            z += Mathf.Sign(z) * (mapPickRange.y + 2);

        return new Vector3(x, agentTransform.position.y, z);
    }

    /// <summary>
    /// �ƶ���Ŀ��λ��
    /// </summary>

}
