using Cultiway.Content.Components;
using Cultiway.Utils.Extension;
using UnityEngine;

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
    /// <summary>
    /// 获取法器状态对应的熟练度提升速率
    /// </summary>
    /// <param name="state"></param>
    /// <returns>熟练度提升速率</returns>
    public static float GetAttunementRate(this ArtifactControlState state)
    {
        return state switch
        {
            ArtifactControlState.Ready => 0.005f,
            ArtifactControlState.Operating => 0.02f,
            ArtifactControlState.Overloaded => 0.012f,
            ArtifactControlState.Cold => 0.001f,
            _ => throw new System.NotImplementedException(),
        };
    }
    /// <summary>
    /// 获取法器状态对应的比例缩放
    /// </summary>
    /// <param name="state">法器状态</param>
    /// <returns>比例缩放</returns>
    public static float GetStateScale(this ArtifactControlState state)
    {
        return state switch
        {
            ArtifactControlState.Ready => 0.78f,
            ArtifactControlState.Operating => 1f,
            ArtifactControlState.Overloaded => 1.08f,
            ArtifactControlState.Cold => 0.55f,
            _ => throw new System.NotImplementedException(),
        };
    }
    /// <summary>
    /// 获取法器状态对应的颜色
    /// </summary>
    /// <param name="state"></param>
    /// <param name="time"></param>
    /// <returns>颜色</returns>
    public static Color GetStateColor(this ArtifactControlState state, float time = 0f)
    {
        return state switch
        {
            ArtifactControlState.Ready => new Color(0.72f, 0.9f, 1f, 0.78f),
            ArtifactControlState.Operating => Color.white,
            ArtifactControlState.Overloaded => Color.Lerp(
                new Color(1f, 0.8f, 0.35f, 1f),
                new Color(1f, 0.25f, 0.2f, 1f),
                (Mathf.Sin(time * 5f) + 1f) * 0.5f),
            ArtifactControlState.Cold => new Color(0.5f, 0.55f, 0.62f, 0.38f),
            _ => throw new System.NotImplementedException(),
        };
    }
}
