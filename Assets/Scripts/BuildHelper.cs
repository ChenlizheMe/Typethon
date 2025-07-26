using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildHelper : MonoBehaviour
{
    public static BuildHelper Instance;
    
    // 存储未完成的建筑方块
    private List<Vector3> pendingBlocks = new List<Vector3>();
    
    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// 根据坐标和可用方块数量随机生成建筑物（支持补建机制）
    /// </summary>
    /// <param name="basePosition">建筑物的基础坐标</param>
    /// <param name="availableBlocks">可用的方块数量</param>
    /// <param name="unbuiltBlocks">无法建造的方块列表（输出参数）</param>
    /// <returns>返回可以建造的建筑物方块坐标列表</returns>
    public List<Vector3> GenerateRandomBuilding(Vector3 basePosition, int availableBlocks, out List<Vector3> unbuiltBlocks)
    {
        List<Vector3> fullBuildingBlocks = new List<Vector3>();
        bool isRebuilding = false;
        
        // 判断是否有未完成的建筑需要补建
        if (pendingBlocks.Count > 0)
        {
            float rebuildChance = Random.Range(0f, 1f);
            if (rebuildChance <= 0.7f) // 70%概率补建
            {
                // 使用未完成的建筑方块
                fullBuildingBlocks = new List<Vector3>(pendingBlocks);
                isRebuilding = true;
                Debug.Log($"选择补建，剩余未建造方块: {pendingBlocks.Count}");
            }
            else
            {
                // 30%概率新建，将之前的未完成方块加入到新的未建造列表
                Debug.Log($"选择新建，放弃之前的 {pendingBlocks.Count} 个未建造方块");
            }
        }
        
        // 如果不是补建，则随机生成新建筑
        if (!isRebuilding)
        {
            // 随机选择建筑类型
            int randomBuildingType = Random.Range(1, 7); // 1-6种建筑类型
            
            switch (randomBuildingType)
            {
                case 1:
                    fullBuildingBlocks = GenerateSimpleWoodenHouse(basePosition);
                    break;
                case 2:
                    fullBuildingBlocks = GenerateSmallTower(basePosition);
                    break;
                case 3:
                    fullBuildingBlocks = GenerateSimpleBridge(basePosition);
                    break;
                case 4:
                    fullBuildingBlocks = GenerateCross(basePosition);
                    break;
                case 5:
                    fullBuildingBlocks = GenerateWell(basePosition);
                    break;
                case 6:
                    fullBuildingBlocks = GenerateFarmland(basePosition);
                    break;
                default:
                    fullBuildingBlocks = GenerateSimpleWoodenHouse(basePosition);
                    break;
            }
            
            Debug.Log($"随机生成新建筑类型: {randomBuildingType}");
        }
        
        // 分离可建造和不可建造的方块
        List<Vector3> buildableBlocks = new List<Vector3>();
        unbuiltBlocks = new List<Vector3>();
        
        for (int i = 0; i < fullBuildingBlocks.Count; i++)
        {
            if (i < availableBlocks)
            {
                buildableBlocks.Add(fullBuildingBlocks[i]);
            }
            else
            {
                unbuiltBlocks.Add(fullBuildingBlocks[i]);
            }
        }
        
        // 更新待建造方块列表
        if (isRebuilding)
        {
            // 如果是补建，更新剩余的未建造方块
            pendingBlocks = new List<Vector3>(unbuiltBlocks);
        }
        else
        {
            // 如果是新建
            if (pendingBlocks.Count == 0)
            {
                // 如果之前没有待建造方块，则设置新建筑的未建造部分
                pendingBlocks = new List<Vector3>(unbuiltBlocks);
            }
            // 如果之前有待建造方块，保持不变，新建筑的未建造部分只通过 unbuiltBlocks 返回
        }
        
        string buildType = isRebuilding ? "补建" : "新建";
        Debug.Log($"{buildType} - 总方块: {fullBuildingBlocks.Count}, 可建造: {buildableBlocks.Count}, 未建造: {unbuiltBlocks.Count}");
        
        return buildableBlocks;
    }
    
    /// <summary>
    /// 获取当前待建造方块的数量
    /// </summary>
    /// <returns>待建造方块数量</returns>
    public int GetPendingBlocksCount()
    {
        return pendingBlocks.Count;
    }
    
    /// <summary>
    /// 清空所有待建造的方块
    /// </summary>
    public void ClearPendingBlocks()
    {
        pendingBlocks.Clear();
        Debug.Log("已清空所有待建造方块");
    }
    
    /// <summary>
    /// 获取待建造方块的副本（用于调试或显示）
    /// </summary>
    /// <returns>待建造方块列表的副本</returns>
    public List<Vector3> GetPendingBlocksCopy()
    {
        return new List<Vector3>(pendingBlocks);
    }
    
    /// <summary>
    /// 生成简单小木屋的方块列表
    /// </summary>
    /// <param name="basePos">小木屋的基础坐标</param>
    /// <returns>组成小木屋的方块坐标列表</returns>
    private List<Vector3> GenerateSimpleWoodenHouse(Vector3 basePos)
    {
        List<Vector3> houseBlocks = new List<Vector3>();
        
        // 地基 (5x5)
        for (int x = 0; x < 5; x++)
        {
            for (int z = 0; z < 5; z++)
            {
                houseBlocks.Add(basePos + new Vector3(x, 0, z));
            }
        }
        
        // 墙壁 (高度为3)
        for (int y = 1; y <= 3; y++)
        {
            // 前墙和后墙
            for (int x = 0; x < 5; x++)
            {
                houseBlocks.Add(basePos + new Vector3(x, y, 0)); // 前墙
                houseBlocks.Add(basePos + new Vector3(x, y, 4)); // 后墙
            }
            
            // 左墙和右墙 (避免重复角落)
            for (int z = 1; z < 4; z++)
            {
                houseBlocks.Add(basePos + new Vector3(0, y, z)); // 左墙
                houseBlocks.Add(basePos + new Vector3(4, y, z)); // 右墙
            }
        }
        
        // 屋顶 (简单平屋顶)
        for (int x = 0; x < 5; x++)
        {
            for (int z = 0; z < 5; z++)
            {
                houseBlocks.Add(basePos + new Vector3(x, 4, z));
            }
        }
        
        // 门口 (移除前墙的中间部分作为门)
        houseBlocks.Remove(basePos + new Vector3(2, 1, 0));
        houseBlocks.Remove(basePos + new Vector3(2, 2, 0));
        
        // 窗户 (移除侧墙的一些方块作为窗户)
        houseBlocks.Remove(basePos + new Vector3(4, 2, 2)); // 右墙窗户
        houseBlocks.Remove(basePos + new Vector3(0, 2, 2)); // 左墙窗户
        
        return houseBlocks;
    }
    
    /// <summary>
    /// 生成小塔的方块列表
    /// </summary>
    /// <param name="basePos">小塔的基础坐标</param>
    /// <returns>组成小塔的方块坐标列表</returns>
    private List<Vector3> GenerateSmallTower(Vector3 basePos)
    {
        List<Vector3> towerBlocks = new List<Vector3>();
        
        // 塔基 (3x3)
        for (int x = 0; x < 3; x++)
        {
            for (int z = 0; z < 3; z++)
            {
                towerBlocks.Add(basePos + new Vector3(x, 0, z));
            }
        }
        
        // 塔身 (高度为6)
        for (int y = 1; y <= 6; y++)
        {
            // 四周墙壁
            for (int x = 0; x < 3; x++)
            {
                towerBlocks.Add(basePos + new Vector3(x, y, 0)); // 前墙
                towerBlocks.Add(basePos + new Vector3(x, y, 2)); // 后墙
            }
            for (int z = 1; z < 2; z++)
            {
                towerBlocks.Add(basePos + new Vector3(0, y, z)); // 左墙
                towerBlocks.Add(basePos + new Vector3(2, y, z)); // 右墙
            }
        }
        
        // 塔顶
        for (int x = 0; x < 3; x++)
        {
            for (int z = 0; z < 3; z++)
            {
                towerBlocks.Add(basePos + new Vector3(x, 7, z));
            }
        }
        
        // 门口
        towerBlocks.Remove(basePos + new Vector3(1, 1, 0));
        towerBlocks.Remove(basePos + new Vector3(1, 2, 0));
        
        // 窗户
        towerBlocks.Remove(basePos + new Vector3(0, 4, 1)); // 左墙窗户
        towerBlocks.Remove(basePos + new Vector3(2, 4, 1)); // 右墙窗户
        
        return towerBlocks;
    }
    
    /// <summary>
    /// 生成简单桥梁的方块列表
    /// </summary>
    /// <param name="basePos">桥梁的基础坐标</param>
    /// <returns>组成桥梁的方块坐标列表</returns>
    private List<Vector3> GenerateSimpleBridge(Vector3 basePos)
    {
        List<Vector3> bridgeBlocks = new List<Vector3>();
        
        // 桥面 (长度为8，宽度为3)
        for (int x = 0; x < 8; x++)
        {
            for (int z = 0; z < 3; z++)
            {
                bridgeBlocks.Add(basePos + new Vector3(x, 0, z));
            }
        }
        
        // 桥梁栏杆 (两侧)
        for (int x = 0; x < 8; x++)
        {
            bridgeBlocks.Add(basePos + new Vector3(x, 1, 0)); // 左侧栏杆
            bridgeBlocks.Add(basePos + new Vector3(x, 1, 2)); // 右侧栏杆
        }
        
        // 桥墩 (两端)
        for (int y = -2; y < 0; y++)
        {
            // 起始端桥墩
            bridgeBlocks.Add(basePos + new Vector3(0, y, 0));
            bridgeBlocks.Add(basePos + new Vector3(0, y, 1));
            bridgeBlocks.Add(basePos + new Vector3(0, y, 2));
            
            // 结束端桥墩
            bridgeBlocks.Add(basePos + new Vector3(7, y, 0));
            bridgeBlocks.Add(basePos + new Vector3(7, y, 1));
            bridgeBlocks.Add(basePos + new Vector3(7, y, 2));
        }
        
        return bridgeBlocks;
    }
    
    /// <summary>
    /// 生成十字架的方块列表
    /// </summary>
    /// <param name="basePos">十字架的基础坐标</param>
    /// <returns>组成十字架的方块坐标列表</returns>
    private List<Vector3> GenerateCross(Vector3 basePos)
    {
        List<Vector3> crossBlocks = new List<Vector3>();
        
        // 十字架底座 (3x3)
        for (int x = 0; x < 3; x++)
        {
            for (int z = 0; z < 3; z++)
            {
                crossBlocks.Add(basePos + new Vector3(x, 0, z));
            }
        }
        
        // 垂直支柱 (中心位置，高度为5)
        for (int y = 1; y <= 5; y++)
        {
            crossBlocks.Add(basePos + new Vector3(1, y, 1));
        }
        
        // 水平横梁 (在高度3处，长度为3)
        for (int x = 0; x < 3; x++)
        {
            crossBlocks.Add(basePos + new Vector3(x, 3, 1));
        }
        
        return crossBlocks;
    }
    
    /// <summary>
    /// 生成水井的方块列表
    /// </summary>
    /// <param name="basePos">水井的基础坐标</param>
    /// <returns>组成水井的方块坐标列表</returns>
    private List<Vector3> GenerateWell(Vector3 basePos)
    {
        List<Vector3> wellBlocks = new List<Vector3>();
        
        // 水井基础平台 (5x5)
        for (int x = 0; x < 5; x++)
        {
            for (int z = 0; z < 5; z++)
            {
                wellBlocks.Add(basePos + new Vector3(x, 0, z));
            }
        }
        
        // 水井外壁 (3x3的空心圆形近似，高度为2)
        for (int y = 1; y <= 2; y++)
        {
            // 外圈方块
            wellBlocks.Add(basePos + new Vector3(1, y, 1));
            wellBlocks.Add(basePos + new Vector3(1, y, 2));
            wellBlocks.Add(basePos + new Vector3(1, y, 3));
            wellBlocks.Add(basePos + new Vector3(2, y, 1));
            wellBlocks.Add(basePos + new Vector3(2, y, 3));
            wellBlocks.Add(basePos + new Vector3(3, y, 1));
            wellBlocks.Add(basePos + new Vector3(3, y, 2));
            wellBlocks.Add(basePos + new Vector3(3, y, 3));
        }
        
        // 水井支撑柱 (两个支柱)
        for (int y = 3; y <= 4; y++)
        {
            wellBlocks.Add(basePos + new Vector3(1, y, 2)); // 左支柱
            wellBlocks.Add(basePos + new Vector3(3, y, 2)); // 右支柱
        }
        
        // 水井顶梁
        wellBlocks.Add(basePos + new Vector3(1, 5, 2));
        wellBlocks.Add(basePos + new Vector3(2, 5, 2));
        wellBlocks.Add(basePos + new Vector3(3, 5, 2));
        
        return wellBlocks;
    }
    
    /// <summary>
    /// 生成农田的方块列表
    /// </summary>
    /// <param name="basePos">农田的基础坐标</param>
    /// <returns>组成农田的方块坐标列表</returns>
    private List<Vector3> GenerateFarmland(Vector3 basePos)
    {
        List<Vector3> farmBlocks = new List<Vector3>();
        
        // 农田土地 (7x5的矩形)
        for (int x = 0; x < 7; x++)
        {
            for (int z = 0; z < 5; z++)
            {
                farmBlocks.Add(basePos + new Vector3(x, 0, z));
            }
        }
        
        // 农田边界围栏 (四周)
        for (int x = -1; x <= 7; x++)
        {
            farmBlocks.Add(basePos + new Vector3(x, 1, -1)); // 前边界
            farmBlocks.Add(basePos + new Vector3(x, 1, 5));  // 后边界
        }
        for (int z = 0; z < 5; z++)
        {
            farmBlocks.Add(basePos + new Vector3(-1, 1, z)); // 左边界
            farmBlocks.Add(basePos + new Vector3(7, 1, z));  // 右边界
        }
        
        // 作物行 (农田上方的作物方块)
        for (int x = 1; x < 6; x += 2) // 每隔一格种植
        {
            for (int z = 1; z < 4; z += 2)
            {
                farmBlocks.Add(basePos + new Vector3(x, 1, z));
            }
        }
        
        // 稻草人 (农田一角)
        Vector3 scarecrowPos = basePos + new Vector3(6, 1, 4);
        farmBlocks.Add(scarecrowPos); // 稻草人底座
        farmBlocks.Add(scarecrowPos + Vector3.up); // 稻草人身体
        farmBlocks.Add(scarecrowPos + Vector3.up * 2); // 稻草人头部
        
        // 稻草人手臂
        farmBlocks.Add(scarecrowPos + Vector3.up + Vector3.left);
        farmBlocks.Add(scarecrowPos + Vector3.up + Vector3.right);
        
        return farmBlocks;
    }
}
