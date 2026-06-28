using Cultiway;
using Cultiway.Core;
using Cultiway.Debug;
using UnityEngine;
using UnityEngine.UI;

public class SectListElement : WindowListElementBase<Sect, SectData>
{
    public Text name_text;
    public Image type_icon;
    public CountUpOnClick era;
    public CountUpOnClick population;
    public CountUpOnClick territory;
    public CountUpOnClick archive;
    public UiUnitAvatarElement leader_avatar;

    public override void show(Sect sect)
    {
        base.show(sect);
        name_text.text = sect.name;
        name_text.color = sect.getColor().getColorText();
        type_icon.sprite = SpriteTextureLoader.getSprite("cultiway/icons/iconSect");
        leader_avatar.show(sect.GetLeaderActor());
        era.setValue(sect.getAge());
        population.setValue(sect.countUnits());
        territory.setValue(sect.GetTerritoryCount());
        archive.setValue(0);
    }

    public override void tooltipAction()
    {
        if (meta_object == null) return;
        Tooltip.show(this, WorldboxGame.Tooltips.Sect.id, new TooltipData
        {
            tip_name = meta_object.id.ToString()
        });
    }

    public override ActorAsset getActorAsset()
    {
        return meta_object.getActorAsset();
    }

    private static void _init()
    {
        GameObject obj = Instantiate(Resources.Load<ListWindow>("windows/list_kingdoms")._list_element_prefab.gameObject, ModClass.I.PrefabLibrary);
        Transform bannerObj = obj.transform.Find("Kingdom Banner");
        KingdomListElement kingdomListElement = obj.GetComponent<KingdomListElement>();
        Image typeIcon = kingdomListElement._icon_species;
        UiUnitAvatarElement leaderAvatar = obj.transform.Find("UnitAvatarElement").GetComponent<UiUnitAvatarElement>();

        bannerObj.name = "Sect Banner";
        DestroyImmediate(kingdomListElement);
        DestroyImmediate(bannerObj.GetComponent<KingdomBanner>());
        SectBanner.HideVanillaBannerDecorations(bannerObj);

        bannerObj.AddComponent<SectBanner>();
        Prefab = obj.AddComponent<SectListElement>();
        Prefab.type_icon = typeIcon;
        Prefab.leader_avatar = leaderAvatar;
        Prefab._icon_favorite = Prefab.transform.Find("Top/Favorited").gameObject;
        Prefab.name_text = Prefab.transform.Find("Top/Name").GetComponent<Text>();

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

        SetIconSprite(ageIcon, "ui/Icons/iconAge");
        SetIconSprite(populationIcon, "ui/Icons/iconPopulation");
        SetIconSprite(zonesIcon, "ui/Icons/iconZones");
        SetIconSprite(citiesIcon, "ui/Icons/iconBooks");

        Prefab.era = ageIcon.GetComponent<CountUpOnClick>();
        Prefab.population = populationIcon.GetComponent<CountUpOnClick>();
        Prefab.territory = zonesIcon.GetComponent<CountUpOnClick>();
        Prefab.archive = citiesIcon.GetComponent<CountUpOnClick>();
    }

    private static void SetIconSprite(Transform statIcon, string spritePath)
    {
        Image image = statIcon.Find("Icon")?.GetComponent<Image>();
        if (image == null) return;

        image.sprite = SpriteTextureLoader.getSprite(spritePath);
    }

    private static SectListElement mPrefab;

    public static SectListElement Prefab
    {
        get
        {
            if (mPrefab == null)
            {
                if (NeoModLoader.utils.OtherUtils.CalledBy("_init", typeof(SectListElement), true))
                {
                    return null;
                }

                Try.Start(() =>
                {
                    _init();
                });
            }

            return mPrefab;
        }
        set => mPrefab = value;
    }

    public static SectListElement Instantiate(Transform pParent = null, bool pWorldPositionStays = false, string pName = null)
    {
        SectListElement val = Object.Instantiate(Prefab, pParent, pWorldPositionStays);
        if (!string.IsNullOrEmpty(pName))
        {
            val.name = pName;
        }

        return val;
    }
}
