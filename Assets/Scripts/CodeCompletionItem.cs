using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 代码补全项UI组件
/// </summary>
public class CodeCompletionItem : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI typeText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image typeIcon;

    [Header("Visual Settings")]
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color selectedTextColor = Color.cyan;
    [SerializeField] private Color normalBackgroundColor = Color.clear;
    [SerializeField] private Color selectedBackgroundColor = new Color(0.2f, 0.4f, 0.8f, 0.3f);

    private CompletionItem item;
    private int index;
    private CodeCompletionManager manager;
    private bool isSelected = false;

    /// <summary>
    /// 初始化补全项
    /// </summary>
    public void Initialize(CompletionItem completionItem, int itemIndex, CodeCompletionManager completionManager)
    {
        item = completionItem;
        index = itemIndex;
        manager = completionManager;

        UpdateDisplay();
        SetSelected(false);
    }

    /// <summary>
    /// 更新显示内容
    /// </summary>
    private void UpdateDisplay()
    {
        if (item == null) return;

        // 设置名称
        if (nameText != null)
        {
            nameText.text = item.Name;
        }

        // 设置类型
        if (typeText != null)
        {
            typeText.text = GetTypeDisplayName(item.Type);
        }

        // 设置描述
        if (descriptionText != null)
        {
            descriptionText.text = item.Description;
        }

        // 设置类型图标
        if (typeIcon != null)
        {
            // 这里可以根据类型设置不同的图标
            // 目前使用颜色来区分类型
            typeIcon.color = GetTypeColor(item.Type);
        }
    }

    /// <summary>
    /// 设置选中状态
    /// </summary>
    public void SetSelected(bool selected)
    {
        isSelected = selected;

        // 更新文本颜色
        Color textColor = selected ? selectedTextColor : normalTextColor;
        if (nameText != null) nameText.color = textColor;
        if (typeText != null) typeText.color = textColor * 0.8f; // 稍微暗一些
        if (descriptionText != null) descriptionText.color = textColor * 0.7f; // 更暗一些

        // 更新背景颜色
        if (backgroundImage != null)
        {
            backgroundImage.color = selected ? selectedBackgroundColor : normalBackgroundColor;
        }
    }

    /// <summary>
    /// 获取类型显示名称
    /// </summary>
    private string GetTypeDisplayName(CompletionType type)
    {
        switch (type)
        {
            case CompletionType.Keyword: return "关键字";
            case CompletionType.Function: return "函数";
            case CompletionType.Variable: return "变量";
            case CompletionType.Class: return "类";
            case CompletionType.Module: return "模块";
            case CompletionType.Constant: return "常量";
            case CompletionType.Method: return "方法";
            case CompletionType.Property: return "属性";
            default: return "其他";
        }
    }

    /// <summary>
    /// 获取类型颜色
    /// </summary>
    private Color GetTypeColor(CompletionType type)
    {
        switch (type)
        {
            case CompletionType.Keyword: return new Color(1f, 0.5f, 0.5f); // 红色
            case CompletionType.Function: return new Color(0.5f, 1f, 0.5f); // 绿色
            case CompletionType.Variable: return new Color(0.5f, 0.5f, 1f); // 蓝色
            case CompletionType.Class: return new Color(1f, 1f, 0.5f); // 黄色
            case CompletionType.Module: return new Color(1f, 0.5f, 1f); // 紫色
            case CompletionType.Constant: return new Color(0.5f, 1f, 1f); // 青色
            case CompletionType.Method: return new Color(0.8f, 1f, 0.5f); // 浅绿色
            case CompletionType.Property: return new Color(1f, 0.8f, 0.5f); // 橙色
            default: return Color.white;
        }
    }

    /// <summary>
    /// 点击事件处理
    /// </summary>
    public void OnClick()
    {
        if (manager != null)
        {
            manager.SelectCompletion(index);
        }
    }

    /// <summary>
    /// 鼠标进入事件
    /// </summary>
    public void OnMouseEnter()
    {
        if (!isSelected)
        {
            // 可以添加鼠标悬停效果
            if (backgroundImage != null)
            {
                backgroundImage.color = selectedBackgroundColor * 0.5f;
            }
        }
    }

    /// <summary>
    /// 鼠标离开事件
    /// </summary>
    public void OnMouseExit()
    {
        if (!isSelected)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = normalBackgroundColor;
            }
        }
    }
}
