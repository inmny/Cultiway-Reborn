using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.Visuals.Forms;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>只读的共享计划。修改原始资料不会改变已经开始的施放。</summary>
public sealed class CompiledAbility
{
    /// <summary>本计划使用的完整资源版本，运行中保持不变。</summary>
    public int ResourceVersion { get; }

    /// <summary>稳定编号。</summary>
    public string Id { get; }

    /// <summary>资料修订号。</summary>
    public int Revision { get; }

    /// <summary>直接来源的修订和改造说明。</summary>
    internal (int Revision, string Change)? Parent { get; }

    /// <summary>进入已登记资源报价的基础需求。</summary>
    public float Cost { get; }

    /// <summary>按声明顺序冻结的资源编号；无消耗使用原编号 none。</summary>
    public IReadOnlyList<string> Resources { get; }

    /// <summary>已验证的资源选择方式。</summary>
    public SkillCastResourceRequirementMode ResourceMode { get; }

    /// <summary>正式受理后的恢复时间。</summary>
    public float Cooldown { get; }

    /// <summary>正式施放使用的作用量。</summary>
    public float Power { get; }

    /// <summary>最大持续时间。</summary>
    public float Lifetime { get; }

    /// <summary>同一人物同一来源的并存上限；零表示不限。</summary>
    public int ConcurrentLimit { get; }

    /// <summary>作者明确提供的目标选择距离，不作为实际命中半径。</summary>
    public float? TargetRange { get; }

    /// <summary>整招默认作用及公开的目标选取关系。</summary>
    public CompiledConstructionImpact Impact { get; }

    /// <summary>是否需要查询真实地块接触；普通战斗不新增地块遍历。</summary>
    public bool HasWorldEffects { get; }

    /// <summary>只有重新进入型地块作用需要逐步检查是否离开；一次作用不遍历历史地块。</summary>
    public bool HasWorldReentry { get; }

    /// <summary>是否明确等待真实地块接触；不把事件监听当作地形操作。</summary>
    public bool HasWorldContactEvents { get; }

    /// <summary>是否实际声明有限地表传播。</summary>
    public bool HasPropagation { get; }

    /// <summary>实际攻击部位及身体采用的最大伤害倍率，供自主决策估算。</summary>
    public float ContactDamageScale { get; }

    /// <summary>实际参与人物接触的独立作用，身体阶段及部位覆盖已经展开。</summary>
    public IReadOnlyList<CompiledConstructionImpact> ActorImpacts { get; }

    /// <summary>是否有实际人物对应，普通构造无需额外实例记录。</summary>
    public bool HasCorrespondences { get; }

    internal int ShadowHoldNode { get; }

    /// <summary>实际承载了活动映像的镜面部位。</summary>
    internal int[] MirrorNodes { get; }

    /// <summary>本次实际承载景物带的部位。</summary>
    internal int[] LandscapeNodes { get; }

    /// <summary>具有实际散料牵引的深口部位。</summary>
    internal int[] MaterialVortexNodes { get; }

    /// <summary>具有真实器口的有限部位。</summary>
    internal int[] WeaponChamberNodes { get; }

    /// <summary>受击分担的参数副本；当次契约另存实际余额。</summary>
    internal CompiledSoulPact SoulPact { get; }

    /// <summary>实际攻击部位及身体采用的最大推拉力绝对值，供自主决策估算。</summary>
    public float ContactForce { get; }

    /// <summary>实际攻击部位与身体声明的最长受击停顿。</summary>
    public float ContactStagger { get; }

    /// <summary>实际攻击部位与身体声明的最长束缚。</summary>
    public float ContactBind { get; }

    /// <summary>不依赖水平推拉的最大离地力度。</summary>
    public float ContactLaunch { get; }

    /// <summary>真实攻击能力及护甲损耗的最长持续时间。</summary>
    public float ContactImpairment { get; }

    /// <summary>已编译双方控制的维持规模，供自主使用估值，不代替实际对抗。</summary>
    public float ContactRelationControl { get; }

    /// <summary>自主选择的控制用途估值，不作为实际速度或秒数。</summary>
    public float ContactControl =>
        Math.Max(ContactRelationControl, Math.Max(ContactImpairment,
            Math.Max(ContactForce, Math.Max(ContactStagger, Math.Max(ContactBind, ContactLaunch)))));

    /// <summary>是否有真实屏障，不把只有拦截作用的构造误报成无用能力。</summary>
    public bool HasBarrier { get; }

    /// <summary>是否声明生成主体收束，供普通演示验证真实回能。</summary>
    public bool HasRecovery { get; }

    /// <summary>存在世界转交或真实材料选择。</summary>
    internal bool HasWorldMaterials { get; }

    /// <summary>最多需要交给世界的记录数，在支付前保留额度。</summary>
    internal int ResidueCount => Nodes.Count(node => node.Residue != null);

    /// <summary>预期留置数值格总量，不临时挤掉已存在的世界材料。</summary>
    internal int ResidueCells => Nodes.Where(node => node.Residue != null).Sum(node =>
        node.FlowSettings == null ? 0 : node.FlowSettings.Cells * node.FlowSettings.Cells);

    /// <summary>有限展开后的形体数。</summary>
    public int FormCount => Nodes.Length;

    /// <summary>本次施放需要借用的库存材料。</summary>
    internal CompiledInventoryInput[] Inventory { get; }
    /// <summary>按原资料顺序固定的库存消耗操作。</summary>
    internal CompiledEventOperation[] InventoryOperations { get; }

    /// <summary>同一真实计划的材料、持续、重复作用和资源规模统计。</summary>
    public ConstructionMeasure Measure { get; }

    /// <summary>同一计划的候选可达包络，与真实接触形状分别维护。</summary>
    public ConstructionReach Reach { get; }

    /// <summary>完整展开后的结构、步骤及数值工作容量。</summary>
    public ConstructionBudget Budget { get; }

    /// <summary>本计划实际采用的操作约定和按参数补齐的状态、资源需求。</summary>
    public IReadOnlyList<CompiledOperationRequirement> Operations { get; }

    /// <summary>生成主体操作的实际资源需求；身体及来源仍由各自维护者核对。</summary>
    public ConstructionOperationResource OperationResources { get; }

    /// <summary>形体、控制骨架和身体合计的基础计算量。</summary>
    public int WorkSize { get; }

    /// <summary>初始及后续材料采用的伤害规则，交给来源玩法核对支持情况。</summary>
    public IReadOnlyList<string> Matters { get; }

    /// <summary>已经编译的身体动作，空值表示不借用身体。</summary>
    public CompiledBodyAction Body { get; }

    /// <summary>已经复制的实际根连接，空值保留首次格式的固定根。</summary>
    internal CompiledBinding Binding { get; }

    /// <summary>主体就是已借用的器物，握持和离手共用同一个形体。</summary>
    internal bool UsesHeldWeapon => Binding?.Anchor == AbilityAnchor.HeldWeapon;

    /// <summary>已经验证的选择类别与使用场景，不改变真实作用的接触筛选。</summary>
    public CompiledConstructionUse Use { get; }

    /// <summary>按父体先于子体排列的连续计划。</summary>
    internal CompiledForm[] Nodes { get; }

    /// <summary>各接触组已经统一的频率和次数规则，最后一组为身体。</summary>
    internal CompiledContactPolicy[] ContactPolicies { get; }

    /// <summary>人物和地块共同定时作用的间隔，只在真实时点拆步。</summary>
    internal float[] PulseIntervals { get; }

    /// <summary>未采用重新进入规则时不扫描已接触记录。</summary>
    internal bool HasReentry { get; }

    /// <summary>是否有必须在受理时提供实际目标的相对路径。</summary>
    internal bool NeedsPathTarget { get; }

    /// <summary>射线从开始时便需要明确的真实目标。</summary>
    internal bool NeedsRayTarget { get; }

    /// <summary>飞行显式目标需要本次完整数值输入。</summary>
    internal bool NeedsFlightGoal { get; }
    /// <summary>飞行采用来源位置，不以根姿态代替人物输入。</summary>
    internal bool NeedsFlightSource { get; }
    /// <summary>飞行采用本次目标位置。</summary>
    internal bool NeedsFlightTarget { get; }
    /// <summary>固定方向飞行需要真实非零出手方向。</summary>
    internal bool NeedsFlightDirection { get; }

    /// <summary>按来源及事件直接索引的等待部位；没有条件时为空。</summary>
    internal int[][] EventListeners { get; }

    /// <summary>同次施放内的有限条件操作。</summary>
    internal CompiledEventOperation[] EventOperations { get; }

    /// <summary>已经按依赖排序的命名操作，不在运行中解释原描述。</summary>
    internal CompiledConstructionStep[] Steps { get; }

    /// <summary>带折景形体且出口唯一的映射端点，显示读取同一对已编译的部位。</summary>
    internal (int Near, int Far)[] SpatialViewPairs { get; }

    /// <summary>只在实际人物接触时可执行的步骤索引。</summary>
    internal int[] ActorTransferSteps { get; }

    /// <summary>等待世界移动真实借用法宝的步骤索引。</summary>
    internal int[] ArtifactTransferSteps { get; }

    /// <summary>向本次指定人物送达实际内容的步骤。</summary>
    internal int[] DirectSignalSteps { get; }

    /// <summary>同类指令、参数与连续绑定的直接索引。</summary>
    internal CompiledOperationProgram Program { get; }

    /// <summary>只有存在命名回能时才分配确认份额，普通构造不增加逐部位账本处理。</summary>
    internal bool HasStepRecovery { get; }

    /// <summary>实际要求用闭合屏障施加状态的部位；其他构造不采集闭环。</summary>
    internal int[] EnclosureNodes { get; }

    /// <summary>复制已经验证的能力头部，保存有限展开结果。</summary>
    /// <param name="definition">已检查资料。</param>
    /// <param name="nodes">已经解析的部位。</param>
    /// <param name="body">已经复制及核对的身体动作。</param>
    /// <param name="contacts">按组统一的接触规则。</param>
    /// <param name="listeners">已经解析的事件关系。</param>
    /// <param name="operations">已经复制的条件操作。</param>
    /// <param name="steps">已解析的命名输入输出。</param>
    internal CompiledAbility(AbilityDefinition definition, CompiledForm[] nodes, CompiledBodyAction body,
        CompiledContactPolicy[] contacts, int[][] listeners, CompiledEventOperation[] operations,
        CompiledConstructionStep[] steps)
    {
        InventoryOperations = operations.Where(operation => operation.Kind == ConstructionOperationKind.ConsumeInventory).ToArray();
        ResourceVersion = FormResources.Version;
        EnclosureNodes = Enumerable.Range(0, nodes.Length).Where(i => nodes[i].EnclosesActors).ToArray();
        Id = definition.Id;
        Revision = definition.Revision;
        if (definition.Parent != null) Parent = (definition.Parent.Revision, definition.Parent.Change);
        Cost = definition.Cost;
        Resources = Array.AsReadOnly(new[] { definition.Resource }.Concat(definition.AdditionalResources).ToArray());
        ResourceMode = definition.ResourceMode;
        Cooldown = definition.Cooldown;
        Power = definition.Power;
        Lifetime = definition.Lifetime;
        ConcurrentLimit = definition.ConcurrentLimit;
        TargetRange = definition.TargetRange;
        Nodes = nodes;
        HasCorrespondences = nodes.Any(node => node.Correspondence != null);
        MirrorNodes = Enumerable.Range(0, nodes.Length).Where(i => nodes[i].Correspondence?.MirrorImage != null).ToArray();
        LandscapeNodes = Enumerable.Range(0, nodes.Length).Where(i => nodes[i].LandscapeSlice != null).ToArray();
        MaterialVortexNodes = Enumerable.Range(0, nodes.Length).Where(i => nodes[i].MaterialVortex != null).ToArray();
        WeaponChamberNodes = Enumerable.Range(0, nodes.Length).Where(i => nodes[i].WeaponChamber != null).ToArray();
        if (WeaponChamberNodes.Length > 4) throw new InvalidDataException("WeaponChamber：同一次施放最多四处器口");
        if (MaterialVortexNodes.Length > 4) throw new InvalidDataException("MaterialVortex：同一次施放最多四处深口");
        if (LandscapeNodes.Sum(i => nodes[i].LandscapeSlice.Width * nodes[i].LandscapeSlice.Length) > 640)
            throw new InvalidDataException("LandscapeSlice：同一次施放最多采六百四十个景物格");
        foreach (int index in MirrorNodes)
        {
            var relation = nodes[index].Correspondence;
            if (relation.MirrorImage.Fracture is not { } mirror) continue;
            int trigger = Array.FindIndex(nodes, node => node.Path == mirror.Trigger);
            if (trigger < 0 || trigger == index || nodes[trigger].ContactRole != ConstructionContactRole.Attack ||
                nodes[trigger].Resource.Contacts.Length == 0 || nodes[trigger].MirrorStrike ||
                nodes[index].ContactRole != ConstructionContactRole.Barrier ||
                relation.DamageFraction <= 0f || relation.Target != ConstructionCorrespondenceTarget.Target ||
                relation.Effects != ConstructionCorrespondenceEffects.None)
                throw new InvalidDataException(nodes[index].Path + "/MirrorImage/Fracture：需要一个实际的击镜攻击部位和可向原身传伤的镜面");
            mirror.TriggerNode = trigger;
            nodes[trigger].MirrorStrike = true;
        }
        ShadowHoldNode = Array.FindIndex(nodes, node => node.Correspondence?.ShadowHold != null);
        if (ShadowHoldNode >= 0 && Array.FindIndex(nodes, ShadowHoldNode + 1, node => node.Correspondence?.ShadowHold != null) >= 0)
            throw new InvalidDataException("构造/Correspondence/ShadowHold：一次施放只能对应一具抱持影身");
        Operations = Array.AsReadOnly(nodes.SelectMany(node => node.Operations).ToArray());
        OperationResources = Operations.Aggregate(ConstructionOperationResource.None,
            (current, operation) => current | operation.Resources);
        Impact = definition.Impact == null
            ? CompiledConstructionImpact.Default
            : new CompiledConstructionImpact(definition.Impact, "构造/Impact");
        Inventory = AbilityCompiler.CompileInventory(definition, operations,
            nodes.Select(node => node.Impact).Append(Impact).Append(body?.Impact)
                .Concat(body?.Stages.Select(stage => stage.Impact) ?? Enumerable.Empty<CompiledConstructionImpact>()));
        Use = new CompiledConstructionUse(definition.Use, Impact.Targets);
        if (definition.SoulPact != null)
            SoulPact = new CompiledSoulPact(definition.SoulPact, Use.Target, Impact.Targets);
        CompiledConstructionImpact[] impacts = nodes
            .Where(node => node.AffectsActors || node.EnclosesActors || node.Conduction != null ||
                           node.EventOperations.Any(op => op.Conduction != null))
            .SelectMany(node => node.BodyWindow && node.Impact == null
                ? body.Stages.Select(stage => stage.Impact ?? Impact)
                : new[] { node.Impact ?? Impact })
            .Concat(body?.Construction.MayContact != true
                ? Array.Empty<CompiledConstructionImpact>()
                : body.Stages.Select(stage => stage.Impact ?? Impact))
            .Distinct().ToArray();
        ActorImpacts = Array.AsReadOnly(impacts);
        var worldEffects = nodes.SelectMany(node => node.BodyWindow && node.Impact == null
                ? body.Stages.Select(stage => stage.Impact ?? Impact)
                : new[] { node.Impact ?? Impact })
            .Concat(body?.Construction.MayContact != true
                ? Array.Empty<CompiledConstructionImpact>()
                : body.Stages.Select(stage => stage.Impact ?? Impact))
            .SelectMany(impact => impact.WorldEffects).ToArray();
        PulseIntervals = contacts.Concat(worldEffects.Select(effect => effect.Contact))
            .Where(policy => policy.Mode == ConstructionContactMode.Pulse).Select(policy => policy.Interval)
            .Concat(nodes.Where(node => node.RadialMotion != null && node.RadialMotion.Speed != 0f).Select(node => node.RadialMotion.Interval)).Distinct().ToArray();
        HasWorldEffects = worldEffects.Length > 0;
        HasWorldReentry = worldEffects.Any(effect => effect.Contact.Mode == ConstructionContactMode.Reentry);
        ContactDamageScale = impacts.Select(impact => impact.DamageScale).DefaultIfEmpty(0f).Max();
        ContactStagger = impacts.Select(impact => impact.StaggerSeconds).DefaultIfEmpty(0f).Max();
        ContactBind = impacts.Select(impact => impact.BindSeconds).DefaultIfEmpty(0f).Max();
        ContactLaunch = impacts.Select(impact => Math.Max(impact.Launch ? impact.Lift : 0f, impact.Impulse?.z ?? 0f))
            .DefaultIfEmpty(0f).Max();
        ContactForce = impacts.Select(impact => Math.Max(Math.Abs(impact.Force), impact.Impulse?.magnitude ?? 0f))
            .DefaultIfEmpty(0f).Max();
        ContactImpairment = impacts.Select(impact => Math.Max(impact.StrainSeconds, impact.ExposureSeconds))
            .DefaultIfEmpty(0f).Max();
        HasBarrier = body?.Guard.Enabled == true || nodes.Any(node => node.ContactRole == ConstructionContactRole.Barrier);
        HasWorldMaterials = nodes.Any(node =>
            node.MaterialVortex != null ||
            node.Residue != null || node.Conduction != null || node.EventOperations.Any(op => op.Conduction != null) ||
            node.Recovery?.WorldMaterials == true ||
            node.Changes.Any(change => change.WorldMatter != null) ||
            node.EventOperations.Any(operation => operation.WorldMatter != null) ||
            node.EventOperations.Any(operation => operation.Recovery?.WorldMaterials == true));
        HasRecovery = nodes.Any(node =>
            node.Recovery != null || node.EventOperations.Any(operation => operation.Recovery != null));
        EventListeners = listeners;
        HasWorldContactEvents = nodes.Any(node => node.Activation?.Trigger == ConstructionChangeTrigger.WorldContact) ||
                                operations.Any(operation => operation.When?.Trigger == ConstructionChangeTrigger.WorldContact);
        EventOperations = operations;
        HasPropagation = operations.Any(operation => operation.Propagation != null);
        NeedsRayTarget = operations.Any(operation => operation.Ray != null);
        Steps = steps;
        SpatialViewPairs = AbilityCast.CompileSpatialViews(nodes, steps);
        ActorTransferSteps = Enumerable.Range(0, steps.Length).Where(i => steps[i].Action.Kind == ConstructionOperationKind.MapActor).ToArray();
        ArtifactTransferSteps = Enumerable.Range(0, steps.Length).Where(i => steps[i].Action.Kind == ConstructionOperationKind.MapArtifact).ToArray();
        DirectSignalSteps = Enumerable.Range(0, steps.Length).Where(i => steps[i].Action.Kind == ConstructionOperationKind.SendSignal).ToArray();
        if (DirectSignalSteps.Length != 0 && Use.Target is not (ActiveAbilityTargetMode.Object or ActiveAbilityTargetMode.Self))
            throw new InvalidDataException("Use/Target：指定传讯须选择人物或本人");
        HasStepRecovery = steps.Any(step => step.Action.Recovery != null);
        Matters = nodes
            .SelectMany(node => node.Changes.Select(change => change.Matter.DamageKind)
                .Concat(node.EventOperations.Where(operation => operation.Matter != null)
                    .Select(operation => operation.Matter.DamageKind)).Prepend(node.Matter.DamageKind))
            .Distinct().ToArray();
        Body = body;
        ContactRelationControl = body?.Construction.Relations.Select(relation =>
                                         relation.ControlStrength * (relation.UntilWorldTime - relation.FromWorldTime))
                                     .DefaultIfEmpty(0f).Max() ??
                                 0f;
        WorkSize = Math.Max(1,
            nodes.Sum(node =>
                1 + (node.FlowSettings == null ? 0 : node.FlowSettings.Cells * node.FlowSettings.Cells * 4) +
                node.Resource.Rig.Count + node.Changes.Length +
                node.EventOperations.Sum(op => 1 + (op.Flight?.Path?.Count ?? 0) + (op.Flight?.Goal?.Steering?.Path?.Count ?? 0) + (op.Flight?.Goal?.OffsetPath?.Count ?? 0) +
                    (op.Propagation?.MaximumCells ?? 0)) + (node.FlightPath?.Count ?? 0) + (node.Flight?.Goal?.Steering?.Path?.Count ?? 0) + (node.Flight?.Goal?.OffsetPath?.Count ?? 0)) +
            (body == null ? 0 : 1));
        NeedsPathTarget = nodes.Any(node =>
            node.FlightPath?.Space == ConstructionPathSpace.TargetLine ||
            node.EventOperations.Any(op => op.Flight?.Path?.Space == ConstructionPathSpace.TargetLine));
        var goals = nodes.Select(node => node.Flight?.Goal).Concat(operations.Select(operation => operation.Flight?.Goal))
            .Where(goal => goal != null).ToArray();
        NeedsFlightGoal = goals.Length > 0;
        NeedsFlightSource = goals.Any(goal => goal.Anchor == ConstructionFlightAnchor.Source);
        NeedsFlightTarget = goals.Any(goal => goal.Anchor == ConstructionFlightAnchor.Target);
        NeedsFlightDirection = goals.Any(goal => goal.Anchor == ConstructionFlightAnchor.Direction);
        ContactPolicies = contacts;
        HasReentry = contacts.Any(policy => policy.Mode == ConstructionContactMode.Reentry);
        Binding = definition.Binding == null ? null : new CompiledBinding(definition.Binding);
        Budget = new ConstructionBudget(this);
        Reach = new ConstructionReach(this);
        Measure = new ConstructionMeasure(this, definition.Power);
        Program = new CompiledOperationProgram(this);
    }
}

/// <summary>只含运行需要的数值及解析后的资源引用，不持有可编辑定义。</summary>
internal sealed class CompiledForm
{
    /// <summary>展开这个部件时实际使用的位置分布。</summary>
    internal ConstructionDistributionSource? DistributionSource;
    /// <summary>已复制的当前控制者失效规则。</summary>
    internal ConstructionSourceLoss SourceLoss;

    /// <summary>本体伤害属性和表面是否随当前实际材料更新。</summary>
    internal bool FollowMatter;

    /// <summary>此实际部位中已核对的原操作需求，合成碎片不虚构作者操作。</summary>
    internal CompiledOperationRequirement[] Operations = Array.Empty<CompiledOperationRequirement>();

    /// <summary>本部位正常结束时接受实际余量的有限后段。</summary>
    internal int[] RemainderReceivers = Array.Empty<int>();

    /// <summary>本次真实吸收的唯一接收部位。负一表示普通屏障。</summary>
    internal int AbsorbedForceOutlet = -1;

    /// <summary>本部位及普通后代所属的接收分支，负一表示使用原始作用量。</summary>
    internal int RemainderRoot = -1;

    /// <summary>接收分支内部共同分配余力的真实部位，不含未来碎解预留件。</summary>
    internal int[] RemainderMembers = Array.Empty<int>();

    /// <summary>展开后的完整部位路径。</summary>
    internal string Path;

    /// <summary>原资料部位路径，不包含挂点集合展开后附加的实例名称。</summary>
    internal string DeclarationPath;

    /// <summary>共用资源。</summary>
    internal FormResource Resource;

    /// <summary>经过检查的实际景物带及实体回合边。</summary>
    internal CompiledLandscapeSlice LandscapeSlice;

    /// <summary>以真实部位为中心的散料牵引与喉口。</summary>
    internal CompiledMaterialVortex MaterialVortex;

    /// <summary>实际器口的世界尺寸及离口速度。</summary>
    internal CompiledWeaponChamber WeaponChamber;

    /// <summary>父体索引，负一表示根。</summary>
    internal int Parent;

    /// <summary>父挂点姿态。</summary>
    internal Matrix4x4 Anchor;

    /// <summary>预先展开的真实碎片所属主体，负一表示普通部位。</summary>
    internal int FragmentSource = -1;

    /// <summary>中断时从本部位原姿态出发的无作用落物。</summary>
    internal int InterruptFall = -1;
    /// <summary>为编译时预留落物读取的存在窗口。</summary>
    internal float InterruptFallSeconds;
    /// <summary>仅保存中断画面的落物，没有独立的施法范围或费用。</summary>
    internal bool IsInterruptFall;

    /// <summary>真实分件及后续规则。</summary>
    internal CompiledFracture Fracture;

    /// <summary>初始定时碎解，负一表示由事件条件执行。</summary>
    internal float FractureAt = -1f;

    /// <summary>父形体挂点使用的控制，负一为固定。</summary>
    internal FormControlBlend AnchorBlend;

    /// <summary>父体实际三角面的独立顶点绑定，普通挂点为空。</summary>
    internal FormSurfaceBinding AnchorSurface;

    /// <summary>父挂点所需的实际成形进度。</summary>
    internal float AnchorBuild;

    /// <summary>尚未开始的部位使用的出口朝向条件。</summary>
    internal CompiledOutlet Outlet;

    /// <summary>本体初始局部位置。</summary>
    internal Vector3 Offset;

    /// <summary>初始局部朝向。</summary>
    internal Quaternion Rotation;

    /// <summary>实际尺寸倍率。</summary>
    internal float Scale;

    /// <summary>射线开始前不提供尚未建立的作用段或附着点。</summary>
    internal bool HasRay;

    /// <summary>明确要求着地的主体实际顶点，编译时固定，工作线程不访问网格。</summary>
    internal FormSurfaceVertex[] GroundPoints;

    /// <summary>初始材料体积，尚未计入尺寸。</summary>
    internal float Volume;

    /// <summary>从实际形体进入浅层流散的参数与时点。</summary>
    internal CompiledFlow Flow;

    /// <summary>负值表示只接受事件流散。</summary>
    internal float FlowAt = -1f;

    /// <summary>生成主体的初始收束操作及开始时点。</summary>
    internal CompiledRecovery Recovery;

    /// <summary>没有定时收束时为负一。</summary>
    internal float RecoveryAt = -1f;

    /// <summary>普通定时导电及开始时点。</summary>
    internal CompiledConduction Conduction;

    /// <summary>定时导电尚未配置时为负一。</summary>
    internal float ConductionAt = -1f;

    /// <summary>独立世界材料的存在规则。</summary>
    internal CompiledResidue Residue;

    /// <summary>初始留置时点，条件留置采用已有事件。</summary>
    internal float LeaveAt = -1f;

    /// <summary>此部位是否需要独占流散工作区。</summary>
    internal CompiledFlow FlowSettings =>
        Flow ?? EventOperations.FirstOrDefault(operation => operation.Flow != null)?.Flow;

    /// <summary>显示和实际接触共用的存在时段，结束边界保留最后一次扫掠。</summary>
    internal UnityEngine.Vector2 Presence;

    /// <summary>可选的真实事件开始条件。</summary>
    internal CompiledConstructionActivation Activation;

    /// <summary>预先解析的直接后代，不在推进中反复搜索整棵结构。</summary>
    internal int[] Children = Array.Empty<int>();

    /// <summary>形变时钟在指定姿态维持，独立运动仍按世界时间执行。</summary>
    internal CompiledConstructionHold Hold;

    /// <summary>表面覆盖。</summary>
    internal string Surface;

    /// <summary>不从显示颜色推测的实际材料。</summary>
    internal AbilityMatter Matter;

    /// <summary>部位明确提供的实际介质通道。</summary>
    internal string MediumChannel;

    /// <summary>只影响显示的染色倍率。</summary>
    internal Color? Dye;

    /// <summary>每项最多执行一次的物态变化。</summary>
    internal CompiledMatterChange[] Changes = Array.Empty<CompiledMatterChange>();

    /// <summary>实际窗口的哪一端采用内部形变进度。</summary>
    internal bool ShapeActiveFrom, ShapeActiveUntil;

    /// <summary>本部位的作用及结束依赖实际身体过程。</summary>
    internal bool BodyWindow;

    /// <summary>同组共享接触去重，跨形态交接不重复命中同一人物。</summary>
    internal int ContactGroup;

    /// <summary>编译阶段分配共用接触组所需的稳定名称。</summary>
    internal string ContactKey;

    /// <summary>此部位明确指定的接触规则，空值由同组继承。</summary>
    internal CompiledContactPolicy ContactPolicy;

    /// <summary>本部位的明确接触作用覆盖，空值采用整招默认。</summary>
    internal CompiledConstructionImpact Impact;

    /// <summary>独立的有限范围运动，空值不分配相关运行记录。</summary>
    internal CompiledConstructionRadialMotion RadialMotion;

    /// <summary>可选的父体支撑抓持；仅当前实际接触能取得。</summary>
    internal CompiledSupportGrip SupportGrip;

    /// <summary>实际接触后的过程变化。</summary>
    internal ConstructionContactResponse OnContact;

    /// <summary>明确的攻击、屏障或装饰用途。</summary>
    internal ConstructionContactRole ContactRole;

    /// <summary>这个实际攻击只用来击镜，可交锋但不直接命中人物。</summary>
    internal bool MirrorStrike;

    /// <summary>地块接触需要的形状，负一沿用所有形状。</summary>
    internal int WorldContactShape = -1;

    /// <summary>本真实部位与受理时原人物的不可变对应。</summary>
    internal CompiledCorrespondence Correspondence;

    /// <summary>攻击形体沿用默认作用；屏障只有明确声明作用才接触人物。</summary>
    internal bool AffectsActors => ContactRole == ConstructionContactRole.Attack ||
        ContactRole == ConstructionContactRole.Barrier && Impact != null && !EnclosesActors;

    /// <summary>按真实形状阻止敌方地面人物穿过。</summary>
    internal bool BlocksGroundActors;

    /// <summary>本部位的实体闭环是状态生效的必要条件。</summary>
    internal bool EnclosesActors;

    /// <summary>本部位初始作用强度倍率。</summary>
    internal float Strength;

    /// <summary>实际屏障交互。</summary>
    internal ConstructionInterception Interception;

    /// <summary>允许的最大反射次数。</summary>
    internal int ReflectionLimit;

    /// <summary>有效时段起止。</summary>
    internal Vector2 Active;

    /// <summary>生长起止；负值表示不采用生长。</summary>
    internal Vector2 Growth = new(-1f, -1f);

    /// <summary>初始布局聚合，空值保持节点原有布局。</summary>
    internal CompiledGather Gather;

    /// <summary>解除连接时点；负值表示保持连接。</summary>
    internal float Detach = -1f;

    /// <summary>旋转开始时点。</summary>
    internal float SpinAt;

    /// <summary>局部角速度。</summary>
    internal Vector3 Spin;

    /// <summary>独立飞行开始时点，负值表示不飞行。</summary>
    internal float FlyAt = -1f;

    /// <summary>飞行的初始局部速度。</summary>
    internal Vector3 Velocity => Flight?.Velocity ?? Vector3.zero;

    /// <summary>初始飞行的完整参数。</summary>
    internal CompiledFlight Flight;

    /// <summary>初始飞行的实际曲线。</summary>
    internal CompiledConstructionPath FlightPath => Flight?.Path;

    /// <summary>条件操作，按原资料顺序执行。</summary>
    internal CompiledEventOperation[] EventOperations = Array.Empty<CompiledEventOperation>();

    /// <summary>已命名原定时操作的步骤索引；没有输出时不分配。</summary>
    internal int[] NativeSteps;

    /// <summary>只有原声明实际命名时才解析其输出，匿名旧构造不增加来源查找。</summary>
    internal bool HasNamedOperations;

    /// <summary>原定时或接触改质逐项对应的输出，未命名项为负一。</summary>
    internal int[] MatterSteps;

    /// <summary>订阅此来源尺寸越界的有序不同阈值，只在边界二分查找。</summary>
    internal float[] SizeThresholds = Array.Empty<float>();

    /// <summary>订阅飞行起点距离的有序不同阈值，比较真实空间距离。</summary>
    internal float[] FlightDistanceThresholds = Array.Empty<float>();

    /// <summary>订阅同一地波实际到达路程的有序不同阈值。</summary>
    internal float[] GroundRouteThresholds = Array.Empty<float>();

    /// <summary>此部位是否需要保持动作过渡的独占起姿。</summary>
    internal bool HasMotionChanges => EventOperations.Any(op => op.Motion != null);

    /// <summary>此部位是否可能真正着地。</summary>
    internal bool HasGround => Flight != null && Flight.Ground != ConstructionGroundResponse.Ignore ||
                               EventOperations.Any(op =>
                                   op.Flight != null && op.Flight.Ground != ConstructionGroundResponse.Ignore);

    /// <summary>已解析的形体动作。</summary>
    internal FormMotion Motion;

    /// <summary>动作开始世界时点与时间倍率。</summary>
    internal bool AnimateWhileHeld;
    internal float MotionAt, MotionSpeed = 1f;

    /// <summary>实际解除父体或外部连接的时点。</summary>
    internal float ReleaseAt => Detach >= 0f ? Detach : Parent < 0 ? FlyAt : -1f;
}

/// <summary>在创建或改造时一次展开结构和名称，不在运行热路径解释资料。</summary>
public static partial class AbilityCompiler
{
    /// <summary>编译时检查实际会用到的表面，避免错误资源到发布时才暴露。</summary>
    /// <param name="matter">已解析的材料。</param><param name="follows">是否采用换材表面。</param>
    /// <param name="path">对应声明路径。</param>
    internal static void ValidateMatterSurface(AbilityMatter matter, bool follows, string path)
    {
        string surface = follows ? matter.EditedSurface : matter.Surface;
        if (surface != null) ConstructionReferences.Resolve(path, () => FormResources.Surface(surface, true), "Surface", surface);
    }

    /// <summary>同一世界可共用的完整资料缓存；内容不同也不会误用相同修订号。</summary>
    private static readonly Dictionary<(int Version, string Definition), CompiledAbility> plans = new();

    /// <summary>按进入顺序回收旧修订，不回收正在施放者自己持有的计划。</summary>
    private static readonly Queue<(int Version, string Definition)> planOrder = new();

    /// <summary>资源成功更新后移除旧缓存；失败只移除失败版本，不破坏原有共享计划。</summary>
    static AbilityCompiler()
    {
        FormResources.Reloaded += version =>
        {
            foreach (var key in plans.Keys.Where(key => key.Version != version).ToArray()) plans.Remove(key);
            planOrder.Clear();
            foreach (var key in plans.Keys) planOrder.Enqueue(key);
        };
        FormResources.VersionReleased += version =>
        {
            foreach (var key in plans.Keys.Where(key => key.Version == version).ToArray()) plans.Remove(key);
            planOrder.Clear();
            foreach (var key in plans.Keys) planOrder.Enqueue(key);
        };
    }

    /// <summary>首轮支持的有限形体预算，不允许资料无限复制。</summary>
    public const int MaxForms = 128;

    /// <summary>当前实际保留的共用计划数量，供开发测量，临时创作不占用。</summary>
    public static int CachedPlans => plans.Count;

    /// <summary>检查外部定义、有限展开并解析资源。所有错误带具体部位路径。</summary>
    /// <param name="definition">玩家改造、创造或存档恢复后的当前资料。</param>
    /// <param name="cache">正式资料可共用计划；中间草稿填否，检查后不长期保留。</param>
    public static CompiledAbility Compile(AbilityDefinition definition, bool cache = true)
    {
        if (definition == null) Fail("构造", "缺少完整资料");
        if (definition.Version != 1) Fail("Version", "当前只支持资料版本1，收到" + definition.Version);
        if (string.IsNullOrWhiteSpace(definition.Id)) Fail("Id", "缺少稳定能力编号");
        if (definition.Revision < 1) Fail("Revision", "修订号须从1开始");
        if (definition.Root == null && definition.Body == null) Fail("Root / Body", "至少需要生成主体或实际身体动作");
        if (!Finite(definition.Lifetime) || definition.Lifetime <= 0f || definition.Lifetime > 3600f)
            Fail("Lifetime", "持续时间须大于零且不超过3600世界秒");
        if (definition.ConcurrentLimit < 0 || definition.ConcurrentLimit > 1024)
            Fail("ConcurrentLimit", "并存上限须为零至1024，零表示不限");
        if (!Finite(definition.Cost) || definition.Cost < 0) Fail("Cost", "费用须为非负有限数");
        if (definition.AdditionalResources == null || definition.AdditionalResources.Length > 15 ||
            !Enum.IsDefined(typeof(Cultiway.Core.SkillLibV3.SkillCastResourceRequirementMode),
                definition.ResourceMode) ||
            definition.AdditionalResources.Length > 0 && (definition.Resource == "none" || definition.ResourceMode ==
                Cultiway.Core.SkillLibV3.SkillCastResourceRequirementMode.Single) ||
            definition.AdditionalResources.Any(string.IsNullOrWhiteSpace) ||
            definition.AdditionalResources.Contains("none"))
            Fail("ResourceMode / AdditionalResources", "最多16个实际通道；额外通道须明确采用任选或全部，不能混入无消耗");
        if (!Finite(definition.Cooldown) || definition.Cooldown < 0) Fail("Cooldown", "冷却须为非负有限数");
        if (!Finite(definition.Power) || definition.Power < 0) Fail("Power", "作用量须为非负有限数");
        if (definition.Calculation != null && (double.IsNaN(definition.Calculation.Demand) ||
                                               double.IsInfinity(definition.Calculation.Demand) ||
                                               definition.Calculation.Demand < 0 ||
                                               definition.Calculation.Demand > float.MaxValue ||
                                               double.IsNaN(definition.Calculation.Recovery) ||
                                               double.IsInfinity(definition.Calculation.Recovery) ||
                                               definition.Calculation.Recovery < 0 ||
                                               definition.Calculation.Recovery > float.MaxValue))
            Fail("Calculation", "未取整费用与恢复基准须为可表示的非负有限数");
        if (definition.TargetRange.HasValue && (!Finite(definition.TargetRange.Value) ||
                                                definition.TargetRange.Value <= 0f ||
                                                definition.TargetRange.Value > 10000f))
            Fail("TargetRange", "选择距离须大于零且不超过一万格");
        if (definition.Parent != null && (string.IsNullOrWhiteSpace(definition.Parent.Id) ||
                                          definition.Parent.Id == definition.Id || definition.Parent.Revision < 1))
            Fail("Parent", "原术来源需要独立编号及有效修订");
        var nodes = new List<CompiledForm>();
        // 先展开并检查结构，再生成缓存键，循环引用及过深资料会在序列化之前被拒绝。
        bool connectedRoot = definition.Binding?.Follow == true;
        if (definition.Binding != null)
        {
            if (definition.Root == null) Fail("Binding", "根连接缺少Root生成主体");
            if (!Enum.IsDefined(typeof(AbilityAnchor), definition.Binding.Anchor))
                Fail("Binding/Anchor", "未知连接对象：" + definition.Binding.Anchor);
            if (definition.Binding.ContactAtDestination && (!definition.Binding.Follow ||
                definition.Binding.Anchor is not (AbilityAnchor.Target or AbilityAnchor.Source or AbilityAnchor.Between)))
                Fail("Binding/ContactAtDestination", "仅来源、目标或两点间的跟随连接能够只在新位置接触");
            if (definition.Binding.Fraction.HasValue && (!Finite(definition.Binding.Fraction.Value) ||
                definition.Binding.Fraction.Value < 0f || definition.Binding.Fraction.Value > 1f))
                Fail("Binding/Fraction", "两点间比例需要零至一的有限数");
            if (!Finite(definition.Binding.Height)) Fail("Binding/Height", "离地高度须为有限数");
            if ((definition.Binding.Anchor is AbilityAnchor.Hand or AbilityAnchor.Effector or AbilityAnchor.HeldWeapon) && definition.Body == null)
                Fail("Binding/Anchor", "手部连接缺少Body实际身体动作");
        }

        if (definition.Root != null)
            Expand(definition.Root, -1, null, Matrix4x4.identity, "root", 0, definition.Lifetime, nodes, connectedRoot);
        CompiledConstructionStep[] steps = CompileSteps(definition, nodes);
        AppendInterruptedFalls(definition.Lifetime, nodes);
        foreach (var node in nodes)
        {
            CheckRayMotionConflict(node);
            foreach (var action in node.EventOperations)
                ConstructionReferences.Resolve(action.Path, () =>
                {
                    action.Implementation.ValidateTarget(node, action, action.Path);
                    return true;
                });
            node.HasRay = node.EventOperations.Any(action => action.Ray != null);
            if (node.FragmentSource < 0 && node.HasGround)
                node.GroundPoints = node.Resource.GroundPoints();
        }
        foreach (CompiledForm node in nodes)
            if ((node.ShapeActiveFrom || node.ShapeActiveUntil) && node.Motion == null &&
                !node.EventOperations.Any(operation => operation.Motion != null))
                Fail(node.Path, "阶段作用时段需要实际制作动作");
        CompiledBodyAction body = definition.Body == null
            ? null
            : new CompiledBodyAction(definition.Body, definition.Lifetime);
        if (definition.Binding?.Anchor == AbilityAnchor.HeldWeapon)
            CheckHeldWeapon(definition, body, nodes);
        foreach (CompiledForm node in nodes)
            if (node.BodyWindow && (body == null || node.ContactKey != "body"))
                Fail(node.Path + "/BodyWindow", "跟随身体作用须提供实际身体并共用body接触组");
        if (body != null && !body.Part.IsArm && definition.Binding?.Anchor == AbilityAnchor.Hand)
            Fail("Binding/Anchor", "此身体动作未控制手腕，生成主体应明确连接实际发力部位Effector");
        var eventOperations = new List<CompiledEventOperation>();
        for (int n = 0; n < nodes.Count; n++)
            foreach (CompiledEventOperation operation in nodes[n].EventOperations)
            {
                operation.Node = n;
                operation.Index = eventOperations.Count;
                eventOperations.Add(operation);
            }

        int[][] listeners = CompileActivations(nodes, body, eventOperations, definition.Impact?.Signal != null);
        if ((definition.Binding?.Anchor is AbilityAnchor.Hand or AbilityAnchor.Effector) && connectedRoot &&
            nodes[0].Activation is
                { Placement: ConstructionEventPlacement.Attached } gate)
        {
            float latest = gate.Trigger == ConstructionChangeTrigger.ActorContact ? body.ActiveUntil : body.Duration;
            if (gate.Source >= 0 || nodes[0].ReleaseAt < 0f || latest + gate.Delay + nodes[0].ReleaseAt > body.Duration)
                throw new InvalidDataException("条件手部附着须由身体事件开始，并在实际收势前解除；可改用事件处独立生成");
        }

        if ((definition.Binding?.Anchor is AbilityAnchor.Hand or AbilityAnchor.Effector) && connectedRoot &&
            !nodes[0].BodyWindow && body.Duration < definition.Lifetime &&
            (nodes[0].ReleaseAt < 0 || nodes[0].ReleaseAt > body.Duration))
            throw new InvalidDataException("手部连接必须在身体收势结束前解除，不能在动作结束后继续借用不存在的手姿态");
        var groups = new Dictionary<string, int>(StringComparer.Ordinal);
        groups.Add("body", nodes.Count);
        int nextGroup = 0;
        var contacts = new CompiledContactPolicy[nodes.Count + (body?.StageCount ?? 1)];
        if (definition.Body?.Contact != null)
            contacts[nodes.Count] = new CompiledContactPolicy(definition.Body.Contact, "body");
        for (int i = 0; i < nodes.Count; i++)
        {
            string group = nodes[i].ContactKey;
            if (group == "body" && body == null) Fail(nodes[i].Path, "接触组body缺少实际身体动作");
            if (!groups.TryGetValue(group, out int index)) groups.Add(group, index = nextGroup++);
            nodes[i].ContactGroup = index;
            CompiledContactPolicy policy = nodes[i].ContactPolicy;
            if (policy == null) continue;
            if (contacts[index] != null && !contacts[index].Matches(policy)) Fail(nodes[i].Path, "同一接触组的规则冲突");
            contacts[index] = policy;
        }

        for (int i = 0; i < contacts.Length; i++) contacts[i] ??= CompiledContactPolicy.Default;
        for (int i = nodes.Count + 1; i < contacts.Length; i++) contacts[i] = contacts[nodes.Count];
        if (!cache)
            return new CompiledAbility(definition, nodes.ToArray(), body, contacts, listeners,
                eventOperations.ToArray(), steps);
        var key = (FormResources.Version, JsonConvert.SerializeObject(definition));
        if (plans.TryGetValue(key, out CompiledAbility found)) return found;
        var result = new CompiledAbility(definition, nodes.ToArray(), body, contacts, listeners,
            eventOperations.ToArray(), steps);
        if (plans.Count >= ConstructionBudget.MaximumCachedPlans) plans.Remove(planOrder.Dequeue());
        plans.Add(key, result);
        planOrder.Enqueue(key);
        return result;
    }

    /// <summary>借出同一件器物时，交接前的模型与尺寸由实际握持决定。</summary>
    private static void CheckHeldWeapon(AbilityDefinition definition, CompiledBodyAction body, List<CompiledForm> nodes)
    {
        if (body?.WeaponForm == null || definition.Body.Guard != null || body.Construction.HasControlRelations ||
            nodes.Count == 0 || nodes[0].Resource.Description.Id != body.WeaponForm)
            Fail("Binding/HeldWeapon", "实际控器需要匹配的握持动作与根形体");
        CompiledForm root = nodes[0];
        var binding = definition.Binding;
        if (!binding.Follow || binding.FaceTarget || binding.ScaleByBody || binding.Height != 0f ||
            root.Offset != Vector3.zero || root.Rotation != Quaternion.identity || root.Scale != 1f ||
            root.Presence.x != 0f || root.Activation != null || root.BodyWindow ||
            root.FlyAt <= 0f || root.FlyAt >= body.Duration || root.ReleaseAt != root.FlyAt ||
            root.Active.x < root.ReleaseAt || definition.Root.Operations.Any(operation =>
                operation.When == null && operation.Kind != ConstructionOperationKind.Fly && operation.At < root.ReleaseAt))
            Fail("Binding/HeldWeapon", "离手前须保持原器物握持姿态，动作结束前开始飞行，离手后再结算接触");
    }

    /// <summary>按挂点集合展开子体，检查首轮操作的时间及关系。</summary>
    /// <param name="node">当前结构。</param>
    /// <param name="parent">展开后的父体索引。</param>
    /// <param name="parentResource">父资源。</param>
    /// <param name="anchor">已经解析的挂点。</param>
    /// <param name="path">错误和来源路径。</param>
    /// <param name="depth">当前嵌套层数。</param>
    /// <param name="lifetime">总持续。</param>
    /// <param name="output">父体先于子体的结果。</param>
    /// <param name="connectedRoot">根部是否有实际可解除的外部连接。</param>
    /// <param name="anchorBlend">挂点随动的父形体表面控制。</param>
    /// <param name="declarationPath">原资料部位路径，根部沿用当前路径。</param>
    private static void Expand(ConstructionNode node, int parent, FormResource parentResource,
        Matrix4x4 anchor, string path, int depth, float lifetime, List<CompiledForm> output, bool connectedRoot = false,
        FormControlBlend anchorBlend = default,
        string declarationPath = null)
    {
        declarationPath ??= path;
        if (node == null) Fail(path, "部位资料为空");
        if (depth > ConstructionBudget.MaximumDepth || output.Count >= MaxForms) Fail(path, "嵌套超过16层或实际形体超过128个");
        if (string.IsNullOrWhiteSpace(node.Id)) Fail(path + "/Id", "缺少部位编号");
        if (string.IsNullOrWhiteSpace(node.Form)) Fail(path + "/Form", "缺少实际形体资源编号");
        if (string.IsNullOrWhiteSpace(node.Matter)) Fail(path + "/Matter", "缺少实际材料编号");
        if (node.Children == null) Fail(path + "/Children", "子结构集合为空值；没有子结构时请提供空列表");
        if (node.Operations == null) Fail(path + "/Operations", "操作集合为空值；没有操作时请提供空列表");
        if (node.Operations.Length > ConstructionBudget.MaximumNodeOperations)
            Fail(path + "/Operations", "每部位最多32项原操作，包含条件及物态变化");
        if (node.Children.Length > MaxForms) Fail(path + "/Children", "子结构已超过整次128个部位预算");
        if (!Enum.IsDefined(typeof(ConstructionContactResponse), node.OnContact)) Fail(path, "未知的接触后行为");
        if (!Enum.IsDefined(typeof(ConstructionSourceLoss), node.SourceLoss)) Fail(path + "/SourceLoss", "未知的来源失效行为");
        if (!Enum.IsDefined(typeof(ConstructionContactRole), node.ContactRole) || !Finite(node.Strength) ||
            node.Strength <= 0f)
            Fail(path, "作用用途或强度无效");
        if (node.BlocksGroundActors && node.ContactRole != ConstructionContactRole.Barrier)
            Fail(path + "/BlocksGroundActors", "只有屏障能阻挡地面人物");
        if (node.Impact != null && node.ContactRole == ConstructionContactRole.Decoration)
            Fail(path, "装饰不对人物结算接触作用，请移除部位作用覆盖");
        if (!Enum.IsDefined(typeof(ConstructionInterception), node.Interception) || node.ReflectionLimit < 0 ||
            node.ReflectionLimit > 32 ||
            node.Interception == ConstructionInterception.Reflect &&
            node.ContactRole != ConstructionContactRole.Barrier)
            Fail(path, "未知拦截规则、非屏障反射或反射次数超过0至32预算");
        if (parent < 0 && !string.IsNullOrEmpty(node.Anchor)) Fail(path, "根部不能绑定父挂点");
        if (parent < 0 && node.Distribution != null) Fail(path + "/Distribution", "根部没有父空间，分布应声明在子结构");
        Vector3 offset = ReadVector(node.Offset, path + "/Offset"),
            rotation = ReadVector(node.Rotation, path + "/Rotation");
        if (!Finite(node.Scale) || node.Scale <= 0 || node.Scale > 10000 || !Finite(node.ActiveFrom) ||
            !Finite(node.ActiveUntil) || node.ActiveFrom < 0 || node.ActiveFrom > lifetime ||
            node.ActiveUntil >= 0 && (node.ActiveUntil < node.ActiveFrom || node.ActiveUntil > lifetime))
            Fail(path, "尺寸或有效时段无效");
        float appear = node.AppearAt ?? 0f, disappear = node.DisappearAt ?? lifetime;
        if (!Finite(appear) || !Finite(disappear) || appear < 0f || disappear <= appear || disappear > lifetime)
            Fail(path, "存在时段须从零至施放结束，结束必须晚于开始");
        FormResource resource = ConstructionReferences.Resolve(path + "/Form", () => FormResources.Load(node.Form), "Form", node.Form, declarationPath);
        if (node.Surface != null)
            ConstructionReferences.Resolve(path + "/Surface", () => FormResources.Surface(node.Surface, true), "Surface", node.Surface, declarationPath);
        AbilityMatter matter = AbilityMatters.Resolve(node.Matter, node.State, path + "/Matter");
        var landscape = node.LandscapeSlice == null ? null :
            new CompiledLandscapeSlice(node.LandscapeSlice, resource, path + "/LandscapeSlice");
        if (node.MaterialVortex != null && (landscape != null || node.ContactRole != ConstructionContactRole.Decoration ||
            node.ActiveFromMarker != null || node.ActiveUntilMarker != null ||
            !(appear < node.ActiveFrom && node.ActiveFrom < node.ActiveUntil && node.ActiveUntil < disappear)))
            Fail(path + "/MaterialVortex", "深口部位须单独承载牵引，并声明依次展开、牵引和闭合的三个有限时段");
        if (node.WeaponChamber != null && (landscape != null || node.MaterialVortex != null || node.BodyWindow ||
            node.ActiveFromMarker != null || node.ActiveUntilMarker != null ||
            !(appear < node.ActiveFrom && node.ActiveUntil - node.ActiveFrom >= .7f && node.ActiveUntil < disappear)))
            Fail(path + "/WeaponChamber", "器口须声明展开、至少0.7秒的接入蓄积、以及抛出后的闭合时段");
        if (landscape != null && (matter.State != "solid" ||
            node.Operations.Any(operation => operation?.Kind is not (ConstructionOperationKind.Detach or ConstructionOperationKind.Fly))))
            Fail(path + "/LandscapeSlice", "景物带以实体呈现，部位运动只用脱离和飞行曲线");
        if (node.InterruptFall != 0f && (!Finite(node.InterruptFall) || node.InterruptFall < 0f ||
            node.InterruptFall > lifetime || resource.Meshes.Length == 0 || resource.Contacts.Length == 0 || matter.State != "solid"))
            Fail(path + "/InterruptFall", "中断落下需要有真实形状的固体，存在秒数不能超过整次上限");
        if (node.Correspondence != null && node.ContactRole == ConstructionContactRole.Decoration)
            Fail(path + "/Correspondence", "装饰部位没有真实交锋形状，不能传递映像作用");
        if (node.Correspondence is { } relation &&
            (relation.OriginalContactOnly || relation.ShowBody) && node.ContactRole != ConstructionContactRole.Attack)
            Fail(path + "/Correspondence", "原身接触和立影需要有主动接触的部位");
        if (node.Correspondence?.ShadowHold != null &&
            (node.Correspondence.ActiveUntil ?? lifetime) > disappear - appear)
            Fail(path + "/Correspondence/ShadowHold", "影身抱持的对应时段须落在实际部位的存在时段内");
        CompiledSpreadMedium.ValidateChannel(node.MediumChannel, path + "/MediumChannel");
        ValidateMatterSurface(matter, node.FollowMatter, path + "/Matter/Surface");
        Color? dye = null;
        if (node.Tint != null)
        {
            Vector3 tint = ReadVector(node.Tint, path + "/Tint");
            if (tint.x < 0 || tint.x > 1 || tint.y < 0 || tint.y > 1 || tint.z < 0 || tint.z > 1)
                Fail(path, "染色倍率必须在0至1之间");
            dye = new Color(tint.x, tint.y, tint.z, 1f);
        }

        int worldContactShape = -1;
        if (node.WorldContactShape != null)
        {
            worldContactShape = Array.FindIndex(resource.Contacts, shape => shape.Id == node.WorldContactShape);
            if (worldContactShape < 0 || node.ContactRole == ConstructionContactRole.Decoration)
                Fail(path + "/WorldContactShape", "需选择非装饰部位的实际作用形状");
        }

        var grip = node.SupportGrip == null ? null : new CompiledSupportGrip(node.SupportGrip, path + "/SupportGrip");
        if (grip != null && (parent < 0 || node.ContactRole != ConstructionContactRole.Attack ||
            resource.Contacts.Length == 0 || node.Activation?.Placement != ConstructionEventPlacement.Attached ||
            node.Activation.Trigger != ConstructionChangeTrigger.WorldContact || node.Activation.Source != ".." ||
            node.Contact?.Mode != ConstructionContactMode.Interval || node.OnContact != ConstructionContactResponse.Continue))
            Fail(path + "/SupportGrip", "抓持需要附着在实际父体，在其接地后开始，并使用可再次接触的穿过型攻击");
        var compiled = new CompiledForm
        {
            Path = path,
            DeclarationPath = declarationPath,
            Parent = parent,
            Anchor = anchor,
            AnchorBlend = anchorBlend,
            Outlet = node.Outlet == null ? null : new CompiledOutlet(node.Outlet,
                parent >= 0 && (!string.IsNullOrEmpty(node.Anchor) || node.Distribution != null) &&
                node.Activation?.Placement != ConstructionEventPlacement.Event, lifetime, path + "/Outlet"),
            Resource = resource,
            LandscapeSlice = landscape,
            MaterialVortex = node.MaterialVortex == null ? null : new CompiledMaterialVortex(node.MaterialVortex, path + "/MaterialVortex"),
            WeaponChamber = node.WeaponChamber == null ? null : new CompiledWeaponChamber(node.WeaponChamber, path + "/WeaponChamber"),
            Offset = offset,
            Rotation = Quaternion.Euler(rotation),
            Scale = node.Scale,
            Volume = node.Volume ?? (landscape == null ? 1f : landscape.Width * landscape.Length * landscape.Thickness),
            Presence = new Vector2(appear, disappear),
            Activation = node.Activation == null
                ? null
                : new CompiledConstructionActivation(node.Activation, lifetime, path + "/Activation"),
            Hold = new CompiledConstructionHold(node.Hold, lifetime, path + "/Hold"),
            Surface = node.Surface,
            Matter = matter,
            FollowMatter = node.FollowMatter,
            MediumChannel = node.MediumChannel,
            Dye = dye,
            ContactKey = node.ContactGroup ?? path,
            BodyWindow = node.BodyWindow,
            AnimateWhileHeld = node.AnimateWhileHeld,
            ContactPolicy = node.Contact == null ? null : new CompiledContactPolicy(node.Contact, path),
            Impact = node.Impact == null ? null : new CompiledConstructionImpact(node.Impact, path + "/Impact"),
            RadialMotion = node.RadialMotion == null ? null : new CompiledConstructionRadialMotion(node.RadialMotion, path + "/RadialMotion"),
            SupportGrip = grip,
            Correspondence = node.Correspondence == null ? null : new CompiledCorrespondence(node.Correspondence, lifetime,
                path + "/Correspondence", resource),
            OnContact = node.OnContact,
            ContactRole = node.ContactRole,
            WorldContactShape = worldContactShape,
            BlocksGroundActors = node.BlocksGroundActors,
            EnclosesActors = node.EnclosesActors,
            Strength = node.Strength,
            Interception = node.Interception,
            ReflectionLimit = node.ReflectionLimit,
            SourceLoss = node.SourceLoss,
            InterruptFallSeconds = node.InterruptFall,
            Active = new Vector2(node.ActiveFrom, node.ActiveUntil < 0 ? lifetime : node.ActiveUntil)
        };
        if (compiled.EnclosesActors && (compiled.ContactRole != ConstructionContactRole.Barrier ||
            compiled.Impact == null || compiled.Impact.Statuses.Count == 0 || compiled.Impact.HasActorEffectBesidesStatuses ||
            compiled.Impact.HasAgingEffect || compiled.Impact.FrostCoating != null || compiled.Impact.WorldEffects.Count > 0 ||
            resource.Contacts.Length is < 3 or > AbilityEnclosure.MaximumShapes || resource.Contacts.Any(contact => contact.IsPrism)))
            Fail(path + "/EnclosesActors", "围合需要有3至48个实际胶囊的实体屏障，且部位作用只声明人物状态");
        var used = new Dictionary<ConstructionOperationKind, string>();
        var operationIds = new HashSet<string>(StringComparer.Ordinal);
        if (!Finite(compiled.Volume) || compiled.Volume < 0 || compiled.Volume > 1000000f) Fail(path, "体积须为0至一百万的有限数");
        var changes = new List<CompiledMatterChange>();
        var reactions = new List<CompiledEventOperation>();
        compiled.Operations = new CompiledOperationRequirement[node.Operations.Length];
        for (int operationIndex = 0; operationIndex < node.Operations.Length; operationIndex++)
        {
            ConstructionOperation operation = node.Operations[operationIndex];
            string operationPath = path + "/Operations[" + operationIndex + "]";
            if (operation?.Id != null && (string.IsNullOrWhiteSpace(operation.Id) || !operationIds.Add(operation.Id)))
                Fail(operationPath, "原操作输出名称为空或重复");
            if (operation?.Id != null) compiled.HasNamedOperations = true;
            ConstructionOperationContract contract = ConstructionOperations.Resolve(operation, operationPath);
            CheckProcessConflict(compiled, operation.Kind, operationPath);
            compiled.Operations[operationIndex] =
                new CompiledOperationRequirement(path, operationIndex, operation, contract, operationPath);
            bool direct = ConstructionOperations.Implementation(operation.Kind).Direct;
            if (operation.When == null && direct && operation.Kind != ConstructionOperationKind.ConvertMatter)
            {
                if (used.TryGetValue(operation.Kind, out string previous))
                    ProcessConflict(compiled, operationPath, previous, "同一部位不能有两项相同的定时操作");
                used.Add(operation.Kind, operationPath);
            }
            if (!Finite(operation.At) || operation.At < 0 || operation.At > lifetime ||
                !Finite(operation.Seconds) || !Finite(operation.TurnRate) || operation.TurnRate < 0)
                Fail(operationPath, "操作时点、历时或转速无效");
            Vector3 vector = ReadVector(operation.Vector, operationPath + "/Vector");
            if (operation.WorldMatter != null)
            {
                if (resource.Meshes.Length == 0 &&
                    (compiled.MaterialVortex == null || operation.Kind != ConstructionOperationKind.Erode))
                    Fail(path, "世界材料选择需要生成主体或带实际范围的深口侵蚀");
                if (operation.Kind == ConstructionOperationKind.ConvertMatter && AbilityMatters.Resolve(operation.Matter, operation.State, path).State is not ("liquid" or "solid"))
                    Fail(path, "世界材料转换需要明确的液态或固态输出");
            }

            if (operation.Kind == ConstructionOperationKind.Leave)
            {
                if (resource.Meshes.Length == 0) Fail(operationPath, "留置需要实际生成主体");
                compiled.Residue = new CompiledResidue(operation.Residue, operationPath);
                if (operation.When == null) compiled.LeaveAt = operation.At;
            }

            if (operation.Kind == ConstructionOperationKind.Conduct && resource.Meshes.Length == 0)
                Fail(path, "导电需要真实生成部位选择触水入口");
            if (operation.Kind == ConstructionOperationKind.Recover && resource.Meshes.Length == 0)
                Fail(path, "收束需要实际生成主体，空连接点与借用身体不能回能");
            if (operation.Kind == ConstructionOperationKind.Fracture)
            {
                compiled.Fracture =
                    new CompiledFracture(operation.Fracture, resource, node.ContactRole, lifetime, operationPath);
                if (operation.When == null) compiled.FractureAt = operation.At;
            }

            if (operation.Kind == ConstructionOperationKind.Gather &&
                (parent < 0 || node.Activation?.Placement == ConstructionEventPlacement.Event))
                Fail(path, "部件聚合需要真实父结构及汇入布局");
            if (operation.When != null || !direct)
            {
                if (operation.Trigger != ConstructionChangeTrigger.Time ||
                    reactions.Count >= ConstructionBudget.MaximumEventOperations)
                    Fail(path, "事件条件不能同时指定旧触发，每部位最多16项条件操作");
                if (operation.Kind == ConstructionOperationKind.Detach && parent < 0 && !connectedRoot)
                    Fail(path, "没有可解除的连接");
                reactions.Add(new CompiledEventOperation(operation, vector, lifetime, operationPath, resource, node,
                    compiled.Fracture));
                continue;
            }

            if (!Enum.IsDefined(typeof(ConstructionChangeTrigger), operation.Trigger) ||
                operation.Kind != ConstructionOperationKind.ConvertMatter &&
                operation.Trigger != ConstructionChangeTrigger.Time ||
                operation.Kind == ConstructionOperationKind.ConvertMatter &&
                operation.Trigger is not (ConstructionChangeTrigger.Time or ConstructionChangeTrigger.ActorContact
                    or ConstructionChangeTrigger.Clash))
                Fail(path, "此操作未提供该触发方式");
            switch (operation.Kind)
            {
                case ConstructionOperationKind.Leave: break;
                case ConstructionOperationKind.Conduct:
                    compiled.Conduction = new CompiledConduction(operation.Conduction, path,
                        Array.IndexOf(node.Operations, operation));
                    if (compiled.Conduction.Pulses > 1 &&
                        operation.At + compiled.Conduction.Span >= compiled.Presence.y)
                        Fail(path, "导电末波须早于部位结束时点");
                    compiled.ConductionAt = operation.At;
                    break;
                case ConstructionOperationKind.Recover:
                    compiled.Recovery = new CompiledRecovery(operation, lifetime, path);
                    compiled.RecoveryAt = operation.At;
                    if (operation.At + operation.Seconds > compiled.Presence.y) Fail(path, "定时收束须在部位结束前完成");
                    break;
                case ConstructionOperationKind.Flow:
                    compiled.Flow = new CompiledFlow(operation.Flow, path);
                    compiled.FlowAt = operation.At;
                    break;
                case ConstructionOperationKind.Fracture: break;
                case ConstructionOperationKind.Gather:
                    compiled.Gather = new CompiledGather(operation, vector, compiled.Rotation, lifetime, path); break;
                case ConstructionOperationKind.Grow:
                    if (operation.Seconds <= 0 || operation.At + operation.Seconds > lifetime) Fail(path, "生长时间无效");
                    compiled.Growth = new Vector2(operation.At, operation.At + operation.Seconds);
                    break;
                case ConstructionOperationKind.Spin:
                    if (!Finite(vector.magnitude) || vector.magnitude > 7200f) Fail(path, "旋转超过每秒7200度的动作预算");
                    compiled.SpinAt = operation.At;
                    compiled.Spin = vector;
                    break;
                case ConstructionOperationKind.Detach:
                    if (parent < 0 && !connectedRoot) Fail(path, "根部没有可解除的父连接或外部连接");
                    compiled.Detach = operation.At;
                    break;
                case ConstructionOperationKind.Fly:
                    compiled.FlyAt = operation.At;
                    compiled.Flight = new CompiledFlight(operation, vector, path);
                    break;
                case ConstructionOperationKind.Animate:
                    if (string.IsNullOrWhiteSpace(operation.Clip) || operation.Seconds < 0f) Fail(path, "动作名称或时长无效");
                    compiled.Motion = ConstructionReferences.Resolve(operationPath + "/Clip",
                        () => resource.Rig.Motion(operation.Clip), "Motion", node.Form + " / " + operation.Clip, declarationPath);
                    compiled.MotionAt = operation.At;
                    compiled.MotionSpeed = operation.Seconds == 0 ? 1f : compiled.Motion.Duration / operation.Seconds;
                    if (operation.Hold != null)
                    {
                        if (node.Hold != null) Fail(operationPath + "/Hold", "同一原动作不能同时采用部位维持和动作维持");
                        if (operation.Hold.At > compiled.Motion.Duration / compiled.MotionSpeed)
                            Fail(operationPath + "/Hold", "维持时点超过本次动作长度");
                        compiled.Hold = new CompiledConstructionHold(
                            new ConstructionHold
                                { At = operation.At + operation.Hold.At, Seconds = operation.Hold.Seconds }, lifetime,
                            operationPath + "/Hold");
                    }

                    if (!Finite(compiled.MotionSpeed) ||
                        compiled.Motion.MaximumAngularRate * compiled.MotionSpeed > 7200f)
                        Fail(path, "骨架动作超过每秒7200度的共同转动预算");
                    break;
                case ConstructionOperationKind.ConvertMatter:
                    if (changes.Count >= 16) Fail(path, "每部位物态变化不能超过16项");
                    AbilityMatter target = AbilityMatters.Resolve(operation.Matter, operation.State, operationPath + "/Matter");
                    ValidateMatterSurface(target, node.FollowMatter, operationPath + "/Matter/Surface");
                    changes.Add(new CompiledMatterChange(operation.At, operation.Trigger, target,
                        operation.WorldMatter == null
                            ? null
                            : new CompiledWorldMatter(operation.WorldMatter, path,
                                Array.IndexOf(node.Operations, operation))));
                    break;
            }
        }

        compiled.Changes = changes.ToArray();
        compiled.EventOperations = reactions.ToArray();
        if (compiled.FlowSettings?.Cooling != null && (compiled.Matter.Id != "iron" || compiled.Matter.State != "liquid" ||
            compiled.Residue != null || node.Operations.Any(operation => operation.Kind == ConstructionOperationKind.ConvertMatter) ||
            compiled.FlowSettings.OpenBoundary || compiled.FlowSettings.Evaporation != 0f))
            Fail(path + "/Flow/Cooling", "冷却当前用于有边界、不蒸散的液态铁；整层不能直接转换或留存为一种材料");
        if (compiled.FlowSettings != null)
        {
            if (resource.Meshes.Length == 0) Fail(path, "流散需要可用形体和表面");
            // 立即脱离并投送必先于自身触地；触地启动流散时会清除空中的投送。
            bool launchBeforeGroundFlow = compiled.FlowAt < 0 && compiled.ReleaseAt == 0 &&
                compiled.EventOperations.Where(operation => operation.Flow != null).All(operation =>
                    operation.When.SourcePath == "." && operation.When.Trigger == ConstructionChangeTrigger.WorldContact);
            if (node.Operations.Any(operation => operation.Kind == ConstructionOperationKind.Fly &&
                                                 (operation.When != null ||
                                                  !(launchBeforeGroundFlow && operation.At == 0) &&
                                                  (compiled.FlowAt < 0 || operation.At > compiled.FlowAt)) &&
                                                 (operation.Path != null || operation.Vector[2] != 0 ||
                                                  operation.Acceleration != null && operation.Acceleration[2] != 0)))
                Fail(path, "浅层流散的后续投送当前支持水平速度、追踪及加速度，三维曲面投送继续待办");
        }

        if (compiled.HasGround && resource.Contacts.Length == 0) Fail(path, "地面停止需要实际作用形状，空支点或纯显示不能冒充触地");
        if (!string.IsNullOrEmpty(node.ActiveFromMarker) || !string.IsNullOrEmpty(node.ActiveUntilMarker))
        {
            compiled.ShapeActiveFrom = !string.IsNullOrEmpty(node.ActiveFromMarker);
            compiled.ShapeActiveUntil = !string.IsNullOrEmpty(node.ActiveUntilMarker);
            if (compiled.Motion != null && compiled.ShapeActiveFrom)
                compiled.Active.x = compiled.MotionAt + ConstructionReferences.Resolve(path + "/ActiveFromMarker",
                    () => compiled.Motion.MarkerTime(node.ActiveFromMarker), "Marker", node.ActiveFromMarker, declarationPath) / compiled.MotionSpeed;
            if (compiled.Motion != null && compiled.ShapeActiveUntil)
                compiled.Active.y = compiled.MotionAt + ConstructionReferences.Resolve(path + "/ActiveUntilMarker",
                    () => compiled.Motion.MarkerTime(node.ActiveUntilMarker), "Marker", node.ActiveUntilMarker, declarationPath) / compiled.MotionSpeed;
            if (compiled.Active.x > compiled.Active.y || compiled.Active.x > lifetime || compiled.Active.y > lifetime)
                Fail(path, "动作阶段超出施放时长或结束早于开始");
        }

        if (compiled.Hold.Seconds > 0f)
        {
            if (compiled.Motion == null) Fail(path, "形变维持需要实际制作动作，静态主体直接延长存在窗口");
            if (compiled.Hold.At < compiled.MotionAt) Fail(path, "维持不能早于制作动作开始");
            if (compiled.Hold.At > compiled.MotionAt + compiled.Motion.Duration / compiled.MotionSpeed ||
                compiled.Hold.At + compiled.Hold.Seconds > disappear + .000001f)
                Fail(path + "/Hold", "维持时点或额外秒数超过实际动作和存在截止");
            if (compiled.ShapeActiveUntil && compiled.Active.y >= compiled.Hold.At &&
                compiled.Active.y + compiled.Hold.Seconds > disappear)
                Fail(path, "维持后的有效阶段结束超过实际存在时段");
        }

        if ((parent >= 0 && compiled.Activation?.Placement != ConstructionEventPlacement.Event ||
             compiled.Detach >= 0f) &&
            compiled.FlyAt >= 0 && (compiled.Detach < 0 || compiled.FlyAt < compiled.Detach))
            Fail(path, "附着部位需要先解除连接才能独立飞行");
        if (compiled.Gather != null && compiled.ReleaseAt >= 0f &&
            compiled.ReleaseAt < compiled.Gather.At + compiled.Gather.Seconds)
            Fail(path, "初始聚合应在计划脱落前完成，途中脱落请使用实际事件");
        int ownIndex = output.Count;
        output.Add(compiled);
        var childIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ConstructionNode child in node.Children)
        {
            if (child == null) Fail(path + "/Children", "子部位为空值");
            if (!childIds.Add(child.Id ?? "")) Fail(path + "/Children", "子部位编号重复：" + child.Id);
            string childPath = path + "/" + child.Id;
            if (child.Distribution != null && !string.IsNullOrEmpty(child.Anchor))
                Fail(childPath + "/Distribution", "分布与Anchor不能同时指定，请保留一种实际位置来源");
            if (child.Distribution == null && string.IsNullOrEmpty(child.Anchor))
            {
                Expand(child, ownIndex, resource, Matrix4x4.identity, childPath, depth + 1, lifetime, output,
                    declarationPath: declarationPath + "/" + child.Id);
                continue;
            }

            FormAnchorBinding[] matches;
            if (child.Distribution != null) matches = child.Distribution.Resolve(resource, childPath + "/Distribution");
            else
            {
                bool prefix = child.Anchor.EndsWith("*", StringComparison.Ordinal);
                string selector = prefix ? child.Anchor.Substring(0, child.Anchor.Length - 1) : child.Anchor;
                matches = resource.Anchors.Where(item => prefix
                    ? item.Id.StartsWith(selector, StringComparison.Ordinal)
                    : item.Id == selector).ToArray();
                if (matches.Length == 0)
                    throw ConstructionReferences.Error(childPath + "/Anchor",
                        "父形体" + node.Form + "没有挂点：" + child.Anchor + "；可用挂点：" +
                        string.Join("、", resource.Anchors.Select(point => point.Id)),
                        declaration: declarationPath + "/" + child.Id, resourceKind: "Anchor", resourceId: node.Form + " / " + child.Anchor);
            }
            foreach (FormAnchorBinding point in matches)
            {
                int childStart = output.Count;
                Expand(child, ownIndex, resource, point.Transform, childPath + "@" + point.Id, depth + 1, lifetime,
                    output,
                    anchorBlend: point.Blend, declarationPath: declarationPath + "/" + child.Id);
                output[childStart].DistributionSource = child.Distribution?.Source;
                output[childStart].AnchorBuild = point.BuildAt;
                output[childStart].AnchorSurface = point.Surface;
            }
        }

        AppendFragments(compiled, ownIndex, lifetime, output);
    }

    /// <summary>在资料边界读取有限三维向量。</summary>
    /// <param name="values">外部三个数值。</param>
    /// <param name="path">当前部位。</param>
    private static Vector3 ReadVector(float[] values, string path)
    {
        if (values?.Length != 3 || values.Any(value => !Finite(value))) Fail(path, "需要三个有限坐标");
        return new Vector3(values[0], values[1], values[2]);
    }

    /// <summary>数值不能包含无穷和无效结果。</summary>
    /// <param name="value">外部数值。</param>
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    /// <summary>给出具体不成立的结构，禁止静默替换成旧技能。</summary>
    /// <param name="path">部位路径。</param>
    /// <param name="reason">缺失内容。</param>
    private static void Fail(string path, string reason) => throw ConstructionReferences.Error(path, reason);

    /// <summary>世界卸载时清除计划，现有施放仍持有自己开始时的计划。</summary>
    public static void Clear()
    {
        plans.Clear();
        planOrder.Clear();
    }
}
