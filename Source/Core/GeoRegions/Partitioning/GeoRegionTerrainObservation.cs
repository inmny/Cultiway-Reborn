using System;

namespace Cultiway.Core.GeoRegions.Partitioning;

/// <summary>
/// 某格较稳定的地面结构，记录它是陆地、水、熔岩、阻挡物等。只有这部分变化才会改变地区的连接方式。
/// </summary>
internal readonly struct GeoRegionTerrainStructure : IEquatable<GeoRegionTerrainStructure>
{
    /// <summary>创建某格的稳定地面结构记录。</summary>
    internal GeoRegionTerrainStructure(
        GeoRegionTerrainLayer layer,
        GeoRegionTerrainKind terrainKind,
        string tileTypeId,
        bool isOceanMaterial,
        bool isFillablePit,
        bool isLava,
        bool isGoo,
        bool isMountain)
    {
        Layer = layer;
        TerrainKind = terrainKind;
        TileTypeId = tileTypeId ?? string.Empty;
        IsOceanMaterial = isOceanMaterial;
        IsFillablePit = isFillablePit;
        IsLava = isLava;
        IsGoo = isGoo;
        IsMountain = isMountain;
    }

    /// <summary>游戏中该格原本所在的地面层。</summary>
    internal GeoRegionTerrainLayer Layer { get; }
    /// <summary>分区时认定的地面种类。</summary>
    internal GeoRegionTerrainKind TerrainKind { get; }
    /// <summary>该格地块材质的标识；没有时为空字符串。</summary>
    internal string TileTypeId { get; }
    /// <summary>该格材质是否属于海洋材质。</summary>
    internal bool IsOceanMaterial { get; }
    /// <summary>该格是否是可被填平的坑。</summary>
    internal bool IsFillablePit { get; }
    /// <summary>该格是否属于熔岩。</summary>
    internal bool IsLava { get; }
    /// <summary>该格是否属于黏液。</summary>
    internal bool IsGoo { get; }
    /// <summary>该格是否属于山体。</summary>
    internal bool IsMountain { get; }

    /// <summary>判断两份稳定地面结构记录是否完全相同。</summary>
    public bool Equals(GeoRegionTerrainStructure other)
    {
        return Layer == other.Layer &&
               TerrainKind == other.TerrainKind &&
               string.Equals(TileTypeId, other.TileTypeId, StringComparison.Ordinal) &&
               IsOceanMaterial == other.IsOceanMaterial &&
               IsFillablePit == other.IsFillablePit &&
               IsLava == other.IsLava &&
               IsGoo == other.IsGoo &&
               IsMountain == other.IsMountain;
    }

    /// <summary>判断传入对象是否是内容相同的地面记录。</summary>
    public override bool Equals(object obj)
    {
        return obj is GeoRegionTerrainStructure other && Equals(other);
    }

    /// <summary>根据记录内容生成用于比较和查表的哈希值。</summary>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (int)Layer;
            hash = hash * 397 ^ (int)TerrainKind;
            hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(TileTypeId ?? string.Empty);
            hash = hash * 397 ^ (IsOceanMaterial ? 1 : 0);
            hash = hash * 397 ^ (IsFillablePit ? 1 : 0);
            hash = hash * 397 ^ (IsLava ? 1 : 0);
            hash = hash * 397 ^ (IsGoo ? 1 : 0);
            hash = hash * 397 ^ (IsMountain ? 1 : 0);
            return hash;
        }
    }
}

/// <summary>
/// 某格通常显示的地表和生物群系，用于判断主要地表及陆地外形，不决定陆水是否相连。
/// </summary>
internal readonly struct GeoRegionTerrainSurface : IEquatable<GeoRegionTerrainSurface>
{
    /// <summary>创建某格通常地表的记录。</summary>
    internal GeoRegionTerrainSurface(
        GeoRegionPrimaryCategoryCode primaryBiomeCode,
        string tileTypeId,
        string biomeId,
        bool isBeachMaterial)
    {
        PrimaryBiomeCode = primaryBiomeCode;
        TileTypeId = tileTypeId ?? string.Empty;
        BiomeId = biomeId ?? string.Empty;
        IsBeachMaterial = isBeachMaterial;
    }

    /// <summary>该格生物群系归入的主要地表类别。</summary>
    internal GeoRegionPrimaryCategoryCode PrimaryBiomeCode { get; }
    /// <summary>该格通常使用的地块材质标识。</summary>
    internal string TileTypeId { get; }
    /// <summary>该格生物群系的标识；没有时为空字符串。</summary>
    internal string BiomeId { get; }
    /// <summary>通常材质是否算作海滩。</summary>
    internal bool IsBeachMaterial { get; }

    /// <summary>判断两份通常地表记录是否完全相同。</summary>
    public bool Equals(GeoRegionTerrainSurface other)
    {
        return PrimaryBiomeCode == other.PrimaryBiomeCode &&
               string.Equals(TileTypeId, other.TileTypeId, StringComparison.Ordinal) &&
               string.Equals(BiomeId, other.BiomeId, StringComparison.Ordinal) &&
               IsBeachMaterial == other.IsBeachMaterial;
    }

    /// <summary>判断传入对象是否是内容相同的地面记录。</summary>
    public override bool Equals(object obj)
    {
        return obj is GeoRegionTerrainSurface other && Equals(other);
    }

    /// <summary>根据记录内容生成用于比较和查表的哈希值。</summary>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (int)PrimaryBiomeCode;
            hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(TileTypeId ?? string.Empty);
            hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(BiomeId ?? string.Empty);
            hash = hash * 397 ^ (IsBeachMaterial ? 1 : 0);
            return hash;
        }
    }
}

/// <summary>
/// 某格后来形成、且可以撤销的地表覆盖。只有持续时间达到要求后，才用它代替通常地表参与分类。
/// </summary>
internal readonly struct GeoRegionTerrainOverlay : IEquatable<GeoRegionTerrainOverlay>
{
    /// <summary>创建某格后来形成的地表覆盖记录。</summary>
    internal GeoRegionTerrainOverlay(
        bool active,
        GeoRegionPrimaryCategoryCode primaryOverrideCode,
        string tileTypeId,
        bool isBeachMaterial)
    {
        Active = active;
        PrimaryOverrideCode = primaryOverrideCode;
        TileTypeId = tileTypeId ?? string.Empty;
        IsBeachMaterial = isBeachMaterial;
    }

    /// <summary>这份覆盖当前是否已达到生效条件。</summary>
    internal bool Active { get; }
    /// <summary>覆盖生效后要改用的主要地表类别。</summary>
    internal GeoRegionPrimaryCategoryCode PrimaryOverrideCode { get; }
    /// <summary>覆盖生效后要使用的地块材质标识。</summary>
    internal string TileTypeId { get; }
    /// <summary>覆盖材质是否算作海滩。</summary>
    internal bool IsBeachMaterial { get; }

    /// <summary>判断两份地表覆盖记录是否完全相同。</summary>
    public bool Equals(GeoRegionTerrainOverlay other)
    {
        return Active == other.Active &&
               PrimaryOverrideCode == other.PrimaryOverrideCode &&
               string.Equals(TileTypeId, other.TileTypeId, StringComparison.Ordinal) &&
               IsBeachMaterial == other.IsBeachMaterial;
    }

    /// <summary>判断传入对象是否是内容相同的地面记录。</summary>
    public override bool Equals(object obj)
    {
        return obj is GeoRegionTerrainOverlay other && Equals(other);
    }

    /// <summary>根据记录内容生成用于比较和查表的哈希值。</summary>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Active ? 1 : 0;
            hash = hash * 397 ^ (int)PrimaryOverrideCode;
            hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(TileTypeId ?? string.Empty);
            hash = hash * 397 ^ (IsBeachMaterial ? 1 : 0);
            return hash;
        }
    }
}

/// <summary>
/// 对一个地图格的完整记录，分别保存稳定结构、通常地表和临时覆盖，供分区计算使用。
/// </summary>
internal readonly struct GeoRegionTerrainObservation : IEquatable<GeoRegionTerrainObservation>
{
    /// <summary>把某格的稳定结构、通常地表和临时覆盖合并成一份观测记录。</summary>
    internal GeoRegionTerrainObservation(
        GeoRegionTerrainStructure structure,
        GeoRegionTerrainSurface surface,
        GeoRegionTerrainOverlay overlay)
    {
        Structure = structure;
        Surface = surface;
        Overlay = overlay;
    }

    /// <summary>该格较稳定、会影响连接关系的地面结构。</summary>
    internal GeoRegionTerrainStructure Structure { get; }
    /// <summary>该格通常采用的地表和生物群系。</summary>
    internal GeoRegionTerrainSurface Surface { get; }
    /// <summary>该格后来形成且可能撤销的覆盖地表。</summary>
    internal GeoRegionTerrainOverlay Overlay { get; }

    /// <summary>替换稳定结构，保留同一格的通常地表和覆盖记录。</summary>
    internal GeoRegionTerrainObservation WithStructure(GeoRegionTerrainStructure value)
    {
        return new GeoRegionTerrainObservation(value, Surface, Overlay);
    }

    /// <summary>替换通常地表，保留同一格的稳定结构和覆盖记录。</summary>
    internal GeoRegionTerrainObservation WithSurface(GeoRegionTerrainSurface value)
    {
        return new GeoRegionTerrainObservation(Structure, value, Overlay);
    }

    /// <summary>替换覆盖记录，保留同一格的稳定结构和通常地表。</summary>
    internal GeoRegionTerrainObservation WithOverlay(GeoRegionTerrainOverlay value)
    {
        return new GeoRegionTerrainObservation(Structure, Surface, value);
    }

    /// <summary>按覆盖是否生效，整理出该格当前真正用于分区的地面数据。</summary>
    internal GeoRegionTerrainCell Compose()
    {
        string tileTypeId = Overlay.Active && !string.IsNullOrEmpty(Overlay.TileTypeId)
            ? Overlay.TileTypeId
            : Surface.TileTypeId;
        if (string.IsNullOrEmpty(tileTypeId)) tileTypeId = Structure.TileTypeId;

        return new GeoRegionTerrainCell(
            Structure.Layer,
            Structure.TerrainKind,
            Overlay.Active && Overlay.PrimaryOverrideCode != GeoRegionPrimaryCategoryCode.None
                ? Overlay.PrimaryOverrideCode
                : Surface.PrimaryBiomeCode,
            tileTypeId,
            Surface.BiomeId,
            Structure.IsOceanMaterial,
            Overlay.Active ? Overlay.IsBeachMaterial : Surface.IsBeachMaterial,
            Structure.IsFillablePit,
            Structure.IsLava,
            Structure.IsGoo,
            Structure.IsMountain);
    }

    /// <summary>判断两份完整观测记录是否完全相同。</summary>
    public bool Equals(GeoRegionTerrainObservation other)
    {
        return Structure.Equals(other.Structure) &&
               Surface.Equals(other.Surface) &&
               Overlay.Equals(other.Overlay);
    }

    /// <summary>判断传入对象是否是内容相同的地面记录。</summary>
    public override bool Equals(object obj)
    {
        return obj is GeoRegionTerrainObservation other && Equals(other);
    }

    /// <summary>根据记录内容生成用于比较和查表的哈希值。</summary>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Structure.GetHashCode();
            hash = hash * 397 ^ Surface.GetHashCode();
            hash = hash * 397 ^ Overlay.GetHashCode();
            return hash;
        }
    }
}
