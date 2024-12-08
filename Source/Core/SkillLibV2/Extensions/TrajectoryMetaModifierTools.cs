using Cultiway.Utils.Extension;

namespace Cultiway.Core.SkillLibV2.Extensions;

public static class TrajectoryMetaModifierTools
{
    public static TrajectoryMeta WithDeltaScale(this TrajectoryMeta meta, TrajectoryMeta.GetDeltaScale delta_scale)
    {
        meta = meta.DeepCopy();
        meta.get_delta_scale = delta_scale;
        return meta;
    }
}