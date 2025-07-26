using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class RbtAction3D : MonoBehaviour
{
    public float obstacleDetectionRange = 1f; // 障碍物检测范围
    public float moveSpeed = 100f; // 移动速度
    public float flySpeed = 2f; // 飞行速度
    public bool isComplete = true; // 表示是否可以执行下一条指令

    private GameObject heldObject; // 当前拾取的物体
    private GameObject potentialPickableObject; // 当前碰撞的可拾取物体

    private GameObject nowCol;

    private Rigidbody rb; // 机器人的刚体组件
    private RbtAbsorb rbtAbsorb; // 引用吸收脚本

    private Vector3 lastPosition; // 记录上一次的位置
    private float stuckTimer = 0f; // 记录卡住的时间
    public float jumpForce = 200f; // 跳跃力
    //public GameObject sorbPos; // 用于获取当前脚下位置


    public GameObject emo_sad;
    public int maxSorbNum = 10; // 最大吸收数量
    RobotAnimationController robotAnimationController; // 机器人动画控制器

    private void Awake()
    {
        emo_sad.SetActive(false);
        // 获取机器人自身的刚体组件
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("机器人缺少 Rigidbody 组件！");
        }
        // 获取ObjectAbsorber组件
        rbtAbsorb = GetComponent<RbtAbsorb>();
        if (rbtAbsorb == null)
        {
            Debug.LogError("机器人缺少 ObjectAbsorber 组件！");
        }
        robotAnimationController = GetComponent<RobotAnimationController>();
    }

    // 机器人执行行为的入口
    public void ExecuteAction(AIAction action)
    {

        if (!isComplete)
            return; // 如果当前指令未完成，则跳过

        isComplete = false; // 开始执行指令
        emo_sad.SetActive(false );
        switch (action.actionType)
        {
            case "move":
                Debug.Log(gameObject.name+"rbt开始移动到目标位置：" + (int)action.targetPosition.x+"  "+(int)action.targetPosition.z);
                StartCoroutine(MoveTo(new Vector2((int)action.targetPosition.x, (int)action.targetPosition.z))); // 只传目标的 x 坐标
                break;


            case "build"://放置
                //DropObjectFromSorb();
                break;

            case "absorb":
                StartCoroutine(AbsorbAndReset(maxSorbNum)); // 启动吸收过程并在完成后恢复操作
                break;

            default:
                Debug.LogWarning("未知行为类型：" + action.actionType);
                isComplete = true; // 未知指令直接标记完成
                break;
        }

    }
    // 检测卡死并进行跳跃
    private void DetectAndJumpIfStuck(Vector3 direction)
    {
        if (!isComplete && Vector3.Distance(transform.position, lastPosition) < 0.1f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= 2f) // 如果物体在2秒内卡住了，并且没有移动
            {
                Debug.Log("物体卡住了，进行跳跃！");
                //emo_sad.SetActive(true);
                robotAnimationController.SetAnimationState("Sad"); // 设置机器人动画状态为悲伤

                // 施加跳跃力，并在卡住方向上施加一定的水平力
                Vector3 jumpDirection = Vector3.up * jumpForce + direction.normalized * (jumpForce * 0.5f);
                rb.AddForce(jumpDirection, ForceMode.Impulse);

                stuckTimer = 0f; // 重置卡住计时器
            }
        }
        else
        {
            // 如果物体移动了，重置卡住计时器
            stuckTimer = 0f;
            //emo_sad.SetActive(false);
        }

        // 更新上次的位置
        lastPosition = transform.position;
    }

    private System.Collections.IEnumerator CloseEmoAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        emo_sad.SetActive(false);
    }


    // 吸收操作：完成后重置状态
    public IEnumerator AbsorbAndReset(int maxNum)
    {
        int sorbNum =Random.Range(2,maxNum); // 随机吸收数量，范围从2到maxNum
        rbtAbsorb.AbsorbObjects(maxNum); // 执行吸收操作
        yield return new WaitForSeconds(1f); // 等待吸收过程完成，可以根据吸收时间调整

        //isComplete = true; // 吸收完成后，允许执行下一个操作
        Debug.Log("吸收完成，可以执行下一条指令！");
    }

    // 调试使用，每帧调用，接收移动方向
    public IEnumerator MoveTo(Vector2 targetPosition)
    {
        // 获取目标点的三维坐标
        Vector3 target3DPosition = new Vector3(targetPosition.x, transform.position.y-6f, targetPosition.y);
        target3DPosition.y = Mathf.Clamp(target3DPosition.y, -6f, 200f);
        target3DPosition.x = Mathf.Clamp(target3DPosition.x, -200f, 200f);
        target3DPosition.z = Mathf.Clamp(target3DPosition.z, -200f, 200f);

        // 计算初始距离
        float distanceToTarget = Vector3.Distance(target3DPosition, transform.position);

        // 定义到达的容许范围
        float acceptableRange = 4f; // 可根据需要调整范围

        // 定义最大尝试时间
        float maxMoveTime = 10f; // 最长移动时间（秒）
        float elapsedTime = 0f; // 已经过的时间

        // 移动循环
        while (distanceToTarget > acceptableRange && elapsedTime < maxMoveTime)
        {
            // 计算方向
            Vector3 directionToTarget = (target3DPosition - transform.position).normalized;

            // 检测是否卡住并跳跃
            DetectAndJumpIfStuck(directionToTarget);

            // 施加移动力
            rb.AddForce(directionToTarget * moveSpeed, ForceMode.Acceleration);

            // 更新距离
            distanceToTarget = Vector3.Distance(target3DPosition, transform.position);

            // 更新已用时间
            elapsedTime += Time.deltaTime;

            yield return null; // 等待一帧
        }

        if (distanceToTarget <= acceptableRange)
        {
            Debug.Log(gameObject.name+"rbt机器人接近目标位置，目标位置：" + target3DPosition);
        }
        else
        {
            Debug.LogWarning("机器人未能到达目标位置，超出最大尝试时间！");
        }

        isComplete = true; // 标记完成状态
    }

    public IEnumerator MoveTo3D(Vector3 targetPosition)
    {
        // 获取目标点的三维坐标
        Vector3 target3DPosition = new Vector3(targetPosition.x, transform.position.y, targetPosition.y);

        // 计算初始距离
        float distanceToTarget = Vector3.Distance(target3DPosition, transform.position);

        // 定义到达的容许范围
        float acceptableRange = 4f; // 可根据需要调整范围

        // 定义最大尝试时间
        float maxMoveTime = 100f; // 最长移动时间（秒）
        float elapsedTime = 0f; // 已经过的时间

        // 移动循环
        while (distanceToTarget > acceptableRange && elapsedTime < maxMoveTime)
        {
            // 计算方向
            Vector3 directionToTarget = (target3DPosition - transform.position).normalized;

            // 检测是否卡住并跳跃
            DetectAndJumpIfStuck(directionToTarget);

            // 施加移动力
            rb.AddForce(directionToTarget * moveSpeed, ForceMode.Acceleration);

            // 更新距离
            distanceToTarget = Vector3.Distance(target3DPosition, transform.position);

            // 更新已用时间
            elapsedTime += Time.deltaTime;

            yield return null; // 等待一帧
        }

        if (distanceToTarget <= acceptableRange)
        {
            Debug.Log("机器人接近目标位置，目标位置：" + target3DPosition);
        }
        else
        {
            Debug.LogWarning("机器人未能到达目标位置，超出最大尝试时间！");
        }

        isComplete = true; // 标记完成状态
    }

    public void TryPickObject()
    {
        if (heldObject == null && potentialPickableObject != null) // 如果没有拾取物体，且当前有可拾取物体
        {
            PickObject(potentialPickableObject); // 拾取当前碰撞的可拾取物体
        }
        else if (heldObject != null)
        {
            Debug.Log("已经拾取了物体：" + heldObject.name);
        }
        else
        {
            Debug.Log("附近没有可拾取的物体！");
        }
    }

    public Transform pickupPoint; // 定义一个挂载点，在机器人模型上标记拾取位置

    public void PickObject(GameObject targetGameObj)
    {
        heldObject = targetGameObj; // 保存物体引用

        // 禁用物体的物理效果
        Rigidbody heldRb = heldObject.GetComponent<Rigidbody>();
        if (heldRb != null)
        {
            heldRb.isKinematic = true; // 设置为运动学模式，禁用物理效果
            heldRb.velocity = Vector3.zero; // 清空速度
        }

        // 将物体设置到挂载点位置
        gameObject.transform.position = new Vector3(transform.position.x, transform.position.y + 1, transform.position.z);
        heldObject.transform.SetParent(pickupPoint); // 设置为挂载点的子物体
        heldObject.transform.localPosition = Vector3.zero; // 固定位置到挂载点中心
        heldObject.transform.localRotation = Quaternion.identity; // 重置旋转

        Debug.Log("拾取物体：" + heldObject.name);

        // 清空潜在可拾取物体的引用
        potentialPickableObject = null;
    }

    // 放下物体逻辑
    public void DropObject()
    {
        if (heldObject != null) // 如果机器人正在拾取物体
        {
            // 释放父子关系并放下物体
            heldObject.transform.SetParent(null);

            // 吸附到最近的网格点
            Vector3 dropPosition = transform.position;
            dropPosition.x = Mathf.Round(dropPosition.x); // 吸附到最近的整数位置（x轴）
            dropPosition.z = Mathf.Round(dropPosition.z); // 吸附到最近的整数位置（z轴）
            heldObject.transform.position = dropPosition;

            // 恢复物体的物理效果
            Rigidbody heldRb = heldObject.GetComponent<Rigidbody>();
            if (heldRb != null)
            {
                heldRb.isKinematic = false; // 重新启用物理效果
            }

            Debug.Log("放下物体：" + heldObject.name);
            heldObject = null; // 重置拾取状态
        }
        else
        {
            Debug.Log("未拾取任何物体，无法放下！");
        }
    }

    

    // 碰撞检测逻辑：记录潜在可拾取物体
    private void OnCollisionEnter(Collision collision)
    {
        nowCol = collision.gameObject;
        if (collision.gameObject.CompareTag("Pickable") && heldObject == null) // 如果碰撞的是可拾取物体，并且当前未拾取任何物体
        {
            potentialPickableObject = collision.gameObject; // 保存可拾取物体的引用
            Debug.Log("检测到可拾取物体：" + potentialPickableObject.name);
        }
    }

    // 碰撞结束：清除潜在可拾取物体的引用
    private void OnCollisionExit(Collision collision)
    {
        nowCol = null;
        if (collision.gameObject == potentialPickableObject) // 如果离开的物体是潜在可拾取物体
        {
            Debug.Log("离开可拾取物体范围：" + potentialPickableObject.name);
            potentialPickableObject = null; // 清除引用
        }
    }

    private void ThrowObject()
    {

    }

    private float MapValue(float max, float value, float start, float end)
    {
        return Mathf.Lerp(start, end, value / max);
    }
}
