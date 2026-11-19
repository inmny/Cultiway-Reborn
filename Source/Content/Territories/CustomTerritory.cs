using System.Collections.Generic;

namespace Cultiway.Content.Territories;

/// <summary>一块由玩家绘制、最多属于一个村庄的地区。</summary>
internal sealed class CustomTerritory
{
    /// <summary>本世界内的地区编号。</summary>
    public int Id;
    /// <summary>玩家设置的地区名称；未命名时按编号显示。</summary>
    public string Name;
    /// <summary>所属村庄编号；零表示尚未建村。</summary>
    public long CityId;
    /// <summary>组成这块地区的原版格子编号。</summary>
    public HashSet<int> Zones = new();
}

/// <summary>随原版世界保存的全部定制地区。</summary>
internal sealed class TerritorySaveData
{
    /// <summary>下次新建地区使用的编号。</summary>
    public int NextId = 1;
    /// <summary>本世界已经绘制的地区。</summary>
    public List<CustomTerritory> Regions = new();
}
