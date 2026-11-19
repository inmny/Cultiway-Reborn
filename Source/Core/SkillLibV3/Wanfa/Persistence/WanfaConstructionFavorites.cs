using System.Collections.Generic;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;

namespace Cultiway.Core.SkillLibV3.Wanfa.Persistence;

/// <summary>本机万法阁的完整构造收藏，与世界内实际掌握关系分别保存。</summary>
internal sealed class WanfaConstructionFavorites
{
    public List<AbilityDefinition> Entries = new();

    internal static SaveDocumentDefinition<WanfaConstructionFavorites> Create() => new(
        "wanfa_construction_favorites", "Saves/global/wanfa_construction_favorites", 1,
        () => new WanfaConstructionFavorites(), null,
        validate: data => data.Entries == null ? "构造收藏缺少条目" : null);
}
