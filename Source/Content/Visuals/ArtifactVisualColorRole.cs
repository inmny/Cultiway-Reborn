using System;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Visuals;
public enum ArtifactVisualColorRole
{
    Primary,
    Secondary,
    Glow,
}

/// <summary>
/// 复用 SkillLibV3 RawAnim 的贴图动画 cue。租约只更新位姿、配色和时长，渲染与回收沿用现有系统。
/// </summary>
