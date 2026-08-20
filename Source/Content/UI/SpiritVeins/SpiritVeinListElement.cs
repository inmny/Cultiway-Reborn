using Cultiway.Content.SpiritVeins;
using Cultiway.Core;
using Cultiway.Debug;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.SpiritVeins;

/// <summary>灵脉列表中的单行条目。</summary>
public sealed class SpiritVeinListElement : WindowListElementBase<SpiritVein, SpiritVeinData>
{
    public Text nameText;
    public Image typeIcon;
    public CountUpOnClick branches;
    public CountUpOnClick sections;
    public CountUpOnClick grounds;
    public CountUpOnClick eyes;

    public override void show(SpiritVein vein)
    {
        base.show(vein);
        nameText.text = vein.Name;
        nameText.color = vein.getColor().getColorText();
        typeIcon.sprite = SpriteTextureLoader.getSprite("cultiway/icons/iconSpiritVein");
        branches.setValue(vein.BranchIds.Count);
        sections.setValue(vein.SectionIds.Count);
        grounds.setValue(vein.GroundIds.Count);
        eyes.setValue(vein.EyeIds.Count);
    }

    public new void click()
    {
        if (meta_object == null || meta_object.isRekt()) return;

        WorldboxGame.MetaTypes.SpiritVein.set_selected(meta_object);
        SelectedObjects.setNanoObject(meta_object);
        int tileId = meta_object.SourceCenterTileId;
        WorldTile[] tiles = World.world?.tiles_list;
        if (tiles == null || (uint)tileId >= (uint)tiles.Length) return;
        MapBox.instance.locatePosition(tiles[tileId].posV);
    }

    public override void tooltipAction()
    {
    }

    private static void InitPrefab()
    {
        GameObject obj = Instantiate(
            Resources.Load<ListWindow>("windows/list_kingdoms")._list_element_prefab.gameObject,
            ModClass.I.PrefabLibrary);
        Transform bannerObject = obj.transform.Find("Kingdom Banner");
        KingdomListElement kingdomElement = obj.GetComponent<KingdomListElement>();
        Image speciesIcon = kingdomElement._icon_species;

        bannerObject.name = "Spirit Vein Banner";
        DestroyImmediate(kingdomElement);
        DestroyImmediate(obj.transform.Find("UnitAvatarElement").gameObject);
        DestroyImmediate(bannerObject.GetComponent<KingdomBanner>());
        HideVanillaBannerDecorations(bannerObject);
        bannerObject.localScale = Vector3.one * 0.5f;
        bannerObject.AddComponent<SpiritVeinBanner>();

        Prefab = obj.AddComponent<SpiritVeinListElement>();
        obj.GetComponent<Button>().onClick.AddListener(Prefab.click);
        Prefab.typeIcon = speciesIcon;
        Prefab._icon_favorite = Prefab.transform.Find("Top/Favorited").gameObject;
        Prefab.nameText = Prefab.transform.Find("Top/Name").GetComponent<Text>();

        Transform ageIcon = Prefab.transform.Find("Icons/Age");
        Transform populationIcon = Prefab.transform.Find("Icons/Population");
        Transform armyIcon = Prefab.transform.Find("Icons/Army");
        Transform citiesIcon = Prefab.transform.Find("Icons/Cities");
        Transform housesIcon = Prefab.transform.Find("Icons/Houses");
        Transform zonesIcon = Prefab.transform.Find("Icons/Zones");

        ageIcon.gameObject.SetActive(true);
        populationIcon.gameObject.SetActive(true);
        armyIcon.gameObject.SetActive(false);
        citiesIcon.gameObject.SetActive(true);
        housesIcon.gameObject.SetActive(false);
        zonesIcon.gameObject.SetActive(true);
        zonesIcon.SetSiblingIndex(citiesIcon.GetSiblingIndex());

        SetStatIcon(ageIcon, "cultiway/icons/iconSpiritVein", "Cultiway.SpiritVein.List.Branches");
        SetStatIcon(populationIcon, "ui/Icons/iconZones", "Cultiway.SpiritVein.List.Sections");
        SetStatIcon(zonesIcon, "ui/Icons/iconCityZones", "Cultiway.SpiritVein.List.Grounds");
        SetStatIcon(citiesIcon, "ui/Icons/iconForbiddenKnowledgeBlackholeEyeOpen", "Cultiway.SpiritVein.List.Eyes");

        Prefab.branches = ageIcon.GetComponent<CountUpOnClick>();
        Prefab.sections = populationIcon.GetComponent<CountUpOnClick>();
        Prefab.grounds = zonesIcon.GetComponent<CountUpOnClick>();
        Prefab.eyes = citiesIcon.GetComponent<CountUpOnClick>();
    }

    private static void SetStatIcon(Transform stat, string spritePath, string tooltipKey)
    {
        Image image = stat.Find("Container/Icon")?.GetComponent<Image>();
        if (image != null)
        {
            Sprite sprite = SpriteTextureLoader.getSprite(spritePath);
            image.sprite = sprite;
            image.overrideSprite = sprite;
        }

        TipButton tip = stat.GetComponent<TipButton>();
        if (tip == null) return;
        tip.textOnClick = tooltipKey;
        tip.textOnClickDescription = tooltipKey + ".Description";
    }

    private static void HideVanillaBannerDecorations(Transform root)
    {
        string[] paths =
        {
            "TiltEffect/Background",
            "TiltEffect/dead",
            "TiltEffect/left",
            "TiltEffect/winner",
            "TiltEffect/loser"
        };
        for (int i = 0; i < paths.Length; i++)
        {
            Transform child = root.Find(paths[i]);
            if (child != null) child.gameObject.SetActive(false);
        }
    }

    private static SpiritVeinListElement prefab;

    public static SpiritVeinListElement Prefab
    {
        get
        {
            if (prefab == null)
            {
                if (NeoModLoader.utils.OtherUtils.CalledBy(nameof(InitPrefab), typeof(SpiritVeinListElement), true))
                {
                    return null;
                }
                Try.Start(InitPrefab);
            }
            return prefab;
        }
        private set => prefab = value;
    }
}
