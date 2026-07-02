using Cultiway.Content;
using HarmonyLib;

namespace Cultiway.Patch;

/// <summary>群系专属树落到其他群系地块上时，直接摧毁。
/// terraformTile 是所有 terraformTop/terraformMain 的汇聚点（玩家用种子刷群系、群系扩散都走这里），
/// 在其后缀里检查地块上的建筑：若是某棵群系专属树且当前地块的 biome 与其归属不一致，就摧毁。</summary>
internal class PatchBiomeTree
{
    [HarmonyPostfix, HarmonyPatch(typeof(MapAction), nameof(MapAction.terraformTile))]
    public static void TerraformTilePostfix(WorldTile pTile, bool pSkipTerraform)
    {
        if (pSkipTerraform) return;
        Building b = pTile.building;
        if (b == null) return;
        if (!Buildings.BiomeTreeHome.TryGetValue(b.asset.id, out string home_biome)) return;
        if (pTile.Type.biome_asset?.id != home_biome)
        {
            b.startDestroyBuilding();
        }
    }
}
