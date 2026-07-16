using System;
using System.Collections.Generic;
using System.IO;
using Cultiway.Abstract;
using Newtonsoft.Json;

namespace Cultiway.Content.Visuals;

/// <summary>能力配置引用的法器视觉语义样式。</summary>
public static class ArtifactVfxStyles
{
    public const string Arcane = "arcane";
    public const string Metal = "metal";
    public const string Ward = "ward";
    public const string Healing = "healing";
    public const string Spirit = "spirit";
    public const string Suppression = "suppression";
    public const string Earth = "earth";
    public const string Seal = "seal";
    public const string Purification = "purification";
    public const string Fire = "fire";
    public const string Devouring = "devouring";
    public const string Soul = "soul";
    public const string Command = "command";
    public const string Wind = "wind";
    public const string Prison = "prison";
    public const string Pearl = "pearl";
    public const string Reflection = "reflection";
    public const string Cloth = "cloth";
    public const string Vehicle = "vehicle";
}

/// <summary>一种法器视觉样式；表面和路径参数可独立使用。</summary>
