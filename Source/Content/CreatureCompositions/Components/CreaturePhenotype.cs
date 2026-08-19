using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cultiway.Content.CreatureCompositions.Models;
using Friflo.Engine.ECS;

namespace Cultiway.Content.CreatureCompositions.Components;

/// <summary>角色当前正在使用的组合身体。</summary>
public struct CreaturePhenotype : IComponent
{
    private CreatureOrganEntry[] organs;
    private ReadOnlyCollection<CreatureOrganEntry> organView;

    public int Version { get; private set; }
    public string BodyPlanId { get; private set; }
    public string MorphId { get; private set; }
    public IReadOnlyList<CreatureOrganEntry> Organs
    {
        get
        {
            if (organView != null) return organView;
            return Array.Empty<CreatureOrganEntry>();
        }
    }
    public string Signature { get; private set; }
    public int Revision { get; private set; }
    public int CompiledIndex { get; private set; }

    public readonly bool IsValid =>
        Version == CreaturePhenotypePlan.CurrentVersion &&
        !string.IsNullOrEmpty(BodyPlanId) &&
        !string.IsNullOrEmpty(MorphId) &&
        !string.IsNullOrEmpty(Signature) &&
        CompiledIndex > 0;

    internal CreaturePhenotype(
        CreaturePhenotypePlan plan,
        CompiledCreaturePhenotype compiled,
        int revision)
    {
        Version = plan.Version;
        BodyPlanId = compiled.BodyPlan.id;
        MorphId = compiled.Morph.id;
        organs = plan.CopyOrgans();
        organView = Array.AsReadOnly(organs);
        Signature = compiled.Signature;
        Revision = revision;
        CompiledIndex = compiled.CompiledIndex;
    }

    /// <summary>复制器官数组，避免两个角色共用可修改的身体记录。</summary>
    public readonly CreaturePhenotype DeepClone()
    {
        CreaturePhenotype clone = this;
        clone.organs = organs == null
            ? Array.Empty<CreatureOrganEntry>()
            : (CreatureOrganEntry[])organs.Clone();
        clone.organView = Array.AsReadOnly(clone.organs);
        return clone;
    }
}
