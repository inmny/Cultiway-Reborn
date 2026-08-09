using System;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Libraries;

/// <summary>骑士流派资产库。</summary>
public sealed class KnightStyleLibrary : AssetLibrary<KnightStyleAsset>
{
}

/// <summary>定义骑士战斗风格及其基础武器准入条件。</summary>
public sealed class KnightStyleAsset : Asset
{
    /// <summary>流派名称本地化键。</summary>
    public string NameKey;

    /// <summary>流派描述本地化键。</summary>
    public string DescriptionKey;

    /// <summary>流派展示图标路径。</summary>
    public string IconPath;

    /// <summary>流派在技能来源页中的稳定排序。</summary>
    public int SortOrder;

    /// <summary>使用该流派所需的最低骑士等级。</summary>
    public int MinimumKnightLevel;

    /// <summary>该流派默认兼容的原版装备组 ID。</summary>
    public string[] WeaponGroups = Array.Empty<string>();

    /// <summary>未来扩展盾牌、坐骑或特殊装备准入时使用的条件。</summary>
    public Func<ActorExtend, Item, bool> AdditionalEquipmentCondition;

    /// <summary>返回流派是否匹配当前真实装备。</summary>
    public bool MatchesEquipment(ActorExtend actor, Item weapon, EquipmentAsset weaponAsset)
    {
        if (weaponAsset == null || Array.IndexOf(WeaponGroups, weaponAsset.group_id) < 0) return false;
        return AdditionalEquipmentCondition?.Invoke(actor, weapon) ?? true;
    }

    /// <summary>取得流派显示名称。</summary>
    public string ResolveName() => NameKey.Localize();

    /// <summary>取得流派描述。</summary>
    public string ResolveDescription() => DescriptionKey.Localize();

    /// <summary>取得流派图标。</summary>
    public Sprite ResolveIcon() => SpriteTextureLoader.getSprite(IconPath);
}
