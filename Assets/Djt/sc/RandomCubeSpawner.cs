using UnityEngine;

public class RandomCubeSpawner : MonoBehaviour
{
    // �������ɵ���������
    private readonly string[] dataTypes = { "int", "return", "char" };


    void Start()
    {
        GenerateRandomCubes(50); // ���÷�������50���������
    }

    private void Update()
    {
        
    }
    // �������ָ�������ķ���
    private void GenerateRandomCubes(int totalLines)
    {
        for (int lineNum = 0; lineNum < totalLines; lineNum++)
        {
            int cubesInLine = Random.Range(0, 20); // ÿ�����1-4������
            string[] randomDataTypes = new string[cubesInLine];

            for (int i = 0; i < cubesInLine; i++)
            {
                // ���ѡ����������
                randomDataTypes[i] = dataTypes[Random.Range(0, dataTypes.Length)];
            }

            // ʹ�����GenerateCube����ʵ�����ɷ���
            GenerateCube.Instance.LineGenerateQ(lineNum, randomDataTypes);
        }
    }
}