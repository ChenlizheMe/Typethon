using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 强制阻止特定按键的TMP输入框
/// 使用反射和多重拦截机制确保完全阻止
/// </summary>
public class ForceBlockTMPInput : TMP_InputField
{
    [Header("Blocked Keys")]
    [SerializeField] private List<KeyCode> blockedKeys = new List<KeyCode>() 
    { 
        KeyCode.PageUp, 
        KeyCode.PageDown 
    };
    
    private bool isProcessingInput = false;
    
    /// <summary>
    /// 最高优先级拦截 - OnUpdateSelected
    /// </summary>
    public override void OnUpdateSelected(BaseEventData eventData)
    {
        if (isProcessingInput) return;
        
        // 检查是否有被阻止的按键被按下
        foreach (var key in blockedKeys)
        {
            if (Input.GetKeyDown(key))
            {
                Debug.Log($"FORCE BLOCKED in OnUpdateSelected: {key}");
                return; // 完全阻止，不调用base
            }
        }
        
        isProcessingInput = true;
        base.OnUpdateSelected(eventData);
        isProcessingInput = false;
    }
    
    /// <summary>
    /// 二级拦截 - ProcessEvent
    /// </summary>
    public new void ProcessEvent(Event e)
    {
        if (e == null) return;
        
        if (e.type == EventType.KeyDown && blockedKeys.Contains(e.keyCode))
        {
            Debug.Log($"FORCE BLOCKED in ProcessEvent: {e.keyCode}");
            e.Use();
            return;
        }
        
        base.ProcessEvent(e);
    }
    
    /// <summary>
    /// 三级拦截 - LateUpdate
    /// </summary>
    protected override void LateUpdate()
    {
        if (isProcessingInput) 
        {
            base.LateUpdate();
            return;
        }
        
        // 在LateUpdate之前进行最后一次检查
        if (isFocused)
        {
            foreach (var key in blockedKeys)
            {
                if (Input.GetKeyDown(key))
                {
                    Debug.Log($"FORCE BLOCKED in LateUpdate: {key}");
                    return; // 不调用base.LateUpdate()
                }
            }
        }
        
        base.LateUpdate();
    }
    
    /// <summary>
    /// 四级拦截 - Update（更早的拦截点）
    /// </summary>
    protected virtual void Update()
    {
        if (isFocused)
        {
            foreach (var key in blockedKeys)
            {
                if (Input.GetKeyDown(key))
                {
                    Debug.Log($"FORCE BLOCKED in Update: {key}");
                    // 消费这个输入事件
                    Input.ResetInputAxes();
                    return;
                }
            }
        }
    }
    
    /// <summary>
    /// 终极拦截 - 通过GUI事件系统
    /// </summary>
    private void OnGUI()
    {
        if (!isFocused) return;
        
        Event e = Event.current;
        if (e != null && e.type == EventType.KeyDown && blockedKeys.Contains(e.keyCode))
        {
            Debug.Log($"FORCE BLOCKED in OnGUI: {e.keyCode}");
            e.Use();
        }
    }
    
    /// <summary>
    /// 添加要阻止的按键
    /// </summary>
    /// <param name="keyCode">按键代码</param>
    public void AddBlockedKey(KeyCode keyCode)
    {
        if (!blockedKeys.Contains(keyCode))
        {
            blockedKeys.Add(keyCode);
            Debug.Log($"Added blocked key: {keyCode}");
        }
    }
    
    /// <summary>
    /// 移除阻止的按键
    /// </summary>
    /// <param name="keyCode">按键代码</param>
    public void RemoveBlockedKey(KeyCode keyCode)
    {
        if (blockedKeys.Remove(keyCode))
        {
            Debug.Log($"Removed blocked key: {keyCode}");
        }
    }
    
    /// <summary>
    /// 清空所有阻止的按键
    /// </summary>
    public void ClearBlockedKeys()
    {
        blockedKeys.Clear();
        Debug.Log("Cleared all blocked keys");
    }
    
    /// <summary>
    /// 获取当前被阻止的按键列表
    /// </summary>
    /// <returns>被阻止的按键列表</returns>
    public List<KeyCode> GetBlockedKeys()
    {
        return new List<KeyCode>(blockedKeys);
    }
}
