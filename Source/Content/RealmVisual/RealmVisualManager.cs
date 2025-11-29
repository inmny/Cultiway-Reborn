using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils;
using Newtonsoft.Json;
using UnityEngine;
using RealmVisualComponent = Cultiway.Content.Components.RealmVisual;

namespace Cultiway.Content.RealmVisual;

internal sealed class RealmVisualManager : ICanInit, ICanReload
{
    private const string ConfigRelativePath = "Content/RealmVisual/realm_visual_config.json";

    private readonly Dictionary<byte, RealmVisualDefinition> _definitionByIndex = new();
    private readonly List<RealmVisualDefinition> _sortedDefinitions = new();
    private readonly Dictionary<string, Sprite> _spriteCache = new(StringComparer.OrdinalIgnoreCase);

    private IndicatorSpritePaths _indicatorPaths = new();
    private Sprite _jindanIndicatorSprite;
    private Sprite _yuanyingIndicatorSprite;
    private Sprite _elementParticleSprite;

    public static RealmVisualManager Instance { get; private set; }

    public int MaxParticlesPerActor => 30;

    public bool VisualEnabled => ModClass.I.GetConfig()["RealmVisualSettings"]["REALM_VISUAL_ENABLED"].BoolVal;
    public bool AuraEnabled => VisualEnabled && ModClass.I.GetConfig()["RealmVisualSettings"]["AURA_ENABLED"].BoolVal;
    public bool ParticleEnabled => VisualEnabled && ModClass.I.GetConfig()["RealmVisualSettings"]["PARTICLE_ENABLED"].BoolVal;
    public bool IndicatorEnabled => VisualEnabled && ModClass.I.GetConfig()["RealmVisualSettings"]["INDICATOR_ENABLED"].BoolVal;

    public void Init()
    {
        Instance = this;
        LoadConfig();
        ActorExtend.RegisterActionOnUpdateStats(UpdateVisualComponent);
    }

    public void OnReload()
    {
        LoadConfig();
        ModClass.I.ActorExtendManager.AllStatsDirty();
    }

    private void LoadConfig()
    {
        try
        {
            var fullPath = Path.Combine(ModClass.I.GetDeclaration().FolderPath, ConfigRelativePath);
            if (!File.Exists(fullPath))
            {
                ModClass.LogWarning($"[RealmVisual] 配置文件缺失: {fullPath}");
                _sortedDefinitions.Clear();
                _definitionByIndex.Clear();
                return;
            }

            var json = File.ReadAllText(fullPath);
            var dto = JsonConvert.DeserializeObject<RealmVisualConfigDto>(json) ?? new RealmVisualConfigDto();

            _sortedDefinitions.Clear();
            _definitionByIndex.Clear();
            _spriteCache.Clear();

            if (dto.realm_visuals != null && dto.realm_visuals.Count > 0)
            {
                var ordered = dto.realm_visuals
                    .Where(v => v != null)
                    .OrderBy(v => v.realm_level)
                    .ToList();

                byte index = 0;
                foreach (var entry in ordered)
                {
                    var def = new RealmVisualDefinition(
                        index,
                        entry.realm_level,
                        entry.aura_color,
                        entry.alpha_range,
                        entry.scale_multiplier,
                        entry.base_particle_count,
                        entry.aura_sprite_path,
                        entry.breath_speed,
                        entry.breath_amplitude,
                        LoadSprite(entry.aura_sprite_path));
                    _sortedDefinitions.Add(def);
                    _definitionByIndex[index] = def;
                    index++;
                }
            }

            _indicatorPaths = dto.indicator_sprites ?? new IndicatorSpritePaths();
            _jindanIndicatorSprite = LoadSprite(_indicatorPaths.jindan);
            _yuanyingIndicatorSprite = LoadSprite(_indicatorPaths.yuanying);
            _elementParticleSprite = LoadSprite(_indicatorPaths.particle);
        }
        catch (Exception ex)
        {
            ModClass.LogError($"[RealmVisual] 配置读取失败: {ex.Message}");
            _sortedDefinitions.Clear();
            _definitionByIndex.Clear();
        }
    }

    private Sprite LoadSprite(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (_spriteCache.TryGetValue(path, out var cached)) return cached;
        var sprite = SpriteTextureLoader.getSprite(path);
        _spriteCache[path] = sprite;
        return sprite;
    }

    private void UpdateVisualComponent(ActorExtend ae)
    {
        if (!VisualEnabled || !ae.HasCultisys<Xian>())
        {
            RemoveComponentIfExists(ae);
            return;
        }

        var definition = GetDefinitionForLevel(ae.GetCultisys<Xian>().CurrLevel);
        if (definition == null)
        {
            RemoveComponentIfExists(ae);
            return;
        }

        var indicatorFlags = (byte)0;
        var hasYuanying = ae.E.HasComponent<Yuanying>();
        if (hasYuanying)
        {
            indicatorFlags |= RealmVisualComponent.IndicatorFlagYuanying;
        }
        else if (ae.E.HasComponent<Jindan>())
        {
            indicatorFlags |= RealmVisualComponent.IndicatorFlagJindan;
        }

        ref var component = ref EnsureComponent(ae);
        component.definition_index = definition.Index;
        component.realm_stage = (byte)Mathf.Clamp(definition.RealmLevel, 0, byte.MaxValue);
        component.visual_state = 0;
        component.indicator_flags = indicatorFlags;
        component.has_element_root = ae.HasElementRoot();
    }

    private static ref RealmVisualComponent EnsureComponent(ActorExtend ae)
    {
        if (!ae.E.HasComponent<RealmVisualComponent>())
        {
            ae.E.AddComponent(new RealmVisualComponent
            {
                definition_index = byte.MaxValue
            });
        }

        return ref ae.E.GetComponent<RealmVisualComponent>();
    }

    private static void RemoveComponentIfExists(ActorExtend ae)
    {
        if (ae.E.HasComponent<RealmVisualComponent>())
        {
            ae.E.RemoveComponent<RealmVisualComponent>();
        }
    }

    public RealmVisualDefinition GetDefinition(byte index)
    {
        return _definitionByIndex.TryGetValue(index, out var value) ? value : null;
    }

    public RealmVisualDefinition GetDefinitionForLevel(int level)
    {
        RealmVisualDefinition result = null;
        foreach (var definition in _sortedDefinitions)
        {
            if (level < definition.RealmLevel)
            {
                break;
            }

            result = definition;
        }

        return result;
    }

    public Sprite GetIndicatorSprite(byte indicatorFlags)
    {
        if ((indicatorFlags & RealmVisualComponent.IndicatorFlagYuanying) != 0)
        {
            return _yuanyingIndicatorSprite;
        }

        if ((indicatorFlags & RealmVisualComponent.IndicatorFlagJindan) != 0)
        {
            return _jindanIndicatorSprite;
        }

        return null;
    }

    public Sprite GetElementParticleSprite() => _elementParticleSprite;

    public Color GetElementColor(int elementIndex)
    {
        return elementIndex switch
        {
            ElementIndex.Iron  => RealmColors.IronElement,
            ElementIndex.Wood  => RealmColors.WoodElement,
            ElementIndex.Water => RealmColors.WaterElement,
            ElementIndex.Fire  => RealmColors.FireElement,
            ElementIndex.Earth => RealmColors.EarthElement,
            _                  => Color.white
        };
    }

    private sealed class RealmVisualConfigDto
    {
        [JsonProperty("realm_visuals")]
        public List<RealmVisualEntryDto> realm_visuals { get; set; } = new();

        [JsonProperty("indicator_sprites")]
        public IndicatorSpritePaths indicator_sprites { get; set; } = new();
    }

    private sealed class RealmVisualEntryDto
    {
        [JsonProperty("id")]
        public string id { get; set; }

        [JsonProperty("realm_level")]
        public int realm_level { get; set; }

        [JsonProperty("aura_color")]
        public string aura_color { get; set; }

        [JsonProperty("alpha_range")]
        public List<float> alpha_range { get; set; }

        [JsonProperty("scale_multiplier")]
        public float scale_multiplier { get; set; } = 1f;

        [JsonProperty("base_particle_count")]
        public int base_particle_count { get; set; }

        [JsonProperty("aura_sprite_path")]
        public string aura_sprite_path { get; set; }

        [JsonProperty("breath_speed")]
        public float breath_speed { get; set; } = 0.5f;

        [JsonProperty("breath_amplitude")]
        public float breath_amplitude { get; set; } = 0.05f;
    }

    private sealed class IndicatorSpritePaths
    {
        [JsonProperty("jindan")]
        public string jindan { get; set; }

        [JsonProperty("yuanying")]
        public string yuanying { get; set; }

        [JsonProperty("particle")]
        public string particle { get; set; }
    }
}

internal sealed class RealmVisualDefinition
{
    public RealmVisualDefinition(
        byte index,
        int realmLevel,
        string colorHex,
        IList<float> alphaRange,
        float scaleMultiplier,
        int baseParticleCount,
        string spritePath,
        float breathSpeed,
        float breathAmplitude,
        Sprite sprite)
    {
        Index = index;
        RealmLevel = realmLevel;
        AuraColor = ParseColor(colorHex);
        AlphaMin = alphaRange != null && alphaRange.Count > 0 ? alphaRange[0] : 0f;
        AlphaMax = alphaRange != null && alphaRange.Count > 1 ? alphaRange[1] : AlphaMin;
        ScaleMultiplier = Mathf.Max(scaleMultiplier, 0f);
        BaseParticleCount = Mathf.Max(0, baseParticleCount);
        AuraSpritePath = spritePath ?? string.Empty;
        BreathSpeed = Mathf.Max(breathSpeed, 0.01f);
        BreathAmplitude = Mathf.Clamp(breathAmplitude, 0f, 1f);
        AuraSprite = sprite;
    }

    public byte Index { get; }
    public int RealmLevel { get; }
    public Color AuraColor { get; }
    public float AlphaMin { get; }
    public float AlphaMax { get; }
    public float ScaleMultiplier { get; }
    public int BaseParticleCount { get; }
    public string AuraSpritePath { get; }
    public float BreathSpeed { get; }
    public float BreathAmplitude { get; }
    public Sprite AuraSprite { get; }

    private static Color ParseColor(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            ColorUtility.TryParseHtmlString(value, out var color))
        {
            return color;
        }

        return RealmColors.QiRefining;
    }
}

