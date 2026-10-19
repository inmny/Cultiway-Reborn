using System.Collections.Generic;
using Cultiway.Core.SubWorlds.Model;
using Cultiway.Core.SubWorlds.Objects;

namespace Cultiway.Core.SubWorlds;

/// <summary>当前世界内全部小世界的稳定记录。</summary>
internal sealed class SubWorldSaveData
{
    /// <summary>分配器不复用已删除小世界的编号。</summary>
    public long NextId;
    /// <summary>各实例拥有独立版本，失效模板只影响对应实例。</summary>
    public List<SubWorldInstanceSaveData> Instances = new();
}

/// <summary>一个小世界的真实地图、局部时间与本地对象。</summary>
internal sealed class SubWorldInstanceSaveData
{
    /// <summary>实例记录版本。</summary>
    public int Version = 1;
    /// <summary>稳定实例编号。</summary>
    public long Id;
    /// <summary>模板编号，读取时沿用当前表现与规则。</summary>
    public string Template;
    /// <summary>创建种子。</summary>
    public int Seed;
    /// <summary>主世界入口横坐标。</summary>
    public int AnchorX;
    /// <summary>主世界入口纵坐标。</summary>
    public int AnchorY;
    /// <summary>实际修改后的地块。</summary>
    public SubWorldMapData Map;
    /// <summary>命名出生点。</summary>
    public Dictionary<string, int> SpawnPoints;
    /// <summary>已运行局部秒数。</summary>
    public double LocalTime;
    /// <summary>已完成逻辑步数。</summary>
    public long Tick;
    /// <summary>未结算的不足一步时间。</summary>
    public double Accumulator;
    /// <summary>当前速度；零表示局部暂停。</summary>
    public float Speed;
    /// <summary>暂停前速度。</summary>
    public float RunningSpeed;
    /// <summary>本地建筑编号分配器。</summary>
    public int NextObjectId;
    /// <summary>本地人物及其移动目标。</summary>
    public List<SubWorldActorSaveData> Actors = new();
    /// <summary>本地建筑及其稳定编号。</summary>
    public List<SubWorldBuildingSaveData> Buildings = new();
}

/// <summary>本地人物当前状态，不保存寻路后台句柄。</summary>
internal sealed class SubWorldActorSaveData
{
    /// <summary>人物资产编号。</summary>
    public string Asset;
    /// <summary>当前位置横坐标。</summary>
    public float X;
    /// <summary>当前位置纵坐标。</summary>
    public float Y;
    /// <summary>显示高度。</summary>
    public float Z;
    /// <summary>每秒移动格数。</summary>
    public float Speed;
    /// <summary>当前已到达地块。</summary>
    public int Tile;
    /// <summary>最终移动目标。</summary>
    public int Target;
    /// <summary>已经开始行走的下一格。</summary>
    public int NextTile;
    /// <summary>显示变体。</summary>
    public int Variant;
    /// <summary>显示阶段。</summary>
    public SubWorldVisualState Visual;
    /// <summary>是否为玩家可直接控制的测试人物。</summary>
    public bool Controllable;
}

/// <summary>本地建筑的稳定身份与显示阶段。</summary>
internal sealed class SubWorldBuildingSaveData
{
    /// <summary>本地稳定编号。</summary>
    public int Id;
    /// <summary>建筑资产编号。</summary>
    public string Asset;
    /// <summary>当前位置横坐标。</summary>
    public float X;
    /// <summary>当前位置纵坐标。</summary>
    public float Y;
    /// <summary>显示高度。</summary>
    public float Z;
    /// <summary>显示变体。</summary>
    public int Variant;
    /// <summary>显示阶段。</summary>
    public SubWorldVisualState Visual;
}
