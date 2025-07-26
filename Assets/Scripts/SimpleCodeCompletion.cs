using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// 补全项数据结构
/// </summary>
[System.Serializable]
public class CompletionItemData
{
    public string name;
    public string type;
    public string description;

    public CompletionItemData(string name, string type, string description)
    {
        this.name = name;
        this.type = type;
        this.description = description;
    }
}

public class SimpleCodeCompletion : MonoBehaviour
{
    public static SimpleCodeCompletion Instance;

    [Header("UI Components")]
    [SerializeField] private TMP_InputField codeInputField;
    [SerializeField] private TextMeshProUGUI completionText; // 显示补全选项的文本组件

    [Header("Settings")]
    [SerializeField] private int maxCompletionItems = 9; // 最多显示9个选项（对应数字键1-9）
    [SerializeField] private int minTriggerLength = 1; // 触发补全的最小字符长度
    [SerializeField] private bool enableAutoCompletion = true;

    // 补全数据
    private Dictionary<string, List<string>> completionDatabase = new Dictionary<string, List<string>>();
    private List<string> currentCompletions = new List<string>();
    private List<CompletionItemData> allCompletionItems = new List<CompletionItemData>(); // 存储所有补全项
    private string currentWord = "";
    private int currentWordStartPos = 0;
    private bool isCompletionVisible = false;

    // CSV文件名
    private const string COMPLETION_CSV_NAME = "completion_items";

    void Awake()
    {
        Instance = this;
        InitializeDatabase();
    }

    void Start()
    {
        if (codeInputField != null)
        {
            codeInputField.onValueChanged.AddListener(OnTextChanged);
        }
        
        HideCompletion();
    }

    void Update()
    {
        if (isCompletionVisible && enableAutoCompletion)
        {
            HandleCompletionInput();
        }
    }

    /// <summary>
    /// 初始化补全数据库
    /// </summary>
    private void InitializeDatabase()
    {
        LoadCompletionItemsFromCSV();
        
        foreach (var item in allCompletionItems)
        {
            string key = GetSearchKey(item.name);
            if (!completionDatabase.ContainsKey(key))
            {
                completionDatabase[key] = new List<string>();
            }
            
            if (!completionDatabase[key].Contains(item.name))
            {
                completionDatabase[key].Add(item.name);
            }
        }
    }

    /// <summary>
    /// 从CSV文件加载补全项
    /// </summary>
    private void LoadCompletionItemsFromCSV()
    {
        TextAsset csvFile = Resources.Load<TextAsset>(COMPLETION_CSV_NAME);
        if (csvFile == null)
        {
            Debug.LogError($"[SimpleCodeCompletion] Could not load {COMPLETION_CSV_NAME}.csv from Resources folder!");
            return;
        }

        string[] lines = csvFile.text.Split('\n');
        bool foundHeader = false;
        
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            
            // 跳过空行和注释行
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) 
                continue;
            
            // 跳过标题行
            if (!foundHeader && line.StartsWith("name,"))
            {
                foundHeader = true;
                continue;
            }
            
            if (!foundHeader) continue; // 还没找到标题行就继续找
            
            string[] parts = line.Split(',');
            if (parts.Length >= 3)
            {
                string name = parts[0].Trim();
                string type = parts[1].Trim();
                string description = parts[2].Trim();
                
                // 处理描述中可能包含逗号的情况
                if (parts.Length > 3)
                {
                    for (int j = 3; j < parts.Length; j++)
                    {
                        description += "," + parts[j].Trim();
                    }
                }
                
                allCompletionItems.Add(new CompletionItemData(name, type, description));
            }
        }
    }

    /// <summary>
    /// 获取搜索关键字（取第一个单词）
    /// </summary>
    private string GetSearchKey(string item)
    {
        // 对于 "math.sqrt" 这样的，取 "math"
        // 对于 "print" 这样的，就是 "print"
        int dotIndex = item.IndexOf('.');
        if (dotIndex > 0)
        {
            return item.Substring(0, dotIndex).ToLower();
        }
        
        int spaceIndex = item.IndexOf(' ');
        if (spaceIndex > 0)
        {
            return item.Substring(0, spaceIndex).ToLower();
        }
        
        return item.ToLower();
    }

    /// <summary>
    /// 文本变化事件
    /// </summary>
    private void OnTextChanged(string newText)
    {
        if (!enableAutoCompletion) return;

        ProcessCompletion();
    }

    /// <summary>
    /// 处理补全逻辑
    /// </summary>
    private void ProcessCompletion()
    {
        if (codeInputField == null || !codeInputField.isFocused) return;

        string text = codeInputField.text;
        int caretPos = codeInputField.caretPosition;

        // 获取当前单词
        var wordInfo = GetCurrentWord(text, caretPos);
        currentWord = wordInfo.word;
        currentWordStartPos = wordInfo.startPos;

        if (string.IsNullOrEmpty(currentWord) || currentWord.Length < minTriggerLength)
        {
            HideCompletion();
            return;
        }

        // 搜索匹配项
        var matches = SearchCompletions(currentWord);
        
        if (matches.Count > 0)
        {
            ShowCompletion(matches);
        }
        else
        {
            HideCompletion();
        }
    }

    /// <summary>
    /// 搜索补全项
    /// </summary>
    private List<string> SearchCompletions(string query)
    {
        var results = new List<string>();
        string lowerQuery = query.ToLower();

        // 只匹配以查询字符串开头的项目
        foreach (var item in allCompletionItems)
        {
            if (item.name.ToLower().StartsWith(lowerQuery))
            {
                results.Add(item.name);
            }
        }

        // 限制数量
        return results.Take(maxCompletionItems).ToList();
    }

    /// <summary>
    /// 获取光标位置的当前单词
    /// </summary>
    private (string word, int startPos) GetCurrentWord(string text, int caretPos)
    {
        if (string.IsNullOrEmpty(text) || caretPos <= 0)
            return ("", caretPos);

        // 找到单词的开始位置
        int startPos = caretPos - 1;
        while (startPos >= 0 && IsWordCharacter(text[startPos]))
        {
            startPos--;
        }
        startPos++; // 调整到单词开始位置

        // 提取单词
        string word = "";
        for (int i = startPos; i < caretPos && i < text.Length; i++)
        {
            if (IsWordCharacter(text[i]))
                word += text[i];
            else
                break;
        }

        return (word, startPos);
    }

    /// <summary>
    /// 检查字符是否为单词字符
    /// </summary>
    private bool IsWordCharacter(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }

    /// <summary>
    /// 显示补全选项
    /// </summary>
    private void ShowCompletion(List<string> matches)
    {
        currentCompletions = matches;
        isCompletionVisible = true;

        // 构建显示文本
        string displayText = "Ctrl + [] : ";
        for (int i = 0; i < matches.Count && i < 4; i++)
        {
            displayText += $"[{i + 1}] {matches[i]}    ";
        }

        if (completionText != null)
        {
            completionText.text = displayText.TrimEnd('\n');
            completionText.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 隐藏补全选项
    /// </summary>
    private void HideCompletion()
    {
        isCompletionVisible = false;
        currentCompletions.Clear();

        if (completionText != null)
        {
            completionText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 处理补全输入（数字键选择）
    /// </summary>
    private void HandleCompletionInput()
    {
        // Ctrl + 数字键选择
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            for (int i = 1; i <= 9; i++)
            {
                KeyCode keyCode = KeyCode.Alpha1 + (i - 1);
                if (Input.GetKeyDown(keyCode))
                {
                    SelectCompletion(i - 1); // 转换为0-based索引
                    return;
                }
            }
        }

        // 数字键直接选择（不需要Ctrl）
        for (int i = 1; i <= 9; i++)
        {
            KeyCode keyCode = KeyCode.Alpha1 + (i - 1);
            if (Input.GetKeyDown(keyCode))
            {
                SelectCompletion(i - 1);
                return;
            }
        }
    }

    /// <summary>
    /// 选择补全项
    /// </summary>
    private void SelectCompletion(int index)
    {
        if (index < 0 || index >= currentCompletions.Count) return;

        string selectedItem = currentCompletions[index];
        ApplyCompletion(selectedItem);
        HideCompletion();
    }

    /// <summary>
    /// 应用补全
    /// </summary>
    private void ApplyCompletion(string completionItem)
    {
        if (codeInputField == null) return;

        string text = codeInputField.text;
        int caretPos = codeInputField.caretPosition;

        // 替换当前单词
        string newText = text.Substring(0, currentWordStartPos) + 
                        completionItem + 
                        text.Substring(caretPos);

        codeInputField.text = newText;

        // 设置新的光标位置 - 移动到补全文本的末尾
        int newCaretPos = currentWordStartPos + completionItem.Length;
        
        // 特殊处理：如果是函数调用，添加括号并将光标移到括号内
        if (IsFunction(completionItem))
        {
            if (!completionItem.EndsWith(":") && !completionItem.Contains("("))
            {
                newText = newText.Insert(newCaretPos, "()");
                codeInputField.text = newText;
                newCaretPos += 1; // 光标移到括号内
            }
        }

        StartCoroutine(SetCaretPositionDelayed(newCaretPos));
    }

    /// <summary>
    /// 判断是否为函数
    /// </summary>
    private bool IsFunction(string item)
    {
        // 简单的函数判断逻辑
        string[] functions = { "print", "len", "range", "str", "int", "float", "list", "dict", 
                              "set", "tuple", "abs", "max", "min", "sum", "sorted", "enumerate", 
                              "zip", "map", "filter", "open", "input", "type", "isinstance", 
                              "hasattr", "getattr", "setattr", "dir", "help", "round", "pow",
                              "bin", "oct", "hex", "ord", "chr" };
        
        return functions.Contains(item) || item.Contains(".");
    }

    /// <summary>
    /// 延迟设置光标位置
    /// </summary>
    private IEnumerator SetCaretPositionDelayed(int position)
    {
        // 等待一帧让文本更新完成
        yield return null;
        
        if (codeInputField != null)
        {
            // 确保输入框有焦点
            // codeInputField.ActivateInputField();
            
            // 设置光标位置
            codeInputField.caretPosition = position;
            codeInputField.selectionAnchorPosition = position;
            codeInputField.selectionFocusPosition = position;
            
            // 再等一帧确保设置生效
            yield return null;
            
            // 再次确认光标位置
            codeInputField.caretPosition = position;
            codeInputField.selectionAnchorPosition = position;
            codeInputField.selectionFocusPosition = position;
        }
    }

    /// <summary>
    /// 手动触发补全
    /// </summary>
    public void TriggerCompletion()
    {
        if (enableAutoCompletion)
        {
            ProcessCompletion();
        }
    }

    /// <summary>
    /// 设置启用状态
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        enableAutoCompletion = enabled;
        if (!enabled)
        {
            HideCompletion();
        }
    }

    /// <summary>
    /// 添加用户自定义的补全项
    /// </summary>
    public void AddUserCompletion(string item)
    {
        // 检查是否已存在
        if (allCompletionItems.Any(x => x.name == item))
            return;

        // 添加新的补全项
        allCompletionItems.Add(new CompletionItemData(item, "user_defined", "用户定义"));
        
        // 更新数据库
        string key = GetSearchKey(item);
        if (!completionDatabase.ContainsKey(key))
        {
            completionDatabase[key] = new List<string>();
        }
        
        if (!completionDatabase[key].Contains(item))
        {
            completionDatabase[key].Add(item);
        }
    }

    /// <summary>
    /// 分析用户代码并添加函数和变量
    /// </summary>
    public void AnalyzeUserCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return;

        // 提取函数定义
        Regex funcRegex = new Regex(@"def\s+([a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.Multiline);
        var funcMatches = funcRegex.Matches(code);
        foreach (Match match in funcMatches)
        {
            AddUserCompletion(match.Groups[1].Value);
        }

        // 提取变量定义
        Regex varRegex = new Regex(@"^([a-zA-Z_][a-zA-Z0-9_]*)\s*=", RegexOptions.Multiline);
        var varMatches = varRegex.Matches(code);
        foreach (Match match in varMatches)
        {
            string varName = match.Groups[1].Value;
            if (!IsKeyword(varName))
            {
                AddUserCompletion(varName);
            }
        }

        // 提取类定义
        Regex classRegex = new Regex(@"class\s+([a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.Multiline);
        var classMatches = classRegex.Matches(code);
        foreach (Match match in classMatches)
        {
            AddUserCompletion(match.Groups[1].Value);
        }
    }

    /// <summary>
    /// 检查是否为关键字
    /// </summary>
    private bool IsKeyword(string word)
    {
        string[] keywords = { "and", "as", "assert", "break", "class", "continue", "def", "del", 
                             "elif", "else", "except", "finally", "for", "from", "global", "if", 
                             "import", "in", "is", "lambda", "not", "or", "pass", "raise", 
                             "return", "try", "while", "with", "yield", "False", "None", "True" };
        return keywords.Contains(word);
    }
}
