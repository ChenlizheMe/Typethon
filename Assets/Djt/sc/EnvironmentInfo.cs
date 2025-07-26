using UnityEngine;

public class EnvironmentInfo : MonoBehaviour
{
    public Vector3 agentPosition; // 智能体当前位置
    public Vector2 mapRange ; // 放置地图范围(1-70)
    public Vector2[] pickMapRange ; // 放置地图范围(1-70)
    public Vector3 cmrPosition; // 相机位置，用于交互
    public Vector2[] nearbyObjects; // 有建筑的区域，不可建造
    public string userInput; // 用户输入内容

    public string currentGoal; // 当前目标描述（AI的任务）
    public bool isGoalReached; // 是否达成目标
    public string actionFeedback; // 用于反馈AI的执行状态
    public int cubeNum;
    RbtAction3D rbtAction3D; // 机器人执行控制脚本
    RbtAbsorb rbtAbsorb; // 机器人吸收控制脚本
    private void Start()
    {
        rbtAbsorb=GetComponent<RbtAbsorb>();
        // 初始化
        currentGoal = "Move to the pick up area"; // 默认目标
        isGoalReached = false;
        actionFeedback = string.Empty;
        mapRange = new Vector2(-20, 20); // 默认地图范围

    }

    // 更新环境信息
    public void UpdateEnvironmentInfo()
    {
        cubeNum = rbtAbsorb.ObjHasAbsorbed.Count;
        // 根据实际情况更新环境信息
        // 示例：更新当前位置等
        agentPosition = transform.position;
        int[] a = new int[4] { 0, 0, 30, 0 };
        currentGoal = "到地图中间吸收方块（absorb），到地图稍外侧区域放置";

    }
}
