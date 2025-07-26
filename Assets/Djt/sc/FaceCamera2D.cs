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

            // 计算摄像头在水平方向的向量（忽略Y）
            Vector3 direction = new Vector3(
                targetPosition.x - currentPosition.x,
                0,
                targetPosition.z - currentPosition.z
            );

            // 如果方向不为零，才进行旋转
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(-direction); // 取反，确保正面朝向摄像机
                transform.rotation = targetRotation;
            }
        }
    }
}
