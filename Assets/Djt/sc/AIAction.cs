using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIAction : MonoBehaviour
{

    public string actionType; // �� "move", "pick", "build"
    public Vector3 targetPosition; // Ŀ��λ��

    public void ACinit(string type,Vector3 tPosition)
    {
        actionType = type;
        targetPosition=tPosition;
    }
}
