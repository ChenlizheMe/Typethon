using UnityEngine;
using System.Collections;

public class RobotAnimationController : MonoBehaviour
{
    // 表情气泡框
    [Header("表情气泡框")]
    public GameObject idleBubble;   // 闲置表情
    public GameObject happyBubble; // 开心表情
    public GameObject sadBubble;   // 伤心表情

    // 帧动画组件
    [Header("帧动画")]
    public Animator idleFrameAnimator; // 用于播放帧动画的 Animator

    // 当前状态
    private string currentState;

    /// <summary>
    /// 切换到指定动画状态
    /// </summary>
    /// <param name="state">目标状态（"Idle", "Happy", "Sad"）</param>
    public void SetAnimationState(string state)
    {
        if (currentState == state) return; // 如果状态未改变，则不切换

        // 关闭所有表情气泡框
        idleBubble?.SetActive(false);
        happyBubble?.SetActive(false);
        sadBubble?.SetActive(false);

        // 根据状态激活对应的表情气泡框或帧动画
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
                Debug.LogWarning($"未知的动画状态: {state}");
                break;
        }

        currentState = state; // 更新当前状态
    }

    private IEnumerator RevertToIdleAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetAnimationState("Idle");
    }
    /// <summary>
    /// 播放帧动画
    /// </summary>
    /// <param name="animationName">动画名称</param>
    //private void PlayFrameAnimation(string animationName)
    //{
    //    if (frameAnimator != null)
    //    {
    //        frameAnimator.Play(animationName);
    //    }
    //}

    /// <summary>
    /// 提供给其他脚本的接口，用于切换到闲置状态
    /// </summary>
    public void SetIdle()
    {
        SetAnimationState("Idle");
    }

    /// <summary>
    /// 提供给其他脚本的接口，用于切换到开心状态
    /// </summary>
    public void SetHappy()
    {
        SetAnimationState("Happy");
    }

    /// <summary>
    /// 提供给其他脚本的接口，用于切换到伤心状态
    /// </summary>
    public void SetSad()
    {
        SetAnimationState("Sad");
    }
}
