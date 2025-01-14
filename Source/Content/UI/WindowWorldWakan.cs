using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.UI.Prefab;
using NeoModLoader.api;
using NeoModLoader.api.attributes;
using NeoModLoader.General.UI.Window;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI;

public class WindowWorldWakan : AbstractWindow<WindowWorldWakan>
{
    protected override void Init()
    {
        var vertical_layout_group = ContentTransform.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical_layout_group.childAlignment = TextAnchor.UpperCenter;
        vertical_layout_group.childControlHeight = true;
        vertical_layout_group.childControlWidth = true;
        vertical_layout_group.childForceExpandHeight = false;
        vertical_layout_group.childForceExpandWidth = true;
        
        var fitter = ContentTransform.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        BackgroundTransform.Find("Scroll View").GetComponent<RectTransform>().sizeDelta = new(200, 215.21f);
        
        _line_pool = new MonoObjPool<LineTitleValue>(LineTitleValue.Prefab, ContentTransform, x =>
        {
            x.Title.resizeTextForBestFit = false;
            x.Title.fontSize = 8;
            x.Value.resizeTextForBestFit = false;
            x.Value.fontSize = 8;
        });
    }
    private MonoObjPool<LineTitleValue> _line_pool;

    private void AddLineValue(string title, string value)
    {
        var line = _line_pool.GetNext();
        line.Setup(title, value);

    }
    [Hotfixable]
    public override void OnNormalEnable()   
    {
        _line_pool.Clear();
        
        AddLineValue("世界灵气总量", $"{WakanMap.I.Sum():g2}");
        AddLineValue("世界灵气均值", $"{(int)WakanMap.I.Avg()}");
        AddLineValue("世界灵气最大值", $"{(int)WakanMap.I.Max()}");
        AddLineValue("世界灵气最小值", $"{(int)WakanMap.I.Min()}");
        AddLineValue("世界浊气总量", $"{DirtyWakanMap.I.Sum():g2}");
        AddLineValue("世界浊气均值", $"{(int)DirtyWakanMap.I.Avg()}");
        AddLineValue("世界浊气最大值", $"{(int)DirtyWakanMap.I.Max()}");
        AddLineValue("世界浊气最小值", $"{(int)DirtyWakanMap.I.Min()}");

        var record_e = ModClass.I.WorldRecord.E;
        if (record_e.HasComponent<WakanTideStatus>())
        {
            var status = record_e.GetComponent<WakanTideStatus>();
            AddLineValue("灵气潮汐状态", status.rise ? "涨潮" : "落潮");
            AddLineValue("灵气潮汐切换时间", $"{status.switch_timer / TimeScales.SecPerYear:N0}年");
        }
    }
}