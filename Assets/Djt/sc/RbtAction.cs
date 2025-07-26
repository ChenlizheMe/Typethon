using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class RbtAction : MonoBehaviour
{
    public float obstacleDetectionRange = 1f; // �ϰ����ⷶΧ
    public float moveSpeed = 100f; // �ƶ��ٶ�
    public float flySpeed = 2f; // �����ٶ�
    private bool isComplete = true; // ��ʾ�Ƿ����ִ����һ��ָ��

    private GameObject heldObject; // ��ǰʰȡ������
    private GameObject potentialPickableObject; // ��ǰ��ײ�Ŀ�ʰȡ����

    private GameObject nowCol;

    private Rigidbody rb; // �����˵ĸ������

    private void Awake()
    {
        // ��ȡ����������ĸ������
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("������ȱ�� Rigidbody �����");
        }
    }

    // ������ִ����Ϊ�����
    public void ExecuteAction(AIAction action)
    {
        if (!isComplete)
            return; // �����ǰָ��δ��ɣ�������

        isComplete = false; // ��ʼִ��ָ��
        switch (action.actionType)
        {
            case "move":
                StartCoroutine(MoveTo(action.targetPosition.x)); // ֻ��Ŀ��� x ����
                break;

            case "pick":
                TryPickObject();
                break;

            case "build":
                DropObject();
                break;

            case "throw":


            default:
                Debug.LogWarning("δ֪��Ϊ���ͣ�" + action.actionType);
                isComplete = true; // δָ֪��ֱ�ӱ�����
                break;
        }
    }

    // ����ʹ�ã�ÿ֡���ã������ƶ�����

    public IEnumerator MoveTo(float targetX)
    {

        float OdistanceToTarget = Mathf.Abs(targetX - transform.position.x); // ������Ŀ���ˮƽ���루��ʼ���룩
        float distanceToTarget = Mathf.Abs(targetX - transform.position.x); // ������Ŀ���ˮƽ���루ʵʱ���£�
        bool isMovingRight = targetX > transform.position.x; // �ж��ƶ�����
        float limitSorb=0.5f;
        while (distanceToTarget > limitSorb) // ��������� 0.3 ʱ�������ƶ�
        {
            //limitSorb = MapValue(60, rb.velocity.x, 0.5f, 4f);
            if (Mathf.Abs(rb.velocity.x) < 10) limitSorb = 0.5f;
            else if (Mathf.Abs(rb.velocity.x) < 30) limitSorb = 2;
            else if (Mathf.Abs(rb.velocity.x) < 50) limitSorb = 3;
            else if (Mathf.Abs(rb.velocity.x) < 70) limitSorb = 4;
            Debug.Log("��ǰ�ٶȣ�" + rb.velocity);
            // ���ǰ���ϰ���
            RaycastHit hit;
            Vector3 rayOrigin = transform.position + new Vector3(0, 1f, 0); // ����ԭ��
            Vector3 rayDirection = isMovingRight ? Vector3.right : Vector3.left; // ���߷���

            if (Physics.Raycast(rayOrigin, rayDirection, out hit, obstacleDetectionRange))//����
            {
                // �����⵽�ϰ���
                float obstacleHeight = hit.collider.bounds.max.y; // ��ȡ�ϰ�����ߵ�
                float targetHeight = obstacleHeight + 1f; // Ŀ��߶�Ϊ�ϰ����Ϸ� 1 ��λ

                if (transform.position.y < targetHeight - 2f) // ���δ�ﵽĿ��߶ȣ�ʩ�����ϵ���
                {
                    rb.AddForce(Vector3.up * flySpeed, ForceMode.Acceleration); // ʹ�ü��ٶ�ʵ�ָ�ƽ������
                }

                else
                {
                    //rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z); // �߶��㹻ʱֹͣ���ϵ���
                    rb.AddForce(Vector3.down * flySpeed); // ʹ�ü��ٶ�ʵ�ָ�ƽ������
                }
            }
            else if (!Physics.Raycast(rayOrigin, rayDirection, out hit,4f)&& nowCol==null)//�½�
            {
                rb.AddForce(Vector3.down * flySpeed, ForceMode.Acceleration);
                // ���ǰ��û���ϰ������ˮƽ�ƶ�
                float moveDirection = isMovingRight ? 1f : -1f;

                // ����ˮƽ�ƶ��ٶ�
                rb.AddForce(Vector3.right * moveDirection * moveSpeed, ForceMode.Acceleration);

                // **�𽥼����߼�**
                if (distanceToTarget <= Mathf.Abs(OdistanceToTarget) / 5) // ������С�� 1/5 ��ʼ����ʱ������ٽ׶�
                {
                    float slowDownFactor = Mathf.Clamp(distanceToTarget / 1f, 0.1f, 1f); // ����Խ�����ٶȱ���ԽС��ȷ�������� 10%
                    rb.velocity = new Vector3(rb.velocity.x * slowDownFactor, rb.velocity.y, rb.velocity.z); // ���ݱ�������ˮƽ�ٶ�
                }
            }
            else if (!Physics.Raycast(rayOrigin, rayDirection, out hit, 4f) && nowCol == null)//�½�
            {
                rb.AddForce(Vector3.down * flySpeed, ForceMode.Acceleration);
                // ���ǰ��û���ϰ������ˮƽ�ƶ�
                float moveDirection = isMovingRight ? 1f : -1f;

                // ����ˮƽ�ƶ��ٶ�
                rb.AddForce(Vector3.right * moveDirection * moveSpeed, ForceMode.Acceleration);

                // **�𽥼����߼�**
                if (distanceToTarget <= Mathf.Abs(OdistanceToTarget) / 5) // ������С�� 1/5 ��ʼ����ʱ������ٽ׶�
                {
                    float slowDownFactor = Mathf.Clamp(distanceToTarget / 1f, 0.1f, 1f); // ����Խ�����ٶȱ���ԽС��ȷ�������� 10%
                    rb.velocity = new Vector3(rb.velocity.x * slowDownFactor, rb.velocity.y, rb.velocity.z); // ���ݱ�������ˮƽ�ٶ�
                }
            }
            else if (rb.velocity.x < -5f)
            {
                rb.AddForce(Vector3.down * flySpeed, ForceMode.Acceleration);
            }//�½�
            else
            {
                // ���ǰ��û���ϰ������ˮƽ�ƶ�
                float moveDirection = isMovingRight ? 1f : -1f;

                // ����ˮƽ�ƶ��ٶ�
                rb.AddForce(Vector3.right * moveDirection * moveSpeed, ForceMode.Acceleration);

                float minSlowSpeed = 4f; // �㶨�����С�ٶȣ������е���

                // �𽥼����߼�
                if (distanceToTarget <= Mathf.Abs(OdistanceToTarget) / 5)
                {
                    // ������ٱ��� (�� 1 �� minSlowSpeed/moveSpeed)
                    float slowDownFactor = Mathf.Clamp(distanceToTarget / (OdistanceToTarget / 5), minSlowSpeed / moveSpeed, 1f);

                    // ���ݱ�������Ŀ���ٶȣ�����minSlowSpeed��moveSpeed֮�䣩
                    float targetSpeed = moveSpeed * slowDownFactor;

                    float currentDirection = Mathf.Sign(rb.velocity.x);
                    rb.velocity = new Vector3(targetSpeed * currentDirection, rb.velocity.y, rb.velocity.z);
                }
            }

            // ������Ŀ��ľ���
            distanceToTarget = Mathf.Abs(targetX - transform.position.x);

            // �������ǳ�С��ֹͣ�����ƶ�
            if (distanceToTarget <= limitSorb)
            {
                rb.velocity = Vector3.zero; // ֹͣ�����ƶ�
                break;
            }

            yield return null; // �ȴ�һ֡ʱ��
        }

        // **������Ŀ������λ��**
        Vector3 snappedPosition = new Vector3(Mathf.Round(targetX), transform.position.y, transform.position.z);
        transform.position = snappedPosition; // ǿ������λ�õ���������

        Debug.Log("�������ƶ���ɣ�Ŀ��λ�ã�" + snappedPosition.x);
        isComplete = true; // ������״̬
    }


    public void TryPickObject()
    {
        if (heldObject == null && potentialPickableObject != null) // ���û��ʰȡ���壬�ҵ�ǰ�п�ʰȡ����
        {
            PickObject(potentialPickableObject); // ʰȡ��ǰ��ײ�Ŀ�ʰȡ����
        }
        else if (heldObject != null)
        {
            Debug.Log("�Ѿ�ʰȡ�����壺" + heldObject.name);
        }
        else
        {
            Debug.Log("����û�п�ʰȡ�����壡");
        }
    }

    public Transform pickupPoint; // ����һ�����ص㣬�ڻ�����ģ���ϱ��ʰȡλ��

    public void PickObject(GameObject targetGameObj)
    {
        heldObject = targetGameObj; // ������������

        // �������������Ч��
        Rigidbody heldRb = heldObject.GetComponent<Rigidbody>();
        if (heldRb != null)
        {
            heldRb.isKinematic = true; // ����Ϊ�˶�ѧģʽ����������Ч��
            heldRb.velocity = Vector3.zero; // ����ٶ�
        }

        // ���������õ����ص�λ��
        gameObject.transform.position = new Vector3(transform.position.x, transform.position.y + 1, transform.position.z);
        heldObject.transform.SetParent(pickupPoint); // ����Ϊ���ص��������
        heldObject.transform.localPosition = Vector3.zero; // �̶�λ�õ����ص�����
        heldObject.transform.localRotation = Quaternion.identity; // ������ת

        Debug.Log("ʰȡ���壺" + heldObject.name);

        // ���Ǳ�ڿ�ʰȡ���������
        potentialPickableObject = null;
    }

    // ���������߼�
    public void DropObject()
    {
        if (heldObject != null) // �������������ʰȡ����
        {
            // �ͷŸ��ӹ�ϵ����������
            heldObject.transform.SetParent(null);

            // ����������������
            Vector3 dropPosition = transform.position;
            dropPosition.x = Mathf.Round(dropPosition.x); // ���������������λ�ã�x�ᣩ
            dropPosition.z = Mathf.Round(dropPosition.z); // ���������������λ�ã�z�ᣩ
            heldObject.transform.position = dropPosition;

            // �ָ����������Ч��
            Rigidbody heldRb = heldObject.GetComponent<Rigidbody>();
            if (heldRb != null)
            {
                heldRb.isKinematic = false; // ������������Ч��
            }

            Debug.Log("�������壺" + heldObject.name);
            heldObject = null; // ����ʰȡ״̬
        }
        else
        {
            Debug.Log("δʰȡ�κ����壬�޷����£�");
        }
    }
    // ��ײ����߼�����¼Ǳ�ڿ�ʰȡ����
    private void OnCollisionEnter(Collision collision)
    {
        nowCol = collision.gameObject;
        if (collision.gameObject.CompareTag("Pickable") && heldObject == null) // �����ײ���ǿ�ʰȡ���壬���ҵ�ǰδʰȡ�κ�����
        {
            potentialPickableObject = collision.gameObject; // �����ʰȡ���������
            Debug.Log("��⵽��ʰȡ���壺" + potentialPickableObject.name);
        }
    }

    // ��ײ���������Ǳ�ڿ�ʰȡ���������
    private void OnCollisionExit(Collision collision)
    {
        nowCol =null;
        if (collision.gameObject == potentialPickableObject) // ����뿪��������Ǳ�ڿ�ʰȡ����
        {
            Debug.Log("�뿪��ʰȡ���巶Χ��" + potentialPickableObject.name);
            potentialPickableObject = null; // �������
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
