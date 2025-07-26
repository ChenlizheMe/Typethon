using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class RbtAction : MonoBehaviour
{
    public float obstacleDetectionRange = 1f; // 障碍物检测范围
    public float moveSpeed = 100f; // 移动速度
    public float flySpeed = 2f; // 飞行速度
    private bool isComplete = true; // 表示是否可以执行下一条指令

    private GameObject heldObject; // 当前拾取的物体
    private GameObject potentialPickableObject; // 当前碰撞的可拾取物体

    private GameObject nowCol;

    private Rigidbody rb; // 机器人的刚体组件

    private void Awake()
    {
        // 获取机器人自身的刚体组件
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("机器人缺少 Rigidbody 组件！");
        }
    }

    // 机器人执行行为的入口
    public void ExecuteAction(AIAction action)
    {
        if (!isComplete)
            return; // 如果当前指令未完成，则跳过

        isComplete = false; // 开始执行指令
        switch (action.actionType)
        {
            case "move":
                StartCoroutine(MoveTo(action.targetPosition.x)); // 只传目标的 x 坐标
                break;

            case "pick":
                TryPickObject();
                break;

            case "build":
                DropObject();
                break;

            case "throw":


            default:
                Debug.LogWarning("未知行为类型：" + action.actionType);
                isComplete = true; // 未知指令直接标记完成
                break;
        }
    }

    // 调试使用，每帧调用，接收移动方向

    public IEnumerator MoveTo(float targetX)
    {

        float OdistanceToTarget = Mathf.Abs(targetX - transform.position.x); // 计算与目标的水平距离（初始距离）
        float distanceToTarget = Mathf.Abs(targetX - transform.position.x); // 计算与目标的水平距离（实时更新）
        bool isMovingRight = targetX > transform.position.x; // 判断移动方向
        float limitSorb=0.5f;
        while (distanceToTarget > limitSorb) // 当距离大于 0.3 时，继续移动
        {
            //limitSorb = MapValue(60, rb.velocity.x, 0.5f, 4f);
            if (Mathf.Abs(rb.velocity.x) < 10) limitSorb = 0.5f;
            else if (Mathf.Abs(rb.velocity.x) < 30) limitSorb = 2;
            else if (Mathf.Abs(rb.velocity.x) < 50) limitSorb = 3;
            else if (Mathf.Abs(rb.velocity.x) < 70) limitSorb = 4;
            Debug.Log("当前速度：" + rb.velocity);
            // 检测前方障碍物
            RaycastHit hit;
            Vector3 rayOrigin = transform.position + new Vector3(0, 1f, 0); // 射线原点
            Vector3 rayDirection = isMovingRight ? Vector3.right : Vector3.left; // 射线方向

            if (Physics.Raycast(rayOrigin, rayDirection, out hit, obstacleDetectionRange))//上升
            {
                // 如果检测到障碍物
                float obstacleHeight = hit.collider.bounds.max.y; // 获取障碍物最高点
                float targetHeight = obstacleHeight + 1f; // 目标高度为障碍物上方 1 单位

                if (transform.position.y < targetHeight - 2f) // 如果未达到目标高度，施加向上的力
                {
                    rb.AddForce(Vector3.up * flySpeed, ForceMode.Acceleration); // 使用加速度实现更平滑的力
                }

                else
                {
                    //rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z); // 高度足够时停止向上的力
                    rb.AddForce(Vector3.down * flySpeed); // 使用加速度实现更平滑的力
                }
            }
            else if (!Physics.Raycast(rayOrigin, rayDirection, out hit,4f)&& nowCol==null)//下降
            {
                rb.AddForce(Vector3.down * flySpeed, ForceMode.Acceleration);
                // 如果前方没有障碍物，正常水平移动
                float moveDirection = isMovingRight ? 1f : -1f;

                // 控制水平移动速度
                rb.AddForce(Vector3.right * moveDirection * moveSpeed, ForceMode.Acceleration);

                // **逐渐减速逻辑**
                if (distanceToTarget <= Mathf.Abs(OdistanceToTarget) / 5) // 当距离小于 1/5 初始距离时进入减速阶段
                {
                    float slowDownFactor = Mathf.Clamp(distanceToTarget / 1f, 0.1f, 1f); // 距离越近，速度比例越小，确保不低于 10%
                    rb.velocity = new Vector3(rb.velocity.x * slowDownFactor, rb.velocity.y, rb.velocity.z); // 根据比例调整水平速度
                }
            }
            else if (!Physics.Raycast(rayOrigin, rayDirection, out hit, 4f) && nowCol == null)//下降
            {
                rb.AddForce(Vector3.down * flySpeed, ForceMode.Acceleration);
                // 如果前方没有障碍物，正常水平移动
                float moveDirection = isMovingRight ? 1f : -1f;

                // 控制水平移动速度
                rb.AddForce(Vector3.right * moveDirection * moveSpeed, ForceMode.Acceleration);

                // **逐渐减速逻辑**
                if (distanceToTarget <= Mathf.Abs(OdistanceToTarget) / 5) // 当距离小于 1/5 初始距离时进入减速阶段
                {
                    float slowDownFactor = Mathf.Clamp(distanceToTarget / 1f, 0.1f, 1f); // 距离越近，速度比例越小，确保不低于 10%
                    rb.velocity = new Vector3(rb.velocity.x * slowDownFactor, rb.velocity.y, rb.velocity.z); // 根据比例调整水平速度
                }
            }
            else if (rb.velocity.x < -5f)
            {
                rb.AddForce(Vector3.down * flySpeed, ForceMode.Acceleration);
            }//下降
            else
            {
                // 如果前方没有障碍物，正常水平移动
                float moveDirection = isMovingRight ? 1f : -1f;

                // 控制水平移动速度
                rb.AddForce(Vector3.right * moveDirection * moveSpeed, ForceMode.Acceleration);

                float minSlowSpeed = 4f; // 你定义的最小速度，可自行调整

                // 逐渐减速逻辑
                if (distanceToTarget <= Mathf.Abs(OdistanceToTarget) / 5)
                {
                    // 计算减速比例 (从 1 到 minSlowSpeed/moveSpeed)
                    float slowDownFactor = Mathf.Clamp(distanceToTarget / (OdistanceToTarget / 5), minSlowSpeed / moveSpeed, 1f);

                    // 根据比例设置目标速度（介于minSlowSpeed与moveSpeed之间）
                    float targetSpeed = moveSpeed * slowDownFactor;

                    float currentDirection = Mathf.Sign(rb.velocity.x);
                    rb.velocity = new Vector3(targetSpeed * currentDirection, rb.velocity.y, rb.velocity.z);
                }
            }

            // 更新与目标的距离
            distanceToTarget = Mathf.Abs(targetX - transform.position.x);

            // 如果距离非常小，停止所有移动
            if (distanceToTarget <= limitSorb)
            {
                rb.velocity = Vector3.zero; // 停止所有移动
                break;
            }

            yield return null; // 等待一帧时间
        }

        // **吸附到目标整数位置**
        Vector3 snappedPosition = new Vector3(Mathf.Round(targetX), transform.position.y, transform.position.z);
        transform.position = snappedPosition; // 强制设置位置到整数坐标

        Debug.Log("机器人移动完成，目标位置：" + snappedPosition.x);
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
        nowCol =null;
        if (collision.gameObject == potentialPickableObject) // 如果离开的物体是潜在可拾取物体
        {
            Debug.Log("离开可拾取物体范围：" + potentialPickableObject.name);
            potentialPickableObject = null; // 清除引用
        }
    }

    private void ThrowObject()
    {

    }

    private float MapValue(float max, float value,float start,float end)
    {
        return Mathf.Lerp(start, end, value / max);
    }
}
