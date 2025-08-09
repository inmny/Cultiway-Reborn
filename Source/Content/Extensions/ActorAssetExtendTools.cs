namespace Cultiway.Content.Extensions;

public static class ActorAssetExtendTools
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
    /// 设置移动动画
    /// </summary>
    public static ActorAsset SetAnimWalk(this ActorAsset asset, params string[] anims)
    {
        asset.animation_walk = anims;
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
    /// 设置游泳动画
    /// </summary>
    public static ActorAsset SetAnimSwim(this ActorAsset asset, params string[] anims)
    {
        asset.animation_swim = anims;
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