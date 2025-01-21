using System;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV2;
using Cultiway.Core.SkillLibV2.Extensions;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;

namespace Cultiway.Utils.ProjectionSkillTools;

public partial class ProjectionSkillTool
{
    private string _id;
    private ProjectionSkillTool(string id)
    {
        _id = id;
        _entity_builder = SkillEntityMeta.StartBuild(id);
        InitStartSkill();
    }

    public static ProjectionSkillTool StartBuild(string id)
    {
        return new ProjectionSkillTool(id);
    }

    public static WrappedSkillAsset Build()
    {
        throw new NotImplementedException();
    }
}