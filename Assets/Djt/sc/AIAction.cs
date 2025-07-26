using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIAction : MonoBehaviour
{

    public string actionType; // 如 "move", "pick", "build"
    public Vector3 targetPosition; // 目标位置

    public void ACinit(string type,Vector3 tPosition)
    {
        actionType = type;
        targetPosition=tPosition;
    }
}
