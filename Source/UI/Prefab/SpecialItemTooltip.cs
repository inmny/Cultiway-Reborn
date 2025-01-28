using System;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.UI.Prefab;

public class SpecialItemTooltip : APrefabPreview<SpecialItemTooltip>
{
    private static Action<GameObject>                         _initiators;
    private static Action<SpecialItemTooltip, string, Entity> _setup_actions;
    public         Tooltip                                    Tooltip { get; private set; }

    protected override void OnInit()
    {
        Tooltip = GetComponent<Tooltip>();
    }

    [Hotfixable]
    public void Setup(string type, Entity entity)
    {
        Init();
        if (entity.TryGetComponent(out EntityName entity_name))
            Tooltip.name.text = entity_name.value;
        else
            Tooltip.name.text = entity.Id.ToString();

        if (entity.TryGetComponent(out ItemLevel level))
        {
            Tooltip.addDescription(level.GetName());
            Tooltip.addDescription("\n");
        }
        Tooltip.addDescription(LM.Get(entity.GetComponent<ItemShape>().shape_id));
        if (entity.TryGetComponent(out ElementRoot element_root))
        {
            Tooltip.addDescription("\n");
            Tooltip.addDescription(element_root.Type.GetName());
            for (var i = 0; i <= ElementIndex.Entropy; i++)
                Tooltip.addLineIntText(ElementIndex.ElementNames[i], (int)(100 * element_root[i]));
        }

        _setup_actions?.Invoke(this, type, entity);
        if (entity.HasComponent<AliveTimer>() && entity.HasComponent<AliveTimeLimit>())
            Tooltip.addBottomDescription(
                $"离消失还剩:{(int)((entity.GetComponent<AliveTimeLimit>().value - entity.GetComponent<AliveTimer>().value) / TimeScales.SecPerYear)}年");
    }

    private static void _init()
    {
        GameObject obj = Instantiate(Resources.Load<GameObject>(WorldboxGame.Tooltips.Tip.prefab_id),
            ModClass.I.PrefabLibrary);


        _initiators?.Invoke(obj);
        Prefab = obj.AddComponent<SpecialItemTooltip>();
    }

    public static void RegisterInitiator(Action<GameObject> initiator)
    {
        _initiators += initiator;
    }

    public static void RegisterSetupAction(Action<SpecialItemTooltip, string, Entity> setup_action)
    {
        _setup_actions += setup_action;
    }
}