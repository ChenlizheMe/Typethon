using UnityEngine;

public class EnvironmentInfo : MonoBehaviour
{
    public Vector3 agentPosition; // �����嵱ǰλ��
    public Vector2 mapRange ; // ���õ�ͼ��Χ(1-70)
    public Vector2[] pickMapRange ; // ���õ�ͼ��Χ(1-70)
    public Vector3 cmrPosition; // ���λ�ã����ڽ���
    public Vector2[] nearbyObjects; // �н��������򣬲��ɽ���
    public string userInput; // �û���������

    public string currentGoal; // ��ǰĿ��������AI������
    public bool isGoalReached; // �Ƿ���Ŀ��
    public string actionFeedback; // ���ڷ���AI��ִ��״̬
    public int cubeNum;
    RbtAction3D rbtAction3D; // ������ִ�п��ƽű�
    RbtAbsorb rbtAbsorb; // ���������տ��ƽű�
    private void Start()
    {
        rbtAbsorb=GetComponent<RbtAbsorb>();
        // ��ʼ��
        currentGoal = "Move to the pick up area"; // Ĭ��Ŀ��
        isGoalReached = false;
        actionFeedback = string.Empty;
        mapRange = new Vector2(-20, 20); // Ĭ�ϵ�ͼ��Χ

    }

    // ���»�����Ϣ
    public void UpdateEnvironmentInfo()
    {
        cubeNum = rbtAbsorb.ObjHasAbsorbed.Count;
        // ����ʵ��������»�����Ϣ
        // ʾ�������µ�ǰλ�õ�
        agentPosition = transform.position;
        int[] a = new int[4] { 0, 0, 30, 0 };
        currentGoal = "����ͼ�м����շ��飨absorb��������ͼ������������";

    }
}
