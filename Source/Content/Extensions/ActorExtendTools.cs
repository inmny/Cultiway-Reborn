using System;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Extensions;

public static class ActorExtendTools
{
    /// <summary>
    ///     使用丹药
    /// </summary>
    /// <remarks>一旦使用成功，丹药实体将被删除</remarks>
    /// <returns>是否使用成功 </returns>
    [Hotfixable]
    public static bool TryConsumeElixir(this ActorExtend ae, Entity elixir_entity)
    {
        ref Elixir elixir = ref elixir_entity.GetComponent<Elixir>();
        ElixirAsset elixir_asset = Libraries.Manager.ElixirLibrary.get(elixir.elixir_id);
        try
        {
            if (elixir_asset.consumable_check_action?.Invoke(ae, elixir_entity, ref elixir) ?? true)
                elixir_asset.effect_action?.Invoke(ae, elixir_entity, ref elixir);
            else
                return false;
        }
        catch (Exception e)
        {
            ModClass.LogError($"[{elixir_entity.Id}] ElixirAsset({elixir.elixir_id}).consumed_action error: {e}");
            return false;
        }

        ModClass.LogInfo($"[{ae}] consumes {elixir_entity.Id}({elixir.elixir_id})");
        elixir_entity.DeleteEntity();
        return true;
    }
    public static void EnhanceSkillRandomly(this ActorExtend ae, string source)
    {
        if (ae.tmp_all_skills.Count == 0) return;
        var skill = ae.tmp_all_skills.GetRandom();
        ModClass.L.WrappedSkillLibrary.get(skill).Enhance(ae, source);
    }
    public static void EnhanceSkill(this ActorExtend ae, string skill_id, string source)
    {
        ModClass.L.WrappedSkillLibrary.get(skill_id).Enhance(ae, source);
    }
    public static bool RestoreWakan(this ActorExtend ae, float value)
    {
        if (value <= 0) return false;
        if (!ae.HasCultisys<Xian>()) return false;
        ref Xian xian = ref ae.GetCultisys<Xian>();
        xian.wakan = Mathf.Min(xian.wakan + value,
            Mathf.Max(xian.wakan, ae.Base.stats[BaseStatses.MaxWakan.id] * XianSetting.WakanRestoreLimit));
        return true;
    }

    public static bool HasCultibook(this ActorExtend ae)
    {
        return ae.E.GetRelations<CultibookMasterRelation>().Any();
    }

    public static CultibookMasterRelation GetCultibookMasterRelation(this ActorExtend ae)
    {
        return ae.E.GetRelations<CultibookMasterRelation>().First();
    }

    public static void SetCultibookMasterRelation(this ActorExtend ae, ref Entity cultibook, float master_value)
    {
        if (ae.E.GetRelations<CultibookMasterRelation>().Any())
        {
            ae.E.GetRelation<CultibookMasterRelation, Entity>(cultibook).MasterValue = master_value;
        }
        else
        {
            ae.E.AddRelation(new CultibookMasterRelation()
            {
                Cultibook = cultibook,
                MasterValue = master_value
            });
        }
    }

    public static ref Yuanying GetYuanying(this ActorExtend ae)
    {
        return ref ae.GetComponent<Yuanying>();
    }
    public static ref Jindan GetJindan(this ActorExtend ae)
    {
        return ref ae.GetComponent<Jindan>();
    }
    public static ref XianBase GetXianBase(this ActorExtend ae)
    {
        return ref ae.GetComponent<XianBase>();
    }
}