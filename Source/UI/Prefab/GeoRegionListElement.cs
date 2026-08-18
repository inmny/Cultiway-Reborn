using Cultiway;
using Cultiway.Core;
using Cultiway.Debug;
using UnityEngine;
using UnityEngine.UI;

/// <summary>地区列表中的单行条目，显示旗帜、名称、类别、年龄、面积、人口和城市数。</summary>
public class GeoRegionListElement : WindowListElementBase<GeoRegion, GeoRegionData>
{
    /// <summary>以原版国家列表行为基础创建地区条目，并换成地区旗帜和面积图标。</summary>
    private static void _init()
    {
        var obj = Instantiate(Resources.Load<ListWindow>("windows/list_kingdoms")._list_element_prefab.gameObject, ModClass.I.PrefabLibrary);
        var banner_obj = obj.transform.Find("Kingdom Banner");
        var kingdom_list_element = obj.GetComponent<KingdomListElement>();
        var type_icon = kingdom_list_element._icon_species;
        banner_obj.name = "GeoRegion Banner";
        DestroyImmediate(kingdom_list_element);
        DestroyImmediate(obj.transform.Find("UnitAvatarElement").gameObject);
        DestroyImmediate(banner_obj.GetComponent<KingdomBanner>());

        banner_obj.AddComponent<GeoRegionBanner>();
        Prefab = obj.AddComponent<GeoRegionListElement>();
        Prefab.type_icon = type_icon;
        Prefab._icon_favorite = Prefab.transform.Find("Top/Favorited").gameObject;
        Prefab.name_text = Prefab.transform.Find("Top/Name").GetComponent<Text>();
        Prefab.transform.Find("Icons/Army").gameObject.SetActive(false);
        Prefab.transform.Find("Icons/Zones").gameObject.SetActive(true);

        Prefab.age = Prefab.transform.Find("Icons/Age").GetComponent<CountUpOnClick>();
        Prefab.tiles = Prefab.transform.Find("Icons/Zones").GetComponent<CountUpOnClick>();
        Prefab.pop = Prefab.transform.Find("Icons/Population").GetComponent<CountUpOnClick>();
        Prefab.cities = Prefab.transform.Find("Icons/Cities").GetComponent<CountUpOnClick>();
    }
    /// <summary>玩家在每一行看到的地区名称。</summary>
    public Text name_text;
    /// <summary>当前地区类别图标。</summary>
    public Image type_icon;
    /// <summary>地区年龄数字。</summary>
    public CountUpOnClick age;
    /// <summary>地区包含的地块数量。</summary>
    public CountUpOnClick tiles;
    /// <summary>地区人口数字。</summary>
    public CountUpOnClick pop;
    /// <summary>地区城市数量。</summary>
    public CountUpOnClick cities;


    /// <summary>列表条目出现或复用时，显示指定地区的名称、类别、年龄和面积。</summary>
    public override void show(GeoRegion region)
    {
        base.show(region);
        name_text.text = region.name;
        type_icon.sprite = region.GetCategory().GetSpriteIcon();
        age.setValue(region.getAge());
        tiles.setValue(region.data.TileCount);
        pop.setValue(0);
        cities.setValue(0);
    }
    /// <summary>列表条目不额外弹出说明，点击行为沿用列表基类。</summary>
    public override void tooltipAction()
    {
    }
    /// <summary>为派生条目保留一次性的初始化入口。</summary>
    protected virtual void OnInit()
    {
    }

    // 地区列表条目模板，以及单个条目是否完成一次性准备。
    private static GeoRegionListElement mPrefab;
    private bool initialized;

    /// <summary>取得地区列表条目模板；首次使用时根据原版列表创建。</summary>
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

    /// <summary>复制一个地区列表条目，并可指定父节点和名称。</summary>
    public static GeoRegionListElement Instantiate(Transform pParent = null, bool pWorldPositionStays = false, string pName = null)
    {
        GeoRegionListElement val = UnityEngine.Object.Instantiate(Prefab, pParent, pWorldPositionStays);
        if (!string.IsNullOrEmpty(pName))
        {
            val.name = pName;
        }

        return val;
    }

    /// <summary>设置列表条目的固定显示尺寸。</summary>
    public void SetSize(Vector2 pSize)
    {
        RectTransform component = GetComponent<RectTransform>();
        if (component != null)
        {
            component.sizeDelta = pSize;
        }
    }

    /// <summary>确保条目只执行一次派生类准备工作。</summary>
    public void Init()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        OnInit();
    }
    /// <summary>将地区条目登记到指定资源路径，供原版列表系统创建。</summary>
    public static void PatchTo<TComponentType>(string pPath) where TComponentType : Component
    {
        NeoModLoader.utils.ResourcesPatch.PatchResource(pPath, Prefab.GetComponent<TComponentType>());
    }
}
