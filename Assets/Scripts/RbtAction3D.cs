using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class RbtAction3D : MonoBehaviour
{
    public float obstacleDetectionRange = 1f; // �ϰ����ⷶΧ
    public float moveSpeed = 100f; // �ƶ��ٶ�
    public float flySpeed = 2f; // �����ٶ�
    public bool isComplete = true; // ��ʾ�Ƿ����ִ����һ��ָ��

    private GameObject heldObject; // ��ǰʰȡ������
    private GameObject potentialPickableObject; // ��ǰ��ײ�Ŀ�ʰȡ����

    private GameObject nowCol;

    private Rigidbody rb; // �����˵ĸ������
    private RbtAbsorb rbtAbsorb; // �������սű�

    private Vector3 lastPosition; // ��¼��һ�ε�λ��
    private float stuckTimer = 0f; // ��¼��ס��ʱ��
    public float jumpForce = 200f; // ��Ծ��
    //public GameObject sorbPos; // ���ڻ�ȡ��ǰ����λ��


    public GameObject emo_sad;
    public int maxSorbNum = 10; // �����������
    RobotAnimationController robotAnimationController; // �����˶���������

    private void Awake()
    {
        emo_sad.SetActive(false);
        // ��ȡ����������ĸ������
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("������ȱ�� Rigidbody �����");
        }
        // ��ȡObjectAbsorber���
        rbtAbsorb = GetComponent<RbtAbsorb>();
        if (rbtAbsorb == null)
        {
            Debug.LogError("������ȱ�� ObjectAbsorber �����");
        }
        robotAnimationController = GetComponent<RobotAnimationController>();
    }

    // ������ִ����Ϊ�����
    public void ExecuteAction(AIAction action)
    {

        if (!isComplete)
            return; // �����ǰָ��δ��ɣ�������

        isComplete = false; // ��ʼִ��ָ��
        emo_sad.SetActive(false );
        switch (action.actionType)
        {
            case "move":
                Debug.Log(gameObject.name+"rbt��ʼ�ƶ���Ŀ��λ�ã�" + (int)action.targetPosition.x+"  "+(int)action.targetPosition.z);
                StartCoroutine(MoveTo(new Vector2((int)action.targetPosition.x, (int)action.targetPosition.z))); // ֻ��Ŀ��� x ����
                break;


            case "build"://����
                //DropObjectFromSorb();
                break;

            case "absorb":
                StartCoroutine(AbsorbAndReset(maxSorbNum)); // �������չ��̲�����ɺ�ָ�����
                break;

            default:
                Debug.LogWarning("δ֪��Ϊ���ͣ�" + action.actionType);
                isComplete = true; // δָ֪��ֱ�ӱ�����
                break;
        }

    }
    // ��⿨����������Ծ
    private void DetectAndJumpIfStuck(Vector3 direction)
    {
        if (!isComplete && Vector3.Distance(transform.position, lastPosition) < 0.1f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= 2f) // ���������2���ڿ�ס�ˣ�����û���ƶ�
            {
                Debug.Log("���忨ס�ˣ�������Ծ��");
                //emo_sad.SetActive(true);
                robotAnimationController.SetAnimationState("Sad"); // ���û����˶���״̬Ϊ����

                // ʩ����Ծ�������ڿ�ס������ʩ��һ����ˮƽ��
                Vector3 jumpDirection = Vector3.up * jumpForce + direction.normalized * (jumpForce * 0.5f);
                rb.AddForce(jumpDirection, ForceMode.Impulse);

                stuckTimer = 0f; // ���ÿ�ס��ʱ��
            }
        }
        else
        {
            // ��������ƶ��ˣ����ÿ�ס��ʱ��
            stuckTimer = 0f;
            //emo_sad.SetActive(false);
        }

        // �����ϴε�λ��
        lastPosition = transform.position;
    }

    private System.Collections.IEnumerator CloseEmoAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        emo_sad.SetActive(false);
    }


    // ���ղ�������ɺ�����״̬
    public IEnumerator AbsorbAndReset(int maxNum)
    {
        int sorbNum =Random.Range(2,maxNum); // ���������������Χ��2��maxNum
        rbtAbsorb.AbsorbObjects(maxNum); // ִ�����ղ���
        yield return new WaitForSeconds(1f); // �ȴ����չ�����ɣ����Ը�������ʱ�����

        //isComplete = true; // ������ɺ�����ִ����һ������
        Debug.Log("������ɣ�����ִ����һ��ָ�");
    }

    // ����ʹ�ã�ÿ֡���ã������ƶ�����
    public IEnumerator MoveTo(Vector2 targetPosition)
    {
        // ��ȡĿ������ά����
        Vector3 target3DPosition = new Vector3(targetPosition.x, transform.position.y-6f, targetPosition.y);
        target3DPosition.y = Mathf.Clamp(target3DPosition.y, -6f, 200f);
        target3DPosition.x = Mathf.Clamp(target3DPosition.x, -200f, 200f);
        target3DPosition.z = Mathf.Clamp(target3DPosition.z, -200f, 200f);

        // �����ʼ����
        float distanceToTarget = Vector3.Distance(target3DPosition, transform.position);

        // ���嵽�������Χ
        float acceptableRange = 4f; // �ɸ�����Ҫ������Χ

        // ���������ʱ��
        float maxMoveTime = 10f; // ��ƶ�ʱ�䣨�룩
        float elapsedTime = 0f; // �Ѿ�����ʱ��

        // �ƶ�ѭ��
        while (distanceToTarget > acceptableRange && elapsedTime < maxMoveTime)
        {
            // ���㷽��
            Vector3 directionToTarget = (target3DPosition - transform.position).normalized;

            // ����Ƿ�ס����Ծ
            DetectAndJumpIfStuck(directionToTarget);

            // ʩ���ƶ���
            rb.AddForce(directionToTarget * moveSpeed, ForceMode.Acceleration);

            // ���¾���
            distanceToTarget = Vector3.Distance(target3DPosition, transform.position);

            // ��������ʱ��
            elapsedTime += Time.deltaTime;

            yield return null; // �ȴ�һ֡
        }

        if (distanceToTarget <= acceptableRange)
        {
            Debug.Log(gameObject.name+"rbt�����˽ӽ�Ŀ��λ�ã�Ŀ��λ�ã�" + target3DPosition);
        }
        else
        {
            Debug.LogWarning("������δ�ܵ���Ŀ��λ�ã����������ʱ�䣡");
        }

        isComplete = true; // ������״̬
    }

    public IEnumerator MoveTo3D(Vector3 targetPosition)
    {
        // ��ȡĿ������ά����
        Vector3 target3DPosition = new Vector3(targetPosition.x, transform.position.y, targetPosition.y);

        // �����ʼ����
        float distanceToTarget = Vector3.Distance(target3DPosition, transform.position);

        // ���嵽�������Χ
        float acceptableRange = 4f; // �ɸ�����Ҫ������Χ

        // ���������ʱ��
        float maxMoveTime = 100f; // ��ƶ�ʱ�䣨�룩
        float elapsedTime = 0f; // �Ѿ�����ʱ��

        // �ƶ�ѭ��
        while (distanceToTarget > acceptableRange && elapsedTime < maxMoveTime)
        {
            // ���㷽��
            Vector3 directionToTarget = (target3DPosition - transform.position).normalized;

            // ����Ƿ�ס����Ծ
            DetectAndJumpIfStuck(directionToTarget);

            // ʩ���ƶ���
            rb.AddForce(directionToTarget * moveSpeed, ForceMode.Acceleration);

            // ���¾���
            distanceToTarget = Vector3.Distance(target3DPosition, transform.position);

            // ��������ʱ��
            elapsedTime += Time.deltaTime;

            yield return null; // �ȴ�һ֡
        }

        if (distanceToTarget <= acceptableRange)
        {
            Debug.Log("�����˽ӽ�Ŀ��λ�ã�Ŀ��λ�ã�" + target3DPosition);
        }
        else
        {
            Debug.LogWarning("������δ�ܵ���Ŀ��λ�ã����������ʱ�䣡");
        }

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
        nowCol = null;
        if (collision.gameObject == potentialPickableObject) // ����뿪��������Ǳ�ڿ�ʰȡ����
        {
            Debug.Log("�뿪��ʰȡ���巶Χ��" + potentialPickableObject.name);
            potentialPickableObject = null; // �������
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
