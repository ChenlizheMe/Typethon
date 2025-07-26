using UnityEngine;

public class FaceCamera2D : MonoBehaviour
{
    public Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (mainCamera != null)
        {
            Vector3 targetPosition = mainCamera.transform.position;
            Vector3 currentPosition = transform.position;

            // ��������ͷ��ˮƽ���������������Y��
            Vector3 direction = new Vector3(
                targetPosition.x - currentPosition.x,
                0,
                targetPosition.z - currentPosition.z
            );

            // �������Ϊ�㣬�Ž�����ת
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(-direction); // ȡ����ȷ�����泯�������
                transform.rotation = targetRotation;
            }
        }
    }
}
