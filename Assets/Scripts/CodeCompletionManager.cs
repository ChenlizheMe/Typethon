using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;
using System.Text.RegularExpressions;
using System;

public class CodeCompletionManager : MonoBehaviour
{
    public static CodeCompletionManager Instance;

    [Header("UI Components")]
    [SerializeField] private TMP_InputField codeInputField;
    [SerializeField] private GameObject completionPanel; // 补全提示面板
    [SerializeField] private Transform completionContent; // 补全项的父容器
    [SerializeField] private GameObject completionItemPrefab; // 补全项预制体
    [SerializeField] private ScrollRect completionScrollRect; // 滚动组件

    [Header("Completion Settings")]
    [SerializeField] private int maxCompletionItems = 10; // 最大显示补全项数量
    [SerializeField] private int minTriggerLength = 1; // 触发补全的最小字符长度
    [SerializeField] private bool enableAutoCompletion = true; // 是否启用自动补全
    [SerializeField] private bool enableSmartSuggestions = true; // 是否启用智能建议
    [SerializeField] private float completionDelay = 0.3f; // 补全延迟时间
    [SerializeField] private bool caseSensitive = false; // 是否区分大小写

    [Header("Visual Settings")]
    [SerializeField] private Color normalItemColor = Color.white;
    [SerializeField] private Color selectedItemColor = Color.cyan;
    [SerializeField] private Color backgroundSelectedColor = new Color(0.2f, 0.4f, 0.8f, 0.3f);
    [SerializeField] private float itemHeight = 25f; // 补全项高度

    // 内部数据
    private List<CompletionItem> currentCompletions = new List<CompletionItem>();
    private List<GameObject> completionItemObjects = new List<GameObject>();
    private int selectedCompletionIndex = -1;
    private string currentWord = "";
    private int currentWordStartPos = 0;
    private bool isCompletionVisible = false;
    private Coroutine completionCoroutine;

    // 补全数据库
    private CompletionDatabase completionDatabase;
    
    // 关键字和内置函数数据
    private static readonly string[] PYTHON_KEYWORDS = {
        "and", "as", "assert", "async", "await", "break", "class", "continue", "def", "del", 
        "elif", "else", "except", "finally", "for", "from", "global", "if", "import", "in", 
        "is", "lambda", "nonlocal", "not", "or", "pass", "raise", "return", "try", "while", 
        "with", "yield", "False", "None", "True"
    };

    private static readonly CompletionItem[] PYTHON_BUILTINS = {
        new CompletionItem("print", CompletionType.Function, "print(*objects, sep=' ', end='\\n', file=sys.stdout, flush=False)", "打印输出"),
        new CompletionItem("len", CompletionType.Function, "len(obj)", "返回对象的长度"),
        new CompletionItem("range", CompletionType.Function, "range([start,] stop[, step])", "生成数字序列"),
        new CompletionItem("str", CompletionType.Function, "str(object='')", "将对象转换为字符串"),
        new CompletionItem("int", CompletionType.Function, "int([x]) or int(x, base=10)", "将对象转换为整数"),
        new CompletionItem("float", CompletionType.Function, "float([x])", "将对象转换为浮点数"),
        new CompletionItem("list", CompletionType.Function, "list([iterable])", "创建列表"),
        new CompletionItem("dict", CompletionType.Function, "dict(**kwarg) or dict(mapping, **kwarg) or dict(iterable, **kwarg)", "创建字典"),
        new CompletionItem("set", CompletionType.Function, "set([iterable])", "创建集合"),
        new CompletionItem("tuple", CompletionType.Function, "tuple([iterable])", "创建元组"),
        new CompletionItem("abs", CompletionType.Function, "abs(x)", "返回数字的绝对值"),
        new CompletionItem("max", CompletionType.Function, "max(iterable, *[, key, default]) or max(arg1, arg2, *args[, key])", "返回最大值"),
        new CompletionItem("min", CompletionType.Function, "min(iterable, *[, key, default]) or min(arg1, arg2, *args[, key])", "返回最小值"),
        new CompletionItem("sum", CompletionType.Function, "sum(iterable[, start])", "求和"),
        new CompletionItem("sorted", CompletionType.Function, "sorted(iterable, *, key=None, reverse=False)", "返回排序后的列表"),
        new CompletionItem("enumerate", CompletionType.Function, "enumerate(iterable, start=0)", "返回枚举对象"),
        new CompletionItem("zip", CompletionType.Function, "zip(*iterables)", "将多个可迭代对象打包"),
        new CompletionItem("map", CompletionType.Function, "map(function, iterable, ...)", "对可迭代对象应用函数"),
        new CompletionItem("filter", CompletionType.Function, "filter(function, iterable)", "过滤可迭代对象"),
        new CompletionItem("open", CompletionType.Function, "open(file, mode='r', buffering=-1, encoding=None, errors=None, newline=None, closefd=True, opener=None)", "打开文件"),
        new CompletionItem("input", CompletionType.Function, "input([prompt])", "获取用户输入"),
        new CompletionItem("type", CompletionType.Function, "type(object)", "返回对象的类型"),
        new CompletionItem("isinstance", CompletionType.Function, "isinstance(obj, class_or_tuple)", "检查对象是否为指定类型的实例"),
        new CompletionItem("hasattr", CompletionType.Function, "hasattr(obj, name)", "检查对象是否有指定属性"),
        new CompletionItem("getattr", CompletionType.Function, "getattr(obj, name[, default])", "获取对象的属性值"),
        new CompletionItem("setattr", CompletionType.Function, "setattr(obj, name, value)", "设置对象的属性值"),
        new CompletionItem("delattr", CompletionType.Function, "delattr(obj, name)", "删除对象的属性"),
        new CompletionItem("dir", CompletionType.Function, "dir([object])", "返回对象的属性和方法列表"),
        new CompletionItem("help", CompletionType.Function, "help([object])", "获取对象的帮助信息"),
        new CompletionItem("id", CompletionType.Function, "id(object)", "返回对象的内存地址"),
        new CompletionItem("hash", CompletionType.Function, "hash(object)", "返回对象的哈希值"),
        new CompletionItem("round", CompletionType.Function, "round(number[, ndigits])", "四舍五入"),
        new CompletionItem("pow", CompletionType.Function, "pow(base, exp[, mod])", "幂运算"),
        new CompletionItem("divmod", CompletionType.Function, "divmod(a, b)", "返回商和余数"),
        new CompletionItem("bin", CompletionType.Function, "bin(x)", "转换为二进制字符串"),
        new CompletionItem("oct", CompletionType.Function, "oct(x)", "转换为八进制字符串"),
        new CompletionItem("hex", CompletionType.Function, "hex(x)", "转换为十六进制字符串"),
        new CompletionItem("ord", CompletionType.Function, "ord(c)", "返回字符的Unicode码"),
        new CompletionItem("chr", CompletionType.Function, "chr(i)", "返回Unicode码对应的字符"),
        new CompletionItem("any", CompletionType.Function, "any(iterable)", "如果任一元素为真则返回True"),
        new CompletionItem("all", CompletionType.Function, "all(iterable)", "如果所有元素为真则返回True"),
        new CompletionItem("next", CompletionType.Function, "next(iterator[, default])", "获取迭代器的下一个元素"),
        new CompletionItem("iter", CompletionType.Function, "iter(object[, sentinel])", "创建迭代器"),
        new CompletionItem("reversed", CompletionType.Function, "reversed(seq)", "返回反向迭代器"),
        new CompletionItem("globals", CompletionType.Function, "globals()", "返回全局命名空间"),
        new CompletionItem("locals", CompletionType.Function, "locals()", "返回本地命名空间"),
        new CompletionItem("vars", CompletionType.Function, "vars([object])", "返回对象的__dict__属性"),
        new CompletionItem("exec", CompletionType.Function, "exec(object[, globals[, locals]])", "执行Python代码"),
        new CompletionItem("eval", CompletionType.Function, "eval(expression[, globals[, locals]])", "计算表达式的值"),
        new CompletionItem("compile", CompletionType.Function, "compile(source, filename, mode[, flags[, dont_inherit[, optimize]]])", "编译源代码"),
        new CompletionItem("callable", CompletionType.Function, "callable(object)", "检查对象是否可调用"),
        new CompletionItem("classmethod", CompletionType.Function, "classmethod(function)", "类方法装饰器"),
        new CompletionItem("staticmethod", CompletionType.Function, "staticmethod(function)", "静态方法装饰器"),
        new CompletionItem("property", CompletionType.Function, "property(fget=None, fset=None, fdel=None, doc=None)", "属性装饰器"),
        new CompletionItem("super", CompletionType.Function, "super([type[, object-or-type]])", "调用父类方法"),
        new CompletionItem("format", CompletionType.Function, "format(value[, format_spec])", "格式化值"),
        new CompletionItem("repr", CompletionType.Function, "repr(object)", "返回对象的字符串表示"),
        new CompletionItem("ascii", CompletionType.Function, "ascii(object)", "返回对象的ASCII字符串表示"),
        new CompletionItem("slice", CompletionType.Function, "slice([start,] stop[, step])", "创建切片对象"),
        new CompletionItem("memoryview", CompletionType.Function, "memoryview(obj)", "创建内存视图对象"),
        new CompletionItem("bytearray", CompletionType.Function, "bytearray([source[, encoding[, errors]]])", "创建字节数组"),
        new CompletionItem("bytes", CompletionType.Function, "bytes([source[, encoding[, errors]]])", "创建字节对象"),
        new CompletionItem("complex", CompletionType.Function, "complex([real[, imag]])", "创建复数"),
        new CompletionItem("frozenset", CompletionType.Function, "frozenset([iterable])", "创建不可变集合"),
        new CompletionItem("object", CompletionType.Function, "object()", "创建基础对象"),
        new CompletionItem("bool", CompletionType.Function, "bool([x])", "将对象转换为布尔值")
    };

    void Awake()
    {
        Instance = this;
        completionDatabase = new CompletionDatabase();
        InitializeCompletionData();
    }

    void Start()
    {
        InitializeCompletionUI();
        if (codeInputField != null)
        {
            codeInputField.onValueChanged.AddListener(OnTextChanged);
            // 注意：由于Unity InputField的限制，我们需要在Update中检查键盘输入
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
    /// 初始化补全数据
    /// </summary>
    private void InitializeCompletionData()
    {
        // 添加Python关键字
        foreach (string keyword in PYTHON_KEYWORDS)
        {
            completionDatabase.AddItem(new CompletionItem(keyword, CompletionType.Keyword, keyword, "Python关键字"));
        }

        // 添加内置函数
        foreach (CompletionItem builtin in PYTHON_BUILTINS)
        {
            completionDatabase.AddItem(builtin);
        }

        // 添加常用模块
        AddCommonModules();
        
        Debug.Log($"[CodeCompletion] Initialized with {completionDatabase.GetItemCount()} completion items");
    }

    /// <summary>
    /// 添加常用模块的补全项
    /// </summary>
    private void AddCommonModules()
    {
        // math模块
        var mathItems = new CompletionItem[]
        {
            new CompletionItem("math", CompletionType.Module, "import math", "数学函数模块"),
            new CompletionItem("math.pi", CompletionType.Constant, "math.pi", "圆周率常数"),
            new CompletionItem("math.e", CompletionType.Constant, "math.e", "自然对数底数"),
            new CompletionItem("math.sqrt", CompletionType.Function, "math.sqrt(x)", "平方根函数"),
            new CompletionItem("math.sin", CompletionType.Function, "math.sin(x)", "正弦函数"),
            new CompletionItem("math.cos", CompletionType.Function, "math.cos(x)", "余弦函数"),
            new CompletionItem("math.tan", CompletionType.Function, "math.tan(x)", "正切函数"),
            new CompletionItem("math.floor", CompletionType.Function, "math.floor(x)", "向下取整"),
            new CompletionItem("math.ceil", CompletionType.Function, "math.ceil(x)", "向上取整"),
            new CompletionItem("math.pow", CompletionType.Function, "math.pow(x, y)", "幂函数"),
            new CompletionItem("math.log", CompletionType.Function, "math.log(x[, base])", "对数函数"),
        };

        // random模块
        var randomItems = new CompletionItem[]
        {
            new CompletionItem("random", CompletionType.Module, "import random", "随机数模块"),
            new CompletionItem("random.random", CompletionType.Function, "random.random()", "生成0-1之间的随机浮点数"),
            new CompletionItem("random.randint", CompletionType.Function, "random.randint(a, b)", "生成指定范围内的随机整数"),
            new CompletionItem("random.choice", CompletionType.Function, "random.choice(seq)", "从序列中随机选择一个元素"),
            new CompletionItem("random.shuffle", CompletionType.Function, "random.shuffle(x)", "随机打乱序列"),
            new CompletionItem("random.sample", CompletionType.Function, "random.sample(population, k)", "从总体中随机抽样"),
        };

        // os模块
        var osItems = new CompletionItem[]
        {
            new CompletionItem("os", CompletionType.Module, "import os", "操作系统接口模块"),
            new CompletionItem("os.path", CompletionType.Module, "os.path", "路径操作子模块"),
            new CompletionItem("os.getcwd", CompletionType.Function, "os.getcwd()", "获取当前工作目录"),
            new CompletionItem("os.listdir", CompletionType.Function, "os.listdir([path])", "列出目录内容"),
            new CompletionItem("os.mkdir", CompletionType.Function, "os.mkdir(path)", "创建目录"),
            new CompletionItem("os.remove", CompletionType.Function, "os.remove(path)", "删除文件"),
        };

        foreach (var item in mathItems.Concat(randomItems).Concat(osItems))
        {
            completionDatabase.AddItem(item);
        }
    }

    /// <summary>
    /// 初始化补全UI
    /// </summary>
    private void InitializeCompletionUI()
    {
        if (completionPanel == null)
        {
            Debug.LogError("[CodeCompletion] Completion panel is not assigned!");
            return;
        }

        if (completionItemPrefab == null)
        {
            Debug.LogError("[CodeCompletion] Completion item prefab is not assigned!");
            return;
        }

        // 确保面板初始为隐藏状态
        completionPanel.SetActive(false);
    }

    /// <summary>
    /// 文本变化事件处理
    /// </summary>
    private void OnTextChanged(string newText)
    {
        if (!enableAutoCompletion) return;

        // 启动延迟补全协程
        if (completionCoroutine != null)
        {
            StopCoroutine(completionCoroutine);
        }
        completionCoroutine = StartCoroutine(DelayedCompletion());
    }

    /// <summary>
    /// 延迟补全协程
    /// </summary>
    private IEnumerator DelayedCompletion()
    {
        yield return new WaitForSeconds(completionDelay);
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

        // 搜索匹配的补全项
        var matches = completionDatabase.Search(currentWord, maxCompletionItems, caseSensitive);
        
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
        return char.IsLetterOrDigit(c) || c == '_' || c == '.';
    }

    /// <summary>
    /// 显示补全面板
    /// </summary>
    private void ShowCompletion(List<CompletionItem> matches)
    {
        if (completionPanel == null) return;

        currentCompletions = matches;
        selectedCompletionIndex = 0;

        // 清除现有的补全项
        foreach (var item in completionItemObjects)
        {
            if (item != null)
                DestroyImmediate(item);
        }
        completionItemObjects.Clear();

        // 创建新的补全项
        for (int i = 0; i < matches.Count; i++)
        {
            CreateCompletionItem(matches[i], i);
        }

        // 定位面板位置
        PositionCompletionPanel();

        completionPanel.SetActive(true);
        isCompletionVisible = true;

        // 更新选中状态
        UpdateCompletionSelection();

        Debug.Log($"[CodeCompletion] Showing {matches.Count} completions for '{currentWord}'");
    }

    /// <summary>
    /// 创建补全项UI
    /// </summary>
    private void CreateCompletionItem(CompletionItem item, int index)
    {
        if (completionItemPrefab == null || completionContent == null) return;

        GameObject itemObj = Instantiate(completionItemPrefab, completionContent);
        completionItemObjects.Add(itemObj);

        // 设置补全项内容
        var itemComponent = itemObj.GetComponent<CodeCompletionItem>();
        if (itemComponent != null)
        {
            itemComponent.Initialize(item, index, this);
        }
        else
        {
            // 如果没有专门的组件，尝试直接设置文本
            var textComponent = itemObj.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.text = $"{GetTypeIcon(item.Type)} {item.Name}";
            }
        }

        // 设置点击事件
        var button = itemObj.GetComponent<Button>();
        if (button != null)
        {
            int capturedIndex = index; // 捕获循环变量
            button.onClick.AddListener(() => SelectCompletion(capturedIndex));
        }
    }

    /// <summary>
    /// 获取类型图标
    /// </summary>
    private string GetTypeIcon(CompletionType type)
    {
        switch (type)
        {
            case CompletionType.Keyword: return "🔑";
            case CompletionType.Function: return "🔧";
            case CompletionType.Variable: return "📦";
            case CompletionType.Class: return "🏗️";
            case CompletionType.Module: return "📚";
            case CompletionType.Constant: return "📊";
            case CompletionType.Method: return "⚙️";
            case CompletionType.Property: return "🏷️";
            default: return "📝";
        }
    }

    /// <summary>
    /// 定位补全面板
    /// </summary>
    private void PositionCompletionPanel()
    {
        if (completionPanel == null || codeInputField == null) return;

        // 这里需要根据光标位置计算面板位置
        // 由于Unity InputField的限制，我们将面板定位到输入框下方
        var inputRect = codeInputField.GetComponent<RectTransform>();
        var panelRect = completionPanel.GetComponent<RectTransform>();

        if (inputRect != null && panelRect != null)
        {
            // 将面板定位到输入框下方
            Vector3[] corners = new Vector3[4];
            inputRect.GetWorldCorners(corners);
            
            Vector3 panelPosition = corners[0]; // 左下角
            panelPosition.y -= panelRect.rect.height; // 向下偏移面板高度

            panelRect.position = panelPosition;
        }
    }

    /// <summary>
    /// 隐藏补全面板
    /// </summary>
    private void HideCompletion()
    {
        if (completionPanel != null)
        {
            completionPanel.SetActive(false);
        }
        
        isCompletionVisible = false;
        selectedCompletionIndex = -1;
        currentCompletions.Clear();
    }

    /// <summary>
    /// 处理补全输入
    /// </summary>
    private void HandleCompletionInput()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Tab))
        {
            if (selectedCompletionIndex >= 0 && selectedCompletionIndex < currentCompletions.Count)
            {
                SelectCompletion(selectedCompletionIndex);
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            selectedCompletionIndex = Mathf.Max(0, selectedCompletionIndex - 1);
            UpdateCompletionSelection();
            return;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            selectedCompletionIndex = Mathf.Min(currentCompletions.Count - 1, selectedCompletionIndex + 1);
            UpdateCompletionSelection();
            return;
        }
    }

    /// <summary>
    /// 更新补全选择状态
    /// </summary>
    private void UpdateCompletionSelection()
    {
        for (int i = 0; i < completionItemObjects.Count; i++)
        {
            var itemObj = completionItemObjects[i];
            if (itemObj == null) continue;

            var itemComponent = itemObj.GetComponent<CodeCompletionItem>();
            if (itemComponent != null)
            {
                itemComponent.SetSelected(i == selectedCompletionIndex);
            }
            else
            {
                // 简单的颜色更改
                var textComponent = itemObj.GetComponentInChildren<TextMeshProUGUI>();
                if (textComponent != null)
                {
                    textComponent.color = (i == selectedCompletionIndex) ? selectedItemColor : normalItemColor;
                }

                var background = itemObj.GetComponent<Image>();
                if (background != null)
                {
                    background.color = (i == selectedCompletionIndex) ? backgroundSelectedColor : Color.clear;
                }
            }
        }

        // 滚动到选中项
        if (completionScrollRect != null && selectedCompletionIndex >= 0)
        {
            float normalizedPosition = 1f - (float)selectedCompletionIndex / (currentCompletions.Count - 1);
            completionScrollRect.verticalNormalizedPosition = normalizedPosition;
        }
    }

    /// <summary>
    /// 选择补全项
    /// </summary>
    public void SelectCompletion(int index)
    {
        if (index < 0 || index >= currentCompletions.Count) return;

        var selectedItem = currentCompletions[index];
        ApplyCompletion(selectedItem);
        HideCompletion();
    }

    /// <summary>
    /// 应用补全
    /// </summary>
    private void ApplyCompletion(CompletionItem item)
    {
        if (codeInputField == null) return;

        string text = codeInputField.text;
        int caretPos = codeInputField.caretPosition;

        // 替换当前单词
        string newText = text.Substring(0, currentWordStartPos) + 
                        item.Name + 
                        text.Substring(caretPos);

        codeInputField.text = newText;

        // 设置新的光标位置
        int newCaretPos = currentWordStartPos + item.Name.Length;
        
        // 如果是函数，添加括号并将光标移到括号内
        if (item.Type == CompletionType.Function && !item.Name.EndsWith(")"))
        {
            newCaretPos = currentWordStartPos + item.Name.Length + 1;
            newText = newText.Insert(currentWordStartPos + item.Name.Length, "()");
            codeInputField.text = newText;
        }

        StartCoroutine(SetCaretPositionDelayed(newCaretPos));

        Debug.Log($"[CodeCompletion] Applied completion: {item.Name}");
    }

    /// <summary>
    /// 延迟设置光标位置
    /// </summary>
    private IEnumerator SetCaretPositionDelayed(int position)
    {
        yield return null; // 等待一帧
        codeInputField.caretPosition = position;
        codeInputField.selectionAnchorPosition = position;
        codeInputField.selectionFocusPosition = position;
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
    /// 添加自定义补全项
    /// </summary>
    public void AddCustomCompletion(CompletionItem item)
    {
        completionDatabase.AddItem(item);
    }

    /// <summary>
    /// 从代码中分析并添加用户定义的变量和函数
    /// </summary>
    public void AnalyzeUserCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return;

        // 分析函数定义
        AnalyzeFunctions(code);
        
        // 分析变量定义
        AnalyzeVariables(code);
        
        // 分析类定义
        AnalyzeClasses(code);
    }

    private void AnalyzeFunctions(string code)
    {
        // 匹配函数定义 def function_name(params):
        Regex funcRegex = new Regex(@"def\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*\([^)]*\):", RegexOptions.Multiline);
        var matches = funcRegex.Matches(code);

        foreach (Match match in matches)
        {
            string funcName = match.Groups[1].Value;
            var item = new CompletionItem(funcName, CompletionType.Function, $"{funcName}()", "用户定义的函数");
            completionDatabase.AddItem(item);
        }
    }

    private void AnalyzeVariables(string code)
    {
        // 匹配变量赋值 variable_name = value
        Regex varRegex = new Regex(@"^([a-zA-Z_][a-zA-Z0-9_]*)\s*=", RegexOptions.Multiline);
        var matches = varRegex.Matches(code);

        foreach (Match match in matches)
        {
            string varName = match.Groups[1].Value;
            if (!PYTHON_KEYWORDS.Contains(varName)) // 排除关键字
            {
                var item = new CompletionItem(varName, CompletionType.Variable, varName, "用户定义的变量");
                completionDatabase.AddItem(item);
            }
        }
    }

    private void AnalyzeClasses(string code)
    {
        // 匹配类定义 class ClassName:
        Regex classRegex = new Regex(@"class\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*:", RegexOptions.Multiline);
        var matches = classRegex.Matches(code);

        foreach (Match match in matches)
        {
            string className = match.Groups[1].Value;
            var item = new CompletionItem(className, CompletionType.Class, className, "用户定义的类");
            completionDatabase.AddItem(item);
        }
    }
}

/// <summary>
/// 补全项数据结构
/// </summary>
[System.Serializable]
public class CompletionItem
{
    public string Name { get; set; }
    public CompletionType Type { get; set; }
    public string Signature { get; set; }
    public string Description { get; set; }
    public int Priority { get; set; }

    public CompletionItem(string name, CompletionType type, string signature = "", string description = "", int priority = 0)
    {
        Name = name;
        Type = type;
        Signature = signature;
        Description = description;
        Priority = priority;
    }
}

/// <summary>
/// 补全项类型
/// </summary>
public enum CompletionType
{
    Keyword,    // 关键字
    Function,   // 函数
    Variable,   // 变量
    Class,      // 类
    Module,     // 模块
    Constant,   // 常量
    Method,     // 方法
    Property    // 属性
}

/// <summary>
/// 补全数据库
/// </summary>
public class CompletionDatabase
{
    private List<CompletionItem> items = new List<CompletionItem>();
    private Dictionary<string, List<CompletionItem>> index = new Dictionary<string, List<CompletionItem>>();

    public void AddItem(CompletionItem item)
    {
        if (items.Any(x => x.Name == item.Name && x.Type == item.Type))
            return; // 避免重复

        items.Add(item);
        
        // 更新索引
        string key = item.Name.ToLower();
        if (!index.ContainsKey(key))
        {
            index[key] = new List<CompletionItem>();
        }
        index[key].Add(item);
    }

    public List<CompletionItem> Search(string query, int maxResults, bool caseSensitive)
    {
        if (string.IsNullOrEmpty(query))
            return new List<CompletionItem>();

        string searchQuery = caseSensitive ? query : query.ToLower();
        var results = new List<CompletionItem>();

        // 精确匹配优先
        foreach (var item in items)
        {
            string itemName = caseSensitive ? item.Name : item.Name.ToLower();
            
            if (itemName == searchQuery)
            {
                results.Add(item);
            }
        }

        // 前缀匹配
        foreach (var item in items)
        {
            string itemName = caseSensitive ? item.Name : item.Name.ToLower();
            
            if (itemName.StartsWith(searchQuery) && !results.Contains(item))
            {
                results.Add(item);
            }
        }

        // 包含匹配
        foreach (var item in items)
        {
            string itemName = caseSensitive ? item.Name : item.Name.ToLower();
            
            if (itemName.Contains(searchQuery) && !results.Contains(item))
            {
                results.Add(item);
            }
        }

        // 按优先级和类型排序
        results = results.OrderBy(x => GetTypePriority(x.Type))
                        .ThenBy(x => x.Name)
                        .Take(maxResults)
                        .ToList();

        return results;
    }

    private int GetTypePriority(CompletionType type)
    {
        switch (type)
        {
            case CompletionType.Keyword: return 1;
            case CompletionType.Function: return 2;
            case CompletionType.Class: return 3;
            case CompletionType.Variable: return 4;
            case CompletionType.Method: return 5;
            case CompletionType.Property: return 6;
            case CompletionType.Module: return 7;
            case CompletionType.Constant: return 8;
            default: return 9;
        }
    }

    public int GetItemCount()
    {
        return items.Count;
    }

    public void Clear()
    {
        items.Clear();
        index.Clear();
    }
}
