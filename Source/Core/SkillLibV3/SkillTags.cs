namespace Cultiway.Core.SkillLibV3;

/// <summary>
/// 法术系统使用的语义标签常量。
/// </summary>
public static class SkillTags
{
    /// <summary>
    /// 获取语义标签的本地化显示文本，不改变标签用于匹配的稳定值。
    /// </summary>
    public static string GetDisplayName(string tag)
    {
        var key = $"Cultiway.SkillTag.{tag}";
        var localized = key.Localize();
        if (localized != key) return localized;

        // 词条资产 ID 也会成为语义标签，此时优先复用资产自己的本地化名称。
        localized = tag.Localize();
        return localized == tag ? tag : localized;
    }

    /// <summary>
    /// 法术元素标签。
    /// </summary>
    public static class Element
    {
        /// <summary>金系元素标签。</summary>
        public const string Iron = "iron";

        /// <summary>木系元素标签。</summary>
        public const string Wood = "wood";

        /// <summary>水系元素标签。</summary>
        public const string Water = "water";

        /// <summary>冰霜、冻结元素标签。</summary>
        public const string Ice = "ice";

        /// <summary>毒、酸蚀、瘴气元素标签。</summary>
        public const string Poison = "poison";

        /// <summary>火系元素标签。</summary>
        public const string Fire = "fire";

        /// <summary>土系元素标签。</summary>
        public const string Earth = "earth";

        /// <summary>阴性元素标签。</summary>
        public const string Neg = "neg";

        /// <summary>阳性元素标签。</summary>
        public const string Pos = "pos";

        /// <summary>熵系元素标签。</summary>
        public const string Entropy = "entropy";

        /// <summary>风系元素标签。</summary>
        public const string Wind = "wind";

        /// <summary>雷系元素标签。</summary>
        public const string Lightning = "lightning";

        /// <summary>没有明确元素时使用的泛化元素标签。</summary>
        public const string Generic = "generic";
    }

    /// <summary>
    /// 法术形态标签。
    /// </summary>
    public static class Form
    {
        /// <summary>斩击、刀刃类法术形态标签。</summary>
        public const string Slash = "slash";

        /// <summary>穿刺、箭矢类法术形态标签。</summary>
        public const string Pierce = "pierce";

        /// <summary>丸、弹、珠类法术形态标签。</summary>
        public const string Ball = "ball";

        /// <summary>范围、阵域类法术形态标签。</summary>
        public const string Aoe = "aoe";

        /// <summary>坠落、陨击类法术形态标签。</summary>
        public const string Falling = "falling";

        /// <summary>持续、流转类法术形态标签。</summary>
        public const string Sustain = "sustain";

        /// <summary>泛用术法形态标签。</summary>
        public const string Spell = "spell";
    }

    /// <summary>
    /// 法术投送方式标签。形态描述外观，投送方式描述效果如何抵达目标。
    /// </summary>
    public static class Delivery
    {
        /// <summary>从施法者位置发出并独立移动的弹丸投送方式。</summary>
        public const string Projectile = "delivery_projectile";

        /// <summary>依附施法者并仅覆盖近身范围的投送方式。</summary>
        public const string Melee = "delivery_melee";

        /// <summary>直接在指定位置产生效果的瞬发投送方式。</summary>
        public const string Instant = "delivery_instant";

        /// <summary>持续占据区域并反复作用的场域投送方式。</summary>
        public const string Field = "delivery_field";
    }

    /// <summary>
    /// 法术轨迹标签。
    /// </summary>
    public static class Motion
    {
        /// <summary>直接飞向目标或指定方向的高速轨迹标签。</summary>
        public const string Direct = "direct";

        /// <summary>持续修正方向并追踪目标的轨迹标签。</summary>
        public const string Homing = "homing";

        /// <summary>坠落轨迹标签。</summary>
        public const string Falling = "falling";

        /// <summary>贴地行进轨迹标签。</summary>
        public const string Ground = "ground";

        /// <summary>瞬发闪击轨迹标签。</summary>
        public const string Snap = "snap";

        /// <summary>旋涡或螺旋追踪轨迹标签。</summary>
        public const string Vortex = "vortex";

        /// <summary>雨落式轨迹标签。</summary>
        public const string Rain = "rain";

        /// <summary>回旋返回轨迹标签。</summary>
        public const string Return = "return";

        /// <summary>折线轨迹标签。</summary>
        public const string Zigzag = "zigzag";

        /// <summary>波形轨迹标签。</summary>
        public const string Wave = "wave";

        /// <summary>螺旋推进轨迹标签。</summary>
        public const string Spiral = "spiral";

        /// <summary>绕目标环行并收束的轨迹标签。</summary>
        public const string Orbit = "orbit";

        /// <summary>直接在目标位置显现的轨迹标签。</summary>
        public const string Appear = "appear";

        /// <summary>围绕施法者完成一次短弧挥砍的近身轨迹标签。</summary>
        public const string MeleeSweep = "melee_sweep";
    }

    /// <summary>
    /// 法术词条标签。
    /// </summary>
    public static class Modifier
    {
        /// <summary>兜底词条标签，用于没有专门命名规则的词条。</summary>
        public const string Fallback = "fallback";

        /// <summary>连发次数词条标签。</summary>
        public const string SalvoCount = "SalvoCount";

        /// <summary>爆发次数词条标签。</summary>
        public const string BurstCount = "BurstCount";
    }

    /// <summary>
    /// 法术资产系列标签。
    /// </summary>
    public static class Series
    {
        /// <summary>金属视觉和系列标签。</summary>
        public const string Metal = "metal";

        /// <summary>单体命中或单发法术标签。</summary>
        public const string Single = "single";
    }

    /// <summary>
    /// 法术词条相似性标签。
    /// </summary>
    public static class Similarity
    {
        /// <summary>控制类词条相似性标签。</summary>
        public const string Control = "control";

        /// <summary>减速类词条相似性标签。</summary>
        public const string Slow = "slow";

        /// <summary>持续伤害类词条相似性标签。</summary>
        public const string Dot = "dot";

        /// <summary>灼烧类词条相似性标签。</summary>
        public const string Burn = "burn";

        /// <summary>火系相关词条相似性标签。</summary>
        public const string Fire = "fire";

        /// <summary>冻结类词条相似性标签。</summary>
        public const string Freeze = "freeze";

        /// <summary>中毒类词条相似性标签。</summary>
        public const string Poison = "poison";

        /// <summary>范围类词条相似性标签。</summary>
        public const string Aoe = "aoe";

        /// <summary>爆破类词条相似性标签。</summary>
        public const string Blast = "blast";

        /// <summary>速度类词条相似性标签。</summary>
        public const string Speed = "speed";

        /// <summary>投射物类词条相似性标签。</summary>
        public const string Projectile = "projectile";

        /// <summary>成长类词条相似性标签。</summary>
        public const string Growth = "growth";

        /// <summary>威力类词条相似性标签。</summary>
        public const string Power = "power";

        /// <summary>伤害类词条相似性标签。</summary>
        public const string Damage = "damage";

        /// <summary>位移类词条相似性标签。</summary>
        public const string Displace = "displace";

        /// <summary>爆发类词条相似性标签。</summary>
        public const string Burst = "burst";

        /// <summary>尺寸类词条相似性标签。</summary>
        public const string Size = "size";

        /// <summary>削弱类词条相似性标签。</summary>
        public const string Debuff = "debuff";

        /// <summary>降低攻击类词条相似性标签。</summary>
        public const string AttackDown = "attack_down";

        /// <summary>降低护甲类词条相似性标签。</summary>
        public const string ArmorDown = "armor_down";

        /// <summary>牵引类词条相似性标签。</summary>
        public const string Pull = "pull";

        /// <summary>眩晕类词条相似性标签。</summary>
        public const string Stun = "stun";

        /// <summary>特殊机制类词条相似性标签。</summary>
        public const string Special = "special";

        /// <summary>随机机制类词条相似性标签。</summary>
        public const string Random = "random";

        /// <summary>交换类词条相似性标签。</summary>
        public const string Swap = "swap";

        /// <summary>连击类词条相似性标签。</summary>
        public const string Combo = "combo";

        /// <summary>沉默类词条相似性标签。</summary>
        public const string Silence = "silence";

        /// <summary>处决类词条相似性标签。</summary>
        public const string Execute = "execute";

        /// <summary>诅咒类词条相似性标签。</summary>
        public const string Curse = "curse";

        /// <summary>轨迹变化类词条相似性标签。</summary>
        public const string Trajectory = "trajectory";

        /// <summary>运动模式类词条相似性标签。</summary>
        public const string Motion = "motion";

        /// <summary>齐射数量类词条相似性标签。</summary>
        public const string Salvo = "salvo";
    }

    /// <summary>
    /// 法术词条冲突标签。
    /// </summary>
    public static class Conflict
    {
        /// <summary>覆盖击杀流程的互斥标签。</summary>
        public const string KillOverride = "kill_override";
    }
}
