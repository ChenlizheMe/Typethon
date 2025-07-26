using System.Collections;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShortcutManager : MonoBehaviour
{
    [SerializeField] private Material GazeMaterial;

    [Header("Code Execution Components")]
    [SerializeField] private TMP_InputField codeInputField;
    [SerializeField] private TextMeshProUGUI outputText; // 用于显示执行结果的UI组件
    
    [Header("Status Indicator")]
    [SerializeField] private Image statusIndicator; // 状态指示器
    [SerializeField] private Color editModeColor = Color.yellow; // 编辑模式颜色（黄色）
    [SerializeField] private Color runModeColor = Color.green; // 运行模式颜色（绿色）
    [SerializeField] private Color errorModeColor = Color.red; // 错误模式颜色（红色）
    
    [Header("Editor Mode Settings")]
    [SerializeField] private bool lockCodeDuringExecution = true; // 执行时是否锁定代码编辑
    
    [Header("Python Settings")]
    [SerializeField] private string pythonExecutablePath = "python"; // Python可执行文件路径
    [SerializeField] private bool useSystemPython = true; // 是否使用系统Python环境
    
    [Header("Output Settings")]
    [SerializeField] private int maxOutputLines = 100; // 最大输出行数
    [SerializeField] private bool autoScrollToBottom = true; // 自动滚动到底部
    [SerializeField] private bool enableLineTracking = true; // 是否启用行跟踪
    [SerializeField] private float lineTrackingDelay = 0.5f; // 行跟踪延迟时间（秒）
    
    private bool isExecuting = false; // 防止重复执行
    private System.Collections.Generic.Queue<string> outputQueue = new System.Collections.Generic.Queue<string>(); // 输出队列
    private System.Collections.Generic.Queue<string> errorQueue = new System.Collections.Generic.Queue<string>(); // 错误队列
    private readonly object queueLock = new object(); // 队列锁
    private System.Collections.Generic.List<string> outputLines = new System.Collections.Generic.List<string>(); // 存储输出行的列表
    private Process currentProcess = null; // 当前正在执行的Python进程
    private bool shouldTerminate = false; // 是否应该终止执行
    private TextHelper textHelper; // TextHelper组件引用
    
    // 行跟踪相关
    private System.Collections.Generic.List<string> codeLines = new System.Collections.Generic.List<string>(); // 代码行列表
    private int currentLineIndex = -1; // 当前执行的行索引
    private Coroutine lineTrackingCoroutine; // 行跟踪协程
    
    // 逐步执行相关
    private bool isStepByStepMode = false; // 是否处于逐步执行模式
    private bool waitingForNextStep = false; // 是否正在等待下一步
    
    // 状态指示器相关
    private bool hasExecutionError = false; // 是否有执行错误
    
    // Agent Service相关
    private Process agentServiceProcess = null; // Agent服务进程
    private Coroutine agentServiceCoroutine = null; // Agent服务协程

    void Update()
    {
        HandleShortcuts();
    }
    
    /// <summary>
    /// 处理快捷键输入
    /// </summary>
    private void HandleShortcuts()
    {
        // 检查 Ctrl+Enter 快捷键（执行代码）
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && 
            Input.GetKeyDown(KeyCode.R))
        {
            StartCoroutine(GazeDebugAction());
            OnCtrlEnterPressed();
        }
        
        // 检查 Ctrl+C 快捷键（终止执行）
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && 
            Input.GetKeyDown(KeyCode.C))
        {
            StartCoroutine(GazeDebugAction());
            OnCtrlCPressed();
        }
        
        // 检查 Ctrl+D 快捷键（执行代码并添加延迟）
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && 
            Input.GetKeyDown(KeyCode.D))
        {
            StartCoroutine(GazeDebugAction());
            OnCtrlDPressed();
        }
        
        // 检查 Ctrl+E 快捷键（执行代码并在每行无限停顿）
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && 
            Input.GetKeyDown(KeyCode.E))
        {
            StartCoroutine(GazeDebugAction());
            OnCtrlEPressed();
        }
        
        // 检查下箭头键（逐步执行模式下继续执行下一行）
        if (isStepByStepMode && waitingForNextStep && Input.GetKeyDown(KeyCode.DownArrow))
        {
            OnDownArrowPressed();
        }
    }

    private IEnumerator GazeDebugAction()
    {
        // 设置GazeMaterial的power从0到1
        while (GazeMaterial.GetFloat("_Power") < 1f)
        {
            float currentPower = GazeMaterial.GetFloat("_Power");
            GazeMaterial.SetFloat("_Power", Mathf.Min(currentPower + 0.2f, 1f));
            yield return null; // 等待一帧
        }
        yield return new WaitForSeconds(0.3f);
        // 重置GazeMaterial的power到0
        while (GazeMaterial.GetFloat("_Power") > 0f)
        {
            float currentPower = GazeMaterial.GetFloat("_Power");
            GazeMaterial.SetFloat("_Power", Mathf.Max(currentPower - 0.2f, 0f));
            yield return null; // 等待一帧
        }
        GazeMaterial.SetFloat("_Power", 0f); // 确保最终值为0
    }
    
    /// <summary>
    /// 更新状态指示器颜色
    /// </summary>
    private void UpdateStatusIndicator()
    {
        if (statusIndicator == null) return;
        
        if (hasExecutionError)
        {
            // 如果有错误，显示红色
            statusIndicator.color = errorModeColor;
        }
        else if (isExecuting)
        {
            // 如果正在执行，显示绿色
            statusIndicator.color = runModeColor;
        }
        else
        {
            // 编辑模式显示黄色
            statusIndicator.color = editModeColor;
        }
    }
    
    /// <summary>
    /// 处理 Ctrl+Enter 按键事件
    /// </summary>
    private void OnCtrlEnterPressed()
    {
        if (codeInputField == null)
        {
            UnityEngine.Debug.LogError("Code InputField is not assigned!");
            return;
        }

        if (isExecuting)
        {
            UnityEngine.Debug.LogWarning("Python code is already executing. Use Ctrl+C or Escape to terminate.");
            return;
        }

        string pythonCode = codeInputField.text;

        if (string.IsNullOrWhiteSpace(pythonCode))
        {
            UnityEngine.Debug.LogWarning("No Python code to execute!");
            return;
        }

        // 清空之前的输出记录
        ClearOutput();
        
        // 重置错误状态并更新状态指示器为运行模式
        hasExecutionError = false;
        UpdateStatusIndicator();

        UnityEngine.Debug.Log("Executing Python code...");
        StartCoroutine(ExecutePythonCode(pythonCode));
    }
    
    /// <summary>
    /// 处理 Ctrl+C 按键事件（终止执行）
    /// </summary>
    private void OnCtrlCPressed()
    {
        if (isExecuting)
        {
            TerminateExecution();
        }
    }
    
    /// <summary>
    /// 处理 Escape 按键事件（终止执行）
    /// </summary>
    private void OnEscapePressed()
    {
        if (isExecuting)
        {
            TerminateExecution();
        }
    }
    
    /// <summary>
    /// 处理 Ctrl+D 按键事件（执行代码并添加延迟）
    /// </summary>
    private void OnCtrlDPressed()
    {
        if (codeInputField == null)
        {
            UnityEngine.Debug.LogError("Code InputField is not assigned!");
            return;
        }
        
        if (isExecuting)
        {
            UnityEngine.Debug.LogWarning("Python code is already executing. Use Ctrl+C or Escape to terminate.");
            return;
        }
        
        string pythonCode = codeInputField.text;
        
        if (string.IsNullOrWhiteSpace(pythonCode))
        {
            UnityEngine.Debug.LogWarning("No Python code to execute!");
            return;
        }
        
        // 清空之前的输出记录
        ClearOutput();
        
        // 重置错误状态并更新状态指示器为运行模式
        hasExecutionError = false;
        UpdateStatusIndicator();
        
        UnityEngine.Debug.Log("Executing Python code with delay...");
        StartCoroutine(ExecutePythonCodeWithDelay(pythonCode));
    }
    
    /// <summary>
    /// 处理 Ctrl+E 按键事件（执行代码并在每行无限停顿）
    /// </summary>
    private void OnCtrlEPressed()
    {
        if (codeInputField == null)
        {
            UnityEngine.Debug.LogError("Code InputField is not assigned!");
            return;
        }
        
        if (isExecuting)
        {
            UnityEngine.Debug.LogWarning("Python code is already executing. Use Ctrl+C or Escape to terminate.");
            return;
        }
        
        string pythonCode = codeInputField.text;
        
        if (string.IsNullOrWhiteSpace(pythonCode))
        {
            UnityEngine.Debug.LogWarning("No Python code to execute!");
            return;
        }
        
        // 清空之前的输出记录
        ClearOutput();
        
        // 重置错误状态并更新状态指示器为运行模式
        hasExecutionError = false;
        UpdateStatusIndicator();
        
        UnityEngine.Debug.Log("Executing Python code with step-by-step mode (Press Down Arrow to continue)...");
        StartCoroutine(ExecutePythonCodeStepByStep(pythonCode));
    }
    
    /// <summary>
    /// 处理下箭头键按键事件（逐步执行模式下继续下一行）
    /// </summary>
    private void OnDownArrowPressed()
    {
        if (isStepByStepMode && waitingForNextStep)
        {
            waitingForNextStep = false;
            
            // 删除等待文件以继续执行
            try
            {
                string waitFile = $"step_wait_{currentLineIndex}.tmp";
                if (System.IO.File.Exists(waitFile))
                {
                    System.IO.File.Delete(waitFile);
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[Step Mode] Failed to delete wait file: {e.Message}");
            }
        }
    }
    
    /// <summary>
    /// 终止当前Python执行
    /// </summary>
    private void TerminateExecution()
    {
        if (!isExecuting)
        {
            return;
        }
        
        UnityEngine.Debug.Log("Terminating Python execution...");
        shouldTerminate = true;
        
        // 尝试终止当前进程
        if (currentProcess != null && !currentProcess.HasExited)
        {
            try
            {
                currentProcess.Kill();
                UnityEngine.Debug.Log("Python process terminated successfully.");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"Failed to terminate Python process: {e.Message}");
            }
        }
        
        // 添加终止信息到输出
        lock (queueLock)
        {
            errorQueue.Enqueue("Execution terminated by user");
            outputQueue.Enqueue("--- Python run stopped ---");
        }
        
        // 重置执行状态
        isExecuting = false;
        currentProcess = null;
        shouldTerminate = false;
        
        // 更新状态指示器到编辑模式（如果没有错误）
        if (!hasExecutionError)
        {
            UpdateStatusIndicator();
        }
        
        // 重置逐步执行状态
        isStepByStepMode = false;
        waitingForNextStep = false;
        
        // 恢复编辑模式
        SetEditorMode(true);
    }
    
    /// <summary>
    /// 执行Python代码的协程
    /// </summary>
    /// <param name="pythonCode">要执行的Python代码</param>
    private IEnumerator ExecutePythonCode(string pythonCode)
    {
        return ExecutePythonCodeInternal(pythonCode, false, false);
    }
    
    /// <summary>
    /// 执行Python代码的协程（带延迟）
    /// </summary>
    /// <param name="pythonCode">要执行的Python代码</param>
    private IEnumerator ExecutePythonCodeWithDelay(string pythonCode)
    {
        return ExecutePythonCodeInternal(pythonCode, true, false);
    }
    
    /// <summary>
    /// 执行Python代码的协程（逐步执行）
    /// </summary>
    /// <param name="pythonCode">要执行的Python代码</param>
    private IEnumerator ExecutePythonCodeStepByStep(string pythonCode)
    {
        return ExecutePythonCodeInternal(pythonCode, false, true);
    }
    
    /// <summary>
    /// 执行Python代码的内部协程
    /// </summary>
    /// <param name="pythonCode">要执行的Python代码</param>
    /// <param name="withDelay">是否在每行之间添加延迟</param>
    /// <param name="stepByStep">是否启用逐步执行模式</param>
    private IEnumerator ExecutePythonCodeInternal(string pythonCode, bool withDelay, bool stepByStep)
    {
        isExecuting = true;
        
        // 设置逐步执行模式状态
        isStepByStepMode = stepByStep;
        waitingForNextStep = false;
        
        // 切换到运行模式（锁定编辑）
        SetEditorMode(false);
        
        // 准备行跟踪
        if (enableLineTracking)
        {
            PrepareLineTracking(pythonCode);
        }
        
        // 显示开始执行的消息
        string executionMessage = $"[{System.DateTime.Now:HH:mm:ss}] Executing Python code:\n{pythonCode}\n";
        UnityEngine.Debug.Log(executionMessage);
        
        if (outputText != null)
        {
            outputText.text = "Executing Python code...";
        }
        
        // 创建临时Python文件
        string tempFilePath = System.IO.Path.GetTempFileName() + ".py";
        bool fileCreated = false;
        
        // 创建临时文件
        try
        {
            // 如果启用了行跟踪，修改Python代码添加跟踪信息
            string modifiedCode = enableLineTracking ? AddLineTrackingToPythonCode(pythonCode, withDelay, stepByStep) : pythonCode;
            
            // 写入Python代码到临时文件
            System.IO.File.WriteAllText(tempFilePath, modifiedCode, Encoding.UTF8);
            fileCreated = true;
        }
        catch (System.Exception e)
        {
            string errorMsg = $"Failed to create or write temporary Python file: {e.Message}";
            UnityEngine.Debug.LogError(errorMsg);
            
            if (outputText != null)
            {
                outputText.text = errorMsg;
            }
            
            isExecuting = false;
            yield break;
        }
        
        // 如果文件创建成功，执行Python进程
        if (fileCreated)
        {
            // 启动实时UI更新协程
            StartCoroutine(UpdateOutputUI());
            
            // 启动行跟踪协程
            if (enableLineTracking)
            {
                lineTrackingCoroutine = StartCoroutine(TrackExecutionProgress());
            }
            
            yield return StartCoroutine(ExecutePythonProcess(tempFilePath));
        }
        
        // 停止行跟踪
        if (lineTrackingCoroutine != null)
        {
            StopCoroutine(lineTrackingCoroutine);
            lineTrackingCoroutine = null;
        }
        
        // 清理临时文件
        CleanupTempFile(tempFilePath);
        
        // 恢复编辑模式
        SetEditorMode(true);
        
        // 重置逐步执行状态
        isStepByStepMode = false;
        waitingForNextStep = false;
        
        isExecuting = false;
    }
    
    /// <summary>
    /// 实时更新输出UI的协程
    /// </summary>
    private IEnumerator UpdateOutputUI()
    {
        while (isExecuting)
        {
            bool hasUpdates = false;
            
            // 处理输出队列
            lock (queueLock)
            {
                while (outputQueue.Count > 0 && outputText != null)
                {
                    string output = outputQueue.Dequeue();
                    AddOutputLine(output, false);
                    hasUpdates = true;
                }
                
                // 处理错误队列
                while (errorQueue.Count > 0 && outputText != null)
                {
                    string error = errorQueue.Dequeue();
                    AddOutputLine($"<color=red>[Error] {error}</color>", true);
                    hasUpdates = true;
                }
            }
            
            // 如果有更新，刷新UI显示
            if (hasUpdates && outputText != null)
            {
                RefreshOutputText();
            }
            
            yield return new WaitForSeconds(0.05f); // 每50毫秒检查一次
        }
    }
    
    /// <summary>
    /// 添加输出行并管理行数限制
    /// </summary>
    /// <param name="line">要添加的行</param>
    /// <param name="isError">是否为错误信息</param>
    private void AddOutputLine(string line, bool isError)
    {
        // 添加新行
        outputLines.Add(line);
        
        // 检查是否超过最大行数限制
        if (outputLines.Count > maxOutputLines)
        {
            // 移除最旧的行（从开头移除）
            int linesToRemove = outputLines.Count - maxOutputLines;
            for (int i = 0; i < linesToRemove; i++)
            {
                outputLines.RemoveAt(0);
            }
        }
    }
    
    /// <summary>
    /// 刷新输出文本显示
    /// </summary>
    private void RefreshOutputText()
    {
        if (outputText == null) return;
        
        // 将所有行合并为一个字符串
        string combinedText = string.Join("\n", outputLines);
        outputText.text = combinedText;
        
        // 强制刷新UI
        outputText.ForceMeshUpdate();
        
        // 如果启用自动滚动，滚动到底部
        if (autoScrollToBottom)
        {
            ScrollToBottom();
        }
    }
    
    /// <summary>
    /// 滚动输出文本到底部
    /// </summary>
    private void ScrollToBottom()
    {
        // 尝试找到ScrollRect组件并滚动到底部
        var scrollRect = outputText.GetComponentInParent<UnityEngine.UI.ScrollRect>();
        if (scrollRect != null)
        {
            // 等待一帧让布局更新
            StartCoroutine(ScrollToBottomDelayed(scrollRect));
        }
    }
    
    /// <summary>
    /// 延迟滚动到底部的协程
    /// </summary>
    private IEnumerator ScrollToBottomDelayed(UnityEngine.UI.ScrollRect scrollRect)
    {
        yield return null; // 等待一帧
        scrollRect.normalizedPosition = new Vector2(0, 0); // 滚动到底部
    }
    
    /// <summary>
    /// 清理临时文件
    /// </summary>
    /// <param name="tempFilePath">临时文件路径</param>
    private void CleanupTempFile(string tempFilePath)
    {
        try
        {
            if (System.IO.File.Exists(tempFilePath))
            {
                System.IO.File.Delete(tempFilePath);
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning($"Failed to delete temporary file: {e.Message}");
        }
    }
    
    /// <summary>
    /// 执行Python进程的协程
    /// </summary>
    /// <param name="tempFilePath">临时Python文件路径</param>
    private IEnumerator ExecutePythonProcess(string tempFilePath)
    {
        // 创建Process来执行Python
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = pythonExecutablePath,
            Arguments = $"-u \"{tempFilePath}\"", // 添加-u参数强制unbuffered输出
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        
        Process process = null;
        StringBuilder outputBuilder = new StringBuilder();
        StringBuilder errorBuilder = new StringBuilder();
        bool processStarted = false;
        
        UnityEngine.Debug.Log($"[Python Debug] Starting Python process with command: {startInfo.FileName} {startInfo.Arguments}");
        
        // 尝试启动进程
        try
        {
            process = new Process();
            process.StartInfo = startInfo;
            currentProcess = process; // 保存当前进程引用
            
            // 设置输出数据接收事件
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                    // 实时显示输出到Debug.Log
                    UnityEngine.Debug.Log($"[Python Output] {e.Data}");
                    
                    // 添加到输出队列
                    lock (queueLock)
                    {
                        outputQueue.Enqueue(e.Data);
                    }
                }
            };
            
            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                    // 实时显示错误到Debug.Log
                    UnityEngine.Debug.LogError($"[Python Error] {e.Data}");
                    
                    // 添加到错误队列
                    lock (queueLock)
                    {
                        errorQueue.Enqueue(e.Data);
                    }
                }
            };
            
            // 启动进程
            process.Start();
            processStarted = true;
            
            UnityEngine.Debug.Log("[Python Debug] Process started successfully");
            
            // 开始异步读取输出
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (System.Exception e)
        {
            string errorMsg = $"Failed to start Python process: {e.Message}";
            UnityEngine.Debug.LogError(errorMsg);
            
            if (outputText != null)
            {
                outputText.text = errorMsg;
            }
            
            if (process != null)
            {
                process.Dispose();
            }
            yield break;
        }
        
        // 如果进程启动成功，等待执行完成
        if (processStarted)
        {
            // 等待进程完成（最多等待30秒）
            float timeout = 36000000f;
            float elapsed = 0f;
            
            // 在try-catch外面进行等待循环
            while (!process.HasExited && elapsed < timeout && !shouldTerminate)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            
            UnityEngine.Debug.Log($"[Python Debug] Process finished. HasExited: {process.HasExited}, Elapsed: {elapsed:F1}s, Terminated: {shouldTerminate}");
            
            // 处理执行结果
            ProcessExecutionResult(process, outputBuilder, errorBuilder, elapsed >= timeout, shouldTerminate);
            
            // 释放进程资源
            process.Dispose();
        }
    }
    
    /// <summary>
    /// 处理Python执行结果
    /// </summary>
    /// <param name="process">Python进程</param>
    /// <param name="outputBuilder">输出构建器</param>
    /// <param name="errorBuilder">错误构建器</param>
    /// <param name="isTimeout">是否超时</param>
    /// <param name="isTerminated">是否被用户终止</param>
    private void ProcessExecutionResult(Process process, StringBuilder outputBuilder, StringBuilder errorBuilder, bool isTimeout, bool isTerminated)
    {
        if (isTerminated)
        {
            // 用户终止了执行
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"Failed to kill process during termination: {e.Message}");
            }
            
            UnityEngine.Debug.LogWarning("[Python] Execution terminated by user.");
            
            // 添加终止信息到输出队列（如果还没添加）
            if (outputText != null)
            {
                lock (queueLock)
                {
                    outputQueue.Enqueue("--- Execution terminated by user ---");
                }
            }
            
            // 设置错误状态
            hasExecutionError = true;
            UpdateStatusIndicator();
        }
        else if (isTimeout)
        {
            // 超时，强制结束进程
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"Failed to kill process: {e.Message}");
            }
            
            UnityEngine.Debug.LogError("[Python] Execution timeout (30 seconds). Process killed.");
            
            // 添加超时信息到输出队列
            if (outputText != null)
            {
                lock (queueLock)
                {
                    errorQueue.Enqueue("Execution timeout (30 seconds)!");
                }
            }
            
            // 设置错误状态
            hasExecutionError = true;
            UpdateStatusIndicator();
        }
        else
        {
            // 进程正常结束
            string finalOutput = outputBuilder.ToString();
            string finalError = errorBuilder.ToString();
            
            UnityEngine.Debug.Log($"[Python Debug] Final output length: {finalOutput.Length}, Final error length: {finalError.Length}");
            UnityEngine.Debug.Log($"[Python Debug] Exit code: {process.ExitCode}");
            
            // 显示最终结果
            string resultMessage = $"[{System.DateTime.Now:HH:mm:ss}] Python execution completed.\n";
            
            if (!string.IsNullOrEmpty(finalOutput))
            {
                resultMessage += $"Output:\n{finalOutput}\n";
            }
            
            if (!string.IsNullOrEmpty(finalError))
            {
                resultMessage += $"Errors:\n{finalError}\n";
                hasExecutionError = true;
                UpdateStatusIndicator();
                UnityEngine.Debug.LogError($"[Python Execution] {resultMessage}");
            }
            else
            {
                // 执行成功，更新状态指示器为编辑模式
                hasExecutionError = false;
                UpdateStatusIndicator();
                UnityEngine.Debug.Log($"[Python Execution] {resultMessage}");
            }
            
            // 添加执行完成标记到输出文本
            if (outputText != null)
            {
                lock (queueLock)
                {
                    outputQueue.Enqueue("--- Execution completed ---");
                }
            }
        }
        
        // 清理进程引用和重置标志
        currentProcess = null;
        shouldTerminate = false;
    }
    
    /// <summary>
    /// 设置编辑器模式
    /// </summary>
    /// <param name="isEditMode">true为编辑模式，false为运行模式</param>
    private void SetEditorMode(bool isEditMode)
    {
        if (!lockCodeDuringExecution) return; // 如果不需要锁定，直接返回
        
        // 设置TextHelper的模式（如果存在）
        if (textHelper != null)
        {
            // IL2CPP兼容：使用SendMessage而不是反射
            try
            {
                textHelper.SendMessage("SetEditMode", isEditMode, SendMessageOptions.DontRequireReceiver);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"Failed to call SetEditMode on TextHelper: {e.Message}");
            }
        }
        else
        {
            // 如果没有TextHelper，直接设置输入框状态
            if (codeInputField != null)
            {
                if (isEditMode)
                {
                    codeInputField.interactable = true;
                    codeInputField.readOnly = false;
                }
                else
                {
                    codeInputField.interactable = true;  // 保持可交互以允许系统控制光标
                    codeInputField.readOnly = true;      // 但设为只读阻止用户编辑
                }
            }
        }
        
        // 输出模式变化日志
        string modeText = isEditMode ? "Edit Mode" : "Run Mode (Cursor tracking enabled)";
        UnityEngine.Debug.Log($"[Editor Mode] Switched to {modeText}");
        
        // 可选：在输出文本中显示模式变化
        if (outputText != null && !isEditMode)
        {
            lock (queueLock)
            {
                outputQueue.Enqueue($"[Mode] Switched to Run Mode - Code editing disabled, cursor tracking enabled");
            }
        }
        else if (outputText != null && isEditMode)
        {
            lock (queueLock)
            {
                outputQueue.Enqueue($"[Mode] Switched to Edit Mode - Code editing enabled, cursor tracking disabled");
            }
        }
    }
    
    /// <summary>
    /// 清空输出文本
    /// </summary>
    public void ClearOutput()
    {
        if (outputText != null)
        {
            outputText.text = "";
            outputLines.Clear();
        }
        
        // 清空队列
        lock (queueLock)
        {
            outputQueue.Clear();
            errorQueue.Clear();
        }
        
        UnityEngine.Debug.Log("Output cleared manually.");
    }
    
    /// <summary>
    /// 设置最大输出行数
    /// </summary>
    /// <param name="maxLines">最大行数</param>
    public void SetMaxOutputLines(int maxLines)
    {
        maxOutputLines = Mathf.Max(10, maxLines); // 最少保持10行
        UnityEngine.Debug.Log($"Max output lines set to: {maxOutputLines}");
        
        // 如果当前行数超过新的限制，进行裁剪
        if (outputLines.Count > maxOutputLines)
        {
            int linesToRemove = outputLines.Count - maxOutputLines;
            for (int i = 0; i < linesToRemove; i++)
            {
                outputLines.RemoveAt(0);
            }
            RefreshOutputText();
        }
    }
    
    /// <summary>
    /// 设置Python可执行文件路径
    /// </summary>
    /// <param name="path">Python可执行文件的完整路径</param>
    public void SetPythonExecutablePath(string path)
    {
        pythonExecutablePath = path;
        UnityEngine.Debug.Log($"Python executable path set to: {path}");
    }
    
    /// <summary>
    /// 测试Python环境是否可用
    /// </summary>
    public void TestPythonEnvironment()
    {
        StartCoroutine(TestPythonEnvironmentCoroutine());
    }
    
    /// <summary>
    /// 测试Python环境的协程
    /// </summary>
    private IEnumerator TestPythonEnvironmentCoroutine()
    {
        UnityEngine.Debug.Log("Testing Python environment...");
        
        string testCode = "print('Python environment test successful!')\nprint('Python version:')\nimport sys\nprint(sys.version)";
        
        yield return StartCoroutine(ExecutePythonCodeInternal(testCode, false, false));
    }
    
    void Start()
    {
        // 自动检测并设置Python路径
        AutoDetectPythonPath();
        
        // 获取TextHelper组件
        textHelper = GetComponent<TextHelper>();
        if (textHelper == null)
        {
            // 尝试从输入框的GameObject获取
            if (codeInputField != null)
            {
                textHelper = codeInputField.GetComponent<TextHelper>();
            }
        }
        
        if (textHelper == null)
        {
            UnityEngine.Debug.LogWarning("TextHelper component not found! Editor mode switching will be limited.");
        }
        else
        {
            UnityEngine.Debug.Log("TextHelper component found and connected.");
        }
        
        // 验证组件引用
        if (codeInputField == null)
        {
            UnityEngine.Debug.LogWarning("Code InputField is not assigned to ShortcutManager!");
        }
        
        if (outputText == null)
        {
            UnityEngine.Debug.LogWarning("Output Text is not assigned to ShortcutManager!");
        }
        
        // 确保初始状态为编辑模式
        SetEditorMode(true);
        
        // 可选：启动时测试Python环境
        if (useSystemPython)
        {
            UnityEngine.Debug.Log("ShortcutManager initialized. Press Ctrl+Enter to execute Python code.");
            // Invoke("TestPythonEnvironment", 1f); // 1秒后测试Python环境
        }
        
        // 如果是打包的exe环境，启动agent服务
        if (Application.platform == RuntimePlatform.WindowsPlayer)
        {
            StartAgentService();
        }
    }
    
    /// <summary>
    /// 准备行跟踪
    /// </summary>
    /// <param name="pythonCode">Python代码</param>
    private void PrepareLineTracking(string pythonCode)
    {
        // 将代码分割成行
        codeLines.Clear();
        codeLines.AddRange(pythonCode.Split('\n'));
        currentLineIndex = -1;
        
        UnityEngine.Debug.Log($"[Line Tracking] Prepared {codeLines.Count} lines for tracking");
    }
    
    /// <summary>
    /// 为Python代码添加行跟踪信息
    /// </summary>
    /// <param name="originalCode">原始Python代码</param>
    /// <param name="withDelay">是否在每行之间添加延迟</param>
    /// <param name="stepByStep">是否启用逐步执行模式</param>
    /// <returns>添加了跟踪信息的Python代码</returns>
    private string AddLineTrackingToPythonCode(string originalCode, bool withDelay = false, bool stepByStep = false)
    {
        if (string.IsNullOrWhiteSpace(originalCode))
            return originalCode;
        
        StringBuilder modifiedCode = new StringBuilder();
        string[] lines = originalCode.Split('\n');
        
        // 添加跟踪导入
        modifiedCode.AppendLine("import sys");
        modifiedCode.AppendLine("import time");
        modifiedCode.AppendLine();
        
        // 添加跟踪函数
        modifiedCode.AppendLine("def __track_line__(line_num):");
        modifiedCode.AppendLine("    print(f'__TRACKING_LINE__:{line_num}', flush=True)");
        modifiedCode.AppendLine();
        
        // 如果是逐步执行模式，添加等待函数
        if (stepByStep)
        {
            modifiedCode.AppendLine("import os");
            modifiedCode.AppendLine("def __wait_for_step__(line_num):");
            modifiedCode.AppendLine("    print(f'__WAITING_FOR_STEP__:{line_num}', flush=True)");
            modifiedCode.AppendLine("    # 创建等待文件");
            modifiedCode.AppendLine("    wait_file = f'step_wait_{line_num}.tmp'");
            modifiedCode.AppendLine("    with open(wait_file, 'w') as f:");
            modifiedCode.AppendLine("        f.write('waiting')");
            modifiedCode.AppendLine("    # 等待文件被删除");
            modifiedCode.AppendLine("    while os.path.exists(wait_file):");
            modifiedCode.AppendLine("        time.sleep(0.1)");
            modifiedCode.AppendLine();
        }
        
        // 为每一行添加跟踪调用
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            
            // 跳过空行和只有空白字符的行
            if (string.IsNullOrWhiteSpace(line))
            {
                modifiedCode.AppendLine(line);
                continue;
            }
            
            // 跳过注释行
            string trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("#"))
            {
                modifiedCode.AppendLine(line);
                continue;
            }
            
            // 检查缩进级别
            string indent = GetIndentation(line);
            
            // 添加跟踪调用
            modifiedCode.AppendLine($"{indent}__track_line__({i})");
            
            // 如果启用延迟，添加time.sleep
            if (withDelay)
            {
                modifiedCode.AppendLine($"{indent}time.sleep(0.5)");
            }
            
            // 如果是逐步执行模式，添加等待步骤
            if (stepByStep)
            {
                modifiedCode.AppendLine($"{indent}__wait_for_step__({i})");
            }
            
            // 添加原始代码行
            modifiedCode.AppendLine(line);
        }
        
        // 在代码末尾添加特殊行来解决Typethon中的一些bug
        modifiedCode.AppendLine();
        modifiedCode.AppendLine("__end_line_for_solve_some_bug_in_typethon=0");
        modifiedCode.AppendLine("time.sleep(0.5)");
        
        return modifiedCode.ToString();
    }
    
    /// <summary>
    /// 获取行的缩进
    /// </summary>
    /// <param name="line">代码行</param>
    /// <returns>缩进字符串</returns>
    private string GetIndentation(string line)
    {
        int indentCount = 0;
        foreach (char c in line)
        {
            if (c == ' ' || c == '\t')
                indentCount++;
            else
                break;
        }
        return line.Substring(0, indentCount);
    }
    
    /// <summary>
    /// 跟踪执行进度的协程
    /// </summary>
    /// <returns></returns>
    private IEnumerator TrackExecutionProgress()
    {
        while (isExecuting && !shouldTerminate)
        {
            // 检查输出队列中是否有跟踪信息
            lock (queueLock)
            {
                var tempQueue = new System.Collections.Generic.Queue<string>();
                
                while (outputQueue.Count > 0)
                {
                    string output = outputQueue.Dequeue();
                    
                    // 检查是否是跟踪信息
                    if (output.StartsWith("__TRACKING_LINE__:"))
                    {
                        string lineNumberStr = output.Substring("__TRACKING_LINE__:".Length);
                        if (int.TryParse(lineNumberStr, out int lineNumber))
                        {
                            currentLineIndex = lineNumber;
                            
                            // 通知TextHelper移动光标
                            if (textHelper != null)
                            {
                                try
                                {
                                    // IL2CPP兼容：使用SendMessage而不是反射
                                    textHelper.SendMessage("SetCurrentExecutingLine", lineNumber, SendMessageOptions.DontRequireReceiver);
                                }
                                catch (System.Exception e)
                                {
                                    UnityEngine.Debug.LogWarning($"Failed to call SetCurrentExecutingLine on TextHelper: {e.Message}");
                                }
                            }
                            
                            UnityEngine.Debug.Log($"[Line Tracking] Now executing line {lineNumber}: {(lineNumber < codeLines.Count ? codeLines[lineNumber] : "unknown")}");
                        }
                    }
                    else if (output.StartsWith("__WAITING_FOR_STEP__:"))
                    {
                        string lineNumberStr = output.Substring("__WAITING_FOR_STEP__:".Length);
                        if (int.TryParse(lineNumberStr, out int lineNumber))
                        {
                            waitingForNextStep = true;
                        }
                    }
                    else
                    {
                        // 不是跟踪信息，放回队列
                        tempQueue.Enqueue(output);
                    }
                }
                
                // 将非跟踪信息放回输出队列
                while (tempQueue.Count > 0)
                {
                    outputQueue.Enqueue(tempQueue.Dequeue());
                }
            }
            
            yield return null;
        }
    }
    
    /// <summary>
    /// 自动检测Python路径
    /// </summary>
    private void AutoDetectPythonPath()
    {
        string detectedPath = GetPythonExecutablePath();
        if (!string.IsNullOrEmpty(detectedPath))
        {
            pythonExecutablePath = detectedPath;
            UnityEngine.Debug.Log($"[Python Path] Auto-detected Python path: {pythonExecutablePath}");
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[Python Path] Could not auto-detect Python path. Using default: {pythonExecutablePath}");
        }
    }
    
    /// <summary>
    /// 获取Python可执行文件路径（根据运行环境自动检测）
    /// </summary>
    /// <returns>Python可执行文件的完整路径</returns>
    private string GetPythonExecutablePath()
    {
        // 在构建的exe环境下，优先使用相对路径的Python环境
        if (Application.platform == RuntimePlatform.WindowsPlayer)
        {
            // exe运行时，尝试使用exe目录下的.pyenv/Scripts/python.exe
            string exeDir = System.IO.Path.GetDirectoryName(Application.dataPath);
            string relativePythonPath = System.IO.Path.Combine(exeDir, ".pyenv", "Scripts", "python.exe");
            
            if (System.IO.File.Exists(relativePythonPath))
            {
                UnityEngine.Debug.Log($"[Python Path] Found Python at relative path: {relativePythonPath}");
                return relativePythonPath;
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[Python Path] Python not found at expected relative path: {relativePythonPath}");
            }
        }
        
        // 在Unity编辑器中或找不到相对路径时的备用方案
        if (Application.isEditor || useSystemPython)
        {
            // 检查当前设置的路径是否有效
            if (!string.IsNullOrEmpty(pythonExecutablePath) && IsValidPythonPath(pythonExecutablePath))
            {
                return pythonExecutablePath;
            }
            
            // 尝试常见的Python路径
            string[] commonPaths = {
                "python",           // 系统PATH中的python
                "python3",          // 系统PATH中的python3
                @"C:\Python39\python.exe",
                @"C:\Python38\python.exe",
                @"C:\Python37\python.exe",
                @"C:\Users\" + System.Environment.UserName + @"\AppData\Local\Programs\Python\Python39\python.exe",
                @"C:\Users\" + System.Environment.UserName + @"\AppData\Local\Programs\Python\Python38\python.exe",
            };
            
            foreach (string path in commonPaths)
            {
                if (IsValidPythonPath(path))
                {
                    UnityEngine.Debug.Log($"[Python Path] Found valid Python at: {path}");
                    return path;
                }
            }
        }
        
        // 如果都找不到，返回默认值
        return pythonExecutablePath;
    }
    
    /// <summary>
    /// 检查指定路径的Python是否有效
    /// </summary>
    /// <param name="pythonPath">Python可执行文件路径</param>
    /// <returns>是否有效</returns>
    private bool IsValidPythonPath(string pythonPath)
    {
        if (string.IsNullOrEmpty(pythonPath))
            return false;
        
        try
        {
            // 如果是相对路径（如"python"），不检查文件存在性，让系统PATH处理
            if (pythonPath == "python" || pythonPath == "python3")
                return true;
            
            // 对于绝对路径，检查文件是否存在
            if (System.IO.File.Exists(pythonPath))
                return true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning($"[Python Path] Error checking Python path {pythonPath}: {e.Message}");
        }
        
        return false;
    }
    
    /// <summary>
    /// 启动Agent服务
    /// </summary>
    private void StartAgentService()
    {
        if (agentServiceCoroutine != null)
        {
            UnityEngine.Debug.LogWarning("[Agent Service] Agent service is already running.");
            return;
        }
        
        // 检查agent_service.py文件是否存在
        string exeDir = System.IO.Path.GetDirectoryName(Application.dataPath);
        string agentServicePath = System.IO.Path.Combine(exeDir, "agent_service.py");
        
        if (!System.IO.File.Exists(agentServicePath))
        {
            UnityEngine.Debug.LogWarning($"[Agent Service] agent_service.py not found at: {agentServicePath}");
            return;
        }
        
        UnityEngine.Debug.Log($"[Agent Service] Starting agent service: {agentServicePath}");
        agentServiceCoroutine = StartCoroutine(RunAgentServiceCoroutine(agentServicePath));
    }
    
    /// <summary>
    /// 运行Agent服务的永不关闭协程
    /// </summary>
    /// <param name="agentServicePath">agent_service.py的路径</param>
    private IEnumerator RunAgentServiceCoroutine(string agentServicePath)
    {
        while (true) // 永不关闭的协程
        {
            bool processStarted = false;
            
            try
            {
                UnityEngine.Debug.Log("[Agent Service] Starting agent service process...");
                
                // 获取Python可执行文件路径
                string pythonPath = GetAgentServicePythonPath();
                
                // 创建进程启动信息
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"-u \"{agentServicePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    WorkingDirectory = System.IO.Path.GetDirectoryName(agentServicePath)
                };
                
                // 启动进程
                agentServiceProcess = new Process();
                agentServiceProcess.StartInfo = startInfo;
                
                // 设置输出处理
                agentServiceProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        UnityEngine.Debug.Log($"[Agent Service] {e.Data}");
                    }
                };
                
                agentServiceProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        UnityEngine.Debug.LogError($"[Agent Service Error] {e.Data}");
                    }
                };
                
                // 启动进程
                agentServiceProcess.Start();
                agentServiceProcess.BeginOutputReadLine();
                agentServiceProcess.BeginErrorReadLine();
                
                processStarted = true;
                UnityEngine.Debug.Log("[Agent Service] Agent service started successfully.");
                
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[Agent Service] Failed to start agent service: {e.Message}");
            }
            
            // 如果进程启动成功，等待进程结束
            if (processStarted && agentServiceProcess != null)
            {
                // 等待进程结束
                while (!agentServiceProcess.HasExited)
                {
                    yield return new WaitForSeconds(1f);
                }
                
                // 进程意外结束，记录日志
                UnityEngine.Debug.LogWarning($"[Agent Service] Agent service process exited with code: {agentServiceProcess.ExitCode}");
                
                // 清理进程资源
                try
                {
                    agentServiceProcess.Dispose();
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[Agent Service] Error disposing process: {e.Message}");
                }
                
                agentServiceProcess = null;
            }
            
            // 等待5秒后重新启动
            UnityEngine.Debug.Log("[Agent Service] Restarting agent service in 5 seconds...");
            yield return new WaitForSeconds(5f);
        }
    }
    
    /// <summary>
    /// 获取Agent服务使用的Python可执行文件路径
    /// </summary>
    /// <returns>Python可执行文件路径</returns>
    private string GetAgentServicePythonPath()
    {
        // 优先使用exe目录下的.pyenv/Scripts/python.exe
        string exeDir = System.IO.Path.GetDirectoryName(Application.dataPath);
        string pyenvPythonPath = System.IO.Path.Combine(exeDir, ".pyenv", "Scripts", "python.exe");
        
        if (System.IO.File.Exists(pyenvPythonPath))
        {
            UnityEngine.Debug.Log($"[Agent Service] Using pyenv Python: {pyenvPythonPath}");
            return pyenvPythonPath;
        }
        
        // 如果找不到pyenv，使用当前配置的Python路径
        UnityEngine.Debug.LogWarning($"[Agent Service] pyenv Python not found at: {pyenvPythonPath}, using configured Python: {pythonExecutablePath}");
        return pythonExecutablePath;
    }
    
    /// <summary>
    /// 停止Agent服务
    /// </summary>
    private void StopAgentService()
    {
        if (agentServiceProcess != null && !agentServiceProcess.HasExited)
        {
            try
            {
                UnityEngine.Debug.Log("[Agent Service] Stopping agent service...");
                agentServiceProcess.Kill();
                agentServiceProcess.Dispose();
                agentServiceProcess = null;
                UnityEngine.Debug.Log("[Agent Service] Agent service stopped.");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[Agent Service] Failed to stop agent service: {e.Message}");
            }
        }
        
        if (agentServiceCoroutine != null)
        {
            StopCoroutine(agentServiceCoroutine);
            agentServiceCoroutine = null;
        }
    }
    
    /// <summary>
    /// 在对象销毁时清理Agent服务
    /// </summary>
    private void OnDestroy()
    {
        StopAgentService();
    }
}