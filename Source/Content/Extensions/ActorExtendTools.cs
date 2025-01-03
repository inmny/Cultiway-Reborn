using System;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Extensions;

public static class ActorExtendTools
{
    /// <summary>
    ///     使用丹药
    /// </summary>
    /// <remarks>一旦使用成功，丹药实体将被删除</remarks>
    /// <returns>是否使用成功 </returns>
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

        elixir_entity.DeleteEntity();
        return true;
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
}