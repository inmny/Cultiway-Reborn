using System.Drawing;
using Cultiway;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Debug;
using Friflo.Engine.ECS;
using UnityEngine;
using UnityEngine.UI;

public class GeoRegionListElement : WindowListElementBase<GeoRegion, GeoRegionData>
{
    private static void _init()
    {
        var obj = Instantiate(Resources.Load<ListWindow>("windows/list_kingdoms")._list_element_prefab.gameObject, ModClass.I.PrefabLibrary);
        var banner_obj = obj.transform.Find("Kingdom Banner");
        banner_obj.name = "GeoRegion Banner";
        DestroyImmediate(obj.GetComponent<KingdomListElement>());
        DestroyImmediate(obj.transform.Find("UnitAvatarElement").gameObject);
        DestroyImmediate(banner_obj.GetComponent<KingdomBanner>());

        banner_obj.AddComponent<GeoRegionBanner>();
        Prefab = obj.AddComponent<GeoRegionListElement>();
        Prefab._icon_favorite = Prefab.transform.Find("Top/Favorited").gameObject;
        Prefab.name_text = Prefab.transform.Find("Top/Name").GetComponent<Text>();
        Prefab.transform.Find("Icons/Army").gameObject.SetActive(false);
        Prefab.transform.Find("Icons/Zones").gameObject.SetActive(true);

        Prefab.age = Prefab.transform.Find("Icons/Age").GetComponent<CountUpOnClick>();
        Prefab.tiles = Prefab.transform.Find("Icons/Zones").GetComponent<CountUpOnClick>();
        Prefab.pop = Prefab.transform.Find("Icons/Population").GetComponent<CountUpOnClick>();
        Prefab.cities = Prefab.transform.Find("Icons/Cities").GetComponent<CountUpOnClick>();
    }
    public Text name_text;
    public CountUpOnClick age;
    public CountUpOnClick tiles;
    public CountUpOnClick pop;
    public CountUpOnClick cities;


    public override void show(GeoRegion region)
    {
        base.show(region);
        name_text.text = region.name;
        age.setValue(region.getAge());
        tiles.setValue(region.E.GetIncomingLinks<BelongToRelation>().Count);
        pop.setValue(0);
        cities.setValue(0);
    }
    public override void tooltipAction()
    {
    }
    protected virtual void OnInit()
    {
    }

    private static GeoRegionListElement mPrefab;
    private bool initialized;

    public static GeoRegionListElement Prefab
    {
        get
        {
            if (mPrefab == null)
            {
                if (NeoModLoader.utils.OtherUtils.CalledBy("_init", typeof(GeoRegionListElement), true))
                {
                    return null;
                }
                Try.Start(()=>
                {
                    _init();
                });
            }

            return mPrefab;
        }
        set => mPrefab = value;
    }

    public static GeoRegionListElement Instantiate(Transform pParent = null, bool pWorldPositionStays = false, string pName = null)
    {
        GeoRegionListElement val = UnityEngine.Object.Instantiate(Prefab, pParent, pWorldPositionStays);
        if (!string.IsNullOrEmpty(pName))
        {
            val.name = pName;
        }

        return val;
    }

    public void SetSize(Vector2 pSize)
    {
        RectTransform component = GetComponent<RectTransform>();
        if (component != null)
        {
            component.sizeDelta = pSize;
        }
    }

    public void Init()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        OnInit();
    }
    public static void PatchTo<TComponentType>(string pPath) where TComponentType : Component
    {
        NeoModLoader.utils.ResourcesPatch.PatchResource(pPath, Prefab.GetComponent<TComponentType>());
    }
}