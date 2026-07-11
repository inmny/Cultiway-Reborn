using Cultiway.Content.Components;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Utils;

public static class EnumTools
{
    public static string GetName(this ArtifactControlState state)
    {
        return $"Cultiway.Artifact.ControlState.{state}".Localize();
    }

    public static string GetName(this ArtifactEquipMode mode)
    {
        return $"Cultiway.Artifact.EquipMode.{mode}".Localize();
    }
}
