using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class EnvSettings : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        DebugManager.instance.enableRuntimeUI = false;
        // 隐藏鼠标
        Cursor.visible = false;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
