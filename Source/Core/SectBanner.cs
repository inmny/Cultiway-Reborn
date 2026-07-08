using System;
using Cultiway.Const;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Core;

public class SectBanner : BannerGeneric<Sect, SectData>
{
    public override MetaType meta_type => MetaTypeExtend.Sect.Back();
    public override void tooltipAction()
    {
        if (meta_object == null) return;
        Tooltip.show(this, WorldboxGame.Tooltips.Sect.id, new TooltipData()
        {
            tip_name = meta_object.id.ToString()
        });
    }

    public override void setupBanner()
    {
        base.setupBanner();
        HideVanillaBannerDecorations(transform);
        part_background.sprite = meta_object.getBannerBackground();
        part_icon.sprite = meta_object.getBannerIcon();
        part_background.color = meta_object.getColor().getColorMainSecond();
        part_icon.color = meta_object.getColor().getColorBanner();
    }

    public static SectBanner Prefab
    {
        get
        {
            if (_prefab == null)
            {
                CreatePrefab();
            }
            return _prefab;
        }
    }

    private static void CreatePrefab()
    {
        var go = Instantiate(Resources.Load<KingdomBanner>("ui/PrefabBannerKingdom"), ModClass.I.PrefabLibrary).gameObject;
        go.SetActive(false);
        Destroy(go.GetComponent<KingdomBanner>());
        HideVanillaBannerDecorations(go.transform);

        go.GetComponent<UiButtonHoverAnimation>().default_scale = new(0.75f, 0.75f, 1);
        go.GetComponent<TipButton>().setDefaultScale(new Vector3(0.75f, 0.75f, 1));
        go.SetActive(true);
        //Destroy(go.transform.Find(""));
        _prefab = go.AddComponent<SectBanner>();
        _prefab.AddComponent<DraggableLayoutElement>();
        _prefab.name = "PrefabBannerSect";
        _prefab.transform.localScale = Vector3.one * 0.75f;
    }

    public static void HideVanillaBannerDecorations(Transform root)
    {
        if (root == null) return;

        root.HideChildrenByPath(
            "TiltEffect/dead",
            "TiltEffect/left",
            "TiltEffect/winner",
            "TiltEffect/loser");
        HideDescendantsByName(root, "war", "revolt", "rebellion", "uprising", "laurel", "crown");
    }

    private static void HideDescendantsByName(Transform root, params string[] nameParts)
    {
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform current = descendants[i];
            if (current == root) continue;
            if (!current.name.ContainsAny(nameParts)) continue;

            current.gameObject.SetActive(false);
        }
    }

    private static SectBanner _prefab;
}
