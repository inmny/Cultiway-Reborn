using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Cultiway.Content.CreatureCompositions.Models;

/// <summary>一次准备交给共用身体整理器的完整身体方案。</summary>
public sealed class CreaturePhenotypePlan
{
    public const int CurrentVersion = 1;

    private readonly CreatureOrganEntry[] organs;
    private readonly ReadOnlyCollection<CreatureOrganEntry> organView;

    public int Version { get; }
    public string BodyPlanId { get; }
    public string MorphId { get; }
    public IReadOnlyList<CreatureOrganEntry> Organs => organView;

    public CreaturePhenotypePlan(
        string bodyPlanId,
        string morphId,
        params CreatureOrganEntry[] organs)
        : this(CurrentVersion, bodyPlanId, morphId, organs)
    {
    }

    public CreaturePhenotypePlan(
        int version,
        string bodyPlanId,
        string morphId,
        CreatureOrganEntry[] organs)
    {
        Version = version;
        BodyPlanId = bodyPlanId;
        MorphId = morphId;
        this.organs = organs == null
            ? Array.Empty<CreatureOrganEntry>()
            : (CreatureOrganEntry[])organs.Clone();
        organView = Array.AsReadOnly(this.organs);
    }

    internal CreatureOrganEntry[] CopyOrgans()
    {
        return (CreatureOrganEntry[])organs.Clone();
    }
}
