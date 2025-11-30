using System;
using System.Collections.Generic;
using System.IO;
using Cultiway.Abstract;
using Cultiway.Content.Const;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
///     负责突破异象的可配置参数读取与查询。
/// </summary>
internal sealed class BreakthroughVisualManager : ICanInit, ICanReload
{
    private const string ConfigRelativePath = "Content/RealmVisual/breakthrough_config.json";

    private readonly Dictionary<byte, BreakthroughVisualDefinition> _definitions = new();

    public static BreakthroughVisualManager Instance { get; private set; }

    public bool Enabled => RealmVisualManager.Instance != null 
                           && RealmVisualManager.Instance.VisualEnabled 
                           && ModClass.I.GetConfig()["RealmVisualSettings"]["BREAKTHROUGH_VISUAL_ENABLED"].BoolVal;

    public void Init()
    {
        Instance = this;
        LoadConfig();
    }

    public void OnReload()
    {
        LoadConfig();
    }

    public BreakthroughVisualDefinition GetDefinition(byte toLevel)
    {
        return _definitions.TryGetValue(toLevel, out var def) ? def : null;
    }

    private void LoadConfig()
    {
        _definitions.Clear();
        try
        {
            var fullPath = Path.Combine(ModClass.I.GetDeclaration().FolderPath, ConfigRelativePath);
            if (File.Exists(fullPath))
            {
                var json = File.ReadAllText(fullPath);
                var dto = JsonConvert.DeserializeObject<BreakthroughVisualConfigDto>(json) ?? new BreakthroughVisualConfigDto();
                if (dto.effects != null)
                {
                    foreach (var effect in dto.effects)
                    {
                        if (effect == null) continue;
                        var def = BuildDefinition(effect);
                        _definitions[def.ToLevel] = def;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ModClass.LogError($"[BreakthroughVisual] 配置读取失败: {ex.Message}");
        }

        if (_definitions.Count == 0)
        {
            LoadFallbackDefinitions();
        }
    }

    private static BreakthroughVisualDefinition BuildDefinition(BreakthroughVisualEntryDto entry)
    {
        return new BreakthroughVisualDefinition(
            id: entry.id ?? string.Empty,
            fromLevel: (byte)Mathf.Clamp(entry.from, 0, byte.MaxValue),
            toLevel: (byte)Mathf.Clamp(entry.to, 0, byte.MaxValue),
            duration: Mathf.Max(entry.duration, 0.5f),
            baseParticleCount: Mathf.Max(entry.base_particle, 1),
            radius: Mathf.Max(entry.radius, 0.1f),
            heightOffset: Mathf.Clamp(entry.height_offset, -2f, 2f),
            primary: ParseColor(entry.primary_color, RealmColors.QiRefining),
            secondary: ParseColor(entry.secondary_color, RealmColors.Foundation),
            shockwave: entry.shockwave,
            useCloud: entry.cloud,
            lightningChance: Mathf.Clamp01(entry.lightning_chance),
            useFunnel: entry.funnel,
            extraIntensity: Mathf.Max(entry.extra_intensity, 0f)
        );
    }

    private void LoadFallbackDefinitions()
    {
        // 练气→筑基
        _definitions[1] = new BreakthroughVisualDefinition(
            "qi_to_foundation",
            0,
            1,
            4f,
            28,
            0.9f,
            0.2f,
            new Color(0.81f, 0.94f, 1f, 1f),
            new Color(0.67f, 0.86f, 0.93f, 1f),
            shockwave: false,
            useCloud: false,
            lightningChance: 0f,
            useFunnel: true,
            extraIntensity: 0.4f);

        // 筑基→金丹
        _definitions[2] = new BreakthroughVisualDefinition(
            "foundation_to_jindan",
            1,
            2,
            5f,
            32,
            1.1f,
            0.8f,
            new Color(0.75f, 0.91f, 1f, 1f),
            new Color(1f, 0.84f, 0f, 1f),
            shockwave: false,
            useCloud: true,
            lightningChance: 0.1f,
            useFunnel: true,
            extraIntensity: 0.6f);

        // 金丹→元婴
        _definitions[3] = new BreakthroughVisualDefinition(
            "jindan_to_yuanying",
            2,
            3,
            6f,
            36,
            1.3f,
            0.4f,
            new Color(1f, 0.84f, 0f, 1f),
            new Color(0.58f, 0.44f, 0.86f, 1f),
            shockwave: true,
            useCloud: true,
            lightningChance: 0.35f,
            useFunnel: false,
            extraIntensity: 0.9f);
    }

    private static Color ParseColor(string value, Color fallback)
    {
        if (!string.IsNullOrWhiteSpace(value) && ColorUtility.TryParseHtmlString(value, out var c))
        {
            return c;
        }

        return fallback;
    }

    private sealed class BreakthroughVisualConfigDto
    {
        [JsonProperty("effects")]
        public List<BreakthroughVisualEntryDto> effects { get; set; } = new();
    }

    private sealed class BreakthroughVisualEntryDto
    {
        [JsonProperty("id")]
        public string id { get; set; }

        [JsonProperty("from")]
        public int from { get; set; }

        [JsonProperty("to")]
        public int to { get; set; }

        [JsonProperty("duration")]
        public float duration { get; set; } = 4f;

        [JsonProperty("base_particle")]
        public int base_particle { get; set; } = 16;

        [JsonProperty("radius")]
        public float radius { get; set; } = 1f;

        [JsonProperty("height_offset")]
        public float height_offset { get; set; } = 0.3f;

        [JsonProperty("primary_color")]
        public string primary_color { get; set; }

        [JsonProperty("secondary_color")]
        public string secondary_color { get; set; }

        [JsonProperty("shockwave")]
        public bool shockwave { get; set; }

        [JsonProperty("cloud")]
        public bool cloud { get; set; }

        [JsonProperty("lightning_chance")]
        public float lightning_chance { get; set; } = 0f;

        [JsonProperty("funnel")]
        public bool funnel { get; set; }

        [JsonProperty("extra_intensity")]
        public float extra_intensity { get; set; } = 0f;
    }
}

internal sealed class BreakthroughVisualDefinition
{
    public BreakthroughVisualDefinition(
        string id,
        byte fromLevel,
        byte toLevel,
        float duration,
        int baseParticleCount,
        float radius,
        float heightOffset,
        Color primary,
        Color secondary,
        bool shockwave,
        bool useCloud,
        float lightningChance,
        bool useFunnel,
        float extraIntensity)
    {
        Id = id;
        FromLevel = fromLevel;
        ToLevel = toLevel;
        Duration = duration;
        BaseParticleCount = baseParticleCount;
        Radius = radius;
        HeightOffset = heightOffset;
        PrimaryColor = primary;
        SecondaryColor = secondary;
        Shockwave = shockwave;
        UseCloud = useCloud;
        LightningChance = lightningChance;
        UseFunnel = useFunnel;
        ExtraIntensity = extraIntensity;
    }

    public string Id { get; }
    public byte FromLevel { get; }
    public byte ToLevel { get; }
    public float Duration { get; }
    public int BaseParticleCount { get; }
    public float Radius { get; }
    public float HeightOffset { get; }
    public Color PrimaryColor { get; }
    public Color SecondaryColor { get; }
    public bool Shockwave { get; }
    public bool UseCloud { get; }
    public float LightningChance { get; }
    public bool UseFunnel { get; }
    public float ExtraIntensity { get; }
}
