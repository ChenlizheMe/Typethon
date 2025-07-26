using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class CustomTMPInput : TMP_InputField
{

    /// <summary>
    /// 重写OnUpdateSelected来拦截键盘输入
    /// </summary>
    /// <param name="eventData">事件数据</param>
    public override void OnUpdateSelected(BaseEventData eventData)
    {
        if (!FileSystemBrowser.Instance.IsInEditMode())
        {
            return;
        }
        // 检查当前帧是否有我们要拦截的按键
        if (Input.GetKeyDown(KeyCode.PageUp))
        {
            Debug.Log("Blocked: PageUp in OnUpdateSelected");
            // 直接返回，不调用base方法，完全阻止处理
            return;
        }

        if (Input.GetKeyDown(KeyCode.PageDown))
        {
            Debug.Log("Blocked: PageDown in OnUpdateSelected");
            // 直接返回，不调用base方法，完全阻止处理
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("Blocked: Escape in OnUpdateSelected");
            // 直接返回，不调用base方法，完全阻止处理
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return))
        {
            TextHelper.Instance.MoveObjectToCaretPosition();
        }

        // 对于其他输入，调用原始的OnUpdateSelected
        base.OnUpdateSelected(eventData);
    }
}
