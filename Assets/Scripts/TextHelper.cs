using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using System.IO;
using System.Text.RegularExpressions;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.XR;

public class TextHelper : MonoBehaviour
{
    public static TextHelper Instance;

    public GameObject enterParticleObject;

    [Header("Code Editor Components")]
    [SerializeField] private TMP_InputField codeInputField;
    [SerializeField] private TextMeshProUGUI highlightText;   // ★ 新增
    [SerializeField] private RectTransform backgroundPanel;   // ★ 背景面板，用于跟随光标Y轴
    [SerializeField] private TextMeshProUGUI numberText; // ★ 用于显示行号的文本组件
    [SerializeField] private RectTransform caretTrace; // ★ 跟踪光标的一个物体

    [SerializeField] private Color backgroundEditColor = new Color(0.1f, 0.1f, 0.1f); // 背景颜色
    [SerializeField] private Color backgroundRunColor = new Color(0.2f, 0.2f, 0.2f); // 背景颜色

    [Header("Auto Completion Settings")]
    [SerializeField] private bool enableBracketCompletion = true;
    [SerializeField] private bool enableQuoteCompletion = true;
    [SerializeField] private bool enableAutoIndent = true;
    [SerializeField] private int indentSize = 4; // 用于空格到制表符的转换计算（每个制表符等于多少个空格）
    [SerializeField] private float tabSize = 20f; // tab的显示宽度（像素）
    [SerializeField] private bool enableCodeCompletion = true; // 是否启用代码补全

    [Header("Background Panel Animation")]
    [SerializeField] private float animationDuration = 0.2f; // 动画持续时间
    [SerializeField] private Ease animationEase = Ease.OutQuart; // 动画缓动类型
    [SerializeField] private float positionThreshold = 0.1f; // 位置变化阈值
    
    [Header("Editor Mode Settings")]
    [SerializeField] private Color editModeColor = Color.white; // 编辑模式文本颜色
    [SerializeField] private Color runModeColor = Color.gray; // 运行模式文本颜色
    [SerializeField] private bool showModeIndicator = true; // 是否显示模式指示器

    [Header("Particle Effect Settings")]
    [SerializeField] private GameObject[] particleEffectPrefabs; // 多个粒子效果预制体
    [SerializeField] private bool enableParticleEffect = true; // 是否启用粒子效果
    [SerializeField] private float particleLifetime = 2f; // 粒子效果持续时间
    [SerializeField] private int maxInstancesPerEffect = 5; // 每种粒子效果的最大实例数量

    private const string SYNTAX_CSV_NAME = "syntax";
    private Dictionary<string, string> tokenColors = new();  // 关键字 → 颜色
    private Regex tokenRegex;                                 // 动态生成的正则
    
    // 语法高亮相关的正则表达式
    private Regex classRegex;       // 类名匹配
    private Regex functionRegex;    // 函数名匹配
    private Regex variableRegex;    // 变量名匹配
    private Regex stringRegex;      // 字符串匹配
    private Regex numberRegex;      // 数字匹配
    private Regex commentRegex;     // 注释匹配
    private Regex decoratorRegex;   // 装饰器匹配
    
    // 语法高亮颜色配置
    private readonly string classNameColor = "#8be9fd";      // 青色 - 类名
    private readonly string functionNameColor = "#50fa7b";   // 绿色 - 函数名
    private readonly string variableColor = "#f8f8f2";       // 白色 - 变量名
    private readonly string stringColor = "#f1fa8c";         // 黄色 - 字符串
    private readonly string numberColor = "#bd93f9";         // 紫色 - 数字
    private readonly string commentColor = "#6272a4";        // 灰色 - 注释
    private readonly string decoratorColor = "#ffb86c";      // 橙色 - 装饰器

    // 括号和引号配对字典
    private Dictionary<char, char> bracketPairs = new Dictionary<char, char>
    {
        {'(', ')'},
        {'{', '}'},
        {'[', ']'},
        {'"', '"'},
        {'\'', '\''}
    };

    private int lastCaretPosition = 0;
    private string lastText = "";
    private bool isAutoCompleting = false; // 防止递归标志
    private int pendingCaretPosition = -1; // 待设置的光标位置
    
    // 编辑模式相关
    private bool isEditMode = true; // 当前是否为编辑模式
    private bool originalInteractable = true; // 原始的可交互状态
    
    // DOTween 相关变量
    private Tween backgroundPanelTween; // 背景面板的移动动画
    private float lastTargetY = float.MinValue; // 上次的目标Y位置，用于避免重复动画
    
    // 执行跟踪相关
    private int currentExecutingLine = -1; // 当前正在执行的行号（0-based）
    private bool isTrackingExecution = false; // 是否正在跟踪执行
    private Coroutine executionTrackingCoroutine; // 执行跟踪协程

    // 粒子效果相关
    // 多粒子效果池管理
    [System.Serializable]
    public class ParticleEffectPool
    {
        public GameObject prefab;                          // 粒子效果预制体
        public List<GameObject> instancePool;              // 实例池
        public List<ParticleSystem> systemPool;            // 粒子系统池
        public int maxInstances;                           // 最大实例数量
        
        public ParticleEffectPool(GameObject prefab, int maxInstances = 5)
        {
            this.prefab = prefab;
            this.maxInstances = maxInstances;
            this.instancePool = new List<GameObject>();
            this.systemPool = new List<ParticleSystem>();
        }
    }
    
    private List<ParticleEffectPool> particleEffectPools = new List<ParticleEffectPool>(); // 所有粒子效果池

    void Awake()
    {
        Instance = this;
        string numberText = "";
        for(int i = 1; i <= 2000; i++) // 假设最多100行
        {
            numberText += $"{i}\n";
        }
        this.numberText.text = numberText; // 设置行号文本
    }

    void Start()
    {
        InitializeCodeEditor();
        LoadSyntaxRules();        // ★ 读 CSV
        BuildTokenRegex();        // ★ 构造正则
        BuildAdvancedRegexes();   // ★ 构造高级语法正则
        SetupTabSize();          // ★ 设置tab显示宽度
        InitializeCodeCompletion(); // ★ 初始化代码补全
        InitializeParticleEffectPools(); // ★ 初始化粒子效果池
        ApplySyntaxHighlight();   // ★ 首次刷新
    }

    void OnDestroy()
    {
        // 清理 DOTween 动画
        if (backgroundPanelTween != null && backgroundPanelTween.IsActive())
        {
            backgroundPanelTween.Kill();
        }
        
        // 清理所有粒子效果池
        for (int poolIndex = 0; poolIndex < particleEffectPools.Count; poolIndex++)
        {
            var pool = particleEffectPools[poolIndex];
            if (pool != null && pool.instancePool != null)
            {
                for (int i = 0; i < pool.instancePool.Count; i++)
                {
                    if (pool.instancePool[i] != null)
                    {
                        DestroyImmediate(pool.instancePool[i]);
                    }
                }
                pool.instancePool.Clear();
                pool.systemPool.Clear();
            }
        }
        particleEffectPools.Clear();
    }

    void Update()
    {
        //记录光标目前的位置
        var caretPosition = codeInputField.caretPosition;

        // 让跟踪光标的物体跟随光标位置
        UpdateCaretTracePosition(caretPosition);

        // 更新粒子效果位置
        UpdateParticleEffectPosition(caretPosition);

        if (isEditMode)
        {
            backgroundPanel.GetComponent<Image>().color = backgroundEditColor;
        }
        else
        {
            backgroundPanel.GetComponent<Image>().color = backgroundRunColor;
        }

        HandleInput();

        if (backgroundPanel != null && codeInputField != null && codeInputField.isFocused)
        {
            float targetY = GetCaretLineYPosition();

            // 只有当目标位置发生变化时才执行动画
            if (Mathf.Abs(targetY - lastTargetY) > positionThreshold)
            {
                lastTargetY = targetY;

                // 停止之前的动画
                if (backgroundPanelTween != null && backgroundPanelTween.IsActive())
                {
                    backgroundPanelTween.Kill();
                }

                // 获取当前位置
                Vector2 currentPos = backgroundPanel.anchoredPosition;
                Vector2 targetPos = new Vector2(currentPos.x, targetY);

                // 使用 DOTween 创建丝滑的移动动画
                backgroundPanelTween = backgroundPanel.DOAnchorPos(targetPos, animationDuration)
                    .SetEase(animationEase) // 使用可配置的缓动类型
                    .SetUpdate(true); // 使用不受时间缩放影响的更新
            }
        }

        // 如果不在编辑模式，还原光标位置
        if (!isEditMode && caretPosition != lastCaretPosition)
        {
            // 在运行模式下，光标位置不变
            codeInputField.caretPosition = lastCaretPosition;
            codeInputField.selectionAnchorPosition = lastCaretPosition;
            codeInputField.selectionFocusPosition = lastCaretPosition;
        }
    }

    void LateUpdate()
    {
        // 备用的光标位置设置机制
        if (pendingCaretPosition >= 0 && codeInputField != null && codeInputField.isFocused)
        {
            codeInputField.caretPosition = pendingCaretPosition;
            codeInputField.selectionAnchorPosition = pendingCaretPosition;
            codeInputField.selectionFocusPosition = pendingCaretPosition;
            pendingCaretPosition = -1; // 重置
        }
    }


    /// <summary>
    /// 初始化代码编辑器
    /// </summary>
    private void InitializeCodeEditor()
    {
        if (codeInputField == null)
        {
            Debug.LogError("Code InputField is not assigned!");
            return;
        }

        // 检查背景面板是否已分配
        if (backgroundPanel == null)
        {
            Debug.LogWarning("Background Panel is not assigned! Cursor position sync will be disabled.");
        }

        // 绑定输入事件
        codeInputField.onValueChanged.AddListener(OnTextChanged);
        codeInputField.onSelect.AddListener(OnInputFieldSelected);

        // 设置初始属性
        codeInputField.lineType = TMP_InputField.LineType.MultiLineNewline;

        lastText = codeInputField.text;
        lastCaretPosition = codeInputField.caretPosition;
        
        codeInputField.textComponent.color = new Color(1, 1, 1, 0);   // 文字透明

        // 2. 单独把光标颜色、宽度设回来
        codeInputField.customCaretColor = true;                       // 允许自定义
        codeInputField.caretColor      = Color.white;                 // 亮色光标
        codeInputField.caretWidth      = 2;                           // 可以略微加宽，易于观察

        // 3. 选区高亮颜色一样要单独设（可选）
        codeInputField.selectionColor  = new Color32(50, 120, 200, 100);
    }

    /// <summary>
    /// 设置Tab显示宽度
    /// </summary>
    private void SetupTabSize()
    {
        if (codeInputField?.textComponent != null)
        {
            // 通过设置字体资源的tab宽度来控制显示
            var textComponent = codeInputField.textComponent;
            if (textComponent.font != null)
            {
                // 使用现代API设置tab宽度
                var faceInfo = textComponent.font.faceInfo;
                faceInfo.tabWidth = tabSize;
                textComponent.font.faceInfo = faceInfo;
            }
        }
        
        if (highlightText != null)
        {
            // 同样设置高亮文本的tab宽度
            if (highlightText.font != null)
            {
                var faceInfo = highlightText.font.faceInfo;
                faceInfo.tabWidth = tabSize;
                highlightText.font.faceInfo = faceInfo;
            }
        }
    }

    /// <summary>
    /// 初始化代码补全功能
    /// </summary>
    private void InitializeCodeCompletion()
    {
        if (!enableCodeCompletion) return;

        // IL2CPP兼容：直接通过GameObject名称查找，避免反射
        var simpleCompletionObj = GameObject.Find("SimpleCodeCompletion");
        if (simpleCompletionObj != null)
        {
            // Debug.Log("[TextHelper] Simple code completion found and initialized successfully");
        }
        else
        {
            Debug.LogWarning("[TextHelper] SimpleCodeCompletion component not found! Please add it to the scene.");
            enableCodeCompletion = false;
        }
    }

    /// <summary>
    /// 触发代码补全
    /// </summary>
    private void TriggerCodeCompletion()
    {
        if (!enableCodeCompletion) return;

        // IL2CPP兼容：使用SendMessage而不是反射
        var simpleCompletionObj = GameObject.Find("SimpleCodeCompletion");
        if (simpleCompletionObj != null)
        {
            try
            {
                simpleCompletionObj.SendMessage("TriggerCompletion", SendMessageOptions.DontRequireReceiver);
                // Debug.Log("[TextHelper] Simple code completion triggered manually");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TextHelper] Failed to trigger code completion: {e.Message}");
            }
        }
    }

    /// <summary>
    /// 分析当前代码并更新补全数据库
    /// </summary>
    public void UpdateCodeCompletionDatabase()
    {
        if (!enableCodeCompletion) return;

        var simpleCompletionObj = GameObject.Find("SimpleCodeCompletion");
        if (simpleCompletionObj != null)
        {
            string currentCode = codeInputField?.text;
            if (!string.IsNullOrEmpty(currentCode))
            {
                try
                {
                    // IL2CPP兼容：使用SendMessage而不是反射，传递代码作为参数
                    simpleCompletionObj.SendMessage("AnalyzeUserCode", currentCode, SendMessageOptions.DontRequireReceiver);
                    // Debug.Log("[TextHelper] Simple code completion database updated");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[TextHelper] Failed to update code completion database: {e.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 设置代码补全启用状态
    /// </summary>
    public void SetCodeCompletionEnabled(bool enabled)
    {
        enableCodeCompletion = enabled;
        
        var simpleCompletionObj = GameObject.Find("SimpleCodeCompletion");
        if (simpleCompletionObj != null)
        {
            try
            {
                // IL2CPP兼容：使用SendMessage而不是反射
                simpleCompletionObj.SendMessage("SetEnabled", enabled, SendMessageOptions.DontRequireReceiver);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TextHelper] Failed to set code completion enabled state: {e.Message}");
            }
        }
    }

    /// <summary>
    /// 初始化粒子效果池
    /// </summary>
    private void InitializeParticleEffectPools()
    {
        particleEffectPools.Clear();
        
        if (particleEffectPrefabs == null || particleEffectPrefabs.Length == 0)
        {
            Debug.LogWarning("[TextHelper] No particle effect prefabs assigned! Particle effects will be disabled.");
            return;
        }
        
        // 为每个粒子效果预制体创建对象池
        for (int i = 0; i < particleEffectPrefabs.Length; i++)
        {
            if (particleEffectPrefabs[i] != null)
            {
                var pool = new ParticleEffectPool(particleEffectPrefabs[i], maxInstancesPerEffect);
                particleEffectPools.Add(pool);
                Debug.Log($"[TextHelper] Initialized particle effect pool {i} with prefab: {particleEffectPrefabs[i].name}");
            }
            else
            {
                Debug.LogWarning($"[TextHelper] Particle effect prefab at index {i} is null! Skipping.");
            }
        }
        
        Debug.Log($"[TextHelper] Total particle effect pools initialized: {particleEffectPools.Count}");
    }

    /// <summary>
    /// 处理输入逻辑
    /// </summary>
    private void HandleInput()
    {

        if (!isEditMode)
        {
            // 检查是否有键盘输入，如果有则阻止
            if (Input.inputString.Length > 0)
            {
                // 阻止所有键盘输入
                foreach (char c in Input.inputString)
                {
                    if (c != '\0') // 如果有实际的字符输入
                    {
                        // 通过清空输入来阻止
                        break;
                    }
                }
            }
            return;
        }
    }

    /// <summary>
    /// 处理文本变化事件
    /// </summary>
    /// <param name="newText">新文本</param>
    private void OnTextChanged(string newText)
    {
        if (isAutoCompleting) return;
        
        if (!isEditMode)
        {

            // 在运行模式下，如果文本发生变化，恢复原始文本（阻止用户修改）
            if (newText != lastText)
            {
                isAutoCompleting = true;
                codeInputField.onValueChanged.RemoveListener(OnTextChanged);
                codeInputField.text = lastText;
                codeInputField.onValueChanged.AddListener(OnTextChanged);
                isAutoCompleting = false;
                return;
            }
            
            ApplySyntaxHighlight();
            lastText = newText;
            lastCaretPosition = codeInputField.caretPosition;
            return;
        }

        // Camera.main.GetComponent<CameraHelper>().ShakeCamera(0.2f, 0.02f, 3, 100, false);
        
        // 播放粒子效果（仅在编辑模式下）
        if (enableParticleEffect && isEditMode && newText != lastText)
        {
            PlayParticleEffect();
        }
        
        // 检查是否需要自动缩进
        if (enableAutoIndent && ShouldAddAutoIndent(newText))
        {
            PerformAutoIndent(newText);
        }
        // 检查是否需要括号补全
        else if (enableBracketCompletion && ShouldAddBracketCompletion(newText))
        {
            PerformBracketCompletion(newText);
        }

        ApplySyntaxHighlight();

        if (enableCodeCompletion && isEditMode)
        {
            CancelInvoke(nameof(UpdateCodeCompletionDatabase));
            Invoke(nameof(UpdateCodeCompletionDatabase), 1.0f); // 1秒后更新
        }

        lastText = newText;
        lastCaretPosition = codeInputField.caretPosition;
    }

    /// <summary>
    /// 判断是否需要进行括号补全
    /// </summary>
    /// <param name="newText">新文本</param>
    /// <returns>是否需要补全</returns>
    private bool ShouldAddBracketCompletion(string newText)
    {
        // 只有当新文本比旧文本长1个字符时才考虑补全
        if (newText.Length != lastText.Length + 1) return false;

        int caretPos = codeInputField.caretPosition;
        if (caretPos == 0) return false;

        // 获取刚输入的字符
        char inputChar = newText[caretPos - 1];

        // 检查是否是需要补全的字符
        return bracketPairs.ContainsKey(inputChar);
    }

    /// <summary>
    /// 判断是否需要进行自动缩进
    /// </summary>
    /// <param name="newText">新文本</param>
    /// <returns>是否需要自动缩进</returns>
    private bool ShouldAddAutoIndent(string newText)
    {
        // 检查是否是换行操作
        if (newText.Length != lastText.Length + 1) return false;
        
        int caretPos = codeInputField.caretPosition;
        if (caretPos == 0) return false;

        // 检查刚输入的字符是否是换行符
        char inputChar = newText[caretPos - 1];

        bool isNewLine = inputChar == '\n';
        if(isNewLine) HandleNewLine();

        return isNewLine;
    }

    /// <summary>
    /// 换行操作触发Handle
    /// </summary>
    private void HandleNewLine()
    {
        // get current line number - IL2CPP兼容：手动计算换行符数量
        string textBeforeCaret = codeInputField.text.Substring(0, codeInputField.caretPosition);
        int currentLine = CountNewlines(textBeforeCaret) + 1;
        
        // get current line text
        string currentLineText = GetPreviousLine(codeInputField.text, codeInputField.caretPosition);
        if(currentLineText.Length == 0)
        {
            // 如果当前行文本为空，直接返回
            return;
        }
        GenerateCube.Instance.LineGenerate(currentLine, currentLineText, 20);
    }
    
    /// <summary>
    /// 计算字符串中换行符的数量（IL2CPP兼容）
    /// </summary>
    /// <param name="text">要计算的字符串</param>
    /// <returns>换行符数量</returns>
    private int CountNewlines(string text)
    {
        int count = 0;
        foreach (char c in text)
        {
            if (c == '\n')
                count++;
        }
        return count;
    }

    /// <summary>
    /// 执行自动缩进
    /// </summary>
    /// <param name="currentText">当前文本</param>
    private void PerformAutoIndent(string currentText)
    {
        int caretPos = codeInputField.caretPosition;

        // 找到当前行的前一行
        string previousLine = GetPreviousLine(currentText, caretPos);


        // 计算需要的缩进
        string indentToAdd = CalculateIndent(previousLine);
        // 即使缩进为空字符串，也要执行（保持当前缩进级别）
        if (indentToAdd != null)
        {
            // 设置标志防止递归触发
            isAutoCompleting = true;

            // 暂时移除事件监听
            codeInputField.onValueChanged.RemoveListener(OnTextChanged);

            // 插入缩进
            string newText = currentText.Insert(caretPos, indentToAdd);
            codeInputField.text = newText;

            // 计算新的光标位置
            int newCaretPosition = caretPos + indentToAdd.Length;
            // 使用协程延迟设置光标位置
            StartCoroutine(SetCaretPositionDelayed(newCaretPosition, newText));
        }
    }

    private float GetCaretLineYPosition()
    {
        // 当前光标在文本中的字符索引
        int caretIndex = codeInputField.caretPosition;

        // 获取 textComponent 的文本信息
        TMP_TextInfo textInfo = codeInputField.textComponent.textInfo;
        codeInputField.textComponent.ForceMeshUpdate(); // 确保是最新

        int lineCount = textInfo.lineCount;
        if (lineCount == 0 || caretIndex >= textInfo.characterCount)
            return 0f;

        // 找到光标所在字符的行索引
        int lineIndex = textInfo.characterInfo[caretIndex].lineNumber;

        // 获取该行的 baseline 坐标（相对于 textComponent）
        TMP_LineInfo lineInfo = textInfo.lineInfo[lineIndex];
        float localY = lineInfo.descender + (lineInfo.lineHeight / 2f); // 或使用 baseline

        return localY;
    }

    /// <summary>
    /// 更新光标跟踪物体的位置
    /// </summary>
    /// <param name="caretPosition">当前光标位置</param>
    private void UpdateCaretTracePosition(int caretPosition)
    {
        if (caretTrace == null || codeInputField?.textComponent == null) return;

        // 获取文本信息
        TMP_TextInfo textInfo = codeInputField.textComponent.textInfo;
        codeInputField.textComponent.ForceMeshUpdate(); // 确保文本信息是最新的

        if (textInfo.characterCount == 0)
        {
            // 如果没有文本，将光标跟踪物体放在起始位置
            caretTrace.anchoredPosition = Vector2.zero;
            return;
        }

        // 确保光标位置在有效范围内
        int safeCaretPos = Mathf.Clamp(caretPosition, 0, textInfo.characterCount);

        Vector3 caretWorldPos;

        if (safeCaretPos == textInfo.characterCount && textInfo.characterCount > 0)
        {
            // 光标在文本末尾，使用最后一个字符的位置
            int lastCharIndex = textInfo.characterCount - 1;
            TMP_CharacterInfo lastCharInfo = textInfo.characterInfo[lastCharIndex];

            // 计算光标应该在最后一个字符之后的位置
            caretWorldPos = new Vector3(
                lastCharInfo.topRight.x, // 使用字符右侧位置
                (lastCharInfo.topRight.y + lastCharInfo.bottomRight.y) * 0.5f, // 字符中心高度
                0
            );
        }
        else if (safeCaretPos < textInfo.characterCount)
        {
            // 光标在字符前面
            TMP_CharacterInfo charInfo = textInfo.characterInfo[safeCaretPos];
            caretWorldPos = new Vector3(
                charInfo.topLeft.x, // 使用字符左侧位置
                (charInfo.topLeft.y + charInfo.bottomLeft.y) * 0.5f, // 字符中心高度
                0
            );
        }
        else
        {
            // 回退到起始位置
            caretWorldPos = Vector3.zero;
        }

        // 将世界坐标转换为相对于caretTrace父对象的本地坐标
        Vector3 localPos = caretTrace.parent.InverseTransformPoint(
            codeInputField.textComponent.transform.TransformPoint(caretWorldPos)
        );

        // 设置光标跟踪物体的位置
        caretTrace.anchoredPosition = new Vector2(localPos.x, localPos.y);
    }

    /// <summary>
    /// 延迟设置光标位置的协程
    /// </summary>
    private IEnumerator SetCaretPositionDelayed(int targetPosition, string newText)
    {
        // Step 1: 等待一帧，让文本更新完成
        yield return null;

        // Step 2: 再等待一帧，确保UI完全更新
        yield return null;

        // Step 3: 强制激活输入框并重新获取焦点
        // codeInputField.ActivateInputField();

        // Step 4: 等待一帧让激活生效
        yield return null;

        // Step 5: 设置光标位置（多次设置确保生效）
        codeInputField.caretPosition = targetPosition;
        codeInputField.selectionAnchorPosition = targetPosition;
        codeInputField.selectionFocusPosition = targetPosition;

        // Step 6: 强制刷新输入框状态
        codeInputField.ForceLabelUpdate();

        // Step 7: 再次确保光标位置正确
        yield return null;
        codeInputField.caretPosition = targetPosition;
        codeInputField.selectionAnchorPosition = targetPosition;
        codeInputField.selectionFocusPosition = targetPosition;

        // Step 8: 设置备用机制
        pendingCaretPosition = targetPosition;

        // Step 9: 重新监听
        codeInputField.onValueChanged.AddListener(OnTextChanged);

        // Step 10: 更新状态
        lastText = newText;
        lastCaretPosition = targetPosition;
        isAutoCompleting = false;
    }

    /// <summary>
    /// 获取前一行的内容
    /// </summary>
    /// <param name="text">文本内容</param>
    /// <param name="caretPos">光标位置</param>
    /// <returns>前一行内容，如果没有返回null</returns>
    private string GetPreviousLine(string text, int caretPos)
    {
        if (caretPos <= 1) 
        {
            return null;
        }

        // 找到当前换行符之前的位置
        int currentLineStart = caretPos - 1; // 当前换行符位置

        // 找到上一行的开始位置
        int previousLineStart = text.LastIndexOf('\n', currentLineStart - 1);
        if (previousLineStart == -1) 
        {
            previousLineStart = 0;
        }
        else 
        {
            previousLineStart++; // 跳过换行符
        }

        // 提取上一行内容
        int previousLineLength = currentLineStart - previousLineStart;
        
        if (previousLineLength <= 0) 
        {
            return "";
        }

        string result = text.Substring(previousLineStart, previousLineLength);
        
        return result;
    }

    /// <summary>
    /// 计算需要的缩进
    /// </summary>
    /// <param name="previousLine">前一行内容</param>
    /// <returns>需要添加的缩进字符串</returns>
    private string CalculateIndent(string previousLine)
    {
        // 如果没有前一行，返回空缩进
        if (previousLine == null) return "";

        // 获取前一行的前导缩进字符数量（包括空格和制表符）
        int leadingTabs = 0;
        int leadingSpaces = 0;
        
        for (int i = 0; i < previousLine.Length; i++)
        {
            if (previousLine[i] == '\t')
                leadingTabs++;
            else if (previousLine[i] == ' ')
                leadingSpaces++;
            else
                break;
        }

        // 将混合的空格和制表符统一转换为制表符数量
        // 假设每个制表符等于 indentSize 个空格
        int totalTabEquivalent = leadingTabs + (leadingSpaces / indentSize);

        // 检查前一行是否以冒号结尾（需要增加缩进级别）
        string trimmedPreviousLine = previousLine.TrimEnd();
        bool shouldIncreaseIndent = trimmedPreviousLine.EndsWith(":");

        // 计算新的缩进级别（制表符数量）
        int newTabLevel = totalTabEquivalent;
        if (shouldIncreaseIndent)
        {
            newTabLevel += 1; // 增加一个制表符
        }

        // 生成缩进字符串（使用制表符）
        string result = new string('\t', newTabLevel);
        return result;
    }

    /// <summary>
    /// 执行括号补全
    /// </summary>
    /// <param name="currentText">当前文本</param>
    private void PerformBracketCompletion(string currentText)
    {
        int caretPos = codeInputField.caretPosition;
        char inputChar = currentText[caretPos - 1];

        if (bracketPairs.TryGetValue(inputChar, out char closingChar))
        {
            // 特殊处理引号：检查是否已经成对
            if ((inputChar == '"' || inputChar == '\''))
            {
                // 检查光标后面是否已经有相同的引号
                if (caretPos < currentText.Length && currentText[caretPos] == inputChar)
                {
                    return; // 如果后面已经有相同引号，不需要补全
                }

                // 检查前面未配对的引号数量
                if (HasUnpairedQuote(currentText, inputChar, caretPos - 1))
                {
                    return; // 如果前面有未配对的引号，不需要补全
                }
            }

            // 设置标志防止递归触发
            isAutoCompleting = true;

            // 暂时移除事件监听，避免触发递归
            codeInputField.onValueChanged.RemoveListener(OnTextChanged);

            // 插入配对的右括号/引号
            string newText = currentText.Insert(caretPos, closingChar.ToString());
            codeInputField.text = newText;
            
            // 使用协程延迟设置光标位置（光标保持在左括号后）
            StartCoroutine(SetCaretPositionDelayedForBracket(caretPos, newText));
        }
    }

    /// <summary>
    /// 延迟设置括号补全后的光标位置
    /// </summary>
    private System.Collections.IEnumerator SetCaretPositionDelayedForBracket(int targetPosition, string newText)
    {
        // 等待一帧让UI更新
        yield return null;
        yield return null;
        
        // 强制激活输入框
        // codeInputField.ActivateInputField();
        yield return null;
        
        // 设置光标位置
        codeInputField.caretPosition = targetPosition;
        codeInputField.selectionAnchorPosition = targetPosition;
        codeInputField.selectionFocusPosition = targetPosition;
        
        // 强制刷新
        codeInputField.ForceLabelUpdate();
        yield return null;
        
        // 再次确保位置正确
        codeInputField.caretPosition = targetPosition;
        codeInputField.selectionAnchorPosition = targetPosition;
        codeInputField.selectionFocusPosition = targetPosition;
        
        // 设置备用机制
        pendingCaretPosition = targetPosition;
        
        // 重新添加事件监听
        codeInputField.onValueChanged.AddListener(OnTextChanged);

        // 更新状态
        lastText = newText;
        lastCaretPosition = targetPosition;

        // 重置标志
        isAutoCompleting = false;
    }

    /// <summary>
    /// 检查指定位置前是否有未配对的引号
    /// </summary>
    /// <param name="text">文本</param>
    /// <param name="quoteChar">引号字符</param>
    /// <param name="position">检查位置</param>
    /// <returns>是否有未配对的引号</returns>
    private bool HasUnpairedQuote(string text, char quoteChar, int position)
    {
        int count = 0;
        bool inEscape = false;

        for (int i = 0; i < position; i++)
        {
            if (inEscape)
            {
                inEscape = false;
                continue;
            }

            if (text[i] == '\\')
            {
                inEscape = true;
                continue;
            }

            if (text[i] == quoteChar)
            {
                count++;
            }
        }

        // 奇数个表示有未配对的引号
        return count % 2 == 1;
    }

    /// <summary>
    /// 输入框选中事件
    /// </summary>
    /// <param name="text">选中的文本</param>
    private void OnInputFieldSelected(string text)
    {
        // TODO: 处理选中事件，可能用于语法高亮等
    }
    
    
    /// <summary> 读取 Resources/syntax.csv，填充 tokenColors 字典 </summary>
    private void LoadSyntaxRules()
    {
        TextAsset csv = Resources.Load<TextAsset>(SYNTAX_CSV_NAME);
        if (csv == null)
        {
            Debug.LogWarning($"No '{SYNTAX_CSV_NAME}.csv' found in Resources; syntax highlight disabled.");
            return;
        }

        foreach (string line in csv.text.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//")) continue;
            var parts = line.Trim().Split(',');
            if (parts.Length < 2) continue;
            string token = parts[0].Trim();
            string color = parts[1].Trim();
            if (!tokenColors.ContainsKey(token))
                tokenColors.Add(token, color);
        }
    }

    /// <summary> 根据 tokenColors 生成形如 \b(if|else|while)\b 的正则 </summary>
    private void BuildTokenRegex()
    {
        if (tokenColors.Count == 0) return;
        
        // IL2CPP兼容：避免使用LINQ，手动构建模式
        var escapedTokens = new System.Collections.Generic.List<string>();
        foreach (var token in tokenColors.Keys)
        {
            escapedTokens.Add(Regex.Escape(token));
        }
        
        string pattern = $@"\b({string.Join("|", escapedTokens)})\b";
        tokenRegex = new Regex(pattern);
    }

    /// <summary> 构建高级语法高亮的正则表达式 </summary>
    private void BuildAdvancedRegexes()
    {
        // 类名匹配：class 关键字后的标识符
        classRegex = new Regex(@"\bclass\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
        
        // 函数名匹配：def 关键字后的标识符，或者函数调用
        functionRegex = new Regex(@"(?:\bdef\s+([A-Za-z_][A-Za-z0-9_]*)|([A-Za-z_][A-Za-z0-9_]*)\s*(?=\())", RegexOptions.Compiled);
        
        // 变量名匹配：赋值操作的左侧
        variableRegex = new Regex(@"([A-Za-z_][A-Za-z0-9_]*)\s*(?==)", RegexOptions.Compiled);
        
        // 字符串匹配：单引号、双引号、三引号字符串
        stringRegex = new Regex(@"(?:""""""[\s\S]*?""""""|'''[\s\S]*?'''|""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*')", RegexOptions.Compiled);
        
        // 数字匹配：整数、浮点数、科学计数法
        numberRegex = new Regex(@"\b(?:\d+\.?\d*(?:[eE][+-]?\d+)?|\.\d+(?:[eE][+-]?\d+)?)\b", RegexOptions.Compiled);
        
        // 注释匹配：# 开头的单行注释
        commentRegex = new Regex(@"#.*$", RegexOptions.Compiled | RegexOptions.Multiline);
        
        // 装饰器匹配：@ 开头的装饰器
        decoratorRegex = new Regex(@"@[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*", RegexOptions.Compiled);
    }

    /// <summary> 把纯文本转换为富文本后赋给 highlightText </summary>
    private void ApplySyntaxHighlight()
    {
        if (highlightText == null) return;

        string src = codeInputField.text;
        if (string.IsNullOrEmpty(src))
        {
            highlightText.text = "";
            return;
        }

        // 先做 HTML 编码，防止 < > 等被解释
        // src = src.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        // 应用所有语法高亮（一次性处理，避免嵌套标签）
        string rich = ApplyAllHighlighting(src);

        // 保持换行等格式
        highlightText.text = rich;
    }

    /// <summary> 应用高级语法高亮 </summary>
    private string ApplyAdvancedHighlighting(string text)
    {
        string result = text;
        
        // 1. 高亮注释（优先级最高，避免注释内容被其他规则匹配）
        if (commentRegex != null)
        {
            result = commentRegex.Replace(result, m => $"<color={commentColor}>{m.Value}</color>");
        }
        
        // 2. 高亮字符串（第二优先级）
        if (stringRegex != null)
        {
            result = stringRegex.Replace(result, m => $"<color={stringColor}>{m.Value}</color>");
        }
        
        // 3. 高亮装饰器
        if (decoratorRegex != null)
        {
            result = decoratorRegex.Replace(result, m => $"<color={decoratorColor}>{m.Value}</color>");
        }
        
        // 4. 高亮数字
        if (numberRegex != null)
        {
            result = numberRegex.Replace(result, m =>
            {
                // 避免高亮已经被处理的内容
                if (m.Value.Contains("<color=")) return m.Value;
                return $"<color={numberColor}>{m.Value}</color>";
            });
        }
        
        // 5. 高亮类名
        if (classRegex != null)
        {
            result = classRegex.Replace(result, m =>
            {
                if (m.Groups[1].Success)
                {
                    string className = m.Groups[1].Value;
                    return m.Value.Replace(className, $"<color={classNameColor}>{className}</color>");
                }
                return m.Value;
            });
        }
        
        // 6. 高亮函数名
        if (functionRegex != null)
        {
            result = functionRegex.Replace(result, m =>
            {
                string replacement = m.Value;
                
                // def 后的函数定义
                if (m.Groups[1].Success)
                {
                    string funcName = m.Groups[1].Value;
                    replacement = replacement.Replace(funcName, $"<color={functionNameColor}>{funcName}</color>");
                }
                // 函数调用
                else if (m.Groups[2].Success)
                {
                    string funcName = m.Groups[2].Value;
                    // 避免重复高亮已经处理的内容
                    if (!funcName.Contains("<color="))
                    {
                        replacement = replacement.Replace(funcName, $"<color={functionNameColor}>{funcName}</color>");
                    }
                }
                
                return replacement;
            });
        }
        
        // 7. 高亮变量名（优先级最低）
        if (variableRegex != null)
        {
            result = variableRegex.Replace(result, m =>
            {
                if (m.Groups[1].Success)
                {
                    string varName = m.Groups[1].Value;
                    // 避免高亮已经被处理的内容和关键字
                    if (!varName.Contains("<color=") && !tokenColors.ContainsKey(varName))
                    {
                        return m.Value.Replace(varName, $"<color={variableColor}>{varName}</color>");
                    }
                }
                return m.Value;
            });
        }
        
        return result;
    }

    /// <summary> 应用所有语法高亮，使用基于位置的安全处理 </summary>
    private string ApplyAllHighlighting(string text)
    {
        // 创建一个标记数组，记录每个字符是否已经被高亮
        bool[] highlighted = new bool[text.Length];
        List<HighlightMatch> matches = new List<HighlightMatch>();

        // 1. 收集所有匹配项
        CollectMatches(text, matches, highlighted);

        // 2. 按优先级和位置排序
        matches.Sort((a, b) => {
            if (a.Priority != b.Priority) return a.Priority.CompareTo(b.Priority);
            return a.Start.CompareTo(b.Start);
        });

        // 3. 应用高亮，跳过冲突的匹配
        foreach (var match in matches)
        {
            bool canApply = true;
            for (int i = match.Start; i < match.Start + match.Length; i++)
            {
                if (highlighted[i])
                {
                    canApply = false;
                    break;
                }
            }

            if (canApply)
            {
                match.CanApply = true;
                for (int i = match.Start; i < match.Start + match.Length; i++)
                {
                    highlighted[i] = true;
                }
            }
        }

        // 4. 构建最终结果 - IL2CPP兼容：避免LINQ
        var applicableMatches = new List<HighlightMatch>();
        foreach (var match in matches)
        {
            if (match.CanApply)
            {
                applicableMatches.Add(match);
            }
        }
        return BuildHighlightedText(text, applicableMatches);
    }

    /// <summary> 收集所有语法匹配项 </summary>
    private void CollectMatches(string text, List<HighlightMatch> matches, bool[] highlighted)
    {
        // 优先级1: 注释
        if (commentRegex != null)
        {
            foreach (Match match in commentRegex.Matches(text))
            {
                matches.Add(new HighlightMatch
                {
                    Start = match.Index,
                    Length = match.Length,
                    Color = commentColor,
                    Priority = 1
                });
            }
        }

        // 优先级2: 字符串
        if (stringRegex != null)
        {
            foreach (Match match in stringRegex.Matches(text))
            {
                matches.Add(new HighlightMatch
                {
                    Start = match.Index,
                    Length = match.Length,
                    Color = stringColor,
                    Priority = 2
                });
            }
        }

        // 优先级3: 装饰器
        if (decoratorRegex != null)
        {
            foreach (Match match in decoratorRegex.Matches(text))
            {
                matches.Add(new HighlightMatch
                {
                    Start = match.Index,
                    Length = match.Length,
                    Color = decoratorColor,
                    Priority = 3
                });
            }
        }

        // 优先级4: 类名
        if (classRegex != null)
        {
            foreach (Match match in classRegex.Matches(text))
            {
                if (match.Groups[1].Success)
                {
                    var group = match.Groups[1];
                    matches.Add(new HighlightMatch
                    {
                        Start = group.Index,
                        Length = group.Length,
                        Color = classNameColor,
                        Priority = 4
                    });
                }
            }
        }

        // 优先级5: 函数名
        if (functionRegex != null)
        {
            foreach (Match match in functionRegex.Matches(text))
            {
                // def 后的函数定义
                if (match.Groups[1].Success)
                {
                    var group = match.Groups[1];
                    matches.Add(new HighlightMatch
                    {
                        Start = group.Index,
                        Length = group.Length,
                        Color = functionNameColor,
                        Priority = 5
                    });
                }
                // 函数调用
                else if (match.Groups[2].Success)
                {
                    var group = match.Groups[2];
                    matches.Add(new HighlightMatch
                    {
                        Start = group.Index,
                        Length = group.Length,
                        Color = functionNameColor,
                        Priority = 5
                    });
                }
            }
        }

        // 优先级6: 数字
        if (numberRegex != null)
        {
            foreach (Match match in numberRegex.Matches(text))
            {
                matches.Add(new HighlightMatch
                {
                    Start = match.Index,
                    Length = match.Length,
                    Color = numberColor,
                    Priority = 6
                });
            }
        }

        // 优先级7: 关键字
        if (tokenRegex != null)
        {
            foreach (Match match in tokenRegex.Matches(text))
            {
                string token = match.Value;
                if (tokenColors.TryGetValue(token, out string color))
                {
                    matches.Add(new HighlightMatch
                    {
                        Start = match.Index,
                        Length = match.Length,
                        Color = color,
                        Priority = 7
                    });
                }
            }
        }

        // 优先级8: 变量名（最低优先级）
        if (variableRegex != null)
        {
            foreach (Match match in variableRegex.Matches(text))
            {
                if (match.Groups[1].Success)
                {
                    var group = match.Groups[1];
                    string varName = group.Value;
                    // 只有非关键字才高亮为变量
                    if (!tokenColors.ContainsKey(varName))
                    {
                        matches.Add(new HighlightMatch
                        {
                            Start = group.Index,
                            Length = group.Length,
                            Color = variableColor,
                            Priority = 8
                        });
                    }
                }
            }
        }
    }

    /// <summary> 构建最终的高亮文本 </summary>
    private string BuildHighlightedText(string originalText, List<HighlightMatch> matches)
    {
        if (matches.Count == 0) return originalText;

        // 按位置排序
        matches.Sort((a, b) => a.Start.CompareTo(b.Start));

        System.Text.StringBuilder result = new System.Text.StringBuilder();
        int currentPos = 0;

        foreach (var match in matches)
        {
            // 添加匹配前的文本
            if (match.Start > currentPos)
            {
                result.Append(originalText.Substring(currentPos, match.Start - currentPos));
            }

            // 添加高亮的文本
            string matchedText = originalText.Substring(match.Start, match.Length);
            result.Append($"<color={match.Color}>{matchedText}</color>");

            currentPos = match.Start + match.Length;
        }

        // 添加剩余的文本
        if (currentPos < originalText.Length)
        {
            result.Append(originalText.Substring(currentPos));
        }

        return result.ToString();
    }

    /// <summary> 高亮匹配项 </summary>
    private class HighlightMatch
    {
        public int Start { get; set; }
        public int Length { get; set; }
        public string Color { get; set; }
        public int Priority { get; set; }
        public bool CanApply { get; set; }
    }
    
    /// <summary>
    /// 设置编辑模式
    /// </summary>
    /// <param name="editMode">true为编辑模式，false为运行模式</param>
    public void SetEditMode(bool editMode)
    {
        isEditMode = editMode;
        
        if (codeInputField == null) return;
        
        if (editMode)
        {
            // 切换到编辑模式
            codeInputField.interactable = true;
            codeInputField.readOnly = false;
            
            // 停止执行跟踪
            StopExecutionTracking();
            
            // 恢复正常的文本颜色
            if (codeInputField.textComponent != null)
            {
                codeInputField.textComponent.color = new Color(editModeColor.r, editModeColor.g, editModeColor.b, 0); // 保持透明用于语法高亮
            }
            
            // 启用所有编辑功能
            enableBracketCompletion = true;
            enableQuoteCompletion = true;
            enableAutoIndent = true;
        }
        else
        {
            // 切换到运行模式
            codeInputField.interactable = true;  // 保持可交互，但设为只读
            codeInputField.readOnly = true;      // 设为只读，禁止编辑但允许光标移动
            
            // 开始执行跟踪
            StartExecutionTracking();
            
            // 设置运行模式的视觉效果
            if (codeInputField.textComponent != null)
            {
                codeInputField.textComponent.color = new Color(runModeColor.r, runModeColor.g, runModeColor.b, 0.3f); // 稍微透明
            }
            
            // 禁用编辑功能以避免在运行时触发
            enableBracketCompletion = false;
            enableQuoteCompletion = false;
            enableAutoIndent = false;
                    }
        
        // 更新语法高亮
        ApplySyntaxHighlight();
    }
    
    /// <summary>
    /// 获取当前是否为编辑模式
    /// </summary>
    /// <returns>true为编辑模式，false为运行模式</returns>
    public bool IsEditMode()
    {
        return isEditMode;
    }
    
    /// <summary>
    /// 切换编辑模式
    /// </summary>
    public void ToggleEditMode()
    {
        SetEditMode(!isEditMode);
    }
    
    /// <summary>
    /// 设置当前执行行并移动光标到该行
    /// </summary>
    /// <param name="lineNumber">行号（0-based）</param>
    public void SetCurrentExecutingLine(int lineNumber)
    {
        if (codeInputField == null) return;
        
        currentExecutingLine = lineNumber;
        
        // 只在运行模式下移动光标
        if (!isEditMode && isTrackingExecution)
        {
            MoveCaretToLine(lineNumber);
        }
    }
    
    /// <summary>
    /// 开始执行跟踪模式
    /// </summary>
    public void StartExecutionTracking()
    {
        isTrackingExecution = true;
        currentExecutingLine = -1;
        
        // 在运行模式下，输入框保持可交互但设为只读，允许系统控制光标
        if (codeInputField != null && !isEditMode)
        {
            codeInputField.interactable = true;  // 保持可交互
            codeInputField.readOnly = true;      // 但设为只读
        }
    }
    
    /// <summary>
    /// 停止执行跟踪模式
    /// </summary>
    public void StopExecutionTracking()
    {
        isTrackingExecution = false;
        currentExecutingLine = -1;
        
        // 停止跟踪协程
        if (executionTrackingCoroutine != null)
        {
            StopCoroutine(executionTrackingCoroutine);
            executionTrackingCoroutine = null;
        }
        
        // 恢复输入框状态
        if (codeInputField != null)
        {
            if (isEditMode)
            {
                codeInputField.interactable = true;
                codeInputField.readOnly = false;
            }
            else
            {
                codeInputField.interactable = true;  // 运行模式下保持可交互
                codeInputField.readOnly = true;      // 但保持只读
            }
        }
    }
    
    /// <summary>
    /// 移动光标到指定行
    /// </summary>
    /// <param name="lineNumber">行号（0-based）</param>
    private void MoveCaretToLine(int lineNumber)
    {
        if (codeInputField == null) return;
        
        string text = codeInputField.text;
        if (string.IsNullOrEmpty(text)) return;
        
        // 计算指定行的字符位置
        int targetPosition = GetCharacterPositionForLine(text, lineNumber);
        
        if (targetPosition >= 0)
        {
            // 在运行模式下，需要临时启用交互来设置光标位置
            bool wasInteractable = codeInputField.interactable;
            bool wasReadOnly = codeInputField.readOnly;
            
            // 确保输入框可以接受光标位置更改
            codeInputField.interactable = true;
            
            // 设置光标位置
            codeInputField.caretPosition = targetPosition;
            codeInputField.selectionAnchorPosition = targetPosition;
            codeInputField.selectionFocusPosition = targetPosition;
            
            // 强制更新UI
            codeInputField.ForceLabelUpdate();
            
            // 恢复原始状态
            codeInputField.interactable = wasInteractable;
            codeInputField.readOnly = wasReadOnly;
            
            // 更新上次光标位置
            lastCaretPosition = targetPosition;
        }
    }
    
    /// <summary>
    /// 获取指定行的字符位置
    /// </summary>
    /// <param name="text">文本内容</param>
    /// <param name="lineNumber">行号（0-based）</param>
    /// <returns>字符位置，如果行号无效返回-1</returns>
    private int GetCharacterPositionForLine(string text, int lineNumber)
    {
        if (lineNumber < 0) return -1;
        
        string[] lines = text.Split('\n');
        if (lineNumber >= lines.Length) return -1;
        
        int position = 0;
        
        // 计算到目标行开始的字符数
        for (int i = 0; i < lineNumber; i++)
        {
            position += lines[i].Length + 1; // +1 for newline character
        }
        
        return position;
    }
    
    /// <summary>
    /// 高亮当前执行行
    /// </summary>
    private void HighlightCurrentExecutingLine()
    {
        if (!isTrackingExecution || currentExecutingLine < 0) return;
        
        // 这里可以添加高亮当前执行行的逻辑
        // 例如修改背景颜色或添加特殊标记
    }
    
    /// <summary>
    /// 获取当前执行行号
    /// </summary>
    /// <returns>当前执行行号（0-based），如果没有在执行返回-1</returns>
    public int GetCurrentExecutingLine()
    {
        return isTrackingExecution ? currentExecutingLine : -1;
    }
    
    /// <summary>
    /// 是否正在跟踪执行
    /// </summary>
    /// <returns>是否正在跟踪执行</returns>
    public bool IsTrackingExecution()
    {
        return isTrackingExecution;
    }
    
    /// <summary>
    /// 更新粒子效果位置，使其跟随光标
    /// </summary>
    /// <param name="caretPosition">当前光标位置</param>
    private void UpdateParticleEffectPosition(int caretPosition)
    {
        if (!enableParticleEffect || particleEffectPools.Count == 0 || codeInputField?.textComponent == null) 
            return;

        // 获取文本信息
        TMP_TextInfo textInfo = codeInputField.textComponent.textInfo;
        // codeInputField.textComponent.ForceMeshUpdate(); // 确保文本信息是最新的

        if (textInfo.characterCount == 0)
        {
            // 如果没有文本，将所有池中的粒子效果放在起始位置
            for (int poolIndex = 0; poolIndex < particleEffectPools.Count; poolIndex++)
            {
                var pool = particleEffectPools[poolIndex];
                if (pool?.instancePool != null)
                {
                    for (int i = 0; i < pool.instancePool.Count; i++)
                    {
                        if (pool.instancePool[i] != null)
                        {
                            pool.instancePool[i].transform.position = codeInputField.textComponent.transform.position;
                        }
                    }
                }
            }
            return;
        }

        // 确保光标位置在有效范围内
        int safeCaretPos = Mathf.Clamp(caretPosition, 0, textInfo.characterCount);
        
        Vector3 caretWorldPos;
        
        if (safeCaretPos == textInfo.characterCount && textInfo.characterCount > 0)
        {
            // 光标在文本末尾，使用最后一个字符的位置
            int lastCharIndex = textInfo.characterCount - 1;
            TMP_CharacterInfo lastCharInfo = textInfo.characterInfo[lastCharIndex];
            
            // 计算光标应该在最后一个字符之后的位置
            caretWorldPos = new Vector3(
                lastCharInfo.topRight.x,
                (lastCharInfo.topRight.y + lastCharInfo.bottomRight.y) * 0.5f,
                0
            );
        }
        else if (safeCaretPos < textInfo.characterCount)
        {
            // 光标在字符前面
            TMP_CharacterInfo charInfo = textInfo.characterInfo[safeCaretPos];
            caretWorldPos = new Vector3(
                charInfo.topLeft.x,
                (charInfo.topLeft.y + charInfo.bottomLeft.y) * 0.5f,
                0
            );
        }
        else
        {
            // 回退到起始位置
            caretWorldPos = Vector3.zero;
        }

        // 将本地坐标转换为世界坐标
        Vector3 worldPos = codeInputField.textComponent.transform.TransformPoint(caretWorldPos);
        
        // 更新所有池中活跃的粒子效果的世界位置
        for (int poolIndex = 0; poolIndex < particleEffectPools.Count; poolIndex++)
        {
            var pool = particleEffectPools[poolIndex];
            if (pool?.instancePool != null)
            {
                for (int i = 0; i < pool.instancePool.Count; i++)
                {
                    if (pool.instancePool[i] != null)
                    {
                        pool.instancePool[i].transform.position = worldPos;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 播放粒子效果（默认播放所有效果）
    /// </summary>
    /// <param name="effectIndex">指定的粒子效果索引，-1表示播放所有效果，-2表示随机选择一种</param>
    private void PlayParticleEffect(int effectIndex = -1)
    {
        if (!enableParticleEffect || particleEffectPools.Count == 0) return;

        if (effectIndex == -2)
        {
            // 随机选择一个粒子效果播放
            int randomIndex = UnityEngine.Random.Range(0, particleEffectPools.Count);
            PlaySingleParticleEffect(randomIndex);
        }
        else if (effectIndex >= 0 && effectIndex < particleEffectPools.Count)
        {
            // 播放指定的粒子效果
            PlaySingleParticleEffect(effectIndex);
        }
        else
        {
            // 默认播放所有粒子效果
            for (int i = 0; i < particleEffectPools.Count; i++)
            {
                PlaySingleParticleEffect(i);
            }
        }
    }
    
    /// <summary>
    /// 播放单个粒子效果
    /// </summary>
    /// <param name="poolIndex">粒子效果池索引</param>
    private void PlaySingleParticleEffect(int poolIndex)
    {
        if (poolIndex < 0 || poolIndex >= particleEffectPools.Count) return;
        
        var pool = particleEffectPools[poolIndex];
        if (pool == null) return;

        // 获取一个可用的粒子实例
        GameObject availableParticle = GetAvailableParticleInstance(poolIndex);
        if (availableParticle == null) return;

        ParticleSystem particleSystem = availableParticle.GetComponent<ParticleSystem>();
        if (particleSystem != null)
        {
            // 停止当前播放（如果正在播放）
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            
            // 更新粒子效果位置到当前光标位置
            UpdateParticleEffectPosition(codeInputField.caretPosition);
            
            // 开始播放
            particleSystem.Play();
            
            // 启动协程来管理粒子效果的生命周期
            StartCoroutine(ManageParticleLifetime(particleSystem));
        }
    }
    
    /// <summary>
    /// 创建粒子效果实例
    /// <summary>
    /// 获取一个可用的粒子实例（优先复用不在播放的实例）
    /// </summary>
    /// <param name="poolIndex">指定粒子效果池索引</param>
    private GameObject GetAvailableParticleInstance(int poolIndex)
    {
        if (poolIndex < 0 || poolIndex >= particleEffectPools.Count) return null;
        
        var pool = particleEffectPools[poolIndex];
        if (pool == null) return null;
        
        // 先检查是否有可复用的实例（不在播放状态的）
        for (int i = 0; i < pool.instancePool.Count; i++)
        {
            if (pool.instancePool[i] != null && pool.systemPool[i] != null)
            {
                if (!pool.systemPool[i].isPlaying)
                {
                    return pool.instancePool[i];
                }
            }
        }
        
        // 如果池未满，创建新实例
        if (pool.instancePool.Count < pool.maxInstances)
        {
            return CreateNewParticleInstance(poolIndex);
        }
        
        // 池已满，强制复用最旧的实例（索引0）
        if (pool.instancePool.Count > 0 && pool.instancePool[0] != null)
        {
            if (pool.systemPool[0] != null)
            {
                pool.systemPool[0].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            return pool.instancePool[0];
        }
        
        return null;
    }
    
    /// <summary>
    /// 创建新的粒子实例并添加到指定池中
    /// </summary>
    /// <param name="poolIndex">目标池索引</param>
    private GameObject CreateNewParticleInstance(int poolIndex)
    {
        if (poolIndex < 0 || poolIndex >= particleEffectPools.Count) return null;
        
        var pool = particleEffectPools[poolIndex];
        if (pool?.prefab == null) return null;

        // 实例化粒子效果预制体
        GameObject newInstance = Instantiate(pool.prefab);
        
        // 获取粒子系统组件
        ParticleSystem newParticleSystem = newInstance.GetComponent<ParticleSystem>();
        
        if (newParticleSystem == null)
        {
            Debug.LogWarning($"TextHelper: Particle effect prefab '{pool.prefab.name}' doesn't have a ParticleSystem component!");
            DestroyImmediate(newInstance);
            return null;
        }

        // 设置粒子系统为不自动播放
        var main = newParticleSystem.main;
        main.playOnAwake = false;
        
        // 设置初始位置
        if (codeInputField?.textComponent != null)
        {
            newInstance.transform.position = codeInputField.textComponent.transform.position;
        }
        
        // 添加到池中
        pool.instancePool.Add(newInstance);
        pool.systemPool.Add(newParticleSystem);
        
        Debug.Log($"TextHelper: New particle effect instance created for pool {poolIndex} ({pool.prefab.name}). Pool size: {pool.instancePool.Count}");
        return newInstance;
    }
    
    /// <summary>
    /// 管理粒子效果的生命周期
    /// </summary>
    private System.Collections.IEnumerator ManageParticleLifetime(ParticleSystem particleSystem)
    {
        if (particleSystem == null) yield break;

        // 等待粒子系统播放完成或达到设定的生命周期
        float elapsedTime = 0f;
        
        while (elapsedTime < particleLifetime && particleSystem != null && particleSystem.isPlaying)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // 停止粒子系统（但保留实例以供下次使用）
        if (particleSystem != null)
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
    
    /// <summary>
    /// 设置粒子效果启用状态
    /// </summary>
    /// <param name="enabled">是否启用粒子效果</param>
    public void SetParticleEffectEnabled(bool enabled)
    {
        enableParticleEffect = enabled;
        
        // 如果禁用，停止当前播放的粒子效果
        if (!enabled)
        {
            for (int poolIndex = 0; poolIndex < particleEffectPools.Count; poolIndex++)
            {
                var pool = particleEffectPools[poolIndex];
                if (pool?.systemPool != null)
                {
                    for (int i = 0; i < pool.systemPool.Count; i++)
                    {
                        if (pool.systemPool[i] != null)
                        {
                            pool.systemPool[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 设置粒子效果预制体数组
    /// </summary>
    /// <param name="prefabs">新的粒子效果预制体数组</param>
    public void SetParticleEffectPrefabs(GameObject[] prefabs)
    {
        // 销毁旧的所有粒子实例
        for (int poolIndex = 0; poolIndex < particleEffectPools.Count; poolIndex++)
        {
            var pool = particleEffectPools[poolIndex];
            if (pool?.instancePool != null)
            {
                for (int i = 0; i < pool.instancePool.Count; i++)
                {
                    if (pool.instancePool[i] != null)
                    {
                        DestroyImmediate(pool.instancePool[i]);
                    }
                }
                pool.instancePool.Clear();
                pool.systemPool.Clear();
            }
        }
        particleEffectPools.Clear();
        
        // 设置新的预制体数组
        particleEffectPrefabs = prefabs;
        
        // 重新初始化粒子效果池
        if (prefabs != null && prefabs.Length > 0 && enableParticleEffect)
        {
            InitializeParticleEffectPools();
            Debug.Log($"TextHelper: New particle effect prefabs set ({prefabs.Length} effects), pools initialized.");
        }
    }
    
    /// <summary>
    /// 添加单个粒子效果预制体到现有池中
    /// </summary>
    /// <param name="prefab">要添加的粒子效果预制体</param>
    public void AddParticleEffectPrefab(GameObject prefab)
    {
        if (prefab == null) return;
        
        // 扩展数组
        var newArray = new GameObject[particleEffectPrefabs?.Length + 1 ?? 1];
        if (particleEffectPrefabs != null)
        {
            System.Array.Copy(particleEffectPrefabs, newArray, particleEffectPrefabs.Length);
        }
        newArray[newArray.Length - 1] = prefab;
        particleEffectPrefabs = newArray;
        
        // 为新预制体创建池
        var newPool = new ParticleEffectPool(prefab, maxInstancesPerEffect);
        particleEffectPools.Add(newPool);
        
        Debug.Log($"TextHelper: Added new particle effect prefab '{prefab.name}'. Total effects: {particleEffectPrefabs.Length}");
    }

    /// <summary>
    /// 获取当前粒子效果是否正在播放
    /// </summary>
    /// <param name="poolIndex">指定池索引，-1表示检查所有池</param>
    /// <returns>是否正在播放</returns>
    public bool IsParticleEffectPlaying(int poolIndex = -1)
    {
        if (poolIndex >= 0 && poolIndex < particleEffectPools.Count)
        {
            // 检查指定池
            var pool = particleEffectPools[poolIndex];
            if (pool?.systemPool != null)
            {
                for (int i = 0; i < pool.systemPool.Count; i++)
                {
                    if (pool.systemPool[i] != null && pool.systemPool[i].isPlaying)
                    {
                        return true;
                    }
                }
            }
        }
        else
        {
            // 检查所有池
            for (int poolIdx = 0; poolIdx < particleEffectPools.Count; poolIdx++)
            {
                var pool = particleEffectPools[poolIdx];
                if (pool?.systemPool != null)
                {
                    for (int i = 0; i < pool.systemPool.Count; i++)
                    {
                        if (pool.systemPool[i] != null && pool.systemPool[i].isPlaying)
                        {
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }
    
    /// <summary>
    /// 手动触发粒子效果播放
    /// </summary>
    /// <param name="effectIndex">指定效果索引，-1表示播放所有效果，-2表示随机选择一种</param>
    public void TriggerParticleEffect(int effectIndex = -1)
    {
        if (enableParticleEffect && isEditMode)
        {
            PlayParticleEffect(effectIndex);
        }
    }
    
    /// <summary>
    /// 获取粒子效果池的数量
    /// </summary>
    /// <returns>池的数量</returns>
    public int GetParticleEffectPoolCount()
    {
        return particleEffectPools.Count;
    }
    
    /// <summary>
    /// 获取指定池中活跃实例的数量
    /// </summary>
    /// <param name="poolIndex">池索引</param>
    /// <returns>活跃实例数量</returns>
    public int GetActiveParticleInstanceCount(int poolIndex)
    {
        if (poolIndex < 0 || poolIndex >= particleEffectPools.Count) return 0;
        
        var pool = particleEffectPools[poolIndex];
        if (pool?.systemPool == null) return 0;
        
        int activeCount = 0;
        for (int i = 0; i < pool.systemPool.Count; i++)
        {
            if (pool.systemPool[i] != null && pool.systemPool[i].isPlaying)
            {
                activeCount++;
            }
        }
        return activeCount;
    }
    
    /// <summary>
    /// 停止所有粒子效果
    /// </summary>
    public void StopAllParticleEffects()
    {
        for (int poolIndex = 0; poolIndex < particleEffectPools.Count; poolIndex++)
        {
            var pool = particleEffectPools[poolIndex];
            if (pool?.systemPool != null)
            {
                for (int i = 0; i < pool.systemPool.Count; i++)
                {
                    if (pool.systemPool[i] != null)
                    {
                        pool.systemPool[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 停止指定池的所有粒子效果
    /// </summary>
    /// <param name="poolIndex">池索引</param>
    public void StopParticleEffect(int poolIndex)
    {
        if (poolIndex < 0 || poolIndex >= particleEffectPools.Count) return;
        
        var pool = particleEffectPools[poolIndex];
        if (pool?.systemPool != null)
        {
            for (int i = 0; i < pool.systemPool.Count; i++)
            {
                if (pool.systemPool[i] != null)
                {
                    pool.systemPool[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }
    }

    /// <summary>
    /// 将指定的GameObject立即移动到当前光标位置
    /// </summary>
    /// <param name="targetObject">要移动的GameObject</param>
    public void MoveObjectToCaretPosition()
    {
        var targetObject = Instantiate(enterParticleObject);

        if (targetObject == null)
        {
            Debug.LogWarning("[TextHelper] Target object is null! Cannot move to caret position.");
            return;
        }

        if (codeInputField?.textComponent == null)
        {
            Debug.LogWarning("[TextHelper] Code input field or text component is null! Cannot get caret position.");
            return;
        }

        // 获取当前光标位置
        int caretPosition = codeInputField.caretPosition;

        // 获取文本信息
        TMP_TextInfo textInfo = codeInputField.textComponent.textInfo;
        codeInputField.textComponent.ForceMeshUpdate(); // 确保文本信息是最新的

        if (textInfo.characterCount == 0)
        {
            // 如果没有文本，将物体放在起始位置
            Vector3 startWorldPos = codeInputField.textComponent.transform.TransformPoint(Vector3.zero);
            targetObject.transform.position = startWorldPos;
            Debug.Log($"[TextHelper] Moved {targetObject.name} to start position (no text).");
            return;
        }

        // 确保光标位置在有效范围内
        int safeCaretPos = Mathf.Clamp(caretPosition, 0, textInfo.characterCount);

        Vector3 caretLocalPos;

        if (safeCaretPos == textInfo.characterCount && textInfo.characterCount > 0)
        {
            // 光标在文本末尾，使用最后一个字符的位置
            int lastCharIndex = textInfo.characterCount - 1;
            TMP_CharacterInfo lastCharInfo = textInfo.characterInfo[lastCharIndex];

            // 计算光标应该在最后一个字符之后的位置
            caretLocalPos = new Vector3(
                lastCharInfo.topRight.x,
                (lastCharInfo.topRight.y + lastCharInfo.bottomRight.y) * 0.5f,
                0
            );
        }
        else if (safeCaretPos < textInfo.characterCount)
        {
            // 光标在字符前面
            TMP_CharacterInfo charInfo = textInfo.characterInfo[safeCaretPos];
            caretLocalPos = new Vector3(
                charInfo.topLeft.x,
                (charInfo.topLeft.y + charInfo.bottomLeft.y) * 0.5f,
                0
            );
        }
        else
        {
            // 回退到起始位置
            caretLocalPos = Vector3.zero;
        }

        // 将本地坐标转换为世界坐标
        Vector3 caretWorldPos = codeInputField.textComponent.transform.TransformPoint(caretLocalPos);

        // 设置目标物体的位置
        targetObject.transform.position = caretWorldPos;
        
        targetObject.SetActive(true);
    }
    
    /// <summary>
    /// 获取当前光标所在的行号（1-based）
    /// </summary>
    /// <returns>当前行号，如果无法确定则返回-1</returns>
    private int GetCurrentLineNumber()
    {
        if (codeInputField?.text == null) return -1;
        
        int caretPosition = codeInputField.caretPosition;
        string textBeforeCaret = codeInputField.text.Substring(0, Mathf.Min(caretPosition, codeInputField.text.Length));
        
        return CountNewlines(textBeforeCaret) + 1;
    }
    
}
