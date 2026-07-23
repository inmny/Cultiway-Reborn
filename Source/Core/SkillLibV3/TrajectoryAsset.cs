using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Editor;
using Cultiway.Core.Semantics;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3;

public delegate void InitDefaultTrajectory(Entity prefab_entity);
public delegate void UpdateTrajectory(ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt);
public class TrajectoryAsset : Asset
{
    /// <summary>轨迹实际表达的运动语义。</summary>
    public SemanticDescriptor Semantics { get; set; } = new();

    public InitDefaultTrajectory OnInit;
    public UpdateTrajectory Action;
    public bool CanBeSelectedByModifier = true;
    public string EditorDescriptionKey;
    public int EditorSortOrder;
    public bool EditorSelectable;
    public bool EditorPersistWhenHidden;
    public HashSet<string> EditorCompatibilityKeys { get; } = new(StringComparer.Ordinal);
    public Func<SkillEditContext, SkillCompatibilityResult> EditorCompatibility;

    /// <summary>该轨迹能够承载的法术运行形态。</summary>
    public SkillTrajectoryDomain Domains { get; private set; }

    /// <summary>
    /// 轨迹的视觉方向姿态（按位或），用于描述动画应按水平、竖直、地面或显现方式呈现。
    /// 运行形态兼容性只由 <see cref="Domains"/> 决定。
    /// </summary>
    public TrajectoryOrientation Orientations { get; set; } = TrajectoryOrientation.Horizontal;

    /// <summary>
    /// 流式声明该轨迹的视觉方向姿态，便于在 Setup 方法链式调用。
    /// </summary>
    public TrajectoryAsset WithOrientations(TrajectoryOrientation orientations)
    {
        Orientations = orientations;
        return this;
    }

    /// <summary>声明该轨迹能够承载的法术运行形态。</summary>
    public TrajectoryAsset WithDomains(SkillTrajectoryDomain domains)
    {
        Domains = domains;
        return this;
    }

    /// <summary>
    /// 声明轨迹的运动语义，供法术容器构建时匹配运动配置。
    /// </summary>
    public TrajectoryAsset AddSemantics(params SemanticAsset[] semantics)
    {
        Semantics = SemanticDescriptor.Weighted(
            Semantics.contributions
                .Concat(semantics.Select(x => new SemanticContribution(x)))
                .GroupBy(x => x.semantic_id, StringComparer.Ordinal)
                .Select(x => x.First())
                .ToArray());
        return this;
    }

    public SkillCompatibilityResult CheckEditorCompatibility(SkillEditContext context,
        bool requireEditorSelectable = true)
    {
        var result = new SkillCompatibilityResult();
        if (!EditorSelectable && (requireEditorSelectable || !EditorPersistWhenHidden))
        {
            result.AddError("trajectory.internal", id);
            return result;
        }

        if (context.EntityAsset != null)
        {
            result.Merge(SkillTrajectoryCompatibility.Check(context.EntityAsset, this));
        }

        if (EditorCompatibility != null)
        {
            result.Merge(EditorCompatibility(context));
        }
        return result;
    }
}
