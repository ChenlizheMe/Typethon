using UnityEngine;
using System.Collections;

public class RobotAnimationController : MonoBehaviour
{
    // �������ݿ�
    [Header("�������ݿ�")]
    public GameObject idleBubble;   // ���ñ���
    public GameObject happyBubble; // ���ı���
    public GameObject sadBubble;   // ���ı���

    // ֡�������
    [Header("֡����")]
    public Animator idleFrameAnimator; // ���ڲ���֡������ Animator

    // ��ǰ״̬
    private string currentState;

    /// <summary>
    /// �л���ָ������״̬
    /// </summary>
    /// <param name="state">Ŀ��״̬��"Idle", "Happy", "Sad"��</param>
    public void SetAnimationState(string state)
    {
        if (currentState == state) return; // ���״̬δ�ı䣬���л�

        // �ر����б������ݿ�
        idleBubble?.SetActive(false);
        happyBubble?.SetActive(false);
        sadBubble?.SetActive(false);

        // ����״̬�����Ӧ�ı������ݿ��֡����
        switch (state)
        {
            case "Idle":
                idleBubble?.SetActive(true);
                //PlayFrameAnimation("Idle");
                break;
            case "Happy":
                happyBubble?.SetActive(true);
                StartCoroutine(RevertToIdleAfterDelay(2f));
                //PlayFrameAnimation("Happy");
                break;
            case "Sad":
                sadBubble?.SetActive(true);
                StartCoroutine(RevertToIdleAfterDelay(2f));
                //PlayFrameAnimation("Sad");
                break;
            default:
                Debug.LogWarning($"δ֪�Ķ���״̬: {state}");
                break;
        }

        currentState = state; // ���µ�ǰ״̬
    }

    private IEnumerator RevertToIdleAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetAnimationState("Idle");
    }
    /// <summary>
    /// ����֡����
    /// </summary>
    /// <param name="animationName">��������</param>
    //private void PlayFrameAnimation(string animationName)
    //{
    //    if (frameAnimator != null)
    //    {
    //        frameAnimator.Play(animationName);
    //    }
    //}

    /// <summary>
    /// �ṩ�������ű��Ľӿڣ������л�������״̬
    /// </summary>
    public void SetIdle()
    {
        SetAnimationState("Idle");
    }

    /// <summary>
    /// �ṩ�������ű��Ľӿڣ������л�������״̬
    /// </summary>
    public void SetHappy()
    {
        SetAnimationState("Happy");
    }

    /// <summary>
    /// �ṩ�������ű��Ľӿڣ������л�������״̬
    /// </summary>
    public void SetSad()
    {
        SetAnimationState("Sad");
    }
}
