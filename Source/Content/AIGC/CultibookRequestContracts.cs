using System.Collections.Generic;
using Cultiway.Content.Libraries;

namespace Cultiway.Content.AIGC;

internal enum CultibookRequestKind
{
    Create,
    Improve,
}

internal enum CultibookRequestState
{
    Pending,
    Succeeded,
    Failed,
    Cancelled,
    Expired,
}

internal sealed class CultibookSkillPromptDto
{
    public int EntityId;
    public string Name;
}

internal sealed class CultibookOriginalPromptDto
{
    public string Id;
    public string Name;
    public string Description;
    public ElementRequirement ElementRequirement;
    public float ElementAffinityThreshold;
    public int MinLevel;
    public int MaxLevel;
    public string CultivateMethodId;
    public string SkillPoolDescription;
}

internal sealed class CultibookPromptSnapshot
{
    public string ActorName;
    public int ActorLevel;
    public string ActorLevelName;
    public string ElementName;
    public string ElementDescription;
    public string CultivateMethodId;
    public string CultivateMethodName;
    public string AllowedCultivateMethods;
    public List<CultibookSkillPromptDto> Skills = new();
    public CultibookOriginalPromptDto Original;
}

public sealed class CultibookSkillDraftDto
{
    public int EntityId;
    public float BaseChance;
    public float MasteryThreshold;
    public int LevelRequirement;
}

public sealed class CultibookDraftDto
{
    public string Name;
    public string Description;
    public ElementRequirement ElementRequirement;
    public float ElementAffinityThreshold;
    public int MinLevel;
    public int MaxLevel;
    public string CultivateMethodId;
    public List<CultibookSkillDraftDto> SkillPool = new();
}
