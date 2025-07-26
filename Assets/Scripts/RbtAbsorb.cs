using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class RbtAbsorb : MonoBehaviour
{
    public Transform absorptionPoint; // ���յ��λ��
    public float absorptionRange = 8f; // ���շ�Χ
    public int maxObjectsToAbsorb = 5; // ������յ���������
    public float absorptionSpeed = 1f; // �����ٶ�
    public float scaleFactor = 0.1f; // �����ٶ�
    public GameObject absorptionEffect = null; // ������ʧ��Ч�����������ã�
    public GameObject effect;

    private bool isAbsorbing = false; // �����Ƿ����ڽ�����

    public TMP_Text num;
    public List<GameObject> ObjHasAbsorbed; // �Ѿ����յ������б�
    RobotAnimationController robotAnimationController; // �����˶���������

    private void Start()
    {
        // ������յ��Ƿ�������
        if (absorptionPoint == null)
        {
            Debug.LogError("���յ� (absorptionPoint) δ���ã�");
        }
        robotAnimationController = GetComponent<RobotAnimationController>();
    }

    private void Update()
    {
        num.text = ObjHasAbsorbed.Count.ToString();
    }

    // ���չ��ܣ�������Χ������
    public void AbsorbObjects(int maxSorbNum)
    {
        maxObjectsToAbsorb = maxSorbNum;

        if (isAbsorbing)
        {
            Debug.Log("�������ڽ����У������ظ�������");
            return; // ����������գ���ֹ�ظ�����
        }

        isAbsorbing = true; // ����������ڽ�����
        Debug.Log("��ʼ������Χ������...");

        // ��ȡ��Χ�Ŀ�ʰȡ����
        Collider[] colliders = Physics.OverlapSphere(transform.position, absorptionRange);
        List<GameObject> objectsToAbsorb = new List<GameObject>();

        foreach (Collider col in colliders)
        {
            if (col.CompareTag("Pickable") && objectsToAbsorb.Count < maxObjectsToAbsorb)
            {
                objectsToAbsorb.Add(col.gameObject);
                Debug.Log($"rbt��⵽��ʰȡ����: {col.gameObject.name}");
            }
        }

        // ���û���ҵ��κ������������
        if (objectsToAbsorb.Count == 0)
        {
            Debug.Log("rbtû���ҵ������յ����壡");
            isAbsorbing = false; // ���ս��������Ϊδ����
            return;
        }

        // ���ҵ���ÿ������ִ������
        foreach (GameObject obj in objectsToAbsorb)
        {
            Rigidbody rbObj = obj.GetComponent<Rigidbody>();
            if (rbObj != null)
            {
                rbObj.isKinematic = true; // ��������Ч��
                Debug.Log($"��������Ч����{obj.name}");
            }

            // �����������ն���
            StartCoroutine(MoveAndShrinkObject(obj));
        }
    }

    // �������壺ʹ�����ƶ������յ㲢����С��ֱ����ʧ
    private IEnumerator MoveAndShrinkObject(GameObject obj)
    {

        Debug.Log($"��ʼ��������: {obj.name}");
        robotAnimationController.SetAnimationState("Happy"); // ���û����˶���״̬Ϊ����
        float duration = 0.7f; // ������������ʱ��
        float timeElapsed = 0f;
        if (ObjHasAbsorbed.Count >= 3) // �������� 3 ������󴥷�ҡ��
        {
            ShakeRobot(1f, 10f); // ���� 1 �룬ҡ��ǿ��Ϊ 10
        }

        // ��������Ч��
        if (effect != null)
        {
            
            effect.SetActive(true); // ȷ����Ч������
            Debug.Log($"����������Ч: {obj.name}");
            StartCoroutine(RevertToIdleAfterDelay(2f));
        }
        else
        {
            Debug.LogWarning("û������������Ч��");
        }

        // �����յ��ƶ�����С����
        Vector3 initialPosition = obj.transform.position;
        Vector3 targetPosition = absorptionPoint.position;

        // ���һЩ���ƫ�ƣ�ģ�ⲻ��������·��
        Vector3 randomOffset = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.2f, 0.2f), Random.Range(-0.5f, 0.5f));

        // ���Ź��������
        Vector3 scale = obj.transform.localScale;

        while (timeElapsed < duration)
        {
            timeElapsed += Time.deltaTime;

            // ģ������·���Ĳ�������
            Vector3 targetWithOffset = targetPosition + randomOffset * Mathf.Sin(timeElapsed * 2f); // ������·��
            obj.transform.position = Vector3.Lerp(initialPosition, targetWithOffset, timeElapsed / duration);

            // ��С����
            float scaleLerp = Mathf.Lerp(1f, scaleFactor, timeElapsed / duration);
            obj.transform.localScale = new Vector3(scale.x * scaleLerp, scale.y * scaleLerp, scale.z * scaleLerp);

            yield return null;
        }

        // ���մ����Ƴ�����
        obj.SetActive(false);
        ObjHasAbsorbed.Add(obj); // ��������ӵ��������б�
        Debug.Log($"���� {obj.name} �����ղ���ʧ");

        isAbsorbing = false; // ������ɣ�������һ������
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
            Debug.Log("û�пɷ��õ����壡");
            return; // ���û������ɷ��ã�ֱ�ӷ���
        }

        List<Vector3> outList = new List<Vector3>();
        int j = 0; // ��ʼ��ƫ����
        BuildHelper.Instance.GenerateRandomBuilding(transform.position + new Vector3(5, j, 5), dropNum, out outList);
        while (dropNum > 0 && ObjHasAbsorbed.Count > 0 && j < outList.Count)
        {
            // ��ȡ���һ������
            GameObject objToDrop = ObjHasAbsorbed[ObjHasAbsorbed.Count - 1];
            objToDrop.SetActive(true); // ��������

            
            objToDrop.transform.position = outList[j]; // �����ڵذ�λ��
            objToDrop.transform.localScale = Vector3.one; // ��������
            ObjHasAbsorbed.RemoveAt(ObjHasAbsorbed.Count - 1); // ���������б����Ƴ�
            Debug.Log($"��������: {objToDrop.name} �����յ�");

            // ����з�����Ч�����������ﲥ��
            if (absorptionEffect != null)
            {
                GameObject effect = Instantiate(absorptionEffect, objToDrop.transform.position, Quaternion.identity);
                Destroy(effect, 2f); // �ӳ�����Ч��
            }
            else
            {
                Debug.LogWarning("û�����÷�����Ч��");
            }

            j++; // ����ƫ����
            dropNum--; // ������Ҫ���õ���������
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

            // ����ҡ�νǶ�
            float shakeAngle = Mathf.Sin(elapsedTime * Mathf.PI * 4) * intensity;

            // Ӧ��ҡ�ε� Z ��
            transform.localRotation = Quaternion.Euler(transform.localRotation.eulerAngles.x, transform.localRotation.eulerAngles.y, shakeAngle);

            yield return null;
        }

        // �ָ�����ʼ�Ƕ�
        transform.localRotation = Quaternion.Euler(transform.localRotation.eulerAngles.x, transform.localRotation.eulerAngles.y, 0f);
    }
}
