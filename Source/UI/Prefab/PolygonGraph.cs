using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.UI.Components;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

public class PolygonGraph : APrefabPreview<PolygonGraph>
{
    public Text                Title      { get; private set; }
    public PolygonComponent    Polygon    { get; private set; }
    public Image               Background { get; private set; }
    public VerticalLayoutGroup Layout     { get; private set; }
    private MonoObjPool<TipIcon> _icon_pool;

    protected override void OnInit()
    {
        Layout = GetComponent<VerticalLayoutGroup>();
        Title = transform.Find(nameof(Title)).GetComponent<Text>();
        Background = transform.Find(nameof(Background)).GetComponent<Image>();
        Polygon = transform.Find(nameof(Background)).Find(nameof(Polygon)).GetComponent<PolygonComponent>();
        _icon_pool = new MonoObjPool<TipIcon>(TipIcon.Prefab, transform, x => x.gameObject.AddComponent<LayoutElement>().ignoreLayout = true);
    }

    public override void SetSize(Vector2 pSize)
    {
        Init();
        base.SetSize(pSize);
        Title.rectTransform.sizeDelta = new Vector2(pSize.x, 10);
        var size = Mathf.Min(pSize.x, pSize.y - 10);
        Layout.spacing = pSize.y - (size + 10);
        Background.rectTransform.sizeDelta = new Vector2(size, size);
        Polygon.rectTransform.sizeDelta = new Vector2(size,    size);
    }

    public void Setup(string       title_key, int sides, float rotation, Color color, Sprite background,
                      List<Tuple<Sprite, TooltipData>> icons = null)
    {
        Init();
        Title.GetComponent<LocalizedText>().setKeyAndUpdate(title_key);
        Background.sprite = background;
        Polygon.rotation = rotation;
        Polygon.color = color;
        Polygon.DrawPolygon(sides);
        
        _icon_pool.Clear();
        if (icons != null)
        {
            var degrees = 360f / sides;
            var i = 0;
            var icon_size = new Vector2(16, 16);
            foreach (var icon in icons)
            {
                var tip_icon = _icon_pool.GetNext();
                tip_icon.Setup(icon.Item1, icon.Item2, icon_size);
                
                var rad = (degrees * i + rotation-180) * Mathf.Deg2Rad;
                var pos = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (Polygon.rectTransform.sizeDelta.x / 2) -
                    new Vector2(0, 10);
                
                tip_icon.transform.localPosition = pos;
                
                ++i;
            }
        }
    }

    public void Draw(List<float> data, float max_value)
    {
        Init();
        Polygon.DrawPolygon(data.Select(x => x / max_value).ToList());
    }

    private static void _init()
    {
        GameObject obj =
            ModClass.NewPrefabPreview(nameof(PolygonGraph), typeof(RectTransform), typeof(VerticalLayoutGroup));
        var vert_layout = obj.GetComponent<VerticalLayoutGroup>();
        vert_layout.childAlignment = TextAnchor.UpperCenter;
        vert_layout.childControlHeight = false;
        vert_layout.childControlWidth = false;
        vert_layout.childForceExpandHeight = false;
        vert_layout.childForceExpandWidth = false;

        GameObject title_obj = obj.NewChild(nameof(Title), typeof(Text), typeof(LocalizedText));
        var text = title_obj.GetComponent<Text>();
        text.alignment = TextAnchor.MiddleCenter;
        text.font = LocalizedTextManager.currentFont;
        text.resizeTextMinSize = 1;
        text.resizeTextForBestFit = true;
        var localization = title_obj.GetComponent<LocalizedText>();
        localization.autoField = true;

        GameObject polygon_obj = obj.NewChild(nameof(Background), typeof(Image));
        GameObject polygon_comp_obj = polygon_obj.NewChild(nameof(Polygon), typeof(PolygonComponent));

        Prefab = obj.AddComponent<PolygonGraph>();
    }
}