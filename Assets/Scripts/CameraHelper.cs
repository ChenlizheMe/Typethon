using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

public class CameraHelper : MonoBehaviour
{
    [Header("Camera Tracking Settings")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private TMP_InputField targetInputField;
    [SerializeField] private RectTransform inputFieldRectTransform;
    
    [Header("Zoom Settings")]
    [SerializeField] private float minZDistance = 60f;
    [SerializeField] private float maxZDistance = 85f;
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float currentZDistance = 72.5f; // 默认中间值
    
    [Header("Tracking Settings")]
    [SerializeField] private bool enableCursorTracking = true;
    [SerializeField] private float trackingSpeed = 8f;
    [SerializeField] private Vector2 trackingOffset = Vector2.zero;
    [SerializeField] private bool smoothTracking = true;
    
    [Header("Camera Limits")]
    [SerializeField] private bool enableCameraLimits = true;
    [SerializeField] private Vector2 minCameraPosition = new Vector2(-100f, -100f);
    [SerializeField] private Vector2 maxCameraPosition = new Vector2(100f, 100f);
    
    [Header("Camera Drift Settings")]
    [SerializeField] private bool enableCameraDrift = false;
    [SerializeField] private float driftIntensity = 0.3f;
    [SerializeField] private float driftSpeed = 0.5f;
    [SerializeField] private Vector2 driftRange = new Vector2(1.5f, 1.5f);
    [SerializeField] private float driftSmoothness = 3f;
    
    // 私有变量
    private Vector3 targetPosition;
    private Vector3 lastCursorWorldPosition;
    private bool isInputFieldFocused = false;
    private Canvas parentCanvas;
    private RectTransform canvasRectTransform;
    private int lastCaretPosition = -1;
    private string lastTextContent = "";
    
    // DOTween 相关变量
    private Tween cameraShakeTween; // 相机抖动动画
    
    // 相机漂移相关变量
    private Vector3 driftOffset = Vector3.zero; // 当前漂移偏移
    private Vector3 driftTarget = Vector3.zero; // 漂移目标位置
    private float driftTimer = 0f; // 漂移计时器
    private float driftChangeInterval = 3f; // 漂移目标改变间隔（增加间隔让切换更平滑）
    private Vector3 driftVelocity = Vector3.zero; // 用于SmoothDamp的速度
    
    void Start()
    {
        InitializeCameraHelper();
    }
    
    void Update()
    {
        HandleZoomInput();
        
        EnsureInputFieldFocused();
        
        if (enableCursorTracking)
        {
            bool caretChanged = targetInputField.caretPosition != lastCaretPosition;
            bool textChanged = targetInputField.text != lastTextContent;
            
            bool enterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            
            if (caretChanged || textChanged || enterPressed)
            {
                StartCoroutine(DelayedCursorTracking());
                lastCaretPosition = targetInputField.caretPosition;
                lastTextContent = targetInputField.text;
            }
        }
        
        // 更新相机漂移
        if (enableCameraDrift)
        {
            UpdateCameraDrift();
        }
        
        UpdateCameraPosition();
    }
    
    /// <summary>
    /// 确保InputField始终保持焦点状态
    /// </summary>
    private void EnsureInputFieldFocused()
    {
        if (targetInputField != null && !targetInputField.isFocused)
        {
            targetInputField.Select();
            // targetInputField.ActivateInputField();
            isInputFieldFocused = true;
        }
    }
    
    /// <summary>
    /// 延迟执行光标跟踪，确保TextMeshPro布局已更新
    /// </summary>
    private IEnumerator DelayedCursorTracking()
    {
        // 等待一帧，让TextMeshPro完成布局更新
        yield return null;
        UpdateCursorTracking();
    }
    
    /// <summary>
    /// 初始化相机助手
    /// </summary>
    private void InitializeCameraHelper()
    {
        // 检查必需的组件是否已分配
        if (targetCamera == null)
        {
            Debug.LogError("CameraHelper: Target Camera is not assigned! Please assign a camera in the inspector.");
            return;
        }
        
        if (targetInputField == null)
        {
            Debug.LogError("CameraHelper: Target Input Field is not assigned! Please assign an input field in the inspector.");
            return;
        }
        
        // 获取InputField的相关组件
        if (inputFieldRectTransform == null)
        {
            inputFieldRectTransform = targetInputField.GetComponent<RectTransform>();
        }
        
        // 绑定焦点事件
        targetInputField.onSelect.AddListener(OnInputFieldFocused);
        targetInputField.onDeselect.AddListener(OnInputFieldUnfocused);
        
        // 立即激活InputField并设置焦点
        targetInputField.Select();
        // targetInputField.ActivateInputField();
        isInputFieldFocused = true;
        
        // 获取父级Canvas
        parentCanvas = targetInputField.GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            canvasRectTransform = parentCanvas.GetComponent<RectTransform>();
        }
        
        // 设置初始相机位置
        Vector3 pos = targetCamera.transform.position;
        pos.z = currentZDistance; // 修复：Z轴正负问题
        targetCamera.transform.position = pos;
        targetPosition = pos;
        
        // 初始化时立即跟踪到光标位置
        if (enableCursorTracking)
        {
            // 初始化跟踪状态
            lastCaretPosition = targetInputField.caretPosition;
            lastTextContent = targetInputField.text;
            
            // 由于InputField已经获得焦点，直接进行跟踪
            UpdateCursorTracking();
            
            // 立即设置相机位置到光标处
            targetCamera.transform.position = targetPosition;
        }
        
        Debug.Log("CameraHelper initialized successfully!");
    }
    
    /// <summary>
    /// 处理滚轮缩放输入
    /// </summary>
    private void HandleZoomInput()
    {
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        
        if (Mathf.Abs(scrollInput) > 0.01f)
        {
            // 计算新的Z距离
            currentZDistance -= scrollInput * zoomSpeed;
            currentZDistance = Mathf.Clamp(currentZDistance, minZDistance, maxZDistance);
            
            // 更新目标位置的Z坐标（修复正负问题）
            targetPosition.z = currentZDistance;
        }
    }
    
    /// <summary>
    /// 更新光标跟踪
    /// </summary>
    private void UpdateCursorTracking()
    {
        if (targetInputField == null || targetCamera == null) return;
        
        // 获取光标在InputField中的位置
        Vector3 cursorWorldPos = GetCursorWorldPosition();
        
        // 移除位置相等的检查，确保每次都能更新（特别是换行时）
        // if (cursorWorldPos != lastCursorWorldPosition)
        {
            // 计算目标位置（添加偏移）
            Vector3 newTargetPos = cursorWorldPos;
            newTargetPos.x += trackingOffset.x;
            newTargetPos.y += trackingOffset.y;
            newTargetPos.z = currentZDistance; // 修复：Z轴正负问题
            
            // 应用相机限制
            if (enableCameraLimits)
            {
                newTargetPos.x = Mathf.Clamp(newTargetPos.x, minCameraPosition.x, maxCameraPosition.x);
                newTargetPos.y = Mathf.Clamp(newTargetPos.y, minCameraPosition.y, maxCameraPosition.y);
            }
            
            targetPosition = newTargetPos;
            lastCursorWorldPosition = cursorWorldPos;
        }
    }
    
    /// <summary>
    /// 获取光标在世界空间中的位置（适用于UI Canvas）
    /// </summary>
    /// <returns>光标的世界坐标</returns>
    private Vector3 GetCursorWorldPosition()
    {
        if (targetInputField == null || inputFieldRectTransform == null)
            return Vector3.zero;
        
        // 获取光标在文本中的位置
        int caretPosition = targetInputField.caretPosition;
        TMP_Text textComponent = targetInputField.textComponent;
        
        if (textComponent == null)
        {
            return GetInputFieldCenterPosition();
        }
        
        // 强制更新TextMeshPro的网格信息 - 多次更新确保布局正确
        textComponent.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases(); // 强制更新Canvas
        textComponent.ForceMeshUpdate(); // 再次强制更新
        
        TMP_TextInfo textInfo = textComponent.textInfo;
        
        // 如果没有文本或字符数为0，返回InputField的起始位置
        if (string.IsNullOrEmpty(targetInputField.text) || textInfo.characterCount == 0)
        {
            return GetInputFieldStartPosition();
        }
        
        Vector3 cursorPos;
        
        if (caretPosition > 0 && caretPosition <= textInfo.characterCount)
        {
            // 获取光标前一个字符的信息
            TMP_CharacterInfo charInfo = textInfo.characterInfo[caretPosition - 1];
            
            // 检查字符是否是换行符
            if (charInfo.character == '\n' || charInfo.character == '\r')
            {
                // 如果前一个字符是换行符，使用下一行的起始位置
                if (caretPosition < textInfo.characterCount)
                {
                    TMP_CharacterInfo nextCharInfo = textInfo.characterInfo[caretPosition];
                    cursorPos = new Vector3(nextCharInfo.topLeft.x, nextCharInfo.baseLine, 0);
                }
                else
                {
                    // 如果是最后一个字符，计算新行位置
                    cursorPos = new Vector3(0, charInfo.baseLine - textComponent.fontSize, 0);
                }
            }
            else
            {
                // 普通字符，使用字符的右边缘
                cursorPos = new Vector3(charInfo.topRight.x, charInfo.baseLine, 0);
            }
        }
        else if (caretPosition == 0 && textInfo.characterCount > 0)
        {
            // 光标在文本开始处
            TMP_CharacterInfo charInfo = textInfo.characterInfo[0];
            cursorPos = new Vector3(charInfo.topLeft.x, charInfo.baseLine, 0);
        }
        else if (caretPosition >= textInfo.characterCount && textInfo.characterCount > 0)
        {
            // 光标在文本末尾
            TMP_CharacterInfo lastCharInfo = textInfo.characterInfo[textInfo.characterCount - 1];
            
            if (lastCharInfo.character == '\n' || lastCharInfo.character == '\r')
            {
                // 如果最后一个字符是换行符，光标应该在新行的开始
                cursorPos = new Vector3(0, lastCharInfo.baseLine - textComponent.fontSize, 0);
            }
            else
            {
                // 否则在最后一个字符的右边
                cursorPos = new Vector3(lastCharInfo.topRight.x, lastCharInfo.baseLine, 0);
            }
        }
        else
        {
            // 默认情况，返回起始位置
            return GetInputFieldStartPosition();
        }
        
        // 将本地坐标转换为世界坐标
        Vector3 worldPos = textComponent.transform.TransformPoint(cursorPos);
        
        // 根据Canvas渲染模式进行坐标转换
        if (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            return worldPos;
        }
        else if (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(parentCanvas.worldCamera, worldPos);
            return targetCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, currentZDistance));
        }
        
        return worldPos;
    }
    
    /// <summary>
    /// 获取InputField的起始位置（左上角）
    /// </summary>
    /// <returns>InputField起始位置的世界坐标</returns>
    private Vector3 GetInputFieldStartPosition()
    {
        if (inputFieldRectTransform == null) return Vector3.zero;
        
        // 获取InputField的左上角位置
        Vector3[] corners = new Vector3[4];
        inputFieldRectTransform.GetWorldCorners(corners);
        
        // corners[1] 是左上角
        Vector3 startPos = corners[1];
        
        if (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(parentCanvas.worldCamera, startPos);
            return targetCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, currentZDistance));
        }
        
        return startPos;
    }
    
    /// <summary>
    /// 获取InputField的中心位置（适用于UI Canvas）
    /// </summary>
    /// <returns>InputField中心的世界坐标</returns>
    private Vector3 GetInputFieldCenterPosition()
    {
        if (inputFieldRectTransform == null) return Vector3.zero;
        
        Vector3 worldPos;
        
        if (parentCanvas != null)
        {
            switch (parentCanvas.renderMode)
            {
                case RenderMode.ScreenSpaceOverlay:
                    // Overlay模式：转换屏幕坐标到世界坐标
                    Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, inputFieldRectTransform.position);
                    worldPos = targetCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, currentZDistance));
                    break;
                    
                case RenderMode.ScreenSpaceCamera:
                    // Camera模式：直接使用世界坐标
                    worldPos = inputFieldRectTransform.position;
                    break;
                    
                case RenderMode.WorldSpace:
                    // World Space模式：直接使用世界坐标
                    worldPos = inputFieldRectTransform.position;
                    break;
                    
                default:
                    worldPos = inputFieldRectTransform.position;
                    break;
            }
        }
        else
        {
            // 如果没有找到Canvas，直接使用Transform位置
            worldPos = inputFieldRectTransform.position;
        }
        
        return worldPos;
    }
    
    /// <summary>
    /// 更新相机漂移效果
    /// </summary>
    private void UpdateCameraDrift()
    {
        // 更新漂移计时器
        driftTimer += Time.deltaTime * driftSpeed * 0.3f; // 减慢计时器速度
        
        // 每隔一定时间更换漂移目标
        if (driftTimer >= driftChangeInterval)
        {
            driftTimer = 0f;
            
            // 生成新的随机漂移目标，使用更小的范围
            driftTarget = new Vector3(
                Random.Range(-driftRange.x * 0.8f, driftRange.x * 0.8f),
                Random.Range(-driftRange.y * 0.8f, driftRange.y * 0.8f),
                0f
            );
        }
        
        // 使用多层Perlin噪声创建更平滑的漂移效果
        float timeScale = Time.time * driftSpeed * 0.2f; // 进一步减慢噪声变化
        
        // 主噪声层 - 慢速大幅度
        float noiseX1 = (Mathf.PerlinNoise(timeScale, 0f) - 0.5f) * 2f;
        float noiseY1 = (Mathf.PerlinNoise(0f, timeScale) - 0.5f) * 2f;
        
        // 细节噪声层 - 快速小幅度
        float noiseX2 = (Mathf.PerlinNoise(timeScale * 2.5f, 100f) - 0.5f) * 0.3f;
        float noiseY2 = (Mathf.PerlinNoise(100f, timeScale * 2.5f) - 0.5f) * 0.3f;
        
        // 组合噪声
        Vector3 noiseOffset = new Vector3(
            (noiseX1 + noiseX2) * driftRange.x * 0.6f,
            (noiseY1 + noiseY2) * driftRange.y * 0.6f,
            0f
        );
        
        // 结合目标漂移和噪声漂移，增加目标权重让移动更有方向性
        Vector3 targetDrift = Vector3.Lerp(noiseOffset, driftTarget, 0.7f);
        
        // 使用SmoothDamp实现更平滑的移动
        driftOffset = Vector3.SmoothDamp(
            driftOffset, 
            targetDrift * driftIntensity, 
            ref driftVelocity, 
            driftSmoothness
        );
    }
    
    /// <summary>
    /// 更新相机位置
    /// </summary>
    private void UpdateCameraPosition()
    {
        if (targetCamera == null) return;

        // 计算最终位置（包含漂移偏移）
        Vector3 finalPosition = targetPosition + driftOffset;
        
        if (smoothTracking)
        {
            // 平滑移动到目标位置
            targetCamera.transform.position = Vector3.Lerp(
                targetCamera.transform.position, 
                finalPosition, 
                trackingSpeed * Time.deltaTime
            );
        }
        else
        {
            // 直接设置位置
            targetCamera.transform.position = finalPosition;
        }
    }
    
    /// <summary>
    /// InputField獲得焦点时调用
    /// </summary>
    /// <param name="text">输入的文本</param>
    private void OnInputFieldFocused(string text)
    {
        isInputFieldFocused = true;
        // 立即更新跟踪状态
        lastCaretPosition = targetInputField.caretPosition;
        lastTextContent = targetInputField.text;
        
        // 延迟跟踪到当前光标位置，确保布局已更新
        if (enableCursorTracking)
        {
            StartCoroutine(DelayedCursorTracking());
        }
    }
    
    /// <summary>
    /// InputField失去焦点时调用 - 立即重新获取焦点
    /// </summary>
    /// <param name="text">输入的文本</param>
    private void OnInputFieldUnfocused(string text)
    {
        // 不允许失去焦点，立即重新激活
        if (targetInputField != null)
        {
            // 使用协程延迟重新激活，避免与Unity的内部焦点管理冲突
            StartCoroutine(RefocusInputField());
        }
    }
    
    /// <summary>
    /// 延迟重新激活InputField焦点
    /// </summary>
    private IEnumerator RefocusInputField()
    {
        // 等待一帧，避免与Unity的焦点管理冲突
        yield return null;
        
        if (targetInputField != null)
        {
            targetInputField.Select();
            // targetInputField.ActivateInputField();
            isInputFieldFocused = true;
        }
    }
    
    /// <summary>
    /// 手动设置相机跟踪目标
    /// </summary>
    /// <param name="inputField">要跟踪的InputField</param>
    public void SetTrackingTarget(TMP_InputField inputField)
    {
        if (targetInputField != null)
        {
            // 移除旧的事件监听
            targetInputField.onSelect.RemoveListener(OnInputFieldFocused);
            targetInputField.onDeselect.RemoveListener(OnInputFieldUnfocused);
        }
        
        targetInputField = inputField;
        
        if (targetInputField != null)
        {
            inputFieldRectTransform = targetInputField.GetComponent<RectTransform>();
            targetInputField.onSelect.AddListener(OnInputFieldFocused);
            targetInputField.onDeselect.AddListener(OnInputFieldUnfocused);
            
            // 立即激活新的InputField
            targetInputField.Select();
            // targetInputField.ActivateInputField();
            isInputFieldFocused = true;
            
            parentCanvas = targetInputField.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                canvasRectTransform = parentCanvas.GetComponent<RectTransform>();
            }
        }
    }
    
    /// <summary>
    /// 设置缩放范围
    /// </summary>
    /// <param name="minZ">最小Z距离</param>
    /// <param name="maxZ">最大Z距离</param>
    public void SetZoomRange(float minZ, float maxZ)
    {
        minZDistance = minZ;
        maxZDistance = maxZ;
        currentZDistance = Mathf.Clamp(currentZDistance, minZDistance, maxZDistance);
    }
    
    /// <summary>
    /// 立即移动相机到光标位置
    /// </summary>
    public void FocusOnCursor()
    {
        if (enableCursorTracking && targetInputField != null)
        {
            // 确保InputField有焦点
            EnsureInputFieldFocused();
            UpdateCursorTracking();
            targetCamera.transform.position = targetPosition;
        }
    }
    
    /// <summary>
    /// 启用/禁用光标跟踪
    /// </summary>
    /// <param name="enabled">是否启用</param>
    public void SetCursorTrackingEnabled(bool enabled)
    {
        enableCursorTracking = enabled;
        
        // 如果启用跟踪，确保InputField有焦点
        if (enabled && targetInputField != null)
        {
            EnsureInputFieldFocused();
        }
    }
    
    /// <summary>
    /// 检查InputField是否始终保持焦点
    /// </summary>
    /// <returns>是否保持焦点</returns>
    public bool IsInputFieldAlwaysFocused()
    {
        return targetInputField != null && targetInputField.isFocused;
    }
    
    /// <summary>
    /// 手动强制InputField获得焦点
    /// </summary>
    public void ForceInputFieldFocus()
    {
        // EnsureInputFieldFocused();
    }
    
    void OnDestroy()
    {
        // 清理事件监听
        if (targetInputField != null)
        {
            targetInputField.onSelect.RemoveListener(OnInputFieldFocused);
            targetInputField.onDeselect.RemoveListener(OnInputFieldUnfocused);
        }
        
        // 清理 DOTween 动画
        if (cameraShakeTween != null && cameraShakeTween.IsActive())
        {
            cameraShakeTween.Kill();
        }
    }
    
    /// <summary>
    /// 执行相机轻微抖动效果
    /// </summary>
    /// <param name="duration">抖动持续时间</param>
    /// <param name="strength">抖动强度</param>
    /// <param name="vibrato">抖动次数</param>
    /// <param name="randomness">随机性(0-1)</param>
    /// <param name="fadeOut">是否逐渐减弱</param>
    public void ShakeCamera(float duration = 0.5f, float strength = 1f, int vibrato = 10, float randomness = 90f, bool fadeOut = true)
    {
        if (targetCamera == null)
        {
            Debug.LogWarning("CameraHelper: Cannot shake camera - targetCamera is null!");
            return;
        }
        
        // 如果已经有抖动动画在运行，先停止它
        if (cameraShakeTween != null && cameraShakeTween.IsActive())
        {
            cameraShakeTween.Kill();
        }
        
        // 执行抖动动画
        cameraShakeTween = targetCamera.DOShakePosition(duration, strength, vibrato, randomness, fadeOut)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => {
                // 抖动结束后，确保相机回到正确的目标位置
                if (targetCamera != null)
                {
                    targetCamera.transform.position = targetPosition;
                }
            });
    }
    
    /// <summary>
    /// 执行相机轻微抖动效果（预设参数 - 温和抖动）
    /// </summary>
    public void ShakeCameraGentle()
    {
        ShakeCamera(0.3f, 0.5f, 8, 90f, true);
    }
    
    /// <summary>
    /// 执行相机轻微抖动效果（预设参数 - 中等抖动）
    /// </summary>
    public void ShakeCameraMedium()
    {
        ShakeCamera(0.5f, 1f, 10, 90f, true);
    }
    
    /// <summary>
    /// 执行相机轻微抖动效果（预设参数 - 强烈抖动）
    /// </summary>
    public void ShakeCameraStrong()
    {
        ShakeCamera(0.8f, 2f, 15, 90f, true);
    }
    
    /// <summary>
    /// 停止当前的相机抖动
    /// </summary>
    public void StopCameraShake()
    {
        if (cameraShakeTween != null && cameraShakeTween.IsActive())
        {
            cameraShakeTween.Kill();
            
            // 立即回到目标位置
            if (targetCamera != null)
            {
                targetCamera.transform.position = targetPosition;
            }
        }
    }
    
    /// <summary>
    /// 检查相机是否正在抖动
    /// </summary>
    /// <returns>是否正在抖动</returns>
    public bool IsCameraShaking()
    {
        return cameraShakeTween != null && cameraShakeTween.IsActive();
    }
    
    /// <summary>
    /// 启用/禁用相机漂移效果
    /// </summary>
    /// <param name="enabled">是否启用漂移</param>
    public void SetCameraDriftEnabled(bool enabled)
    {
        enableCameraDrift = enabled;
        
        // 如果禁用漂移，重置漂移偏移
        if (!enabled)
        {
            driftOffset = Vector3.zero;
            driftTarget = Vector3.zero;
            driftTimer = 0f;
            driftVelocity = Vector3.zero;
        }
    }
    
    /// <summary>
    /// 设置相机漂移参数
    /// </summary>
    /// <param name="intensity">漂移强度</param>
    /// <param name="speed">漂移速度</param>
    /// <param name="range">漂移范围</param>
    /// <param name="smoothness">平滑度（可选）</param>
    public void SetCameraDriftSettings(float intensity, float speed, Vector2 range, float smoothness = -1f)
    {
        driftIntensity = intensity;
        driftSpeed = speed;
        driftRange = range;
        
        if (smoothness > 0f)
        {
            driftSmoothness = smoothness;
        }
    }
    
    /// <summary>
    /// 获取当前漂移偏移
    /// </summary>
    /// <returns>当前漂移偏移值</returns>
    public Vector3 GetCurrentDriftOffset()
    {
        return driftOffset;
    }
    
    /// <summary>
    /// 重置相机漂移状态
    /// </summary>
    public void ResetCameraDrift()
    {
        driftOffset = Vector3.zero;
        driftTarget = Vector3.zero;
        driftTimer = 0f;
        driftVelocity = Vector3.zero;
    }
    
    /// <summary>
    /// 检查相机漂移是否启用
    /// </summary>
    /// <returns>是否启用漂移</returns>
    public bool IsCameraDriftEnabled()
    {
        return enableCameraDrift;
    }
    
    /// <summary>
    /// 获取Canvas的渲染模式信息（调试用）
    /// </summary>
    /// <returns>Canvas渲染模式的字符串描述</returns>
    public string GetCanvasRenderModeInfo()
    {
        if (parentCanvas == null) return "No Canvas found";
        
        return $"Canvas Render Mode: {parentCanvas.renderMode}" +
               $"\nCanvas Camera: {(parentCanvas.worldCamera != null ? parentCanvas.worldCamera.name : "None")}" +
               $"\nCanvas Sort Order: {parentCanvas.sortingOrder}";
    }
    
    /// <summary>
    /// 检查组件设置是否正确
    /// </summary>
    /// <returns>是否设置正确</returns>
    public bool ValidateSetup()
    {
        bool isValid = true;
        
        if (targetCamera == null)
        {
            Debug.LogError("CameraHelper: Target Camera is not assigned!");
            isValid = false;
        }
        
        if (targetInputField == null)
        {
            Debug.LogError("CameraHelper: Target Input Field is not assigned!");
            isValid = false;
        }
        
        if (parentCanvas == null)
        {
            Debug.LogWarning("CameraHelper: Parent Canvas not found. This may cause tracking issues.");
        }
        
        return isValid;
    }
    
    // 调试用的Gizmos
    void OnDrawGizmosSelected()
    {
        if (targetCamera != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(targetPosition, Vector3.one * 2f);
            
            // 绘制当前光标位置
            if (Application.isPlaying && isInputFieldFocused)
            {
                Gizmos.color = Color.green;
                Vector3 cursorPos = GetCursorWorldPosition();
                Gizmos.DrawWireSphere(cursorPos, 1f);
            }
            
            if (enableCameraLimits)
            {
                Gizmos.color = Color.red;
                Vector3 min = new Vector3(minCameraPosition.x, minCameraPosition.y, targetPosition.z);
                Vector3 max = new Vector3(maxCameraPosition.x, maxCameraPosition.y, targetPosition.z);
                Gizmos.DrawWireCube((min + max) * 0.5f, max - min);
            }
        }
    }
}
