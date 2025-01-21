using Cultiway.Core.SkillLibV2;
using Cultiway.Core.SkillLibV2.Extensions;
using UnityEngine;

namespace Cultiway.Utils.ProjectionSkillTools;

public partial class ProjectionSkillTool
{
    private SkillEntityMeta.MetaBuilder _entity_builder;
    private SkillEntityMeta _entity_meta;

    public ProjectionSkillTool EntityAddAnim(Sprite[] frames, float interval = 0.3f,float scale = 1f, bool loop = true)
    {
        _entity_builder.AddAnim(frames, scale, interval, loop);
        return this;
    }
}