using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

/// <summary>
/// 高级自定义TMP输入框，通过反射完全控制键盘输入
/// </summary>
public class AdvancedCustomTMPInput : TMP_InputField
{
    private MethodInfo originalKeyPressed;
    
    protected override void Awake()
    {
        base.Awake();
        
        // 通过反射获取原始的KeyPressed方法
        originalKeyPressed = typeof(TMP_InputField).GetMethod("KeyPressed", 
            BindingFlags.NonPublic | BindingFlags.Instance);
    }

    /// <summary>
    /// 完全自定义的ProcessEvent，绕过原始的KeyPressed
    /// </summary>
    /// <param name="e">键盘事件</param>
    public new void ProcessEvent(Event e)
    {
        // 完全使用自定义的键盘处理
        CustomKeyPressed(e);
    }
    
    /// <summary>
    /// 完全自定义的键盘处理方法
    /// </summary>
    /// <param name="e">键盘事件</param>
    protected virtual void CustomKeyPressed(Event e)
    {
        if (!isFocused)
            return;
            
        switch (e.type)
        {
            case EventType.KeyDown:
                HandleKeyDown(e);
                break;
            case EventType.KeyUp:
                HandleKeyUp(e);
                break;
        }
    }
    
    /// <summary>
    /// 处理按键按下事件
    /// </summary>
    /// <param name="e">键盘事件</param>
    protected virtual void HandleKeyDown(Event e)
    {
        switch (e.keyCode)
        {
            case KeyCode.PageUp:
                Debug.Log("Advanced Custom: Page Up pressed");
                HandlePageUp(e);
                break;
                
            case KeyCode.PageDown:
                Debug.Log("Advanced Custom: Page Down pressed");
                HandlePageDown(e);
                break;
                
            case KeyCode.Tab:
                Debug.Log("Advanced Custom: Tab pressed");
                HandleTab(e);
                break;
                
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                Debug.Log("Advanced Custom: Enter pressed");
                HandleEnter(e);
                break;
                
            case KeyCode.Escape:
                Debug.Log("Advanced Custom: Escape pressed");
                HandleEscape(e);
                break;
                
            default:
                // 对于其他键，可以选择调用原始处理或自定义处理
                HandleOtherKeys(e);
                break;
        }
    }
    
    /// <summary>
    /// 处理按键释放事件
    /// </summary>
    /// <param name="e">键盘事件</param>
    protected virtual void HandleKeyUp(Event e)
    {
        // 在这里处理按键释放逻辑
        Debug.Log($"Advanced Custom: Key up - {e.keyCode}");
    }
    
    /// <summary>
    /// 处理Page Up键
    /// </summary>
    protected virtual void HandlePageUp(Event e)
    {
        // 自定义Page Up逻辑
        // 例如：向上滚动多行
        var textInfo = textComponent.textInfo;
        if (textInfo.lineCount > 1)
        {
            // 计算当前行
            int currentLine = textComponent.textInfo.characterInfo[caretPosition].lineNumber;
            int targetLine = Mathf.Max(0, currentLine - 10); // 向上10行
            
            // 移动光标到目标行
            if (targetLine < textInfo.lineCount)
            {
                caretPosition = textInfo.lineInfo[targetLine].firstCharacterIndex;
            }
        }
        e.Use(); // 标记事件已处理
    }
    
    /// <summary>
    /// 处理Page Down键
    /// </summary>
    protected virtual void HandlePageDown(Event e)
    {
        // 自定义Page Down逻辑
        var textInfo = textComponent.textInfo;
        if (textInfo.lineCount > 1)
        {
            int currentLine = textComponent.textInfo.characterInfo[caretPosition].lineNumber;
            int targetLine = Mathf.Min(textInfo.lineCount - 1, currentLine + 10); // 向下10行
            
            if (targetLine < textInfo.lineCount)
            {
                caretPosition = textInfo.lineInfo[targetLine].firstCharacterIndex;
            }
        }
        e.Use();
    }
    
    /// <summary>
    /// 处理Tab键
    /// </summary>
    protected virtual void HandleTab(Event e)
    {
        if (e.shift)
        {
            // Shift+Tab: 减少缩进
            Debug.Log("Shift+Tab: Decrease indent");
            // 在这里实现减少缩进逻辑
        }
        else
        {
            // Tab: 增加缩进
            Debug.Log("Tab: Increase indent");
            // 在这里实现增加缩进逻辑
            InsertText("    "); // 插入4个空格作为缩进
        }
        e.Use();
    }
    
    /// <summary>
    /// 处理Enter键
    /// </summary>
    protected virtual void HandleEnter(Event e)
    {
        if (multiLine)
        {
            // 多行模式下插入换行
            InsertText("\n");
            Debug.Log("Enter: New line inserted");
        }
        else
        {
            // 单行模式下可能提交或执行其他操作
            Debug.Log("Enter: Submit or execute");
            // 调用提交事件
            onSubmit?.Invoke(text);
        }
        e.Use();
    }
    
    /// <summary>
    /// 处理Escape键
    /// </summary>
    protected virtual void HandleEscape(Event e)
    {
        Debug.Log("Escape: Cancel or clear");
        // 可以实现取消编辑、清空文本等逻辑
        DeactivateInputField();
        e.Use();
    }
    
    /// <summary>
    /// 处理其他键
    /// </summary>
    protected virtual void HandleOtherKeys(Event e)
    {
        // 对于普通文本输入，可以选择调用原始方法
        if (originalKeyPressed != null && ShouldUseOriginalHandling(e))
        {
            try
            {
                originalKeyPressed.Invoke(this, new object[] { e });
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to invoke original KeyPressed: {ex.Message}");
                // 降级到基本文本处理
                HandleBasicTextInput(e);
            }
        }
        else
        {
            // 使用自定义的基本文本处理
            HandleBasicTextInput(e);
        }
    }
    
    /// <summary>
    /// 判断是否应该使用原始处理
    /// </summary>
    protected virtual bool ShouldUseOriginalHandling(Event e)
    {
        // 对于普通字符输入，使用原始处理
        return char.IsControl(e.character) == false || 
               e.keyCode == KeyCode.Backspace || 
               e.keyCode == KeyCode.Delete ||
               e.keyCode == KeyCode.LeftArrow ||
               e.keyCode == KeyCode.RightArrow ||
               e.keyCode == KeyCode.UpArrow ||
               e.keyCode == KeyCode.DownArrow;
    }
    
    /// <summary>
    /// 基本的文本输入处理
    /// </summary>
    protected virtual void HandleBasicTextInput(Event e)
    {
        if (!char.IsControl(e.character))
        {
            // 插入普通字符
            InsertText(e.character.ToString());
            e.Use();
        }
        else if (e.keyCode == KeyCode.Backspace)
        {
            // 处理退格
            if (caretPosition > 0)
            {
                int deletePos = caretPosition - 1;
                text = text.Remove(deletePos, 1);
                caretPosition = deletePos;
            }
            e.Use();
        }
        else if (e.keyCode == KeyCode.Delete)
        {
            // 处理删除
            if (caretPosition < text.Length)
            {
                text = text.Remove(caretPosition, 1);
            }
            e.Use();
        }
        // 可以继续添加其他基本操作的处理
    }
    
    /// <summary>
    /// 插入文本的辅助方法
    /// </summary>
    protected virtual void InsertText(string insertString)
    {
        text = text.Insert(caretPosition, insertString);
        caretPosition += insertString.Length;
    }
}
