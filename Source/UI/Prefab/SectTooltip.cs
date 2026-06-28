using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.UI.Prefab;

public class SectTooltip : APrefabPreview<SectTooltip>
{
    public Tooltip Tooltip { get; private set; }
    private SectBanner _banner;

    protected override void OnInit()
    {
        Tooltip = GetComponent<Tooltip>();
        _banner = GetComponentInChildren<SectBanner>(true);
    }

    public void Setup(Sect sect)
    {
        Init();
        if (_banner == null)
        {
            throw new System.InvalidOperationException("Sect tooltip 缺少宗门 banner");
        }

        _banner.load(sect);
    }

    private static void _init()
    {
        GameObject obj = Instantiate(Resources.Load<GameObject>("tooltips/tooltip_kingdom"), ModClass.I.PrefabLibrary);
        obj.name = "tooltip_cultiway_sect";
        obj.transform.HideChildrenByPath("Traits Background");
        ReplaceKingdomBanners(obj);

        Prefab = obj.AddComponent<SectTooltip>();
    }

    private static void ReplaceKingdomBanners(GameObject obj)
    {
        KingdomBanner[] banners = obj.GetComponentsInChildren<KingdomBanner>(true);
        if (banners.Length == 0)
        {
            throw new System.InvalidOperationException("Sect tooltip 原版 Header 缺少 KingdomBanner");
        }

        for (int i = 0; i < banners.Length; i++)
        {
            KingdomBanner kingdomBanner = banners[i];
            SectBanner sectBanner = kingdomBanner.gameObject.AddComponent<SectBanner>();
            kingdomBanner.CopyCompatibleSerializedFieldsTo(sectBanner);
            Object.DestroyImmediate(kingdomBanner);
        }
    }
}
