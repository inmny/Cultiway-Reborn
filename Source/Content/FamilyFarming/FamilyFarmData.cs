using System.Collections.Generic;
using Newtonsoft.Json;

namespace Cultiway.Content.FamilyFarming;

/// <summary>一个村庄的家庭田块设置与经营记录，作物和粮食仍用原版存档。</summary>
public sealed class FamilyFarmData
{
    /// <summary>一名农民起步田块的宽度。</summary>
    public int Width = 6;
    /// <summary>一名农民起步田块的高度。</summary>
    public int Height = 8;
    /// <summary>同一家庭在本村可以经营的面积上限。</summary>
    public int MaxArea = 48;
    /// <summary>下一块田地的稳定编号。</summary>
    public int NextId = 1;
    /// <summary>已经登记的矩形田地。</summary>
    public List<FamilyFarmPlot> Plots = new();
    /// <summary>开垦前的地表编号，空值表示裸地。</summary>
    public Dictionary<int, string> Grounds = new();
    /// <summary>退耕地格与允许最后收获的那一株成熟作物编号。</summary>
    public Dictionary<int, long> Retiring = new();

    /// <summary>验证玩家输入的田块尺寸和家庭面积上限。</summary>
    /// <param name="width">起步宽度。</param>
    /// <param name="height">起步高度。</param>
    /// <param name="maxArea">每户面积上限。</param>
    public static bool ValidSettings(int width, int height, int maxArea) =>
        width is >= 1 and <= 12 && height is >= 1 and <= 12 && maxArea >= width * height && maxArea <= 144;
}

/// <summary>由一个家庭或单人农户经营的完整矩形。</summary>
public sealed class FamilyFarmPlot
{
    /// <summary>村内唯一田块编号。</summary>
    public int Id;
    /// <summary>家庭编号；负数为未成家的个人编号，零为无主田。</summary>
    public long Owner;
    /// <summary>矩形左下角横坐标。</summary>
    public int X;
    /// <summary>矩形左下角纵坐标。</summary>
    public int Y;
    /// <summary>矩形宽度。</summary>
    public int Width;
    /// <summary>矩形高度。</summary>
    public int Height;
    /// <summary>矩形包含的地格数。</summary>
    [JsonIgnore] public int Area => Width * Height;

    /// <summary>顺序枚举田块中的地格编号。</summary>
    public IEnumerable<int> Cells()
    {
        for (int y = Y; y < Y + Height; y++)
        for (int x = X; x < X + Width; x++) yield return x + y * MapBox.width;
    }
}
