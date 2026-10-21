using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Settings;

/// <summary>单个法术的元素、尺度和接触设置，由蓝图保存。</summary>
public struct SkillSettings : IComponent
{
    /// <summary>覆盖模板的元素性质，空值沿用模板。</summary>
    public string NatureId;
    /// <summary>实际作用范围倍率。</summary>
    public float AreaMultiplier;
    /// <summary>实际效果强度倍率。</summary>
    public float PowerMultiplier;
    /// <summary>正数时覆盖存续世界秒数，零沿用模板。</summary>
    public float Duration;
    /// <summary>该持续区域是否可以传导雷法。</summary>
    public bool ConductiveWater;
    /// <summary>是否执行对象接触。</summary>
    public bool ContactEnabled;
}
