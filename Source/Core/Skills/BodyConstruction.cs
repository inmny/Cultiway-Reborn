using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>身体构造的实际对象，不以世界中某次人物编号代替部位。</summary>
public enum BodyPartSubject
{
    /// <summary>施放者身体。</summary>
    Source = 0,

    /// <summary>实际目标的接触表面或身体部位。</summary>
    Target = 1,

    /// <summary>本次借用的实际持有物。</summary>
    Held = 2
}

/// <summary>一次动作内可被支撑、握持和接触引用的命名部位。</summary>
public sealed class BodyPartInput
{
    /// <summary>本构造内的稳定名称。</summary>
    public string Id;

    /// <summary>实际对象。</summary>
    public BodyPartSubject Subject;

    /// <summary>对象提供的真实部位，例如hand.right、foot.left、pelvis、item或surface。</summary>
    public string Part;
}

/// <summary>真实支撑部位及其在身体方向中的固定落点，位置以体高为单位。</summary>
public sealed class BodySupportPoint
{
    /// <summary>引用同一构造中的本方部位。</summary>
    public string Part;

    /// <summary>依次为前方、侧方和高度；地表及步态执行另行核对。</summary>
    public float[] Position = new float[3];
}

/// <summary>支撑与承重点资料；动作关键点中的躯干位移继续推动同一身体。</summary>
public sealed class BodySupportDefinition
{
    /// <summary>提供承重点的本方部位引用。</summary>
    public string Center;

    /// <summary>承重点相对该部位的体高偏移，不冒充骨盆位移。</summary>
    public float[] CenterOffset = new float[3];

    /// <summary>各个实际落点，不根据当前发力手猜测左右足。</summary>
    public BodySupportPoint[] Points;
}

/// <summary>两方实际部位之间的作用方式，不是多招组合配方。</summary>
public enum BodyRelationKind
{
    /// <summary>真实形状接触后使用同一身体作用。</summary>
    Touch = 0,

    /// <summary>实际手内握点与持有物握点连接。</summary>
    Grip = 1,

    /// <summary>双方抵住；须有相应控制执行后才能正式施放。</summary>
    Brace = 2,

    /// <summary>双方抓持；须有相应控制执行后才能正式施放。</summary>
    Grab = 3,

    /// <summary>沿真实接触点拨压对方兵器，收回或脱离时解除，不拖走目标身体。</summary>
    Deflect = 4
}

/// <summary>同一动作时钟内的部位关系，引用姿态标记而不另设计时器。</summary>
public sealed class BodyRelationDefinition
{
    /// <summary>本构造内的关系编号。</summary>
    public string Id;

    /// <summary>本方与对方的命名部位引用。</summary>
    public string From, To;

    /// <summary>实际作用方式。</summary>
    public BodyRelationKind Kind;

    /// <summary>双方控制的维持强度，默认一；接触和普通持物不使用此值。</summary>
    [System.ComponentModel.DefaultValue(1f), JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float ControlStrength = 1f;

    /// <summary>抓住后由实际接触点带动目标；关闭时继续原地维持。</summary>
    [System.ComponentModel.DefaultValue(false), JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool MoveTarget;

    /// <summary>带动时随真实抓点一起旋转目标；关闭时保持目标建立时的朝向。</summary>
    [System.ComponentModel.DefaultValue(false), JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool RotateTarget;

    /// <summary>被带动身体真实撞上环境时的撞伤倍率；零表示只搬动，不产生撞伤。</summary>
    [System.ComponentModel.DefaultValue(1f), JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float CollisionDamageScale = 1f;

    /// <summary>接触形状编号或握持挂点；接触为空时使用该部位全部已提供形状。</summary>
    public string FromAnchor, ToAnchor;

    /// <summary>建立及解除的已有姿态标记。</summary>
    public string FromMarker = "active", UntilMarker = "recover";
}

/// <summary>战技构造资料；过程、发力及有效时段仍使用Body已有的共同字段。</summary>
public sealed class BodyConstructionDefinition
{
    /// <summary>同一动作内的实际部位输入。</summary>
    public BodyPartInput[] Parts = Array.Empty<BodyPartInput>();

    /// <summary>明确支撑和承重点；空值沿用既有双足站立。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public BodySupportDefinition Support;

    /// <summary>本体步法，采用已有局部曲线和动作秒数，坐标以体高为单位。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionPath Footwork;

    /// <summary>握持及实际双方接触关系。</summary>
    public BodyRelationDefinition[] Relations = Array.Empty<BodyRelationDefinition>();
}

/// <summary>已解析的部位，不持有可编辑资料或真实人物。</summary>
public readonly struct CompiledBodyPart
{
    /// <summary>构造内名称和实际部位。</summary>
    public readonly string Id, Part;

    /// <summary>部位的实际对象。</summary>
    public readonly BodyPartSubject Subject;

    /// <summary>复制已经检查的部位。</summary>
    /// <param name="source">输入部位。</param>
    internal CompiledBodyPart(BodyPartInput source)
    {
        Id = source.Id;
        Part = source.Part;
        Subject = source.Subject;
    }
}

/// <summary>实际部位和已解析的动作区间，运行只读取数字与稳定名称。</summary>
public readonly struct CompiledBodyRelation
{
    /// <summary>关系编号。</summary>
    public readonly string Id;

    /// <summary>实际连接两方。</summary>
    public readonly CompiledBodyPart From, To;

    /// <summary>实际作用方式。</summary>
    public readonly BodyRelationKind Kind;

    /// <summary>已检查的双方控制强度，非控制关系为零。</summary>
    public readonly float ControlStrength;

    /// <summary>真实抓点的运动是否带动目标，不在选中时提前搬运。</summary>
    public readonly bool MoveTarget;

    /// <summary>绕真实抓点旋转目标完整表面。</summary>
    public readonly bool RotateTarget;

    /// <summary>真实环境撞伤的已检查倍率。</summary>
    public readonly float CollisionDamageScale;

    /// <summary>已选择的接触形状或握点。</summary>
    public readonly string FromAnchor, ToAnchor;

    /// <summary>原动作时间中的建立和解除时点，姿态维持不启动另一只时钟。</summary>
    public readonly float FromTime, UntilTime;

    /// <summary>加入姿态维持后的施放时点，解除时刻不会因局部时间停住而反复生效。</summary>
    public readonly float FromWorldTime, UntilWorldTime;

    /// <summary>复制已检查的关系。</summary>
    /// <param name="source">原关系。</param><param name="from">本方。</param><param name="to">对方。</param>
    /// <param name="start">建立时点。</param><param name="until">解除时点。</param>
    /// <param name="holdAt">共同维持位置。</param><param name="holdSeconds">实际维持秒数。</param>
    internal CompiledBodyRelation(BodyRelationDefinition source, CompiledBodyPart from, CompiledBodyPart to,
        float start, float until, float holdAt, float holdSeconds)
    {
        Id = source.Id;
        Kind = source.Kind;
        MoveTarget = source.MoveTarget;
        RotateTarget = source.RotateTarget;
        CollisionDamageScale = source.CollisionDamageScale;
        ControlStrength = Kind is BodyRelationKind.Brace or BodyRelationKind.Grab or BodyRelationKind.Deflect ? source.ControlStrength : 0f;
        From = from;
        To = to;
        FromAnchor = source.FromAnchor;
        ToAnchor = source.ToAnchor;
        FromTime = start;
        UntilTime = until;
        FromWorldTime = start + (start > holdAt ? holdSeconds : 0f);
        UntilWorldTime = until + (until > holdAt ? holdSeconds : 0f);
    }
}

/// <summary>身体构造的不可变输入与关系，编译一次解析名称，不在并行取样时反射。</summary>
public sealed class CompiledBodyConstruction
{
    /// <summary>正式名称索引，发布后只读。</summary>
    public IReadOnlyDictionary<string, CompiledBodyPart> Parts { get; }

    /// <summary>同一动作内已经解析的关系。</summary>
    public IReadOnlyList<CompiledBodyRelation> Relations { get; }

    /// <summary>按实际部位解析的本方发力点。</summary>
    public string Effector { get; }

    /// <summary>实际手内与器物握点。</summary>
    public string HandGrip { get; } = "grip";

    /// <summary>已选择的器物握点。</summary>
    public string Grip { get; }

    /// <summary>相对脚下、以体高为单位的固定双足落点。</summary>
    public Vector3 LeftFoot { get; } = new(0, -CompiledBodyAction.FootSide, 0);

    /// <summary>右足实际落点。</summary>
    public Vector3 RightFoot { get; } = new(0, CompiledBodyAction.FootSide, 0);

    /// <summary>承重点相对骨盆的实际偏移。</summary>
    public Vector3 CenterOffset { get; }

    /// <summary>是否有明确支撑声明，供控制占用和实际身体接入核对。</summary>
    public bool HasSupport { get; }

    /// <summary>是否声明实际本体步法。</summary>
    public bool HasFootwork => footwork != null;

    /// <summary>整段步法的单位体高路程上界，空间筛选须包含未走完的部分。</summary>
    public float FootworkTravel { get; }

    /// <summary>单位体高下的最大步法速度，编译时固定。</summary>
    public float FootworkSpeed { get; }

    /// <summary>只计算一次的路径精度步长，运行时再截在实际曲段边界。</summary>
    private readonly float footworkStep = float.PositiveInfinity;

    /// <summary>指定阶段前能够到达的根点包络。</summary>
    /// <param name="until">已经换算为动作局部时间的时点。</param>
    internal float FootworkExtent(float until) => footwork?.ExtentBudget(until) ?? 0f;

    /// <summary>脚下曲线需要的数值步长，不用巨大直线替代弯曲路径。</summary>
    /// <param name="age">动作局部时间。</param>
    internal float FootworkStep(float age) => footwork == null
        ? float.PositiveInfinity
        : Mathf.Min(footwork.UntilBoundary(age, 0f), footworkStep);

    /// <summary>至少声明一个实际接触，未声明时只有身体姿态而没有虚构作用。</summary>
    public bool MayContact => HasTouchRelations || HasControlRelations;

    /// <summary>是否有需要真实双方控制的关系。</summary>
    public bool HasControlRelations => Relations.Any(r => r.Kind is BodyRelationKind.Brace or BodyRelationKind.Grab or BodyRelationKind.Deflect);

    /// <summary>身体普通命中与控制接触分别确认。</summary>
    public bool HasTouchRelations => implicitRelations || Relations.Any(r => r.Kind == BodyRelationKind.Touch);

    /// <summary>旧资料没有显式关系，继续采用原实际接触。</summary>
    private readonly bool implicitRelations;

    /// <summary>已经复制的共同曲线。</summary>
    private readonly CompiledConstructionPath footwork;

    /// <summary>解析部位、支撑、姿态标记、步法及双方关系，再提供不可变执行输入。</summary>
    /// <param name="body">已有身体动作，发力与过程仍来自同一份资料。</param>
    /// <param name="holdAt">共同维持位置。</param><param name="holdSeconds">实际维持秒数。</param>
    internal CompiledBodyConstruction(BodyActionDefinition body, float holdAt, float holdSeconds)
    {
        BodyConstructionDefinition source = body.Construction;
        var parts = new Dictionary<string, CompiledBodyPart>(StringComparer.Ordinal);
        if (source != null)
        {
            if (source.Parts == null || source.Parts.Length > 32 || source.Relations == null ||
                source.Relations.Length > 32)
                throw new InvalidDataException("Body/Construction：部位和关系须为有限集合，各最多32项");
            foreach (var part in source.Parts)
            {
                if (part == null || string.IsNullOrWhiteSpace(part.Id) || string.IsNullOrWhiteSpace(part.Part) ||
                    !Enum.IsDefined(typeof(BodyPartSubject), part.Subject) || parts.ContainsKey(part.Id))
                    throw new InvalidDataException("Body/Construction/Parts：部位名称、对象或实际部位无效或重复");
                if (part.Subject == BodyPartSubject.Held && body.WeaponForm == null)
                    throw new InvalidDataException("Body/Construction/Parts/" + part.Id + "：没有声明实际借用器物");
                parts.Add(part.Id, new CompiledBodyPart(part));
            }
        }

        Parts = new ReadOnlyDictionary<string, CompiledBodyPart>(parts);
        Effector = source == null ? body.Effector : Resolve(body.Effector, BodyPartSubject.Source).Part;
        Grip = body.Grip;
        HasSupport = source?.Support != null;
        if (HasSupport)
        {
            var support = source.Support;
            if (Resolve(support.Center, BodyPartSubject.Source).Part != "pelvis" || support.Points?.Length != 2)
                throw new InvalidDataException("Body/Construction/Support：当前身体需要骨盆承重点及明确的左右足支撑");
            CenterOffset = Vector(support.CenterOffset, "Support/CenterOffset");
            var used = new HashSet<string>();
            foreach (var point in support.Points)
            {
                string part = point == null ? null : Resolve(point.Part, BodyPartSubject.Source).Part;
                if (part is not ("foot.left" or "foot.right") || !used.Add(part))
                    throw new InvalidDataException("Body/Construction/Support/Points：当前需要不重复的foot.left和foot.right");
                Vector3 position = Vector(point.Position, "Support/Points/" + point.Part);
                if (position.z != 0f ||
                    position.sqrMagnitude >= CompiledBodyAction.LegReach * CompiledBodyAction.LegReach)
                    throw new InvalidDataException("Body/Construction/Support/Points：当前固定站立需要地面落点及可达脚距，抬脚按后续步态执行");
                if (part == "foot.left") LeftFoot = position;
                else RightFoot = position;
            }

            if ((LeftFoot - RightFoot).sqrMagnitude < .000001f)
                throw new InvalidDataException("Body/Construction/Support/Points：两个支撑不能重合");
        }

        if (source?.Footwork != null)
        {
            if (source.Footwork.Space != ConstructionPathSpace.Local || source.Footwork.Loop)
                throw new InvalidDataException("Body/Construction/Footwork：本体步法需要有限局部曲线");
            if (source.Footwork.Forward != null)
                throw new InvalidDataException("Body/Construction/Footwork：完整身体朝向使用Heading，平面位移曲线的Forward须为空");
            footwork = new CompiledConstructionPath(source.Footwork, "Body/Construction/Footwork");
            if (source.Footwork.Keys.Any(key => key.Point[2] != 0f || key.In[2] != 0f || key.Out[2] != 0f))
                throw new InvalidDataException("Body/Construction/Footwork：平面步法的高度须为零，腾起和跃落使用同刻Support/Rise");
            if (footwork.Duration > body.Keys[body.Keys.Length - 1].At)
                throw new InvalidDataException("Body/Construction/Footwork：步法不能超过身体动作过程");
            FootworkTravel = (float)footwork.TravelBudget(footwork.Duration, 1f);
            FootworkSpeed = footwork.SpeedBudget(1f);
            footworkStep = Mathf.Min(.02f / Mathf.Max(.0001f, FootworkSpeed),
                Mathf.Sqrt(.01f / Mathf.Max(.0001f, footwork.AccelerationBound.magnitude)));
        }

        var markers = new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["start"] = 0f, ["release"] = body.ReleaseAt ?? body.ActiveFrom, ["active"] = body.ActiveFrom,
            ["recover"] = body.ActiveUntil, ["end"] = body.Keys[body.Keys.Length - 1].At
        };
        foreach (var key in body.Keys.Where(k => k.Id != null))
            if (string.IsNullOrWhiteSpace(key.Id) || markers.ContainsKey(key.Id))
                throw new InvalidDataException("Body/Keys/Id：姿态标记为空、重复或占用了已有阶段名");
            else markers.Add(key.Id, key.At);
        implicitRelations = source == null;
        var relations = new List<CompiledBodyRelation>();
        foreach (var relation in source?.Relations ?? Array.Empty<BodyRelationDefinition>())
        {
            if (relation == null || string.IsNullOrWhiteSpace(relation.Id) || relations.Any(r => r.Id == relation.Id) ||
                !Enum.IsDefined(typeof(BodyRelationKind), relation.Kind) ||
                !markers.TryGetValue(relation.FromMarker ?? "", out float start) ||
                !markers.TryGetValue(relation.UntilMarker ?? "", out float until) || start >= until)
                throw new InvalidDataException("Body/Construction/Relations：关系编号、种类或姿态阶段引用无效");
            CompiledBodyPart from = Resolve(relation.From), to = Resolve(relation.To);
            if (relation.Kind == BodyRelationKind.Deflect && to.Part != "item")
                throw new InvalidDataException("Body/Construction/Relations/Deflect：拨压需要目标实际持有物item");
            if (relation.MoveTarget && relation.Kind != BodyRelationKind.Grab)
                throw new InvalidDataException("Body/Construction/Relations/MoveTarget：只有真实抓持关系可以带动目标");
            if (relation.RotateTarget && !relation.MoveTarget)
                throw new InvalidDataException("Body/Construction/Relations/RotateTarget：旋转需要真实带动关系");
            if (!CompiledForceRouting.Finite(relation.CollisionDamageScale) || relation.CollisionDamageScale < 0f ||
                relation.CollisionDamageScale > 1000f)
                throw new InvalidDataException("Body/Construction/Relations/CollisionDamageScale：撞伤倍率须在0至1000之间");
            if (relation.Kind == BodyRelationKind.Grip)
            {
                if (relations.Any(r => r.Kind == BodyRelationKind.Grip) || from.Subject != BodyPartSubject.Source ||
                    from.Part != Effector || to.Subject != BodyPartSubject.Held || to.Part != "item" ||
                    string.IsNullOrWhiteSpace(relation.FromAnchor) || string.IsNullOrWhiteSpace(relation.ToAnchor) ||
                    start != 0f || until != markers["end"])
                    throw new InvalidDataException("Body/Construction/Relations/Grip：当前握持须连接实际发力手与整件器物，并覆盖完整动作");
                HandGrip = relation.FromAnchor;
                Grip = relation.ToAnchor;
            }
            else if (relation.Kind == BodyRelationKind.Touch && (start < body.ActiveFrom || until > body.ActiveUntil))
                throw new InvalidDataException("Body/Construction/Relations/Touch：接触区间须位于实际有效阶段内");

            if (relation.Kind is BodyRelationKind.Brace or BodyRelationKind.Grab or BodyRelationKind.Deflect &&
                (from.Subject is not (BodyPartSubject.Source or BodyPartSubject.Held) ||
                 to.Subject != BodyPartSubject.Target ||
                 start < body.ActiveFrom || until > body.ActiveUntil ||
                 !CompiledForceRouting.Finite(relation.ControlStrength) ||
                 relation.ControlStrength <= 0f || relation.ControlStrength > 1000f))
                throw new InvalidDataException("Body/Construction/Relations：双方控制需要本方与目标实际部位、有效动作内的区间及0至1000之间的正控制强度");

            relations.Add(new CompiledBodyRelation(relation, from, to, start, until, holdAt, holdSeconds));
        }

        if (source != null && body.WeaponForm != null && !relations.Any(r => r.Kind == BodyRelationKind.Grip))
            throw new InvalidDataException("Body/Construction/Relations：借用器物缺少实际握持关系");
        Relations = relations.AsReadOnly();
    }

    /// <summary>资料边界解析同一构造的真实输入。</summary>
    /// <param name="id">部位名称。</param><param name="subject">需要的对象，空值允许任一实际对象。</param>
    private CompiledBodyPart Resolve(string id, BodyPartSubject? subject = null) =>
        id != null && Parts.TryGetValue(id, out var part) && (!subject.HasValue || part.Subject == subject)
            ? part
            : throw new InvalidDataException("Body/Construction/Parts：缺少实际部位引用或对象不符：" + id);

    /// <summary>只选声明的实际作用形状，完整包络与实际接触共用同一选择。</summary>
    /// <param name="held">是否来自借用器物。</param><param name="shape">资源作用形状名称。</param>
    /// <param name="age">施放世界时间，空值查询是否在任一时点使用。</param>
    /// <param name="includeEnd">末段扫掠保留解除时点的实际形状。</param>
    public bool UsesContact(bool held, string shape, float? age = null, bool includeEnd = false)
    {
        if (implicitRelations) return true;
        for (int i = 0; i < Relations.Count; i++)
        {
            var relation = Relations[i];
            if (relation.Kind is BodyRelationKind.Touch or BodyRelationKind.Brace or BodyRelationKind.Grab or BodyRelationKind.Deflect &&
                relation.From.Subject == (held ? BodyPartSubject.Held : BodyPartSubject.Source) &&
                MatchesContact(relation.FromAnchor, shape) &&
                (!age.HasValue || age.Value >= relation.FromWorldTime &&
                    (includeEnd ? age.Value <= relation.UntilWorldTime : age.Value < relation.UntilWorldTime)))
                return true;
        }

        return false;
    }

    /// <summary>精确编号或斜线分组均可选择真实形状，空值表示未限制。</summary>
    /// <param name="selection">具体编号或分组。</param><param name="shape">资源中的完整形状编号。</param>
    public static bool MatchesContact(string selection, string shape) => selection == null || selection == shape ||
                                                                         shape.Length > selection.Length &&
                                                                         shape[selection.Length] == '/' &&
                                                                         shape.StartsWith(selection,
                                                                             StringComparison.Ordinal);

    /// <summary>步法与姿态共用动作时间，具体人物移动由同一正式执行入口接入。</summary>
    /// <param name="age">已扣除维持的动作时间。</param><param name="position">本体局部位移。</param>
    /// <param name="velocity">局部速度。</param>
    public void SampleFootwork(float age, out Vector3 position, out Vector3 velocity)
    {
        position = velocity = Vector3.zero;
        footwork?.Sample(age, out position, out velocity);
    }

    /// <summary>只在资料入口复制三维数字。</summary>
    /// <param name="value">三个有限分量。</param><param name="path">具体字段位置。</param>
    private static Vector3 Vector(float[] value, string path) =>
        CompiledBodyAction.Vector(value, "Body/Construction/" + path);
}
