using UnityEngine;

public class RandomCubeSpawner : MonoBehaviour
{
    // 可以生成的数据类型
    private readonly string[] dataTypes = { "int", "return", "char" };


    void Start()
    {
        GenerateRandomCubes(50); // 调用方法生成50行随机方块
    }

    private void Update()
    {
        
    }
    // 随机生成指定行数的方块
    private void GenerateRandomCubes(int totalLines)
    {
        for (int lineNum = 0; lineNum < totalLines; lineNum++)
        {
            int cubesInLine = Random.Range(0, 20); // 每行随机1-4个方块
            string[] randomDataTypes = new string[cubesInLine];

            for (int i = 0; i < cubesInLine; i++)
            {
                // 随机选择数据类型
                randomDataTypes[i] = dataTypes[Random.Range(0, dataTypes.Length)];
            }

            // 使用你的GenerateCube单例实例生成方块
            GenerateCube.Instance.LineGenerateQ(lineNum, randomDataTypes);
        }
    }
}