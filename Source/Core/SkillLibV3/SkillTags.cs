namespace Cultiway.Core.SkillLibV3;

/// <summary>
/// 法术系统使用的语义标签常量。
/// </summary>
public static class SkillTags
{
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
    /// 法术轨迹标签。
    /// </summary>
    public static class Motion
    {
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
    }

    /// <summary>
    /// 法术词条标签。
    /// </summary>
    public static class Modifier
    {
        /// <summary>兜底词条标签，用于没有专门命名规则的词条。</summary>
        public const string Fallback = "fallback";

        /// <summary>死亡裁决词条标签。</summary>
        public const string DeathSentence = "DeathSentence";

        /// <summary>永恒诅咒词条标签。</summary>
        public const string EternalCurse = "EternalCurse";

        /// <summary>轮回试炼词条标签。</summary>
        public const string ReincarnationTrial = "ReincarnationTrial";

        /// <summary>沉默封禁词条标签。</summary>
        public const string Silence = "Silence";

        /// <summary>燃尽词条标签。</summary>
        public const string Burnout = "Burnout";

        /// <summary>混乱词条标签。</summary>
        public const string Chaos = "Chaos";

        /// <summary>连击词条标签。</summary>
        public const string Combo = "Combo";

        /// <summary>慈悲词条标签。</summary>
        public const string Mercy = "Mercy";

        /// <summary>交换或位移词条标签。</summary>
        public const string Swap = "Swap";

        /// <summary>随机附加词条标签。</summary>
        public const string RandomAffix = "RandomAffix";

        /// <summary>重力牵引词条标签。</summary>
        public const string Gravity = "Gravity";

        /// <summary>破甲词条标签。</summary>
        public const string ArmorBreak = "ArmorBreak";

        /// <summary>巨型化词条标签。</summary>
        public const string Huge = "Huge";

        /// <summary>眩晕词条标签。</summary>
        public const string Daze = "Daze";

        /// <summary>削弱词条标签。</summary>
        public const string Weaken = "Weaken";

        /// <summary>爆炸词条标签。</summary>
        public const string Explosion = "Explosion";

        /// <summary>冻结词条标签。</summary>
        public const string Freeze = "Freeze";

        /// <summary>中毒词条标签。</summary>
        public const string Poison = "Poison";

        /// <summary>灼烧词条标签。</summary>
        public const string Burn = "Burn";

        /// <summary>齐射词条标签。</summary>
        public const string Volley = "Volley";

        /// <summary>击退词条标签。</summary>
        public const string Knockback = "Knockback";

        /// <summary>加速词条标签。</summary>
        public const string Haste = "Haste";

        /// <summary>强化词条标签。</summary>
        public const string Empower = "Empower";

        /// <summary>减速词条标签。</summary>
        public const string Slow = "Slow";

        /// <summary>熟练度词条标签。</summary>
        public const string Proficiency = "Proficiency";

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
