using Cultiway.Abstract;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 法器主动能力使用的技能实体资产。根执行承载跨帧状态，实际 Body 可借用法器或由普通技能实体提供。
/// </summary>
[Dependency(typeof(SkillVfxElements))]
public sealed class ArtifactSkillExecutions : ExtendLibrary<SkillEntityAsset, ArtifactSkillExecutions>
{
    /// <summary>借用真实法器 Body 追踪、穿刺并折返的空间攻击执行，可持续寻敌或单次出击。</summary>
    public static SkillEntityAsset FlyingSword { get; private set; }

    /// <summary>统一调度分光剑阵阵形、出剑时序和生命周期的隐藏根执行。</summary>
    public static SkillEntityAsset SwordArray { get; private set; }

    /// <summary>分光剑阵中单道可见剑影，使用通用动画渲染与扫掠碰撞。</summary>
    public static SkillEntityAsset SwordArrayBlade { get; private set; }

    protected override bool AutoRegisterAssets() => true;

    protected override string Prefix() => "Cultiway.ArtifactSkillExecution";

    protected override void OnInit()
    {
        FlyingSword.Element = ElementComposition.Static.Iron;
        FlyingSword.PrefabEntity = FlyingSword.World.CreateEntity(
            new SkillExecution(),
            new SkillExecutionWithoutBody(),
            new SkillEntity
            {
                Asset = FlyingSword,
                VfxElement = SkillVfxElements.Metal,
            },
            new SkillContext(),
            new AnimAfterimage
            {
                Count = 2,
                Layout = AnimAfterimageLayout.Linear,
                SpacingRatio = 0.085f,
                MinSpacing = 0f,
                NewestAlpha = 0.32f,
                OldestAlpha = 0.025f,
                LocalDirection = Vector2.down,
                Tint = Color.white,
            },
            new Position(),
            new Rotation(),
            new AliveTimer(),
            Tags.Get<TagPrefab>());
        FlyingSword
            .SetupColliderSphere(1f, new ColliderConfig
            {
                Enabled = true,
                Actor = true,
                Building = true,
                Enemy = true,
            });

        SwordArray.Element = ElementComposition.Static.Iron;
        SwordArray.PrefabEntity = SwordArray.World.CreateEntity(
            new SkillExecution(),
            new SkillExecutionWithoutBody(),
            new SkillEntity
            {
                Asset = SwordArray,
                VfxElement = SkillVfxElements.Metal,
            },
            new SkillContext(),
            new ArtifactSwordArrayExecutionState(),
            new Position(),
            new Rotation(),
            new AliveTimer(),
            Tags.Get<TagPrefab>());

        SwordArrayBlade.Element = ElementComposition.Static.Iron;
        SwordArrayBlade.PrefabEntity = SwordArrayBlade.World.CreateEntity(
            new SkillEntity
            {
                Asset = SwordArrayBlade,
                VfxElement = SkillVfxElements.Metal,
            },
            new SkillContext(),
            new Position(),
            Tags.Get<TagPrefab>());
        SwordArrayBlade.PrefabEntity.AddComponent(new SkillHitMemory());
        SwordArrayBlade.PrefabEntity.AddComponent(new AliveTimer());
        SwordArrayBlade
            .SetupColliderSphere(1f, new ColliderConfig
            {
                Enabled = false,
                Actor = true,
                Enemy = true,
            });
    }
}
