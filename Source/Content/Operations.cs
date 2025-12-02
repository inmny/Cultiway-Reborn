using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using HarmonyLib;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// DataGain一次性操作定义
/// </summary>
[Dependency(typeof(Jindans))]
public class Operations : ExtendLibrary<OperationAsset, Operations>
{
    public static OperationAsset ChangeGender { get; private set; }
    public static OperationAsset OpenElementRoot { get; private set; }
    public static OperationAsset EnhanceJindan { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.Operation";

    protected override void OnInit()
    {
        ChangeGender.Action = (ae, _, _) => TryChangeGender(ae.Base);
        ChangeGender.PreCheck = (ae, _, _) => PreCheckChangeGender(ae.Base);
        OpenElementRoot.Action = (ae, _, args) => TryOpenElementRoot(ae, args);
        OpenElementRoot.PreCheck = (ae, _, _) => PreCheckOpenElementRoot(ae.Base);
        EnhanceJindan.Action = (ae, mul, args) => TryEnhanceJindan(ae, args, mul);
        EnhanceJindan.PreCheck = (ae, _, _) => PreCheckEnhanceJindan(ae.Base);
    }

    private static bool TryChangeGender(Actor actor)
    {
        if (actor == null) return false;
        actor.data.sex = actor.data.sex == ActorSex.Male ? ActorSex.Female : ActorSex.Male;
        actor.setStatsDirty();
        return true;
    }
    private static bool PreCheckChangeGender(Actor actor)
    {
        return actor.data.sex == ActorSex.Male || actor.data.sex == ActorSex.Female;
    }
    private static bool PreCheckOpenElementRoot(Actor actor)
    {
        return !actor.GetExtend().HasElementRoot();
    }
    private static bool PreCheckEnhanceJindan(Actor actor)
    {
        return actor.GetExtend().HasComponent<Jindan>();
    }
    private static bool TryOpenElementRoot(ActorExtend ae, Dictionary<string, string> opArgs)
    {
        ElementRoot root = default;
        var hasTarget = false;
        if (opArgs != null && opArgs.TryGetValue("element_root", out var rootId) && !string.IsNullOrEmpty(rootId))
        {
            var asset = ModClass.L.ElementRootLibrary.get(rootId);
            if (asset != null)
            {
                root = new ElementRoot(asset.composition.AsArray());
                hasTarget = true;
            }
        }

        if (!hasTarget)
        {
            root = ElementRoot.Roll();
        }
        ae.AddComponent(root);
        ae.Base.setStatsDirty();
        return true;
    }

    private static bool TryEnhanceJindan(ActorExtend ae, Dictionary<string, string> opArgs, float multiplier)
    {
        ref var jindan = ref ae.GetOrAddComponent<Jindan>();
        jindan.stage = Math.Max(1, jindan.stage + 1);
        jindan.strength = Mathf.Max(jindan.strength, multiplier);
        ae.Base.setStatsDirty();
        return true;
    }
}
