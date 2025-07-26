using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class RbtAbsorb : MonoBehaviour
{
    public Transform absorptionPoint; // 吸收点的位置
    public float absorptionRange = 8f; // 吸收范围
    public int maxObjectsToAbsorb = 5; // 最多吸收的物体数量
    public float absorptionSpeed = 1f; // 吸收速度
    public float scaleFactor = 0.1f; // 缩放速度
    public GameObject absorptionEffect = null; // 吸收消失特效（可自行设置）
    public GameObject effect;

    private bool isAbsorbing = false; // 吸收是否正在进行中

    public TMP_Text num;
    public List<GameObject> ObjHasAbsorbed; // 已经吸收的物体列表
    RobotAnimationController robotAnimationController; // 机器人动画控制器

    private void Start()
    {
        // 检查吸收点是否已设置
        if (absorptionPoint == null)
        {
            Debug.LogError("吸收点 (absorptionPoint) 未设置！");
        }
        robotAnimationController = GetComponent<RobotAnimationController>();
    }

    private void Update()
    {
        num.text = ObjHasAbsorbed.Count.ToString();
    }

    // 吸收功能：吸收周围的物体
    public void AbsorbObjects(int maxSorbNum)
    {
        maxObjectsToAbsorb = maxSorbNum;

        if (isAbsorbing)
        {
            Debug.Log("吸收正在进行中，不能重复触发！");
            return; // 如果正在吸收，阻止重复触发
        }

        isAbsorbing = true; // 标记吸收正在进行中
        Debug.Log("开始吸收周围的物体...");

        // 获取周围的可拾取物体
        Collider[] colliders = Physics.OverlapSphere(transform.position, absorptionRange);
        List<GameObject> objectsToAbsorb = new List<GameObject>();

        foreach (Collider col in colliders)
        {
            if (col.CompareTag("Pickable") && objectsToAbsorb.Count < maxObjectsToAbsorb)
            {
                objectsToAbsorb.Add(col.gameObject);
                Debug.Log($"rbt检测到可拾取物体: {col.gameObject.name}");
            }
        }

        // 如果没有找到任何物体进行吸收
        if (objectsToAbsorb.Count == 0)
        {
            Debug.Log("rbt没有找到可吸收的物体！");
            isAbsorbing = false; // 吸收结束，标记为未吸收
            return;
        }

        // 对找到的每个物体执行吸收
        foreach (GameObject obj in objectsToAbsorb)
        {
            Rigidbody rbObj = obj.GetComponent<Rigidbody>();
            if (rbObj != null)
            {
                rbObj.isKinematic = true; // 禁用物理效果
                Debug.Log($"禁用物理效果：{obj.name}");
            }

            // 启动物体吸收动画
            StartCoroutine(MoveAndShrinkObject(obj));
        }
    }

    // 吸收物体：使物体移动到吸收点并逐渐缩小，直到消失
    private IEnumerator MoveAndShrinkObject(GameObject obj)
    {

        Debug.Log($"开始吸收物体: {obj.name}");
        robotAnimationController.SetAnimationState("Happy"); // 设置机器人动画状态为吸收
        float duration = 0.7f; // 吸收物体所需时间
        float timeElapsed = 0f;
        if (ObjHasAbsorbed.Count >= 3) // 假设吸收 3 个物体后触发摇晃
        {
            ShakeRobot(1f, 10f); // 持续 1 秒，摇晃强度为 10
        }

        // 播放吸收效果
        if (effect != null)
        {
            
            effect.SetActive(true); // 确保特效被激活
            Debug.Log($"播放吸收特效: {obj.name}");
            StartCoroutine(RevertToIdleAfterDelay(2f));
        }
        else
        {
            Debug.LogWarning("没有设置吸收特效！");
        }

        // 向吸收点移动并缩小物体
        Vector3 initialPosition = obj.transform.position;
        Vector3 targetPosition = absorptionPoint.position;

        // 添加一些随机偏移，模拟不规则吸收路径
        Vector3 randomOffset = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.2f, 0.2f), Random.Range(-0.5f, 0.5f));

        // 缩放过程随机化
        Vector3 scale = obj.transform.localScale;

        while (timeElapsed < duration)
        {
            timeElapsed += Time.deltaTime;

            // 模拟吸收路径的不规则性
            Vector3 targetWithOffset = targetPosition + randomOffset * Mathf.Sin(timeElapsed * 2f); // 不规则路径
            obj.transform.position = Vector3.Lerp(initialPosition, targetWithOffset, timeElapsed / duration);

            // 缩小物体
            float scaleLerp = Mathf.Lerp(1f, scaleFactor, timeElapsed / duration);
            obj.transform.localScale = new Vector3(scale.x * scaleLerp, scale.y * scaleLerp, scale.z * scaleLerp);

            yield return null;
        }

        // 最终处理：移除物体
        obj.SetActive(false);
        ObjHasAbsorbed.Add(obj); // 将物体添加到已吸收列表
        Debug.Log($"物体 {obj.name} 被吸收并消失");

        isAbsorbing = false; // 吸收完成，允许下一次吸收
    }

    private IEnumerator RevertToIdleAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        effect.SetActive(false);
    }

    public void DropObjectFromSorb(int dropNum)
    {
        if (ObjHasAbsorbed.Count == 0)
        {
            Debug.Log("没有可放置的物体！");
            return; // 如果没有物体可放置，直接返回
        }

        List<Vector3> outList = new List<Vector3>();
        int j = 0; // 初始化偏移量
        BuildHelper.Instance.GenerateRandomBuilding(transform.position + new Vector3(5, j, 5), dropNum, out outList);
        while (dropNum > 0 && ObjHasAbsorbed.Count > 0 && j < outList.Count)
        {
            // 获取最后一个物体
            GameObject objToDrop = ObjHasAbsorbed[ObjHasAbsorbed.Count - 1];
            objToDrop.SetActive(true); // 激活物体

            
            objToDrop.transform.position = outList[j]; // 放置在地板位置
            objToDrop.transform.localScale = Vector3.one; // 重置缩放
            ObjHasAbsorbed.RemoveAt(ObjHasAbsorbed.Count - 1); // 从已吸收列表中移除
            Debug.Log($"放置物体: {objToDrop.name} 到吸收点");

            // 如果有放置特效，可以在这里播放
            if (absorptionEffect != null)
            {
                GameObject effect = Instantiate(absorptionEffect, objToDrop.transform.position, Quaternion.identity);
                Destroy(effect, 2f); // 延迟销毁效果
            }
            else
            {
                Debug.LogWarning("没有设置放置特效！");
            }

            j++; // 增加偏移量
            dropNum--; // 减少需要放置的物体数量
        }
    }
    public void ShakeRobot(float shakeDuration, float shakeIntensity)
    {
        StartCoroutine(ShakeCoroutine(shakeDuration, shakeIntensity));
    }

    private IEnumerator ShakeCoroutine(float duration, float intensity)
    {
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            // 计算摇晃角度
            float shakeAngle = Mathf.Sin(elapsedTime * Mathf.PI * 4) * intensity;

            // 应用摇晃到 Z 轴
            transform.localRotation = Quaternion.Euler(transform.localRotation.eulerAngles.x, transform.localRotation.eulerAngles.y, shakeAngle);

            yield return null;
        }

        // 恢复到初始角度
        transform.localRotation = Quaternion.Euler(transform.localRotation.eulerAngles.x, transform.localRotation.eulerAngles.y, 0f);
    }
}
