using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Persistence;
using Cultiway.Core.Progression;
using Cultiway.Core.Skills;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Newtonsoft.Json;

namespace Cultiway.Content.Skills;

/// <summary>创法机会来自实际修炼、自然突破或指定种子的开发请求。</summary>
public enum ConstructionCreationOrigin
{
    /// <summary>实际修炼活动。</summary>
    Cultivation,

    /// <summary>自然突破已经成功提交。</summary>
    Breakthrough,

    /// <summary>指定输入的开发调用。</summary>
    Script
}

/// <summary>随人物保存的创法等待及未完成请求，不保存人物对象或编译缓存。</summary>
public struct ConstructedCreationState : IComponent
{
    /// <summary>下一次普通修炼可以获得创法机会的世界时点。</summary>
    public double NextOpportunity;

    /// <summary>已经获得的机会序号，用于固定下一次选择及创作输入。</summary>
    public int Sequence;

    /// <summary>是否已经接受尚未处理的请求。</summary>
    public bool Pending;

    /// <summary>已接受请求的完整创作种子，读档不重新抽取。</summary>
    public int Seed;

    /// <summary>已接受请求的实际来源。</summary>
    public ConstructionCreationOrigin Origin;

    /// <summary>最近成功创出的正式能力编号。</summary>
    public string LastAbility;

    /// <summary>最近一次有界处理未能创作的具体原因。</summary>
    public string LastFailure;

    /// <summary>受理时固定的角色资料，不在排队或读档后重新采样。</summary>
    public ConstructionInspiration Inspiration;

    /// <summary>受理时独立复制的原术；完成后释放，正式结果自己保留完整结构。</summary>
    public AbilityDefinition Source;

    /// <summary>独立创作受理时的模型数值输入；完成后释放，不持有图形资源。</summary>
    public SynthesisResources Resources;
}

/// <summary>从既有活动收集有限创法请求，在共同逻辑边界逐项处理。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>与正式世界目录已有保存容量一致，所有登记入口共用。</summary>
    private const int MaximumDefinitions = 100000;

    /// <summary>世界同时接受的待处理请求上限，不为每个请求创建独立任务。</summary>
    private const int MaximumPendingCreations = 256;

    /// <summary>等待队列只存长期人物编号，由处理时取得当前人物。</summary>
    private static readonly Queue<long> creationQueue = new();

    /// <summary>避免重复恢复或重复事件为同一人物占用多个队列位置。</summary>
    private static readonly HashSet<long> queuedCreators = new();

    /// <summary>当前世界实际已受理的请求数量。</summary>
    public static int PendingCreations => creationQueue.Count;

    /// <summary>登记自然突破观察和独立人物保存；首次载入的旧世界使用默认空状态。</summary>
    private static void InitializeCreation()
    {
        ProgressionLifecycle.RegisterCommitted(OnCreationProgression);
        ModClass.I.WorldSaves.Actors.Register(new SaveSection<ActorExtend, CreationSave>("ability_creation", "创法机会与请求",
            33,
            actor => actor.E.TryGetComponent(out ConstructedCreationState state)
                ? new CreationSave
                {
                    Remaining = Math.Max(0d, state.NextOpportunity - LogicTime), Sequence = state.Sequence,
                    Pending = state.Pending, Seed = state.Seed, Origin = state.Origin,
                    LastAbility = state.LastAbility, LastFailure = state.LastFailure, Inspiration = state.Inspiration,
                    Source = state.Pending ? state.Source : null, Resources = state.Pending ? state.Resources : null
                }
                : null,
            (actor, data) =>
            {
                var state = new ConstructedCreationState
                {
                    NextOpportunity = LogicTime + data.Remaining, Sequence = data.Sequence,
                    Pending = data.Pending, Seed = data.Seed, Origin = data.Origin,
                    LastAbility = data.LastAbility, LastFailure = data.LastFailure, Inspiration = data.Inspiration,
                    Source = data.Pending ? data.Source : null, Resources = data.Pending ? data.Resources : null
                };
                if (state.Pending && !EnqueueCreation(actor.Base.id))
                {
                    state.Pending = false;
                    state.LastFailure = "恢复时创法等待队列已满";
                    ModClass.I.WorldSaves.LastReport.Skip("人物 " + actor.Base.id, "创法请求", state.LastFailure);
                }

                if (data.DiscardedPending)
                    ModClass.I.WorldSaves.LastReport.Skip("人物 " + actor.Base.id, "创法请求", data.LastFailure);
                actor.E.AddComponent(state);
            }, data =>
            {
                if (double.IsNaN(data.Remaining) || double.IsInfinity(data.Remaining) || data.Remaining < 0d ||
                    data.Remaining > TimeScales.SecPerMonth || data.Sequence < 0 ||
                    !Enum.IsDefined(typeof(ConstructionCreationOrigin), data.Origin) ||
                    data.Pending && (data.Inspiration == null || data.Resources == null &&
                        (data.Inspiration.Rules >= 7 || data.Source == null && data.Inspiration.Rules >= 2)))
                    throw new InvalidDataException("创法等待、机会序号或来源无效");
                if (data.Pending && data.Source != null) ValidateDefinition(data.Source, cache: false);
                if (data.Pending && data.Resources != null)
                {
                    ConstructionResources.Validate(data.Resources.Resource, false);
                    if (data.Inspiration.Rules >= 9 && data.Resources.Forms.Any(form => form.Parts.Length == 0))
                        throw new InvalidDataException("新创法请求缺少受理时的实际部件输入");
                    if (data.Inspiration.Rules >= 10 && !data.Resources.Forms.Any(form => form.SpanRadius.HasValue))
                        throw new InvalidDataException("新投送请求缺少受理时的真实伸展模型");
                    if (data.Inspiration.Rules >= 17 && data.Resources.WorldActions == null)
                        throw new InvalidDataException("新创法请求缺少受理时的世界操作输入");
                    if (data.Inspiration.Rules >= 23 && (data.Resources.Forms.Any(form => form.Formations == null) ||
                        !data.Resources.Forms.Any(form => form.Formations.Length > 0)))
                        throw new InvalidDataException("新创法请求缺少受理时的逐段成形动作输入");
                    if (data.Inspiration.Rules >= 26 && data.Resources.InventoryMaterials == null)
                        throw new InvalidDataException("新创法请求缺少受理时的实际库存材料输入");
                    if (data.Inspiration.Rules >= 30 && !data.Resources.Forms.Any(form => form.Id == "fireball"))
                        throw new InvalidDataException("双门创法请求缺少受理时冻结的门形");
                    if (data.Inspiration.Rules >= 28 && data.Resources.ErosionMaterials == null)
                        throw new InvalidDataException("新创法请求缺少受理时的侵蚀材料输入");
                    if (data.Inspiration.Rules >= 24 && (data.Resources.DeformationForms == null || data.Resources.Forms.Any(form => form.Controls == null)))
                        throw new InvalidDataException("新创法请求缺少受理时的结构与真实控制输入");
                    if (data.Inspiration.Rules <= 6 && data.Resources.Bodies.Length != 2)
                        throw new InvalidDataException("旧创法请求需要原有两份身体动作快照");
                    foreach (AbilityDefinition body in data.Resources.Bodies) ValidateDefinition(body, cache: false);
                }
            }, migrations: new ISaveMigration[] { new CreationV1ToV2(), new CreationV2ToV3(), new CreationV3ToV4(), new CreationV4ToV5(), new CreationV5ToV6(), new CreationV6ToV7(), new CreationV7ToV8(), new CreationV8ToV9(), new CreationV9ToV10(), new CreationV10ToV11(), new CreationV11ToV12(), new CreationV12ToV13(), new CreationV13ToV14(), new CreationV14ToV15(), new CreationV15ToV16(), new CreationV16ToV17(), new CreationV17ToV18(), new CreationV18ToV19(), new CreationV19ToV20(), new CreationV20ToV21(), new CreationV21ToV22(), new CreationV22ToV23(), new CreationV23ToV24(), new CreationV24ToV25(), new CreationV25ToV26(), new CreationV26ToV27(), new CreationV27ToV28(), new CreationV28ToV29(), new CreationV29ToV30(), new CreationV30ToV31(), new CreationV31ToV32(), new CreationV32ToV33() }));
        InitializeInspiration();
    }

    /// <summary>成功修炼只检查本人物；一个世界月内的多次行为不会重复抽取机会。</summary>
    /// <param name="actor">真正完成修炼的生灵。</param>
    internal static void NotifyCultivation(ActorExtend actor) => OfferCreation(actor, false);

    /// <summary>自然突破完成后提供额外机会；直接授予等级不重复生成能力。</summary>
    /// <param name="evt">已经提交的真实进阶。</param>
    private static void OnCreationProgression(ProgressionCommittedEvent evt)
    {
        if ((evt.Cultisys == Cultisyses.Xian || evt.Cultisys == Cultisyses.Yao) && evt.Mode == ProgressionMode.Natural && evt.ToLevel > evt.FromLevel)
            OfferCreation(evt.Actor, true);
    }

    /// <summary>先确认生灵与容量，再记录机会及种子；受理只入队，不在活动调用栈编译构造。</summary>
    /// <param name="actor">当前实际修士。</param>
    /// <param name="breakthrough">是否为已经提交的自然突破。</param>
    private static void OfferCreation(ActorExtend actor, bool breakthrough)
    {
        if (!GeneralSettings.EnableSkillSystems || !CanCreate(actor) ||
            !(actor.HasCultisys<Xian>() && actor.GetCultisys<Xian>().level >= 2 || actor.HasCultisys<Yao>() && actor.GetCultisys<Yao>().CurrLevel >= 2)) return;
        actor.E.TryGetComponent(out ConstructedCreationState state);
        if (state.Pending || !breakthrough && LogicTime < state.NextOpportunity ||
            state.Sequence == int.MaxValue) return;
        state.Sequence++;
        state.NextOpportunity = LogicTime + TimeScales.SecPerMonth;
        int seed = unchecked((int)actor.Base.id * 73856093 ^ state.Sequence * 19349663);
        state.LastFailure = null;
        var random = new Random(seed);
        if (random.NextDouble() < (breakthrough ? .25d : .015d))
        {
            state.Seed = random.Next();
            state.Origin = breakthrough
                ? ConstructionCreationOrigin.Breakthrough
                : ConstructionCreationOrigin.Cultivation;
            ConstructionInspiration inspiration = ReadInspiration(actor);
            AbilityDefinition source = CaptureCreationSource(actor, state.Seed, null);
            SynthesisResources resources = inspiration.Rules >= 7 || source == null ? CaptureCreationResources(actor, state.Seed, inspiration) : null;
            state.Pending = EnqueueCreation(actor.Base.id);
            if (state.Pending)
            {
                state.Inspiration = inspiration;
                state.Source = source;
                state.Resources = resources;
            }

            if (!state.Pending) state.LastFailure = "创法等待队列已满，等待下次机会";
        }

        actor.E.AddComponent(state);
    }

    /// <summary>指定种子也走同一队列与正式生成入口，便于复现自然请求而不强行推进世界。</summary>
    /// <param name="actor">真实创造者。</param>
    /// <param name="seed">已经确定的创作输入。</param>
    /// <param name="sourceId">指定已学原术；省略时按相同自然规则选择原术或重新探索。</param>
    public static bool TryQueueCreation(ActorExtend actor, int seed, string sourceId = null)
    {
        if (!GeneralSettings.EnableSkillSystems || !CanCreate(actor)) return false;
        actor.E.TryGetComponent(out ConstructedCreationState state);
        if (state.Pending) return false;
        ConstructionInspiration inspiration = ReadInspiration(actor);
        AbilityDefinition source = CaptureCreationSource(actor, seed, sourceId);
        SynthesisResources resources = inspiration.Rules >= 7 || source == null ? CaptureCreationResources(actor, seed, inspiration) : null;
        if (!EnqueueCreation(actor.Base.id)) return false;
        state.Pending = true;
        state.Seed = seed;
        state.Origin = ConstructionCreationOrigin.Script;
        state.Inspiration = inspiration;
        state.Source = source;
        state.Resources = resources;
        state.LastFailure = null;
        actor.E.AddComponent(state);
        return true;
    }

    /// <summary>合并同人物请求并执行世界队列容量限制。</summary>
    /// <param name="actorId">当前世界的长期人物编号。</param>
    private static bool EnqueueCreation(long actorId)
    {
        if (queuedCreators.Contains(actorId)) return true;
        if (creationQueue.Count >= MaximumPendingCreations) return false;
        queuedCreators.Add(actorId);
        creationQueue.Enqueue(actorId);
        return true;
    }

    /// <summary>沿用正式已学与世界保存上限，不让成功编译后才发现无处授予。</summary>
    /// <param name="actor">当前真实创造者。</param>
    private static bool CanCreate(ActorExtend actor) => actor?.Base?.data != null && actor.Base.isAlive() &&
                                                        definitions.Count < MaximumDefinitions &&
                                                        (!actor.E.TryGetComponent(out ConstructedKnowledge known) ||
                                                         known.Ids.Length < 128);

    /// <summary>每个共同逻辑周期只处理一项；失败有明确原因，不在当前周期无限重试。</summary>
    private static void ProcessCreation()
    {
        if (!GeneralSettings.EnableSkillSystems || creationQueue.Count == 0) return;
        long id = creationQueue.Dequeue();
        queuedCreators.Remove(id);
        Actor actor = World.world.units.get(id);
        if (actor == null || !actor.TryGetExtend(out ActorExtend owner)) return;
        if (!owner.E.TryGetComponent(out ConstructedCreationState state) || !state.Pending) return;
        state.Pending = false;
        state.LastFailure = null;
        if (!CanCreate(owner)) state.LastFailure = "创造者已经失效或构造容量已满";
        else
            try
            {
                state.LastAbility = Create(owner, state.Seed, state.Inspiration, state.Source, state.Resources);
            }
            catch (Exception error) when (error is InvalidDataException or IOException or ArgumentException)
            {
                state.LastFailure = error.Message;
            }

        state.Source = null;
        state.Resources = null;
        owner.E.AddComponent(state);
    }

    /// <summary>切世界只清理旧队列，正式载入从各人物已保存状态恢复。</summary>
    private static void ClearCreation()
    {
        creationQueue.Clear();
        queuedCreators.Clear();
    }

    /// <summary>正式存档只记录剩余等待及确定的创作输入。</summary>
    private sealed class CreationSave
    {
        /// <summary>下一次普通机会的剩余世界时间。</summary>
        public double Remaining;

        /// <summary>机会序号。</summary>
        public int Sequence;

        /// <summary>是否等待处理。</summary>
        public bool Pending;

        /// <summary>确定的创作种子。</summary>
        public int Seed;

        /// <summary>实际请求来源。</summary>
        public ConstructionCreationOrigin Origin;

        /// <summary>最近成功能力。</summary>
        public string LastAbility;

        /// <summary>最近失败原因。</summary>
        public string LastFailure;

        /// <summary>固定的创作倾向输入。</summary>
        public ConstructionInspiration Inspiration;

        /// <summary>受理时的原术结构；旧格式为空，表示继续原有独立创作。</summary>
        public AbilityDefinition Source;

        /// <summary>只在等待独立创作时保存模型数值，不在已完成人物上重复保存。</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public SynthesisResources Resources;

        /// <summary>仅由首版转换入口标记已经取消的旧未完成请求。</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool DiscardedPending = false;
    }
}
