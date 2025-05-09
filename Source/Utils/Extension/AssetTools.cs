using System.Collections.Generic;
using Cultiway.Content.Const;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV2;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;

namespace Cultiway.Utils.Extension;

public static class AssetTools
{
    public static TExtend GetExtend<TExtend>(this Asset asset) where TExtend : class, new()
    {
        return AssetExtendWrapper<Asset, TExtend>.GetExtend(asset);
    }

    private static class AssetExtendWrapper<TAsset, TExtend> where TAsset : Asset where TExtend : class, new()
    {
        private static readonly Dictionary<TAsset, TExtend> _extends = new();

        public static TExtend GetExtend(TAsset asset)
        {
            if (!_extends.TryGetValue(asset, out TExtend extend))
            {
                extend = new TExtend();
                _extends.Add(asset, extend);
            }

            return extend;
        }
    }

    public static void AddToPool(this CombatActionAsset asset, ListPool<CombatActionAsset> pool, int rate = -1)
    {
        if (rate == -1)
        {
            rate = asset.rate;
        }
        for (int i = 0; i < rate; i++)
        {
            pool.Add(asset);
        }
    }

    public static WrappedSkillAsset SelfWrap(this TriggerActionMeta<StartSkillTrigger, StartSkillContext> start_skill_meta, params WrappedSkillType[] types)
    {
        var asset = new WrappedSkillAsset()
        {
            id = start_skill_meta.id,
        };
        ModClass.L.WrappedSkillLibrary.add(asset);
        foreach (WrappedSkillType type in types)
        {
            asset.SetSkillType(type);
        }

        return asset;
    }
}