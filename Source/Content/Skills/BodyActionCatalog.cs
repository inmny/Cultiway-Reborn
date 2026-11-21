using System.Collections.Generic;
using System.IO;
using Cultiway.Core.Skills;
using Newtonsoft.Json;
using System.Linq;

namespace Cultiway.Content.Skills;

/// <summary>首批动作只提供可改造的完整资料，执行共用构造系统。</summary>
public static class BodyActionCatalog
{
    /// <summary>保留旧掌击的正式编号，供原有能力入口接入。</summary>
    public const string PalmId = "Cultiway.PalmStrike";

    /// <summary>万法阁可直接取用的架掌资料。</summary>
    public const string GuardId = "Cultiway.Body.PalmGuard";

    /// <summary>使用实际铁盾架挡的正式战技。</summary>
    public const string ShieldGuardId = "Cultiway.Body.ShieldGuard";

    /// <summary>原武器握持、离手和收回共用的正式参考动作。</summary>
    public const string SwordReleaseId = "Cultiway.Body.SwordRelease";

    /// <summary>已经读取的基础动作资料。</summary>
    private static AbilityDefinition[] all;

    /// <summary>旧规则生成以及已有创法请求使用的掌击底稿。</summary>
    private static AbilityDefinition historicalPalm;

    /// <summary>读取制作资料，不在每次出手时解释文件。</summary>
    internal static AbilityDefinition[] All => all ??= LoadAll();

    /// <summary>新动作放在原目录末尾，已冻结的创作输入保持顺序。</summary>
    private static AbilityDefinition[] LoadAll()
    {
        AbilityDefinition palm = Load("palm-strike-motion");
        AbilityDefinition guard = CreateFrom(palm, GuardId, "架掌",
            new BodyActionRequest
            {
                Effector = "hand.right", Kind = BodyActionKind.Guard, Seconds = 1.6f,
                Responses = new[] { new BodyResultResponse { Result = BodyActionResult.Blocked, RecoverFrom = "rest" } }
            });
        guard.TargetRange = 12f;
        guard.Body.MatchDistance = false;
        guard.Body.Guard = new BodyGuardDefinition { Capacity = 100f, Normal = new[] { 0f, 0f, 1f } };
        AbilityDefinition thrust = Load("sword-thrust");
        AbilityDefinition shortThrust = Load("short-blade-thrust");
        AbilityDefinition shortCut = Load("short-blade-cut");
        AbilityDefinition shortSweep = Load("short-blade-sweep");
        return new[] { palm, Load("sweeping-palm"), thrust, guard, CreateSwordRelease(thrust),
            Load("beast-horn-thrust"), Load("beast-pounce"), Load("palm-push"), Load("straight-punch"), Load("folded-elbow"),
            Load("rising-knee"), Load("low-sweep"), Load("shoulder-drive"), shortThrust, StoneSword(shortThrust),
            shortCut, StoneSword(shortCut), shortSweep, StoneSword(shortSweep), Load("long-spear-thrust"), Load("long-spear-sweep"),
            Load("long-spear-deflect"), Load("heavy-hammer-drop"), Load("shield-guard"), Load("palm-yield"),
            Load("angled-retreat"), Load("charging-shoulder"), Load("bounding-step"),
            Load("turning-throw"), Load("pressing-throw"), Load("beast-tail-sweep"), Load("beast-wing-cut") };
    }

    /// <summary>保留完整手路及发力，石剑版本以本身的握点及刃口执行。</summary>
    /// <param name="original">以相同握持、接触名称制作的实际铁剑战技。</param>
    private static AbilityDefinition StoneSword(AbilityDefinition original)
    {
        var result = ConstructionEditing.Copy(original);
        result.Id += ".Stone";
        result.Name += "·石剑";
        result.Body.WeaponForm = "stone_short_sword";
        return result;
    }

    /// <summary>从同一铁剑握持动作起势，只改变离手后的运动和接触。</summary>
    private static AbilityDefinition CreateSwordRelease(AbilityDefinition thrust)
    {
        const float release = .27f;
        AbilityDefinition result = ConstructionEditing.Copy(thrust);
        result.Id = SwordReleaseId;
        result.Name = "掷剑回锋";
        result.Revision = 1;
        result.Lifetime = 2.2f;
        result.Cooldown = .6f;
        result.TargetRange = 4.6f;
        result.Body.MatchDistance = false;
        result.Binding = new ConstructionBinding { Anchor = AbilityAnchor.HeldWeapon, Follow = true };
        result.Root = new ConstructionNode
        {
            Id = "sword", Form = "sword", ActiveFrom = release,
            Operations = new[]
            {
                new ConstructionOperation
                {
                    Kind = ConstructionOperationKind.Fly, At = release, Vector = new[] { 0f, 0f, 12f },
                    TurnRate = 360f, InheritVelocity = false,
                    Goal = new ConstructionFlightGoal
                    {
                        Anchor = ConstructionFlightAnchor.Target,
                        Arrival = ConstructionFlightArrival.Continue, Radius = .58f
                    }
                }
            }
        };
        result.Steps = new[]
        {
            new ConstructionStep
            {
                Id = "return_to_owner",
                Inputs = new Dictionary<string, ConstructionOutputReference> { ["part"] = new() { Node = "root" } },
                Operation = new ConstructionOperation
                {
                    Kind = ConstructionOperationKind.Fly, WorldVelocity = true, Vector = new float[3],
                    InheritVelocity = true, TurnRate = 520f,
                    When = new ConstructionCondition
                    {
                        Source = "root", Trigger = ConstructionChangeTrigger.DistanceFromFlightStart, Threshold = 3.9f
                    },
                    Goal = new ConstructionFlightGoal
                    {
                        Anchor = ConstructionFlightAnchor.Source, Offset = new[] { 0f, 0f, .58f },
                        Steering = new ConstructionFlightSteering { Side = 1.25f, Lift = .22f, Decay = 5f },
                        Arrival = ConstructionFlightArrival.End, Radius = .70f
                    }
                }
            }
        };
        return result;
    }

    /// <summary>原地直掌的完整资料。</summary>
    public static AbilityDefinition Palm => All[0];

    /// <summary>延续最早两种创法底稿及其抽取顺序，保证旧请求可按原规则重现。</summary>
    /// <param name="index">旧规则在直掌和横扫中选取的位置。</param>
    internal static AbilityDefinition HistoricalCreationBase(int index) => index == 0
        ? historicalPalm ??= Load("palm-strike")
        : All[index];

    /// <summary>由共用动作创建可正式学习、自由编辑和保存的构造，不增加固定技能登记。</summary>
    /// <param name="id">新构造的稳定编号。</param><param name="name">显示名称。</param>
    /// <param name="request">实际部位与动作参数。</param>
    public static AbilityDefinition Create(string id, string name, BodyActionRequest request)
        => CreateFrom(Palm, id, name, request);

    /// <summary>由现有动作资料展开同样可编辑的实际身体姿态。</summary>
    /// <param name="basis">原动作外层资料。</param><param name="id">正式编号。</param>
    /// <param name="name">显示名称。</param><param name="request">实际部位和动作。</param>
    private static AbilityDefinition CreateFrom(AbilityDefinition basis, string id, string name, BodyActionRequest request)
    {
        AbilityDefinition result = ConstructionEditing.Copy(basis);
        result.Id = id;
        result.Name = name;
        result.Revision = 1;
        result.Body = BodyActionGenerator.Create(request);
        result.Lifetime = result.Body.Keys.Last().At;
        result.Cooldown = result.Lifetime;
        result.TargetRange = null;
        return result;
    }

    /// <summary>从内容目录读取一个已制作的动作。</summary>
    /// <param name="name">资料文件名。</param>
    private static AbilityDefinition Load(string name) => JsonConvert.DeserializeObject<AbilityDefinition>(
        File.ReadAllText(
            Path.Combine(ModClass.I.GetDeclaration().FolderPath, "Content/Skills/Body", name + ".json")));
}
