using System;
using System.Collections.Generic;
using Cultiway.Core.SkillLibV3.Visuals;

namespace Cultiway.Core.SkillLibV3.Settings;

/// <summary>内容层登记可选的元素性质，伤害与显示共用稳定编号。</summary>
public static class SkillNatureCatalog
{
    /// <summary>按稳定编号查找元素组成及对应表现。</summary>
    public static readonly Dictionary<string, SkillNatureDefinition> Natures = new(StringComparer.Ordinal);
}

/// <summary>单个法术可选的一种元素性质。</summary>
public sealed class SkillNatureDefinition
{
    /// <summary>长期保持稳定的编号。</summary>
    public string Id;
    /// <summary>名称的本地化编号。</summary>
    public string NameKey;
    /// <summary>实际伤害组成。</summary>
    public ElementComposition Element;
    /// <summary>与该性质匹配的表现。</summary>
    public SkillVfxElementAsset Visual;
    /// <summary>该性质的持续区域能否传导雷法。</summary>
    public bool CanConduct;
}
