using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RbtPlayerCtrl : MonoBehaviour
{
    private RbtAction3D rbtAction; // ���ڵ��û�������Ϊ�ű�
    private Rigidbody rb; // �����˵ĸ������
    RbtAbsorb rbt; // ���ڵ������սű�


    public Vector2 flyTar;
    public bool isFlyTo=false;
    public bool isMove=false;
    AIAction moveAction = new AIAction
    {
        actionType = "move",
        targetPosition = new Vector3(7, 0, 2) // �ƶ��� x=7
    };

    public Vector3 newTar;

    // Start is called before the first frame update
    void Start()
    {


        

        // ��ȡ�������ϵ� RbtAction �ű�
        rbtAction = GetComponent<RbtAction3D>();
        if (rbtAction == null)
        {
            Debug.LogError("δ�ҵ� RbtAction �ű���");
        }

        // ��ȡ�������
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("δ�ҵ� Rigidbody �����");
        }
    }

    // Update is called once per frame
    void Update()
    {
        
        if (isMove)
        {
            moveAction.targetPosition = newTar;
            GetComponent<RbtAction3D>().ExecuteAction(moveAction);
            isMove = false;
        }
        
        HandleInput();

    }

    // ������������
    private void HandleInput()
    {
    }



}