using System.Collections.Generic;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using HarmonyLib;

namespace Cultiway.Content.Extensions;

public static class ActorAssetTools
{
    /// <summary>
    /// 设置默认所属阵营
    /// </summary>
    public static ActorAsset SetCamp(this ActorAsset asset, KingdomAsset kingdom)
    {
        asset.kingdom_id_wild = kingdom.id;
        return asset;
    }
    /// <summary>
    /// 添加死后执行的动作
    /// </summary>
    public static ActorAsset ActionOnDeath(this ActorAsset asset, WorldAction action)
    {
        asset.action_death += action;
        return asset;
    }
    /// <summary>
    /// 设置ai
    /// </summary>
    public static ActorAsset AddJob(this ActorAsset asset, ActorJob job)
    {
        asset.job ??= [];
        asset.job = asset.job.AddToArray(job.id);
        return asset;
    }
    /// <summary>
    /// 设置ai
    /// </summary>
    public static ActorAsset AddJob(this ActorAsset asset, string job_id)
    {
        asset.job ??= [];
        asset.job = asset.job.AddToArray(job_id);
        return asset;
    }
    /// <summary>
    /// 设置移动动画
    /// </summary>
    public static ActorAsset SetAnimWalk(this ActorAsset asset, params string[] anims)
    {
        asset.animation_walk = anims;
        return asset;
    }
    /// <summary>
    /// 设置移动动画速度，默认值为10
    /// </summary>
    public static ActorAsset SetAnimWalkSpeed(this ActorAsset asset, float speed)
    {
        asset.animation_walk_speed = speed;
        return asset;
    }
    /// <summary>
    /// 设置游动动画速度，默认值为10
    /// </summary>
    public static ActorAsset SetAnimSwimSpeed(this ActorAsset asset, float speed)
    {
        asset.animation_swim_speed = speed;
        return asset;
    }
    /// <summary>
    /// 设置待机动画速度，默认值为10
    /// </summary>
    public static ActorAsset SetAnimIdleSpeed(this ActorAsset asset, float speed)
    {
        asset.animation_idle_speed = speed;
        return asset;
    }
    /// <summary>
    /// 设置是否站着睡觉
    /// </summary>
    public static ActorAsset SetStandWhileSleeping(this ActorAsset asset, bool value)
    {
        asset.GetExtend<ActorAssetExtend>().sleep_standing_up = value;
        return asset;
    }
    /// <summary>
    /// 设置是否隐藏手上物品
    /// </summary>
    public static ActorAsset SetHideHandItem(this ActorAsset asset, bool value)
    {
        asset.GetExtend<ActorAssetExtend>().hide_hand_item = value;
        return asset;
    }
    /// <summary>
    /// 同样设置移动动画，输入格式为"walk_0,walk_1,walk_2,walk_3,walk_4,walk_5,walk_6,walk_7"
    /// </summary>
    public static ActorAsset SetAnimWalkRaw(this ActorAsset asset, string anims)
    {
        asset.animation_walk = anims.Split(',');
        return asset;
    }
    /// <summary>
    /// 设置站立动画
    /// </summary>
    public static ActorAsset SetAnimIdle(this ActorAsset asset, params string[] anims)
    {
        asset.animation_idle = anims;
        return asset;
    }
    /// <summary>
    /// 同样设置站立动画，输入格式为"walk_0,walk_1,walk_2,walk_3,walk_4,walk_5,walk_6,walk_7"
    /// </summary>
    public static ActorAsset SetAnimIdleRaw(this ActorAsset asset, string anims)
    {
        asset.animation_idle = anims.Split(',');
        return asset;
    }
    /// <summary>
    /// 设置游泳动画
    /// </summary>
    public static ActorAsset SetAnimSwim(this ActorAsset asset, params string[] anims)
    {
        asset.animation_swim = anims;
        return asset;
    }
    /// <summary>
    /// 同样设置游泳动画，输入格式为"walk_0,walk_1,walk_2,walk_3,walk_4,walk_5,walk_6,walk_7"
    /// </summary>
    public static ActorAsset SetAnimSwimRaw(this ActorAsset asset, string anims)
    {
        asset.animation_swim = anims.Split(',');
        return asset;
    }
    /// <summary>
    /// 设置是否移动时自动跳跃(蹦跶)
    /// </summary>
    public static ActorAsset SetJumpAnimation(this ActorAsset asset, bool value)
    {
        asset.disable_jump_animation = value;
        return asset;
    }
    /// <summary>
    /// 设置生物默认武器(随机选择其一)
    /// </summary>
    public static ActorAsset SetDefaultWeapons(this ActorAsset asset, params string[] weapon_ids)
    {
        asset.default_weapons = weapon_ids;
        return asset;
    }
    /// <summary>
    /// 设置某一项属性
    /// </summary>
    public static ActorAsset Stats(this ActorAsset asset, string stats_id, float stats_value)
    {
        asset.base_stats[stats_id] = stats_value;
        return asset;
    }
    /// <summary>
    /// 添加特质
    /// </summary>
    public static ActorAsset AddTrait(this ActorAsset asset, string trait_id)
    {
        asset.addTrait(trait_id);
        return asset;
    }
    /// <summary>
    /// 添加特质
    /// </summary>
    public static ActorAsset AddTrait(this ActorAsset asset, ActorTrait trait)
    {
        return asset.AddTrait(trait.id);
    }
    /// <summary>
    /// 添加亚种栏特质
    /// </summary>
    public static ActorAsset AddSubspeciesTrait(this ActorAsset asset, string trait_id)
    {
        asset.addSubspeciesTrait(trait_id);
        return asset;
    }
    /// <summary>
    /// 添加亚种栏特质
    /// </summary>
    public static ActorAsset AddSubspeciesTrait(this ActorAsset asset, SubspeciesTrait trait)
    {
        asset.AddSubspeciesTrait(trait.id);
        return asset;
    }
    /// <summary>
    /// 添加文化栏特质
    /// </summary>
    public static ActorAsset AddCultureTrait(this ActorAsset asset, string trait_id)
    {
        asset.addCultureTrait(trait_id);
        return asset;
    }
    /// <summary>
    /// 添加文化栏特质
    /// </summary>
    public static ActorAsset AddCultureTrait(this ActorAsset asset, CultureTrait trait)
    {
        asset.AddCultureTrait(trait.id);
        return asset;
    }
    /// <summary>
    /// 设置图标路径
    /// </summary>
    public static ActorAsset SetIcon(this ActorAsset asset, string icon_path)
    {
        if (icon_path.ToLower().StartsWith("ui/icons"))
        {
            asset.icon = icon_path.Substring(8);
        }
        else
        {
            asset.icon = $"../../{icon_path}";
        }

        return asset;
    }
}