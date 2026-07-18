using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Core.Progression;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

/// <summary>人物修炼体系总览页中的池化体系条目。</summary>
public sealed class CultisysOverviewRow : APrefabPreview<CultisysOverviewRow>
{
    private const float RowWidth = 238f;
    private Image _icon;
    private Text _name;
    private Text _realm;
    private Text _progression;

    protected override void OnInit()
    {
        _icon = transform.Find("Icon").GetComponent<Image>();
        _name = transform.Find("Labels/Name").GetComponent<Text>();
        _realm = transform.Find("Labels/Detail/Realm").GetComponent<Text>();
        _progression = transform.Find("Labels/Detail/Progression").GetComponent<Text>();
    }

    /// <summary>绑定一个角色已经拥有的修炼体系及其当前只读进阶快照。</summary>
    public void Setup(BaseCultisysAsset asset, ActorExtend actor)
    {
        Init();
        int level = asset.GetCurrentLevel(actor);
        _icon.sprite = SpriteTextureLoader.getSprite(asset.IconPath)
                       ?? SpriteTextureLoader.getSprite("cultiway/icons/iconCultivation");
        _name.text = asset.GetName();
        _realm.text = level >= 0 && level < asset.LevelNumber
            ? asset.GetLevelName(level)
            : "Cultiway.CultisysOverview.UI.UnknownRealm".Localize();

        ProgressionQuery query = asset.QueryProgression(actor);
        _progression.text = CultisysPresentation.FormatProgression(query);
        _progression.color = CultisysPresentation.ResolveProgressionColor(query);
        UiTooltip.Set(_icon.gameObject, () => CultisysTooltip.Show(_icon.gameObject, asset, actor));
    }

    private static void _init()
    {
        GameObject obj = UiLayout.Create(ModClass.I.PrefabLibrary, nameof(CultisysOverviewRow), true,
            RowWidth, 38f, 3f, TextAnchor.MiddleLeft);
        UiListRowChrome.Attach(obj, false);

        GameObject icon = new("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        icon.transform.SetParent(obj.transform, false);
        UiLayout.SetSize(icon.transform, 34f, 34f);
        icon.GetComponent<Image>().preserveAspect = true;

        GameObject labels = UiLayout.Create(obj.transform, "Labels", false, 198f, 34f, 0f);
        Text name = UiElements.CreateText(labels.transform, "Name", string.Empty, 198f, 18f, 7,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        GameObject detail = UiLayout.Create(labels.transform, "Detail", true, 198f, 16f, 2f);
        Text realm = UiElements.CreateText(detail.transform, "Realm", string.Empty, 72f, 16f, 6,
            TextAnchor.MiddleLeft);
        Text progression = UiElements.CreateText(detail.transform, "Progression", string.Empty, 124f, 16f, 6,
            TextAnchor.MiddleRight);
        ConfigureBestFit(name, 5, 7);
        ConfigureBestFit(realm, 5, 6);
        ConfigureBestFit(progression, 5, 6);

        Prefab = obj.AddComponent<CultisysOverviewRow>();
    }

    private static void ConfigureBestFit(Text text, int minSize, int maxSize)
    {
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = minSize;
        text.resizeTextMaxSize = maxSize;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }
}
