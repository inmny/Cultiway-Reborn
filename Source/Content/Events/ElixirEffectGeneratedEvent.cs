using Cultiway.Content.Const;
using Cultiway.Content.Libraries;
using System.Collections.Generic;

namespace Cultiway.Content.Events;

public struct ElixirEffectGeneratedEvent
{
    public string ElixirId;
    public ElixirEffectType EffectType;
    public ElixirEffectGenerator.StatusEffectDraft StatusDraft;
    public ElixirEffectGenerator.DataGainEffect DataGainDraft;
}
