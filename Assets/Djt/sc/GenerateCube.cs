using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;
using System.Linq;
using DG.Tweening;

public class GenerateCube : MonoBehaviour
{
    public static GenerateCube Instance { get; private set; }
    public Material baseMaterial; // 允许在编辑器中设置基础材质
    public GameObject collisionEffectPrefab; // 碰撞时生成的GameObject预制体
    public Camera shakeCamera; // 用于抖动的相机
    private Dictionary<string, string> tokenColors = new Dictionary<string, string>();
    private Regex tokenRegex; // 动态生成的正则表达式

    private const string SYNTAX_CSV_NAME = "syntax"; // 确保 csv 文件位于 Resources 文件夹内
    
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
    private readonly string stringColorHex = "#f1fa8c";      // 黄色 - 字符串
    private readonly string numberColorHex = "#bd93f9";      // 紫色 - 数字
    private readonly string commentColorHex = "#6272a4";     // 灰色 - 注释
    private readonly string decoratorColorHex = "#ffb86c";   // 橙色 - 装饰器

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // ����Ѿ�����ʵ���Ҳ��ǵ�ǰ�������ٵ�ǰ����
            Destroy(gameObject);
            return;
        }

        // ���õ�ǰ����Ϊ����ʵ��
        Instance = this;

        // �����Ҫ�ڳ����л�ʱ�����˶���ȡ��ע�����´���
        // DontDestroyOnLoad(gameObject);

        // 加载 CSV 文件中的魔法方法和颜色
        LoadSyntaxRules();
        BuildTokenRegex();
        BuildAdvancedRegexes();
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
        string pattern = $@"\b({string.Join("|", tokenColors.Keys.Select(Regex.Escape))})\b";
        tokenRegex = new Regex(pattern);
    }

    /// <summary> 构建高级语法高亮的正则表达式 </summary>
    private void BuildAdvancedRegexes()
    {
        // 类名匹配：class 关键字后的标识符
        classRegex = new Regex(@"\bclass\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
        
        // 函数名匹配：def 关键字后的标识符
        functionRegex = new Regex(@"\bdef\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
        
        // 变量名匹配：赋值操作的左侧标识符
        variableRegex = new Regex(@"\b([A-Za-z_][A-Za-z0-9_]*)\s*=", RegexOptions.Compiled);
        
        // 字符串匹配：单引号、双引号、三引号字符串
        stringRegex = new Regex(@"(""""""[\s\S]*?""""""|'''[\s\S]*?'''|""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*')", RegexOptions.Compiled);
        
        // 数字匹配：整数、浮点数、科学计数法
        numberRegex = new Regex(@"\b(?:\d+\.?\d*(?:[eE][+-]?\d+)?|\.\d+(?:[eE][+-]?\d+)?)\b", RegexOptions.Compiled);
        
        // 注释匹配：# 开头的单行注释
        commentRegex = new Regex(@"#.*$", RegexOptions.Compiled | RegexOptions.Multiline);
        
        // 装饰器匹配：@ 开头的装饰器
        decoratorRegex = new Regex(@"@[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*", RegexOptions.Compiled);
    }

    /// <summary>
    /// 根据方法名称获取颜色 - 使用改进的解析逻辑
    /// </summary>
    /// <param name="dataType">数据类型或关键字</param>
    /// <returns>对应的颜色</returns>
    private Color GetColor(string dataType)
    {
        // 首先检查是否在tokenColors字典中（关键字）
        if (tokenColors.TryGetValue(dataType, out string colorHex))
        {
            if (ColorUtility.TryParseHtmlString(colorHex, out Color color))
                return color;
        }
        
        // 使用正则表达式进行高级匹配
        Color? regexColor = GetColorByRegex(dataType);
        if (regexColor.HasValue)
            return regexColor.Value;
        
        // 如果找不到对应的颜色，返回默认颜色
        return Color.white;
    }
    
    /// <summary>
    /// 使用正则表达式匹配获取颜色
    /// </summary>
    /// <param name="token">要匹配的token</param>
    /// <returns>匹配到的颜色，如果没有匹配则返回null</returns>
    private Color? GetColorByRegex(string token)
    {
        // 检查是否为数字
        if (numberRegex != null && numberRegex.IsMatch(token))
        {
            if (ColorUtility.TryParseHtmlString(numberColorHex, out Color color))
                return color;
        }
        
        // 检查是否为字符串（简单检查引号）
        if ((token.StartsWith("\"") && token.EndsWith("\"")) || 
            (token.StartsWith("'") && token.EndsWith("'")))
        {
            if (ColorUtility.TryParseHtmlString(stringColorHex, out Color color))
                return color;
        }
        
        // 检查是否为注释
        if (token.StartsWith("#"))
        {
            if (ColorUtility.TryParseHtmlString(commentColorHex, out Color color))
                return color;
        }
        
        // 检查是否为装饰器
        if (token.StartsWith("@"))
        {
            if (ColorUtility.TryParseHtmlString(decoratorColorHex, out Color color))
                return color;
        }
        
        return null; // 没有匹配
    }

    // 根据代码行号和内容生成对应的方块
    public void LineGenerate(int LineNum, string text, int offset=50)
    {
        string[] dataTypes = ProcessLine(LineNum, text);
        for (int i = 0; i < dataTypes.Length; i++)
        {
            if (string.IsNullOrEmpty(dataTypes[i]))
            {
                continue; // 如果数据为空，跳过处理
            }

            // 根据数据类型获取颜色
            Color cubeColor = GetColor(dataTypes[i]);

            // 创建一个立方体
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.layer = LayerMask.NameToLayer("Robot"); // 设置图层为Robot

            // 在xz = -8到8的范围内随机选择整数位置，y固定为25
            float randomX = Mathf.Round(Random.Range(-8f, 8f));
            float randomZ = Mathf.Round(Random.Range(-8f, 8f));
            cube.transform.position = new Vector3(randomX, 11f, randomZ);

            // 获取立方体的渲染器
            Renderer cubeRenderer = cube.GetComponent<Renderer>();
            if (cubeRenderer != null)
            {
                // ʹ�ô��벽���Ŀ���
                if (baseMaterial != null)
                {
                    cubeRenderer.material = new Material(baseMaterial);
                    // ֻ����_OuterColor��_InnerColor������һ��
                    cubeRenderer.material.SetColor("_OuterColor", cubeColor);
                    cubeRenderer.material.SetColor("_InnerColor", cubeColor);
                }
                else
                {
                    // ���û�д��벽�ʣ�ʹ��ԭ�������÷�ʽ
                    cubeRenderer.material.color = cubeColor;
                    cubeRenderer.material.SetFloat("_Mode", 3); // ����Ϊ͸��ģʽ
                    cubeRenderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    cubeRenderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    cubeRenderer.material.SetInt("_ZWrite", 0);
                    cubeRenderer.material.DisableKeyword("_ALPHATEST_ON");
                    cubeRenderer.material.EnableKeyword("_ALPHABLEND_ON");
                    cubeRenderer.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    cubeRenderer.material.renderQueue = 3000;

                    // �����Է���Ч��
                    cubeRenderer.material.SetColor("_EmissionColor", cubeColor);
                    cube.GetComponent<Renderer>().material.EnableKeyword("_EMISSION");
                }
            }

            // 添加刚体
            Rigidbody cubeRigidbody = cube.AddComponent<Rigidbody>();

            // 缩放碰撞体为原来的0.99倍
            BoxCollider cubeCollider = cube.GetComponent<BoxCollider>();
            if (cubeCollider != null)
            {
                cubeCollider.size = cubeCollider.size * 0.99f;
            }

            // 设置物理材质 - 无弹力
            PhysicMaterial noBounceMaterial = new PhysicMaterial("NoBounce");
            noBounceMaterial.bounciness = 0f;        // 弹力为0
            noBounceMaterial.staticFriction = 0.8f;  // 静摩擦力
            noBounceMaterial.dynamicFriction = 0.6f; // 动摩擦力
            noBounceMaterial.frictionCombine = PhysicMaterialCombine.Average;
            noBounceMaterial.bounceCombine = PhysicMaterialCombine.Minimum;
            cubeCollider.material = noBounceMaterial;

            // 调整重力和质量以加快掉落速度
            cubeRigidbody.mass = 35f;               // 进一步增加质量
            cubeRigidbody.drag = 0.05f;             // 进一步减少空气阻力
            cubeRigidbody.angularDrag = 0.02f;      // 进一步减少角度阻力
            
            // 给一个初始向下的力来加速掉落
            Vector3 initialForce = Vector3.down * 1000f; // 向下50单位的力
            cubeRigidbody.AddForce(initialForce);

            // 只冻结x和z位置旋转（允许y轴自由运动和少量旋转）
            cubeRigidbody.constraints = RigidbodyConstraints.FreezeRotationX |
                                        RigidbodyConstraints.FreezeRotationY |
                                         RigidbodyConstraints.FreezeRotationZ |
                                         RigidbodyConstraints.FreezePositionX |
                                            RigidbodyConstraints.FreezePositionZ;
            cubeRigidbody.velocity = new Vector3(0, -100f, 0); // 设置初始速度向下
            
            // 添加碰撞检测组件
            CubeCollisionHandler collisionHandler = cube.AddComponent<CubeCollisionHandler>();
            collisionHandler.Initialize(collisionEffectPrefab, shakeCamera);
            
            cube.tag = "Pickable";
        }
    }

    // 根据代码行号和内容数组生成对应的方块
    public void LineGenerateQ(int LineNum, string[] dataTypes)
    {
         
        for (int i = 0; i < dataTypes.Length; i++)
        {
            if (string.IsNullOrEmpty(dataTypes[i]))
            {
                continue; // 如果数据为空，跳过处理
            }

            // 根据数据类型获取颜色
            Color cubeColor = GetColor(dataTypes[i]);

            // 创建一个立方体
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

            // 在xz = -8到8的范围内随机选择整数位置，y固定为25
            float randomX = Mathf.Round(Random.Range(-8f, 8f));
            float randomZ = Mathf.Round(Random.Range(-8f, 8f));
            cube.transform.position = new Vector3(randomX, 25f, randomZ);

            // ��ȡ����Ĳ��ʲ�������ɫ
            Renderer cubeRenderer = cube.GetComponent<Renderer>();
            if (cubeRenderer != null)
            {
                cubeRenderer.material.color = cubeColor;
                cubeRenderer.material.SetFloat("_Mode", 3); // ����Ϊ͸��ģʽ
                cubeRenderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                cubeRenderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                cubeRenderer.material.SetInt("_ZWrite", 0);
                cubeRenderer.material.DisableKeyword("_ALPHATEST_ON");
                cubeRenderer.material.EnableKeyword("_ALPHABLEND_ON");
                cubeRenderer.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                cubeRenderer.material.renderQueue = 3000;

                // �����Է���Ч��
                cubeRenderer.material.SetColor("_EmissionColor", cubeColor);
                cube.GetComponent<Renderer>().material.EnableKeyword("_EMISSION");
            }

            // 添加刚体
            Rigidbody cubeRigidbody = cube.AddComponent<Rigidbody>();

            // 缩放碰撞体为原来的0.99倍
            BoxCollider cubeCollider = cube.GetComponent<BoxCollider>();
            if (cubeCollider != null)
            {
                cubeCollider.size = cubeCollider.size * 0.99f;
            }

            // 设置物理材质 - 无弹力
            PhysicMaterial noBounceMaterial = new PhysicMaterial("NoBounce");
            noBounceMaterial.bounciness = 0f;        // 弹力为0
            noBounceMaterial.staticFriction = 0.8f;  // 静摩擦力
            noBounceMaterial.dynamicFriction = 0.6f; // 动摩擦力
            noBounceMaterial.frictionCombine = PhysicMaterialCombine.Average;
            noBounceMaterial.bounceCombine = PhysicMaterialCombine.Minimum;
            cubeCollider.material = noBounceMaterial;

            // 调整重力和质量以加快掉落速度
            cubeRigidbody.mass = 35000f;               // 进一步增加质量
            cubeRigidbody.drag = 1f;             // 进一步减少空气阻力
            cubeRigidbody.angularDrag = 0.02f;      // 进一步减少角度阻力
            
            // 给一个初始向下的力来加速掉落
            Vector3 initialForce = Vector3.down * 50f; // 向下50单位的力
            cubeRigidbody.AddForce(initialForce, ForceMode.Impulse);
            
            // 只冻结x和z位置旋转（允许y轴自由运动和少量旋转）
            cubeRigidbody.constraints = RigidbodyConstraints.FreezeRotationX |
                                         RigidbodyConstraints.FreezeRotationZ;
            
            // 添加碰撞检测组件
            CubeCollisionHandler collisionHandler = cube.AddComponent<CubeCollisionHandler>();
            collisionHandler.Initialize(collisionEffectPrefab, shakeCamera);
            
            cube.tag = "Pickable";
        }
    }

    /// <summary>
    /// 处理代码行，使用改进的解析逻辑（参考TextHelper的实现）
    /// </summary>
    /// <param name="lineNumber">行号</param>
    /// <param name="lineContent">行内容</param>
    /// <returns>解析后的token数组</returns>
    public string[] ProcessLine(int lineNumber, string lineContent)
    {
        if (string.IsNullOrEmpty(lineContent))
            return new string[0];

        List<string> tokens = new List<string>();
        
        // 先处理字符串字面量（避免字符串内容被分割）
        var stringMatches = new List<(int start, int end, string value)>();
        if (stringRegex != null)
        {
            foreach (Match match in stringRegex.Matches(lineContent))
            {
                stringMatches.Add((match.Index, match.Index + match.Length, match.Value));
            }
        }
        
        // 处理注释（避免注释内容被分割）
        var commentMatches = new List<(int start, int end, string value)>();
        if (commentRegex != null)
        {
            foreach (Match match in commentRegex.Matches(lineContent))
            {
                commentMatches.Add((match.Index, match.Index + match.Length, match.Value));
            }
        }
        
        // 合并所有需要保护的区域
        var protectedRegions = stringMatches.Concat(commentMatches)
            .OrderBy(x => x.start)
            .ToList();
        
        int currentPos = 0;
        
        foreach (var region in protectedRegions)
        {
            // 处理保护区域之前的普通代码
            if (currentPos < region.start)
            {
                string beforeRegion = lineContent.Substring(currentPos, region.start - currentPos);
                tokens.AddRange(TokenizeRegularCode(beforeRegion));
            }
            
            // 添加保护区域的内容
            tokens.Add(region.value);
            currentPos = region.end;
        }
        
        // 处理最后一个保护区域之后的代码
        if (currentPos < lineContent.Length)
        {
            string remaining = lineContent.Substring(currentPos);
            tokens.AddRange(TokenizeRegularCode(remaining));
        }
        
        // 如果没有保护区域，直接处理整行
        if (protectedRegions.Count == 0)
        {
            tokens.AddRange(TokenizeRegularCode(lineContent));
        }
        
        // 过滤空token
        var result = tokens.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();
        
        Debug.Log($"Line {lineNumber}: Processed and split into {result.Length} tokens: [{string.Join(", ", result)}]");
        
        return result;
    }
    
    /// <summary>
    /// 对普通代码进行分词
    /// </summary>
    /// <param name="code">代码字符串</param>
    /// <returns>分词结果</returns>
    private List<string> TokenizeRegularCode(string code)
    {
        var tokens = new List<string>();
        
        if (string.IsNullOrWhiteSpace(code))
            return tokens;
        
        // 定义分隔符
        char[] separators = {' ', '\t', '(', ')', '{', '}', '[', ']', ',', ';', 
                           ':', '.', '+', '-', '*', '/', '%', '=', '<', '>', '!', '&', '|'};
        
        // 使用更智能的分词逻辑
        var parts = code.Split(separators, System.StringSplitOptions.RemoveEmptyEntries);
        
        foreach (string part in parts)
        {
            string trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                tokens.Add(trimmed);
            }
        }
        
        return tokens;
    }
}

/// <summary>
/// 方块碰撞处理器 - 检测方块落地并生成效果
/// </summary>
public class CubeCollisionHandler : MonoBehaviour
{
    private GameObject effectPrefab;
    private Camera cameraToShake;
    private bool hasCollided = false; // 防止多次触发

    private Rigidbody cubeRigidbody;

    void Start()
    {
        cubeRigidbody = GetComponent<Rigidbody>();
    }

    void Update()
    {
        cubeRigidbody.AddForce(Vector3.down * 10f, ForceMode.Force); // 持续向下施加力，确保方块掉落
    }

    /// <summary>
    /// 初始化碰撞处理器
    /// </summary>
    /// <param name="prefab">要生成的GameObject预制体</param>
    /// <param name="camera">要抖动的相机</param>
    public void Initialize(GameObject prefab, Camera camera)
    {
        effectPrefab = prefab;
        cameraToShake = camera;
    }

    /// <summary>
    /// 碰撞检测 - 当方块与其他物体碰撞时触发
    /// </summary>
    /// <param name="collision">碰撞信息</param>
    private void OnCollisionEnter(Collision collision)
    {
        // 防止多次触发和检查是否有效果预制体
        if (hasCollided || effectPrefab == null) return;
        if (gameObject.transform.position.y > 9) return;

        hasCollided = true;

        // 在方块位置生成效果GameObject
        GameObject effectObject = Instantiate(effectPrefab, transform.position, Quaternion.identity);

        // 设置为激活状态
        effectObject.SetActive(true);

        // 启动协程，5秒后删除效果
        StartCoroutine(DestroyEffectAfterDelay(effectObject, 5f));

        // 触发相机抖动效果
        if (cameraToShake != null)
        {
            // 停止所有现有的相机动画（避免冲突）
            cameraToShake.transform.DOKill();
            
            // 记录相机原始位置
            Vector3 originalPosition = cameraToShake.transform.position;
            
            // 使用DOTween进行相机抖动，完成后回到原位
            // 参数：持续时间0.3秒，抖动强度0.5，振动频率10，随机度90，不淡出
            cameraToShake.DOShakePosition(0.3f, 0.5f, 10, 90, false)
                .SetEase(DG.Tweening.Ease.OutQuad) // 添加缓动效果
                .OnComplete(() => {
                    // 抖动完成后，平滑回到原始位置
                    cameraToShake.transform.DOMove(originalPosition, 0.2f)
                        .SetEase(DG.Tweening.Ease.OutCubic);
                });
            
            Debug.Log($"Camera shake triggered at position: {originalPosition}");
        }
        Debug.Log($"Cube collided at position: {transform.position}, spawned effect: {effectObject.name}");
    }

    /// <summary>
    /// 延迟删除效果的协程
    /// </summary>
    /// <param name="effectObject">要删除的效果对象</param>
    /// <param name="delay">延迟时间（秒）</param>
    /// <returns></returns>
    private System.Collections.IEnumerator DestroyEffectAfterDelay(GameObject effectObject, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (effectObject != null)
        {
            Destroy(effectObject);
        }

        yield return new WaitForSeconds(20);
        Destroy(gameObject.GetComponent<Rigidbody>());
        Destroy(this);
    }
}
