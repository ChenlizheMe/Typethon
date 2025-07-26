using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 文件系统浏览器 - 用于浏览文件夹和Python脚本
/// 支持键盘导航：回车/空格展开文件夹，ESC返回上级，上下/WS选择项目
/// </summary>
public class FileSystemBrowser : MonoBehaviour
{
    /*
     * 文件树显示优化说明：
     * 1. 避免操作时刷新整个文件树 - 改用增量更新，保持展开状态
     * 2. 修复多级文件树显示错误 - 正确的递归扫描顺序，先添加文件夹再递归子目录
     * 3. 新文件/文件夹创建、删除、重命名操作都使用增量更新而非全量刷新
     * 4. 自动使用exe所在目录下的"Python"文件夹作为工作目录
     * 5. 支持命令行参数指定工作目录
     */
    
    public static FileSystemBrowser Instance;
    public bool isEdit = false;

    public TextMeshProUGUI helpText;
    public GameObject[] ChooseMask;
    public GameObject[] EditMask;

    // 编辑相关
    private string currentEditingFilePath = "";
    private Coroutine autoSaveCoroutine;

    [Header("UI Components")]
    [SerializeField] private ScrollRect scrollView;
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject folderPrefab;
    [SerializeField] private GameObject scriptPrefab;
    [SerializeField] private CustomTMPInput textEditor; // 文本编辑器组件
    
    [Header("Settings")]
    [SerializeField] private string fileExtension = ".py";
    [SerializeField] private int maxDepth = 10;
    
    [Header("Layout Settings")]
    [SerializeField] private float itemSpacing = 5f;
    [SerializeField] private float folderHeight = 40f;
    [SerializeField] private float fileHeight = 35f;
    [SerializeField] private RectOffset contentPadding;
    
    [Header("Selection Colors")]
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color normalColor = Color.white;
    
    [Header("Material Animation")]
    [SerializeField] private Material animationMaterial; // 用于动画的材质
    
    // 当前浏览状态
    private string currentPath;
    private string rootPath; // 根路径，将自动设置为exe目录下的Python目录
    private List<FileSystemItem> allItems = new List<FileSystemItem>(); // 所有扫描到的项目
    private List<FileSystemItem> currentItems = new List<FileSystemItem>(); // 当前显示的项目（考虑折叠状态）
    private int selectedIndex = 0;
    private Stack<string> pathHistory = new Stack<string>();
    
    // UI管理
    private List<GameObject> itemObjects = new List<GameObject>();
    private RectTransform contentRect;
    
    // UI布局控制
    private Vector2 originalScrollViewPosition;
    private Vector2 originalScrollViewSize;
    private Image scrollViewImage;
    private bool layoutInitialized = false;
    
    // 材质动画控制
    private Coroutine materialAnimationCoroutine;
    
    // 重命名控制
    private bool isRenaming = false;
    private TMP_InputField currentRenamingInputField;
    private string originalItemName;
    private string originalItemPath;
    
    /// <summary>
    /// 文件系统项目信息
    /// </summary>
    [System.Serializable]
    public class FileSystemItem
    {
        public string name;
        public string fullPath;
        public bool isDirectory;
        public bool isExpanded; // 文件夹是否展开
        public int indentLevel; // 缩进级别（用于显示层级结构）
        public GameObject uiObject;
        
        public FileSystemItem(string name, string fullPath, bool isDirectory, int indentLevel = 0)
        {
            this.name = name;
            this.fullPath = fullPath;
            this.isDirectory = isDirectory;
            this.isExpanded = false;
            this.indentLevel = indentLevel;
        }
    }
    
    void Awake()
    {
        Instance = this;
        // 确保列表被正确初始化
        if (allItems == null)
            allItems = new List<FileSystemItem>();
        if (currentItems == null)
            currentItems = new List<FileSystemItem>();
        if (itemObjects == null)
            itemObjects = new List<GameObject>();
        if (pathHistory == null)
            pathHistory = new Stack<string>();
        
        // 初始化RectOffset（不能在字段初始化器中创建）
        if (contentPadding == null)
            contentPadding = new RectOffset(10, 10, 10, 10);
    }
    
    /// <summary>
    /// 解析命令行参数获取工作目录
    /// </summary>
    /// <returns>指定的工作目录路径，如果没有指定则返回null</returns>
    private string ParseCommandLineWorkingDirectory()
    {
        string[] args = Environment.GetCommandLineArgs();
        
        for (int i = 0; i < args.Length - 1; i++)
        {
            // 支持 --directory 或 -d 参数
            if (args[i].Equals("--directory", StringComparison.OrdinalIgnoreCase) || 
                args[i].Equals("-d", StringComparison.OrdinalIgnoreCase))
            {
                string specifiedPath = args[i + 1];
                
                // 验证路径是否存在或可以创建
                try
                {
                    if (!Directory.Exists(specifiedPath))
                    {
                        Directory.CreateDirectory(specifiedPath);
                        Debug.Log($"FileSystemBrowser: Created specified directory: {specifiedPath}");
                    }
                    
                    Debug.Log($"FileSystemBrowser: Using command line specified directory: {specifiedPath}");
                    return Path.GetFullPath(specifiedPath);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"FileSystemBrowser: Failed to create/access specified directory '{specifiedPath}': {ex.Message}");
                    Debug.LogWarning("FileSystemBrowser: Falling back to default directory.");
                }
            }
        }
        
        return null; // 没有找到有效的命令行参数
    }
    
    /// <summary>
    /// 在指定目录中创建Typethon文件夹及示例内容
    /// </summary>
    /// <param name="parentDirectory">父目录路径</param>
    private void EnsureTypethonDirectoryExists(string parentDirectory)
    {
        string typethonDirectoryPath = Path.Combine(parentDirectory, "Typethon");
        
        if (!Directory.Exists(typethonDirectoryPath))
        {
            try
            {
                Directory.CreateDirectory(typethonDirectoryPath);
                Debug.Log($"FileSystemBrowser: Created Typethon directory at: {typethonDirectoryPath}");
                
                CreateTypethonExampleFiles(typethonDirectoryPath);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"FileSystemBrowser: Failed to create Typethon directory: {ex.Message}");
            }
        }
        else
        {
            // 即使目录存在，也检查示例文件是否存在，如果不存在则创建
            CreateTypethonExampleFiles(typethonDirectoryPath);
        }
    }
    
    /// <summary>
    /// 在Typethon目录中创建示例文件
    /// </summary>
    /// <param name="typethonDirectoryPath">Typethon目录路径</param>
    private void CreateTypethonExampleFiles(string typethonDirectoryPath)
    {
        try
        {
            // 在Typethon文件夹中创建Welcome.py示例文件
            string welcomeFilePath = Path.Combine(typethonDirectoryPath, "Welcome.py");
            if (!File.Exists(welcomeFilePath))
            {
                string welcomeContent = "# Welcome to Typethon!\n" +
                                      "# This is your Python workspace powered by Typethon.\n" +
                                      "# You can create and edit Python files here.\n" +
                                      "# This folder contains examples and your projects.\n\n" +
                                      "print(\"Hello from Typethon!\")\n" +
                                      "print(\"Ready to start coding Python?\")\n";
                File.WriteAllText(welcomeFilePath, welcomeContent);
                Debug.Log($"FileSystemBrowser: Created Welcome.py at: {welcomeFilePath}");
            }
            
            // 创建一个简单的示例文件
            string exampleFilePath = Path.Combine(typethonDirectoryPath, "Example.py");
            if (!File.Exists(exampleFilePath))
            {
                string exampleContent = "# Example Python Script\n" +
                                      "# This is a simple example to get you started\n\n" +
                                      "def greet(name):\n" +
                                      "    \"\"\"A simple greeting function\"\"\"\n" +
                                      "    return f\"Hello, {name}! Welcome to Python programming!\"\n\n" +
                                      "# Main execution\n" +
                                      "if __name__ == \"__main__\":\n" +
                                      "    user_name = \"Developer\"\n" +
                                      "    message = greet(user_name)\n" +
                                      "    print(message)\n" +
                                      "    \n" +
                                      "    # Basic Python concepts\n" +
                                      "    numbers = [1, 2, 3, 4, 5]\n" +
                                      "    squared = [x**2 for x in numbers]\n" +
                                      "    print(f\"Numbers: {numbers}\")\n" +
                                      "    print(f\"Squared: {squared}\")\n";
                File.WriteAllText(exampleFilePath, exampleContent);
                Debug.Log($"FileSystemBrowser: Created Example.py at: {exampleFilePath}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"FileSystemBrowser: Failed to create example files: {ex.Message}");
        }
    }
    
    void Start()
    {
        InitializeBrowser();
    }
    
    void Update()
    {
        if (!isEdit)
        {
            textEditor.enabled = false;
            Camera.main.GetComponent<CameraHelper>().enabled = false;
            for (int i = 0; i < ChooseMask.Length; i++)
            {
                ChooseMask[i].SetActive(true);
            }
            for (int i = 0; i < EditMask.Length; i++)
            {
                EditMask[i].SetActive(false);
            }
            // 上下：选择文件；Enter：选择文件；ESC：返回上级
            helpText.text = "[Up/Down]: Choose a file        [Enter/Space]: Select a file        [ESC]: back to parent directory";
        }
        else
        {
            Camera.main.GetComponent<CameraHelper>().enabled = true;
            textEditor.enabled = true;
            textEditor.ActivateInputField();
            for (int i = 0; i < ChooseMask.Length; i++)
            {
                ChooseMask[i].SetActive(false);
            }
            for (int i = 0; i < EditMask.Length; i++)
            {
                EditMask[i].SetActive(true);
            }
            // ctrl+r: 运行 ctrl+d: 自动逐行运行  ctrl+e: 手动逐行运行
            helpText.text = "[Ctrl+R]: Run Script     [Ctrl+D]: Auto-Stepping     [Ctrl+E]: Manual-Stepping     [Ctrl+C]: Stop Script";
        }
        
        // 更新UI布局
        UpdateUILayout();
        
        HandleInput();
    }
    
    /// <summary>
    /// 初始化浏览器
    /// </summary>
    private void InitializeBrowser()
    {
        if (scrollView == null)
        {
            Debug.LogError("FileSystemBrowser: ScrollView not assigned!");
            return;
        }
        
        if (folderPrefab == null || scriptPrefab == null)
        {
            Debug.LogError("FileSystemBrowser: Folder or Script prefab not assigned!");
            return;
        }
        
        // 获取ScrollView的content区域
        if (contentParent == null)
        {
            contentRect = scrollView.content;
        }
        else
        {
            contentRect = contentParent.GetComponent<RectTransform>();
        }
        
        if (contentRect == null)
        {
            Debug.LogError("FileSystemBrowser: Cannot find content area!");
            return;
        }
        
        // 确保Content有正确的布局组件
        SetupContentLayout();
        
        // 保存原始布局信息
        InitializeLayoutSettings();
        
        // 首先尝试从命令行参数获取工作目录
        string workingDirectory = ParseCommandLineWorkingDirectory();
        
        if (string.IsNullOrEmpty(workingDirectory))
        {
            // 如果没有命令行参数，使用exe所在的文件夹下的Python文件夹
            string exeDirectory = Path.GetDirectoryName(Application.dataPath); // 获取exe所在目录
            workingDirectory = Path.Combine(exeDirectory, "Python");
            Debug.Log($"FileSystemBrowser: Using exe directory Python folder: {workingDirectory}");
        }
        
        // 确保工作目录存在
        if (!Directory.Exists(workingDirectory))
        {
            try
            {
                Directory.CreateDirectory(workingDirectory);
                Debug.Log($"FileSystemBrowser: Created working directory: {workingDirectory}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"FileSystemBrowser: Failed to create working directory '{workingDirectory}': {ex.Message}");
                // 回退到用户文档目录
                workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                Debug.LogWarning($"FileSystemBrowser: Falling back to Documents: {workingDirectory}");
            }
        }
        
        // 确保Typethon目录及示例文件存在
        EnsureTypethonDirectoryExists(workingDirectory);
        
        rootPath = workingDirectory;
        currentPath = rootPath;
        RefreshCurrentDirectory();
        
        Debug.Log($"FileSystemBrowser: Initialized with root path: {rootPath}");
    }
    
    /// <summary>
    /// 设置Content区域的布局组件
    /// </summary>
    private void SetupContentLayout()
    {
        // 确保有VerticalLayoutGroup组件
        VerticalLayoutGroup layoutGroup = contentRect.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup == null)
        {
            layoutGroup = contentRect.gameObject.AddComponent<VerticalLayoutGroup>();
        }
        
        // 配置VerticalLayoutGroup - 使用手动设置的参数
        layoutGroup.childAlignment = TextAnchor.UpperCenter;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.spacing = itemSpacing; // 使用手动设置的间距
        
        // 使用手动设置的内边距
        layoutGroup.padding = contentPadding;
        
        // 确保有ContentSizeFitter组件
        ContentSizeFitter sizeFitter = contentRect.GetComponent<ContentSizeFitter>();
        if (sizeFitter == null)
        {
            sizeFitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
        }
        
        // 配置ContentSizeFitter
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        Debug.Log("FileSystemBrowser: Content layout components set up successfully.");
    }
    
    /// <summary>
    /// 初始化布局设置
    /// </summary>
    private void InitializeLayoutSettings()
    {
        if (scrollView != null)
        {
            RectTransform scrollViewRect = scrollView.GetComponent<RectTransform>();
            if (scrollViewRect != null)
            {
                // 保存原始位置和大小
                originalScrollViewPosition = scrollViewRect.anchoredPosition;
                originalScrollViewSize = scrollViewRect.sizeDelta;
            }
            
            // 获取ScrollView的Image组件
            scrollViewImage = scrollView.GetComponent<Image>();
            if (scrollViewImage == null)
            {
                // 如果没有Image组件，尝试从子对象获取
                scrollViewImage = scrollView.GetComponentInChildren<Image>();
            }
        }
        
        layoutInitialized = true;
        Debug.Log("FileSystemBrowser: Layout settings initialized.");
    }
    
    /// <summary>
    /// 更新UI布局根据编辑状态
    /// </summary>
    private void UpdateUILayout()
    {
        if (!layoutInitialized || scrollView == null) return;
        
        RectTransform scrollViewRect = scrollView.GetComponent<RectTransform>();
        if (scrollViewRect == null) return;
        
        if (isEdit)
        {
            // 编辑模式：移动到右边边缘，并设置透明度为30%
            Canvas parentCanvas = scrollView.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();
                if (canvasRect != null)
                {
                    // 计算右边边缘位置
                    float canvasWidth = canvasRect.rect.width;
                    float scrollViewWidth = scrollViewRect.sizeDelta.x;
                    
                    // 将ScrollView移动到右边边缘
                    Vector2 rightEdgePosition = new Vector2(
                        99999, 
                        originalScrollViewPosition.y
                    );
                    
                    scrollViewRect.anchoredPosition = rightEdgePosition;
                }
                else
                {
                    // 如果无法获取Canvas，使用备用方法
                    Vector2 rightPosition = new Vector2(
                        originalScrollViewPosition.x + 200f, // 向右偏移200像素
                        originalScrollViewPosition.y
                    );
                    scrollViewRect.anchoredPosition = rightPosition;
                }
            }
            
            // 设置透明度为30%
            if (scrollViewImage != null)
            {
                Color color = scrollViewImage.color;
                color.a = 0.01f;
                scrollViewImage.color = color;
            }
        }
        else
        {
            // 非编辑模式：恢复到中间位置，完全不透明
            scrollViewRect.anchoredPosition = originalScrollViewPosition;
            scrollViewRect.sizeDelta = originalScrollViewSize;
            
            // 设置完全不透明
            if (scrollViewImage != null)
            {
                Color color = scrollViewImage.color;
                color.a = 1.0f;
                scrollViewImage.color = color;
            }
        }
    }
    
    /// <summary>
    /// 材质强度动画协程
    /// </summary>
    /// <returns></returns>
    private System.Collections.IEnumerator MaterialIntensityAnimation()
    {
        if (animationMaterial == null) yield break;
        
        // 动画参数
        float animationDuration = 0.3f; // 总动画时长
        float halfDuration = animationDuration / 2f; // 单向动画时长
        
        // 第一阶段：从1降到0
        float startTime = Time.time;
        float startIntensity = 1f;
        float targetIntensity = 0f;
        
        while (Time.time - startTime < halfDuration)
        {
            float elapsed = Time.time - startTime;
            float t = elapsed / halfDuration;
            
            // 使用平滑插值
            float currentIntensity = Mathf.Lerp(startIntensity, targetIntensity, t);
            animationMaterial.SetFloat("_Intensity", currentIntensity);
            
            yield return null;
        }
        
        // 确保完全到达目标值
        animationMaterial.SetFloat("_Intensity", 0f);
        
        yield return new WaitForSeconds(0.1f); // 等待0.1秒以确保视觉效果
        // 第二阶段：从0回到1
        startTime = Time.time;
        startIntensity = 0f;
        targetIntensity = 1f;
        
        while (Time.time - startTime < halfDuration)
        {
            float elapsed = Time.time - startTime;
            float t = elapsed / halfDuration;
            
            // 使用平滑插值
            float currentIntensity = Mathf.Lerp(startIntensity, targetIntensity, t);
            animationMaterial.SetFloat("_Intensity", currentIntensity);
            
            yield return null;
        }
        
        // 确保完全到达目标值
        animationMaterial.SetFloat("_Intensity", 1f);
        
        // 清空协程引用
        materialAnimationCoroutine = null;
        
        Debug.Log("FileSystemBrowser: Material intensity animation completed.");
    }
    
    /// <summary>
    /// 启动材质强度动画
    /// </summary>
    private void StartMaterialAnimation()
    {
        // 如果已有动画在运行，先停止它
        if (materialAnimationCoroutine != null)
        {
            StopCoroutine(materialAnimationCoroutine);
        }
        
        // 启动新的材质动画
        materialAnimationCoroutine = StartCoroutine(MaterialIntensityAnimation());
        
        Debug.Log("FileSystemBrowser: Material intensity animation started.");
    }
    
    /// <summary>
    /// 处理键盘输入
    /// </summary>
    private void HandleInput()
    {
        // 如果正在重命名，不处理任何按键（让输入框自己处理）
        if (isRenaming)
        {
            Debug.Log("is renameing");
            return;
        }
        
        // 检查是否在编辑状态
        if (isEdit)
        {
            // 在编辑状态下，ESC键退出编辑
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ExitEditMode();
                return;
            }
            // 在编辑状态下，其他按键不处理文件浏览器导航
            return;
        }
        
        // 处理文件操作快捷键（即使目录为空也要处理）
        // Ctrl+N - 创建新文件
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.N) && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
        {
            CreateNewFile();
            return;
        }
        // Ctrl+Shift+N - 创建新文件夹
        else if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.N))
        {
            CreateNewFolder();
            return;
        }
        
        // 如果没有项目，不处理其他导航和删除操作
        if (currentItems == null || currentItems.Count == 0) return;
        
        // 非编辑状态下的正常导航
        // 上下键/WS键选择
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            MovePrevious();
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            MoveNext();
        }
        // 回车/空格键 - 展开文件夹或选择脚本
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            SelectCurrentItem();
        }
        // ESC键 - 返回上一级
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            GoToParentDirectory();
        }
        // Ctrl+D - 删除文件/文件夹
        else if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.D))
        {
            DeleteCurrentItem();
        }
        // Ctrl+R - 重命名文件/文件夹
        else if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.R))
        {
            RenameCurrentItem();
        }
    }
    
    /// <summary>
    /// 向上移动选择
    /// </summary>
    private void MovePrevious()
    {
        if (currentItems == null || currentItems.Count == 0) return;
        
        selectedIndex = (selectedIndex - 1 + currentItems.Count) % currentItems.Count;
        UpdateSelection();
    }
    
    /// <summary>
    /// 向下移动选择
    /// </summary>
    private void MoveNext()
    {
        if (currentItems == null || currentItems.Count == 0) return;
        
        selectedIndex = (selectedIndex + 1) % currentItems.Count;
        UpdateSelection();
    }
    
    /// <summary>
    /// 选择当前项目
    /// </summary>
    private void SelectCurrentItem()
    {
        if (currentItems == null || selectedIndex < 0 || selectedIndex >= currentItems.Count) return;
        
        FileSystemItem selectedItem = currentItems[selectedIndex];
        
        if (selectedItem.isDirectory)
        {
            // 切换文件夹展开状态
            selectedItem.isExpanded = !selectedItem.isExpanded;
            
            // 重新构建显示列表
            RebuildDisplayList();
            
            Debug.Log($"文件夹 {selectedItem.name} {(selectedItem.isExpanded ? "展开" : "折叠")}");
        }
        else
        {
            // 选择脚本文件 - 进入编辑状态
            EnterEditModeWithFile(selectedItem.fullPath);
            Debug.Log($"脚本名字: {selectedItem.name} - 进入编辑状态");
        }
    }
    
    /// <summary>
    /// 获取创建新项目的目标目录
    /// </summary>
    /// <returns>目标目录路径</returns>
    private string GetTargetDirectoryForNewItem()
    {
        // 如果有选中的项目
        if (currentItems != null && selectedIndex >= 0 && selectedIndex < currentItems.Count)
        {
            FileSystemItem selectedItem = currentItems[selectedIndex];
            
            if (selectedItem.isDirectory)
            {
                // 如果选中的是文件夹，在该文件夹内创建
                return selectedItem.fullPath;
            }
            else
            {
                // 如果选中的是文件，在该文件所在的目录创建
                return Path.GetDirectoryName(selectedItem.fullPath);
            }
        }
        
        // 如果没有选中项目，在当前目录创建
        return currentPath;
    }
    
    /// <summary>
    /// 创建新文件
    /// </summary>
    private void CreateNewFile()
    {
        try
        {
            // 获取目标目录
            string targetDirectory = GetTargetDirectoryForNewItem();
            
            string newFileName = "NewFile" + fileExtension;
            string newFilePath = Path.Combine(targetDirectory, newFileName);
            
            // 确保文件名不重复
            int counter = 1;
            while (File.Exists(newFilePath))
            {
                newFileName = $"NewFile{counter}{fileExtension}";
                newFilePath = Path.Combine(targetDirectory, newFileName);
                counter++;
            }
            
            // 创建空文件
            File.WriteAllText(newFilePath, "");
            
            // 添加新文件到数据结构而不是刷新整个树
            AddNewItemToTree(newFilePath, false);
            
            Debug.Log($"FileSystemBrowser: Created new file: {newFilePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"FileSystemBrowser: Error creating new file: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 创建新文件夹
    /// </summary>
    private void CreateNewFolder()
    {
        try
        {
            // 获取目标目录
            string targetDirectory = GetTargetDirectoryForNewItem();
            
            string newFolderName = "NewFolder";
            string newFolderPath = Path.Combine(targetDirectory, newFolderName);
            
            // 确保文件夹名不重复
            int counter = 1;
            while (Directory.Exists(newFolderPath))
            {
                newFolderName = $"NewFolder{counter}";
                newFolderPath = Path.Combine(targetDirectory, newFolderName);
                counter++;
            }
            
            // 创建文件夹
            Directory.CreateDirectory(newFolderPath);
            
            // 添加新文件夹到数据结构而不是刷新整个树
            AddNewItemToTree(newFolderPath, true);
            
            Debug.Log($"FileSystemBrowser: Created new folder: {newFolderPath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"FileSystemBrowser: Error creating new folder: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 保存当前的展开状态和选中状态
    /// </summary>
    /// <returns>展开状态字典</returns>
    private Dictionary<string, bool> SaveExpandedState()
    {
        Dictionary<string, bool> expandedState = new Dictionary<string, bool>();
        
        foreach (var item in allItems)
        {
            if (item.isDirectory)
            {
                expandedState[item.fullPath] = item.isExpanded;
            }
        }
        
        return expandedState;
    }
    
    /// <summary>
    /// 恢复展开状态
    /// </summary>
    /// <param name="expandedState">保存的展开状态</param>
    private void RestoreExpandedState(Dictionary<string, bool> expandedState)
    {
        foreach (var item in allItems)
        {
            if (item.isDirectory && expandedState.ContainsKey(item.fullPath))
            {
                item.isExpanded = expandedState[item.fullPath];
            }
        }
    }
    
    /// <summary>
    /// 添加新项目到树结构（使用智能刷新保持状态）
    /// </summary>
    /// <param name="itemPath">新项目的路径</param>
    /// <param name="isDirectory">是否为目录</param>
    private void AddNewItemToTree(string itemPath, bool isDirectory)
    {
        try
        {
            // 保存当前的展开状态
            Dictionary<string, bool> expandedState = SaveExpandedState();
            
            // 确保父目录展开（如果新项目要可见）
            string parentPath = Path.GetDirectoryName(itemPath);
            if (!string.IsNullOrEmpty(parentPath) && parentPath != currentPath)
            {
                expandedState[parentPath] = true;
            }
            
            // 重新扫描整个目录结构
            ScanDirectory(currentPath);
            
            // 恢复展开状态
            RestoreExpandedState(expandedState);
            
            // 重建显示列表
            RebuildDisplayList();
            
            // 选择新创建的项目
            SelectItemByPath(itemPath);
            
            // 找到新创建的项目并开始重命名
            FileSystemItem newItem = allItems.FirstOrDefault(item => item.fullPath == itemPath);
            if (newItem != null)
            {
                StartCoroutine(StartRenamingDelayed(newItem));
            }
            
            Debug.Log($"FileSystemBrowser: Added new item with smart refresh: {itemPath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"FileSystemBrowser: Error adding new item to tree: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 判断新项目是否应该显示（父目录是否展开）
    /// </summary>
    /// <param name="parentPath">父目录路径</param>
    /// <returns>是否应该显示</returns>
    private bool ShouldShowNewItem(string parentPath)
    {
        // 如果父目录就是根目录，总是显示
        if (parentPath == currentPath)
            return true;
            
        // 查找父目录项目
        FileSystemItem parentItem = allItems.FirstOrDefault(item => 
            item.isDirectory && item.fullPath == parentPath);
            
        if (parentItem == null)
            return false;
            
        // 递归检查所有父级是否都展开
        return parentItem.isExpanded && ShouldShowItem(parentItem);
    }
    
    /// <summary>
    /// 根据路径选择项目
    /// </summary>
    /// <param name="itemPath">项目路径</param>
    private void SelectItemByPath(string itemPath)
    {
        for (int i = 0; i < currentItems.Count; i++)
        {
            if (currentItems[i].fullPath == itemPath)
            {
                selectedIndex = i;
                UpdateSelection();
                break;
            }
        }
    }

    /// <summary>
    /// 删除当前选中的项目
    /// </summary>
    private void DeleteCurrentItem()
    {
        if (currentItems == null || selectedIndex < 0 || selectedIndex >= currentItems.Count) return;
        
        FileSystemItem selectedItem = currentItems[selectedIndex];
        
        if(selectedItem.name == "Typethon")return; // 不允许删除Typethon目录
        try
        {
            if (selectedItem.isDirectory)
            {
                // 删除文件夹（包括所有内容）
                Directory.Delete(selectedItem.fullPath, true);
                Debug.Log($"FileSystemBrowser: Deleted folder: {selectedItem.fullPath}");
            }
            else
            {
                // 删除文件
                File.Delete(selectedItem.fullPath);
                Debug.Log($"FileSystemBrowser: Deleted file: {selectedItem.fullPath}");
            }
            
            // 从数据结构中移除项目而不是刷新整个树
            RemoveItemFromTree(selectedItem);
            
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"FileSystemBrowser: Error deleting {(selectedItem.isDirectory ? "folder" : "file")} {selectedItem.fullPath}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 从树结构中移除项目（使用智能刷新保持状态）
    /// </summary>
    /// <param name="itemToRemove">要移除的项目</param>
    private void RemoveItemFromTree(FileSystemItem itemToRemove)
    {
        try
        {
            // 保存当前的展开状态和选中项目路径
            Dictionary<string, bool> expandedState = SaveExpandedState();
            string currentSelectedPath = (currentItems != null && selectedIndex >= 0 && selectedIndex < currentItems.Count) 
                ? currentItems[selectedIndex].fullPath : null;
            
            // 重新扫描整个目录结构
            ScanDirectory(currentPath);
            
            // 恢复展开状态
            RestoreExpandedState(expandedState);
            
            // 重建显示列表
            RebuildDisplayList();
            
            // 尝试恢复选中项目，如果被删除的项目是选中项目，则选择邻近项目
            if (!string.IsNullOrEmpty(currentSelectedPath) && currentSelectedPath != itemToRemove.fullPath)
            {
                SelectItemByPath(currentSelectedPath);
            }
            else
            {
                // 如果删除的是选中项目，选择下一个或上一个项目
                if (selectedIndex >= currentItems.Count && currentItems.Count > 0)
                {
                    selectedIndex = currentItems.Count - 1;
                }
                else if (currentItems.Count == 0)
                {
                    selectedIndex = 0;
                }
                UpdateSelection();
            }
            
            Debug.Log($"FileSystemBrowser: Removed item with smart refresh: {itemToRemove.fullPath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"FileSystemBrowser: Error removing item from tree: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 重命名当前选中的项目
    /// </summary>
    private void RenameCurrentItem()
    {
        if (currentItems == null || selectedIndex < 0 || selectedIndex >= currentItems.Count) return;
        
        FileSystemItem selectedItem = currentItems[selectedIndex];
        if(selectedItem.name == "Typethon")return; // 不允许重命名Typethon目录
        try
        {
            // 使用延迟重命名，确保UI状态正确
            StartCoroutine(StartRenamingDelayed(selectedItem));
            Debug.Log($"FileSystemBrowser: Started renaming {(selectedItem.isDirectory ? "folder" : "file")}: {selectedItem.name}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"FileSystemBrowser: Error starting rename for {selectedItem.name}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// <summary>
    /// 延迟开始重命名的协程
    /// </summary>
    /// <param name="item">要重命名的项目</param>
    /// <returns></returns>
    private System.Collections.IEnumerator StartRenamingDelayed(FileSystemItem item)
    {
        // 等待几帧确保UI完全构建
        yield return null;
        yield return null;
        
        // 确保项目仍然存在且UI对象有效
        if (item != null && item.uiObject != null && currentItems.Contains(item))
        {
            StartRenaming(item);
        }
    }
    
    /// <summary>
    /// Input Field编辑结束事件处理
    /// </summary>
    /// <param name="newValue">新的输入值</param>
    /// <param name="item">对应的文件系统项目</param>
    private void OnInputFieldEndEdit(string newValue, FileSystemItem item)
    {
        Debug.Log($"FileSystemBrowser: OnInputFieldEndEdit called - isRenaming: {isRenaming}, newValue: '{newValue}', item: '{item.name}'");
        
        if (!isRenaming) 
        {
            Debug.Log("FileSystemBrowser: Not in renaming mode, ignoring input field end edit");
            return;
        }
        
        // 提取纯文件名（去除前缀）
        string prefix = new string(' ', (item.indentLevel + 1) * 2);
        if (item.isDirectory)
        {
            prefix += item.isExpanded ? "▼ " : "> ";
        }
        
        string newName = newValue;
        if (newValue.StartsWith(prefix))
        {
            newName = newValue.Substring(prefix.Length);
        }
        
        // 如果名称没有改变或为空，取消重命名
        if (string.IsNullOrEmpty(newName) || newName == originalItemName)
        {
            CancelRenaming();
            return;
        }
        
        // 执行重命名操作
        PerformRename(item, newName);
    }
    
    /// <summary>
    /// 执行重命名操作
    /// </summary>
    /// <param name="item">要重命名的项目</param>
    /// <param name="newName">新名称</param>
    private void PerformRename(FileSystemItem item, string newName)
    {
        try
        {
            // 如果是文件且没有.py后缀，自动添加
            if (!item.isDirectory && !newName.EndsWith(fileExtension))
            {
                newName += fileExtension;
                Debug.Log($"FileSystemBrowser: Auto-added extension: {newName}");
            }
            
            string directoryPath = Path.GetDirectoryName(item.fullPath);
            string newPath = Path.Combine(directoryPath, newName);
            
            // 检查新名称是否已存在
            if ((item.isDirectory && Directory.Exists(newPath)) || (!item.isDirectory && File.Exists(newPath)))
            {
                Debug.LogWarning($"FileSystemBrowser: Name '{newName}' already exists.");
                CancelRenaming();
                return;
            }
            
            // 执行重命名
            if (item.isDirectory)
            {
                Directory.Move(item.fullPath, newPath);
            }
            else
            {
                File.Move(item.fullPath, newPath);
            }
            
            Debug.Log($"FileSystemBrowser: Renamed '{originalItemName}' to '{newName}'");
            
            // 更新项目信息而不是刷新整个树
            UpdateItemAfterRename(item, newPath, newName);
            
            // 完成重命名
            FinishRenaming();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"FileSystemBrowser: Error renaming item: {ex.Message}");
            CancelRenaming();
        }
    }
    
    /// <summary>
    /// 更新重命名后的项目信息（使用智能刷新保持状态）
    /// </summary>
    /// <param name="item">被重命名的项目</param>
    /// <param name="newPath">新路径</param>
    /// <param name="newName">新名称</param>
    private void UpdateItemAfterRename(FileSystemItem item, string newPath, string newName)
    {
        try
        {
            // 保存当前的展开状态
            Dictionary<string, bool> expandedState = SaveExpandedState();
            
            // 如果重命名的是文件夹，需要更新展开状态中的路径映射
            if (item.isDirectory && expandedState.ContainsKey(originalItemPath))
            {
                bool wasExpanded = expandedState[originalItemPath];
                expandedState.Remove(originalItemPath);
                expandedState[newPath] = wasExpanded;
            }
            
            // 重新扫描整个目录结构
            ScanDirectory(currentPath);
            
            // 恢复展开状态
            RestoreExpandedState(expandedState);
            
            // 重建显示列表
            RebuildDisplayList();
            
            // 选择重命名后的项目
            SelectItemByPath(newPath);
            
            Debug.Log($"FileSystemBrowser: Updated item after rename with smart refresh: {newName}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"FileSystemBrowser: Error updating item after rename: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 开始重命名模式
    /// </summary>
    /// <param name="item">要重命名的项目</param>
    private void StartRenaming(FileSystemItem item)
    {
        if (item.uiObject == null) return;
        
        Transform inputTransform = item.uiObject.transform.GetChild(0);
        TMP_InputField inputField = inputTransform.GetComponent<TMP_InputField>();
        
        if (inputField == null) return;
        
        isRenaming = true;
        currentRenamingInputField = inputField;
        originalItemName = item.name;
        originalItemPath = item.fullPath;
        
        // 添加事件监听器
        inputField.onEndEdit.RemoveAllListeners();
        inputField.onEndEdit.AddListener((value) => OnInputFieldEndEdit(value, item));
        
        // 设置为可编辑
        inputField.readOnly = false;
        
        // 选中文件名部分（不包括前缀）
        string prefix = new string(' ', (item.indentLevel + 1) * 2);
        if (item.isDirectory)
        {
            prefix += item.isExpanded ? "▼ " : "> ";
        }
        
        // 激活输入框并选中文件名部分
        inputField.ActivateInputField();
        
        // 延迟选择文本，确保输入框已激活
        StartCoroutine(SelectTextAfterFrame(inputField, prefix.Length, inputField.text.Length));
        
        Debug.Log($"FileSystemBrowser: Started renaming '{item.name}'");
    }
    
    /// <summary>
    /// 延迟选择文本的协程
    /// </summary>
    private System.Collections.IEnumerator SelectTextAfterFrame(TMP_InputField inputField, int startPos, int endPos)
    {
        yield return null; // 等待一帧
        
        if (inputField != null && isRenaming)
        {
            inputField.selectionAnchorPosition = startPos;
            inputField.selectionFocusPosition = endPos;
        }
    }
    
    /// <summary>
    /// 取消重命名
    /// </summary>
    private void CancelRenaming()
    {
        if (!isRenaming || currentRenamingInputField == null) return;
        
        // 恢复原始文本
        FileSystemItem item = GetItemByPath(originalItemPath);
        if (item != null)
        {
            string prefix = new string(' ', (item.indentLevel + 1) * 2);
            if (item.isDirectory)
            {
                prefix += item.isExpanded ? "▼ " : "> ";
            }
            currentRenamingInputField.text = prefix + originalItemName;
        }
        
        FinishRenaming();
        
        Debug.Log("FileSystemBrowser: Renaming cancelled");
    }

    /// <summary>
    /// 完成重命名
    /// </summary>
    private void FinishRenaming()
    {
        if (currentRenamingInputField != null)
        {
            // 移除事件监听器
            currentRenamingInputField.onEndEdit.RemoveAllListeners();
            currentRenamingInputField.readOnly = true;
            // currentRenamingInputField.DeactivateInputField();
        }

        currentRenamingInputField = null;
        originalItemName = "";
        originalItemPath = "";
        StartCoroutine(delayStopRenaming());
    }

    private IEnumerator delayStopRenaming()
    {
        yield return new WaitForSeconds(0.1f);
        
        Debug.Log("Finish Renaming: 清理重命名状态");
        isRenaming = false;
    }
    
    /// <summary>
    /// 根据路径获取文件系统项目
    /// </summary>
    private FileSystemItem GetItemByPath(string path)
    {
        return currentItems.FirstOrDefault(item => item.fullPath == path);
    }
    
    /// <summary>
    /// 退出编辑模式
    /// </summary>
    private void ExitEditMode()
    {
        // 保存文件
        SaveCurrentFile();
        
        // 停止自动保存协程
        if (autoSaveCoroutine != null)
        {
            StopCoroutine(autoSaveCoroutine);
            autoSaveCoroutine = null;
        }
        
        // 清空编辑器内容
        if (textEditor != null)
        {
            textEditor.text = "";
            // textEditor.gameObject.SetActive(false);
        }
        
        // 重置状态
        isEdit = false;
        currentEditingFilePath = "";
        
        // 启动材质动画
        StartMaterialAnimation();
        
        // 立即更新UI布局
        UpdateUILayout();
        
        Debug.Log("退出编辑状态并保存文件");
    }
    
    /// <summary>
    /// 进入编辑模式并加载文件
    /// </summary>
    /// <param name="filePath">要编辑的文件路径</param>
    private void EnterEditModeWithFile(string filePath)
    {
        if (textEditor == null)
        {
            Debug.LogError("FileSystemBrowser: TextEditor component not assigned!");
            return;
        }
        
        try
        {
            // 读取文件内容
            string fileContent = File.ReadAllText(filePath);
            
            // 设置到编辑器
            textEditor.text = fileContent;
            textEditor.gameObject.SetActive(true);
            
            // 激活输入框
            textEditor.ActivateInputField();
            
            // 设置编辑状态
            isEdit = true;
            currentEditingFilePath = filePath;
            
            // 启动材质动画
            StartMaterialAnimation();
            
            // 立即更新UI布局
            UpdateUILayout();
            
            // 开始自动保存协程
            if (autoSaveCoroutine != null)
            {
                StopCoroutine(autoSaveCoroutine);
            }
            autoSaveCoroutine = StartCoroutine(AutoSaveCoroutine());
            
            Debug.Log($"加载文件进入编辑状态: {filePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"FileSystemBrowser: Error loading file {filePath}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 保存当前编辑的文件
    /// </summary>
    private void SaveCurrentFile()
    {
        if (string.IsNullOrEmpty(currentEditingFilePath) || textEditor == null)
        {
            return;
        }
        
        try
        {
            File.WriteAllText(currentEditingFilePath, textEditor.text);
            Debug.Log($"文件已保存: {currentEditingFilePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"FileSystemBrowser: Error saving file {currentEditingFilePath}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 自动保存协程
    /// </summary>
    /// <returns></returns>
    private System.Collections.IEnumerator AutoSaveCoroutine()
    {
        while (isEdit)
        {
            yield return new WaitForSeconds(10f); // 等待10秒
            
            if (isEdit && !string.IsNullOrEmpty(currentEditingFilePath))
            {
                SaveCurrentFile();
                Debug.Log("自动保存执行");
            }
        }
    }
    
    /// <summary>
    /// 获取当前是否处于编辑状态
    /// </summary>
    /// <returns>是否在编辑状态</returns>
    public bool IsInEditMode()
    {
        return isEdit;
    }
    
    /// <summary>
    /// 获取当前编辑的文件路径
    /// </summary>
    /// <returns>当前编辑的文件路径</returns>
    public string GetCurrentEditingFilePath()
    {
        return currentEditingFilePath;
    }
    
    /// <summary>
    /// 公共方法：进入编辑模式
    /// </summary>
    public void EnterEditMode()
    {
        isEdit = true;
        
        // 启动材质动画
        StartMaterialAnimation();
        
        // 立即更新UI布局
        UpdateUILayout();
        
        Debug.Log("外部调用：进入编辑状态");
    }
    
    /// <summary>
    /// 公共方法：进入编辑模式并加载指定文件
    /// </summary>
    /// <param name="filePath">要编辑的文件路径</param>
    public void EnterEditModeWithFilePublic(string filePath)
    {
        if (File.Exists(filePath))
        {
            EnterEditModeWithFile(filePath);
        }
        else
        {
            Debug.LogWarning($"FileSystemBrowser: File does not exist: {filePath}");
        }
    }
    
    /// <summary>
    /// 公共方法：退出编辑模式
    /// </summary>
    public void ExitEditModePublic()
    {
        ExitEditMode();
    }
    
    /// <summary>
    /// 公共方法：手动保存当前文件
    /// </summary>
    public void SaveCurrentFilePublic()
    {
        SaveCurrentFile();
    }
    
    /// <summary>
    /// 进入指定目录
    /// </summary>
    /// <param name="directoryPath">目录路径</param>
    private void EnterDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Debug.LogWarning($"FileSystemBrowser: Directory does not exist: {directoryPath}");
            return;
        }
        
        // 检查目标目录是否在根目录范围内
        if (!IsPathWithinRoot(directoryPath))
        {
            Debug.LogWarning($"FileSystemBrowser: Cannot navigate outside root directory. Target: {directoryPath}, Root: {rootPath}");
            return;
        }
        
        // 保存当前路径到历史记录
        pathHistory.Push(currentPath);
        
        // 切换到新目录
        currentPath = directoryPath;
        selectedIndex = 0;
        RefreshCurrentDirectory();
        
        Debug.Log($"FileSystemBrowser: Entered directory: {directoryPath}");
    }
    
    /// <summary>
    /// 返回上一级目录
    /// </summary>
    private void GoToParentDirectory()
    {
        // 检查是否已经在根目录，如果是则不允许继续向上
        if (IsAtRootDirectory())
        {
            Debug.Log("FileSystemBrowser: Already at root directory, cannot go higher.");
            return;
        }
        
        if (pathHistory.Count > 0)
        {
            // 从历史记录恢复上一级路径
            string previousPath = pathHistory.Pop();
            
            // 确保不会超出根目录
            if (IsPathWithinRoot(previousPath))
            {
                currentPath = previousPath;
            }
            else
            {
                // 如果历史路径超出根目录，则返回到根目录
                currentPath = rootPath;
                pathHistory.Clear();
                Debug.LogWarning($"FileSystemBrowser: Previous path was outside root, returning to root: {rootPath}");
            }
        }
        else
        {
            // 如果没有历史记录，尝试获取父目录
            DirectoryInfo parentDir = Directory.GetParent(currentPath);
            if (parentDir != null && parentDir.Exists)
            {
                string parentPath = parentDir.FullName;
                
                // 确保父目录不会超出根目录
                if (IsPathWithinRoot(parentPath))
                {
                    currentPath = parentPath;
                }
                else
                {
                    Debug.Log("FileSystemBrowser: Cannot go above root directory.");
                    return;
                }
            }
            else
            {
                Debug.Log("FileSystemBrowser: Already at root directory.");
                return;
            }
        }
        
        selectedIndex = 0;
        RefreshCurrentDirectory();
        
        Debug.Log($"FileSystemBrowser: Returned to directory: {currentPath}");
    }
    
    /// <summary>
    /// 检查当前是否在根目录
    /// </summary>
    /// <returns>是否在根目录</returns>
    private bool IsAtRootDirectory()
    {
        return string.Equals(Path.GetFullPath(currentPath), Path.GetFullPath(rootPath), 
                           System.StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// 检查指定路径是否在根目录范围内
    /// </summary>
    /// <param name="path">要检查的路径</param>
    /// <returns>是否在根目录范围内</returns>
    private bool IsPathWithinRoot(string path)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(rootPath))
            return false;
            
        try
        {
            string fullPath = Path.GetFullPath(path);
            string fullRootPath = Path.GetFullPath(rootPath);
            
            // 检查路径是否以根路径开始（即在根目录下或就是根目录）
            return fullPath.StartsWith(fullRootPath, System.StringComparison.OrdinalIgnoreCase);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"FileSystemBrowser: Error checking path bounds: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 刷新当前目录内容
    /// </summary>
    private void RefreshCurrentDirectory()
    {
        // 扫描当前目录（这会清理并重建allItems和currentItems）
        ScanDirectory(currentPath);
        
        Debug.Log($"FileSystemBrowser: Refreshed directory: {currentPath} ({currentItems.Count} visible items, {allItems.Count} total items)");
    }
    
    /// <summary>
    /// 扫描指定目录（递归构建层级结构）
    /// </summary>
    /// <param name="directoryPath">目录路径</param>
    private void ScanDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Debug.LogWarning($"FileSystemBrowser: Directory does not exist: {directoryPath}");
            return;
        }
        
        // 清空所有项目列表
        allItems.Clear();
        
        try
        {
            // 递归扫描目录结构
            ScanDirectoryRecursive(directoryPath, 0);
            
            // 构建当前显示列表
            RebuildDisplayList();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"FileSystemBrowser: Error scanning directory {directoryPath}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 递归扫描目录
    /// </summary>
    /// <param name="directoryPath">目录路径</param>
    /// <param name="indentLevel">缩进级别</param>
    private void ScanDirectoryRecursive(string directoryPath, int indentLevel)
    {
        if (indentLevel > maxDepth) return;
        
        try
        {
            // 获取所有子文件夹
            string[] directories = Directory.GetDirectories(directoryPath);
            List<FileSystemItem> folderItems = new List<FileSystemItem>();
            
            foreach (string dir in directories)
            {
                DirectoryInfo dirInfo = new DirectoryInfo(dir);
                if (!dirInfo.Attributes.HasFlag(FileAttributes.Hidden))
                {
                    FileSystemItem folderItem = new FileSystemItem(dirInfo.Name, dir, true, indentLevel);
                    folderItems.Add(folderItem);
                }
            }
            
            // 获取所有指定扩展名的文件
            string[] files = Directory.GetFiles(directoryPath, "*" + fileExtension);
            List<FileSystemItem> fileItems = new List<FileSystemItem>();
            
            foreach (string file in files)
            {
                FileInfo fileInfo = new FileInfo(file);
                if (!fileInfo.Attributes.HasFlag(FileAttributes.Hidden))
                {
                    FileSystemItem fileItem = new FileSystemItem(fileInfo.Name, file, false, indentLevel);
                    fileItems.Add(fileItem);
                }
            }
            
            // 对当前目录的文件夹和文件进行排序
            folderItems.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
            fileItems.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
            
            // 先添加所有文件夹到allItems
            foreach (var folderItem in folderItems)
            {
                allItems.Add(folderItem);
                // 递归扫描子文件夹内容
                ScanDirectoryRecursive(folderItem.fullPath, indentLevel + 1);
            }
            
            // 再添加所有文件到allItems
            foreach (var fileItem in fileItems)
            {
                allItems.Add(fileItem);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"FileSystemBrowser: Error scanning directory {directoryPath}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 根据展开状态重建显示列表
    /// </summary>
    private void RebuildDisplayList()
    {
        currentItems.Clear();
        
        foreach (var item in allItems)
        {
            // 检查是否应该显示此项目
            if (ShouldShowItem(item))
            {
                currentItems.Add(item);
            }
        }
        
        // 重新创建UI
        CreateUIItems();
        UpdateSelection();
    }
    
    /// <summary>
    /// 判断项目是否应该显示（考虑父文件夹的展开状态）
    /// </summary>
    /// <param name="item">要检查的项目</param>
    /// <returns>是否应该显示</returns>
    private bool ShouldShowItem(FileSystemItem item)
    {
        // 根级别的项目总是显示
        if (item.indentLevel == 0) return true;
        
        // 查找父文件夹
        string parentPath = Path.GetDirectoryName(item.fullPath);
        FileSystemItem parentFolder = allItems.FirstOrDefault(parent => 
            parent.isDirectory && 
            parent.fullPath == parentPath &&
            parent.indentLevel == item.indentLevel - 1);
        
        // 如果找不到父文件夹或父文件夹未展开，则不显示
        if (parentFolder == null || !parentFolder.isExpanded)
        {
            return false;
        }
        
        // 递归检查所有父级文件夹
        return ShouldShowItem(parentFolder);
    }
    
    /// <summary>
    /// 创建UI项目
    /// </summary>
    private void CreateUIItems()
    {
        if (contentRect == null) return;
        
        // 清理旧的UI对象
        ClearCurrentItems();
        
        foreach (FileSystemItem item in currentItems)
        {
            GameObject prefab = item.isDirectory ? folderPrefab : scriptPrefab;
            GameObject uiObject = Instantiate(prefab, contentRect);
            
            // 确保每个UI对象都有LayoutElement组件
            SetupItemLayout(uiObject, item.isDirectory);
            
            // 设置缩进（通过调整LayoutGroup的padding或直接调整位置）
            SetupItemIndent(uiObject, item.indentLevel);
            
            // 设置标题文本（确保使用TextMeshPro Input Field）
            if (uiObject.transform.childCount > 0)
            {
                Transform titleTransform = uiObject.transform.GetChild(0);
                TMP_InputField titleInputField = titleTransform.GetComponent<TMP_InputField>();
                
                if (titleInputField != null)
                {
                    // 添加缩进前缀和文件夹状态指示
                    string prefix = new string(' ', (item.indentLevel + 1) * 2);
                    if (item.isDirectory)
                    {
                        prefix += item.isExpanded ? "▼ " : "> ";
                    }
                    titleInputField.text = prefix + item.name;
                    
                    // 设置为只读模式（防止意外编辑）
                    titleInputField.readOnly = true;
                    
                    // 清除任何现有的事件监听器
                    titleInputField.onEndEdit.RemoveAllListeners();
                }
                else
                {
                    Debug.LogError($"FileSystemBrowser: Child(0) of prefab must have TMP_InputField component! Item: {item.name}");
                }
            }
            
            // 保存UI对象引用
            item.uiObject = uiObject;
            itemObjects.Add(uiObject);
        }
    }
    
    /// <summary>
    /// 设置项目的缩进
    /// </summary>
    /// <param name="itemObject">项目GameObject</param>
    /// <param name="indentLevel">缩进级别</param>
    private void SetupItemIndent(GameObject itemObject, int indentLevel)
    {
        // 通过HorizontalLayoutGroup添加左边距来实现缩进
        HorizontalLayoutGroup horizontalLayout = itemObject.GetComponent<HorizontalLayoutGroup>();
        if (horizontalLayout == null)
        {
            horizontalLayout = itemObject.AddComponent<HorizontalLayoutGroup>();
        }
        
        // 设置左边距来实现缩进效果
        int indentPixels = indentLevel * 20; // 每级缩进20像素
        horizontalLayout.padding = new RectOffset(indentPixels, 0, 0, 0);
        horizontalLayout.childControlWidth = false;
        horizontalLayout.childControlHeight = false;
        horizontalLayout.childForceExpandWidth = true;
        horizontalLayout.childForceExpandHeight = true;
    }
    
    /// <summary>
    /// 设置单个项目的布局属性
    /// </summary>
    /// <param name="itemObject">项目GameObject</param>
    /// <param name="isDirectory">是否为文件夹</param>
    private void SetupItemLayout(GameObject itemObject, bool isDirectory)
    {
        // 确保有LayoutElement组件
        LayoutElement layoutElement = itemObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = itemObject.AddComponent<LayoutElement>();
        }
        
        // 根据类型设置不同的高度
        float itemHeight = isDirectory ? folderHeight : fileHeight;
        
        // 配置LayoutElement
        layoutElement.minHeight = itemHeight; // 最小高度
        layoutElement.preferredHeight = itemHeight; // 首选高度
        layoutElement.flexibleHeight = 0f; // 不允许灵活高度
        layoutElement.flexibleWidth = 1f; // 允许宽度填充
        
        // 确保RectTransform设置正确
        RectTransform rectTransform = itemObject.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            // 设置锚点为拉伸填充宽度
            rectTransform.anchorMin = new Vector2(0, 0.5f);
            rectTransform.anchorMax = new Vector2(1, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            
            // 重置位置和大小
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            // 设置具体的尺寸
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, itemHeight);
        }
    }
    
    /// <summary>
    /// 更新选择状态
    /// </summary>
    private void UpdateSelection()
    {
        if (currentItems == null) return;
        
        for (int i = 0; i < currentItems.Count; i++)
        {
            if (currentItems[i].uiObject != null)
            {
                // 获取第一个子对象的TMP_InputField组件来改变文字颜色
                if (currentItems[i].uiObject.transform.childCount > 0)
                {
                    Transform inputTransform = currentItems[i].uiObject.transform.GetChild(0);
                    TMP_InputField inputComponent = inputTransform.GetComponent<TMP_InputField>();
                    
                    if (inputComponent != null)
                    {
                        // 改变文字颜色来表示选择状态
                        inputComponent.textComponent.color = (i == selectedIndex) ? selectedColor : normalColor;
                    }
                    else
                    {
                        Debug.LogWarning($"FileSystemBrowser: Child(0) of {currentItems[i].name} does not have TMP_InputField component!");
                    }
                }
            }
        }
        
        // 确保选中的项目在ScrollView中可见
        ScrollToSelectedItem();
    }
    
    /// <summary>
    /// 滚动到选中的项目
    /// </summary>
    private void ScrollToSelectedItem()
    {
        if (scrollView == null || currentItems == null || selectedIndex < 0 || selectedIndex >= currentItems.Count) return;
        
        FileSystemItem selectedItem = currentItems[selectedIndex];
        if (selectedItem.uiObject == null) return;
        
        // 等待一帧让布局更新
        StartCoroutine(ScrollToSelectedItemDelayed());
    }
    
    /// <summary>
    /// 延迟滚动到选中项目（等待布局更新）
    /// </summary>
    private System.Collections.IEnumerator ScrollToSelectedItemDelayed()
    {
        yield return null; // 等待一帧
        
        if (currentItems == null || selectedIndex < 0 || selectedIndex >= currentItems.Count) yield break;
        
        FileSystemItem selectedItem = currentItems[selectedIndex];
        if (selectedItem.uiObject == null) yield break;
        
        RectTransform selectedRect = selectedItem.uiObject.GetComponent<RectTransform>();
        if (selectedRect == null) yield break;
        
        // 强制重建布局
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        
        // 计算选中项目的世界位置
        Vector3[] corners = new Vector3[4];
        selectedRect.GetWorldCorners(corners);
        Vector3 itemWorldPos = corners[0]; // 左下角
        
        // 获取viewport的世界位置
        Vector3[] viewportCorners = new Vector3[4];
        scrollView.viewport.GetWorldCorners(viewportCorners);
        Vector3 viewportWorldPos = viewportCorners[0]; // 左下角
        
        // 计算项目在content中的相对位置
        Vector2 localItemPos = contentRect.InverseTransformPoint(itemWorldPos);
        Vector2 localViewportPos = contentRect.InverseTransformPoint(viewportWorldPos);
        
        // 计算需要滚动的距离
        float contentHeight = contentRect.sizeDelta.y;
        float viewportHeight = scrollView.viewport.rect.height;
        
        if (contentHeight > viewportHeight)
        {
            // 计算项目在content中的normalized位置
            float itemY = -localItemPos.y;
            float normalizedY = itemY / (contentHeight - viewportHeight);
            
            // 限制在有效范围内
            normalizedY = Mathf.Clamp01(normalizedY);
            
            // 设置滚动位置
            scrollView.verticalNormalizedPosition = 1f - normalizedY;
        }
    }
    
    /// <summary>
    /// 清理当前UI项目
    /// </summary>
    private void ClearCurrentItems()
    {
        // 销毁UI对象
        if (itemObjects != null)
        {
            foreach (GameObject obj in itemObjects)
            {
                if (obj != null)
                {
                    DestroyImmediate(obj);
                }
            }
            itemObjects.Clear();
        }
        
        // 清理UI对象引用
        if (currentItems != null)
        {
            foreach (var item in currentItems)
            {
                item.uiObject = null;
            }
        }
    }

    /// <summary>
    /// 设置根路径
    /// </summary>
    /// <param name="newRootPath">新的根路径</param>
    public void SetRootPath(string newRootPath)
    {
        if (!Directory.Exists(newRootPath))
        {
            Debug.LogWarning($"FileSystemBrowser: Invalid root path: {newRootPath}");
            return;
        }

        // 更新根路径
        rootPath = newRootPath;

        // 检查当前路径是否仍在新的根路径范围内
        if (!IsPathWithinRoot(currentPath))
        {
            // 如果当前路径超出新的根路径，则重置到根路径
            currentPath = rootPath;
            Debug.Log($"FileSystemBrowser: Current path was outside new root, reset to root: {rootPath}");
        }

        isEdit = false; // 重置编辑状态

        pathHistory.Clear();
        selectedIndex = 0;
        RefreshCurrentDirectory();
    }
    
    /// <summary>
    /// 设置文件扩展名过滤
    /// </summary>
    /// <param name="extension">文件扩展名（包含点号，如".py"）</param>
    public void SetFileExtension(string extension)
    {
        fileExtension = extension;
        RefreshCurrentDirectory();
    }
    
    /// <summary>
    /// 获取当前路径
    /// </summary>
    /// <returns>当前路径</returns>
    public string GetCurrentPath()
    {
        return currentPath;
    }
    
    /// <summary>
    /// 获取当前选择的项目
    /// </summary>
    /// <returns>当前选择的文件系统项目</returns>
    public FileSystemItem GetSelectedItem()
    {
        if (currentItems == null || selectedIndex < 0 || selectedIndex >= currentItems.Count)
        {
            return null;
        }
        return currentItems[selectedIndex];
    }
    
    /// <summary>
    /// 获取当前目录的项目数量
    /// </summary>
    /// <returns>项目数量</returns>
    public int GetItemCount()
    {
        return currentItems?.Count ?? 0;
    }
    
    /// <summary>
    /// 直接跳转到指定路径
    /// </summary>
    /// <param name="targetPath">目标路径</param>
    public void NavigateToPath(string targetPath)
    {
        if (!Directory.Exists(targetPath))
        {
            Debug.LogWarning($"FileSystemBrowser: Cannot navigate to invalid path: {targetPath}");
            return;
        }
        
        // 检查目标路径是否在根目录范围内
        if (!IsPathWithinRoot(targetPath))
        {
            Debug.LogWarning($"FileSystemBrowser: Cannot navigate outside root directory. Target: {targetPath}, Root: {rootPath}");
            return;
        }
        
        // 保存当前路径到历史记录
        pathHistory.Push(currentPath);
        
        currentPath = targetPath;
        selectedIndex = 0;
        RefreshCurrentDirectory();
        
        Debug.Log($"FileSystemBrowser: Navigated to: {targetPath}");
    }
    
    /// <summary>
    /// 刷新当前视图
    /// </summary>
    public void Refresh()
    {
        RefreshCurrentDirectory();
    }
    
    /// <summary>
    /// 重置到根目录
    /// </summary>
    public void ResetToRoot()
    {
        currentPath = rootPath;
        pathHistory.Clear();
        selectedIndex = 0;
        RefreshCurrentDirectory();
        
        Debug.Log($"FileSystemBrowser: Reset to root directory: {rootPath}");
    }
    
    /// <summary>
    /// 设置项目间距
    /// </summary>
    /// <param name="spacing">间距值</param>
    public void SetItemSpacing(float spacing)
    {
        itemSpacing = spacing;
        SetupContentLayout();
        
        Debug.Log($"FileSystemBrowser: Item spacing set to {spacing}");
    }
    
    /// <summary>
    /// 设置文件夹高度
    /// </summary>
    /// <param name="height">文件夹高度</param>
    public void SetFolderHeight(float height)
    {
        folderHeight = height;
        RefreshCurrentDirectory();
        
        Debug.Log($"FileSystemBrowser: Folder height set to {height}");
    }
    
    /// <summary>
    /// 设置文件高度
    /// </summary>
    /// <param name="height">文件高度</param>
    public void SetFileHeight(float height)
    {
        fileHeight = height;
        RefreshCurrentDirectory();
        
        Debug.Log($"FileSystemBrowser: File height set to {height}");
    }
    
    /// <summary>
    /// 设置内容边距
    /// </summary>
    /// <param name="left">左边距</param>
    /// <param name="right">右边距</param>
    /// <param name="top">上边距</param>
    /// <param name="bottom">下边距</param>
    public void SetContentPadding(int left, int right, int top, int bottom)
    {
        contentPadding = new RectOffset(left, right, top, bottom);
        SetupContentLayout();
        
        Debug.Log($"FileSystemBrowser: Content padding set to ({left}, {right}, {top}, {bottom})");
    }
    
    /// <summary>
    /// 一次性设置所有布局参数
    /// </summary>
    /// <param name="spacing">项目间距</param>
    /// <param name="folderH">文件夹高度</param>
    /// <param name="fileH">文件高度</param>
    /// <param name="padding">内容边距</param>
    public void SetLayoutParameters(float spacing, float folderH, float fileH, RectOffset padding)
    {
        itemSpacing = spacing;
        folderHeight = folderH;
        fileHeight = fileH;
        contentPadding = padding;
        
        SetupContentLayout();
        RefreshCurrentDirectory();
        
        Debug.Log($"FileSystemBrowser: All layout parameters updated - Spacing: {spacing}, Folder: {folderH}, File: {fileH}");
    }
    
    /// <summary>
    /// 设置选中颜色
    /// </summary>
    /// <param name="color">选中时的颜色</param>
    public void SetSelectedColor(Color color)
    {
        selectedColor = color;
        UpdateSelection();
        
        Debug.Log($"FileSystemBrowser: Selected color set to {color}");
    }
    
    /// <summary>
    /// 设置正常颜色
    /// </summary>
    /// <param name="color">未选中时的颜色</param>
    public void SetNormalColor(Color color)
    {
        normalColor = color;
        UpdateSelection();
        
        Debug.Log($"FileSystemBrowser: Normal color set to {color}");
    }
    
    /// <summary>
    /// 展开所有文件夹
    /// </summary>
    public void ExpandAll()
    {
        if (allItems != null)
        {
            foreach (var item in allItems)
            {
                if (item.isDirectory)
                {
                    item.isExpanded = true;
                }
            }
            RebuildDisplayList();
            
            Debug.Log("FileSystemBrowser: All folders expanded");
        }
    }
    
    /// <summary>
    /// 折叠所有文件夹
    /// </summary>
    public void CollapseAll()
    {
        if (allItems != null)
        {
            foreach (var item in allItems)
            {
                if (item.isDirectory)
                {
                    item.isExpanded = false;
                }
            }
            RebuildDisplayList();
            
            Debug.Log("FileSystemBrowser: All folders collapsed");
        }
    }
    
    void OnDestroy()
    {
        SaveCurrentFile();
        
        // 停止自动保存协程
        if (autoSaveCoroutine != null)
        {
            StopCoroutine(autoSaveCoroutine);
            autoSaveCoroutine = null;
        }
        
        // 停止材质动画协程
        if (materialAnimationCoroutine != null)
        {
            StopCoroutine(materialAnimationCoroutine);
            materialAnimationCoroutine = null;
        }
        
        // 清理重命名状态
        if (isRenaming)
        {
            FinishRenaming();
        }
        
        // 清理资源
        ClearCurrentItems();
        if (allItems != null)
        {
            allItems.Clear();
        }
    }
    
    /// <summary>
    /// 设置动画材质
    /// </summary>
    /// <param name="material">用于动画的材质</param>
    public void SetAnimationMaterial(Material material)
    {
        animationMaterial = material;
        Debug.Log($"FileSystemBrowser: Animation material set to {(material != null ? material.name : "null")}");
    }
    
    /// <summary>
    /// 获取当前动画材质
    /// </summary>
    /// <returns>当前设置的动画材质</returns>
    public Material GetAnimationMaterial()
    {
        return animationMaterial;
    }
    
    /// <summary>
    /// 手动触发材质动画
    /// </summary>
    public void TriggerMaterialAnimation()
    {
        StartMaterialAnimation();
    }
    
    /// <summary>
    /// 公共方法：创建新文件
    /// </summary>
    public void CreateNewFilePublic()
    {
        if (!isEdit)
        {
            CreateNewFile();
        }
        else
        {
            Debug.LogWarning("FileSystemBrowser: Cannot create new file while in edit mode.");
        }
    }
    
    /// <summary>
    /// 公共方法：创建新文件夹
    /// </summary>
    public void CreateNewFolderPublic()
    {
        if (!isEdit)
        {
            CreateNewFolder();
        }
        else
        {
            Debug.LogWarning("FileSystemBrowser: Cannot create new folder while in edit mode.");
        }
    }
    
    /// <summary>
    /// 公共方法：删除当前选中的项目
    /// </summary>
    public void DeleteCurrentItemPublic()
    {
        if (!isEdit)
        {
            DeleteCurrentItem();
        }
        else
        {
            Debug.LogWarning("FileSystemBrowser: Cannot delete items while in edit mode.");
        }
    }
    
    /// <summary>
    /// 公共方法：重命名当前选中的项目
    /// </summary>
    public void RenameCurrentItemPublic()
    {
        if (!isEdit && !isRenaming && currentItems != null && selectedIndex >= 0 && selectedIndex < currentItems.Count)
        {
            StartRenaming(currentItems[selectedIndex]);
        }
        else
        {
            Debug.LogWarning("FileSystemBrowser: Cannot rename items while in edit mode or already renaming.");
        }
    }
}
