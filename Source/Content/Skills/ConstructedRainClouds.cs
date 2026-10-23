using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Patch;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>保留法术产生的实际原版雨云，移动、降雨和消散仍由原版推进。</summary>
internal static class ConstructedRainClouds
{
    /// <summary>明确限制本模组同时保留的雨云及保存记录数量。</summary>
    private const int MaximumClouds = 4096;

    /// <summary>只跟踪模组实际生成的云，不接管自然天气。</summary>
    private static readonly Dictionary<Cloud, Identity> Clouds = new();

    /// <summary>保存中稳定的云编号，与原版池中的对象位置分开。</summary>
    private static long nextId;

    /// <summary>原版移动速度和已存续秒数；只在主线程读取和恢复。</summary>
    private static readonly AccessTools.FieldRef<Cloud, float> Speed =
            AccessTools.FieldRefAccess<Cloud, float>("speed"),
        Age = AccessTools.FieldRefAccess<Cloud, float>("alive_time"),
        Lifespan = AccessTools.FieldRefAccess<Cloud, float>("_lifespan"),
        Action1 = AccessTools.FieldRefAccess<Cloud, float>("_timer_action_1"),
        Action2 = AccessTools.FieldRefAccess<Cloud, float>("_timer_action_2");

    /// <summary>原版生成和消散阶段，不能把读档后的云重新当作刚出生。</summary>
    private static readonly AccessTools.FieldRef<BaseEffect, int> State =
        AccessTools.FieldRefAccess<BaseEffect, int>("state");

    /// <summary>原版显示淡入和淡出已经走过的进度。</summary>
    private static readonly AccessTools.FieldRef<BaseEffect, float> Alpha =
        AccessTools.FieldRefAccess<BaseEffect, float>("alpha");

    /// <summary>登记正式天气区段和换世界清理，不建立第二个天气时钟。</summary>
    internal static void Initialize()
    {
        PatchMapBox.RegisterActionOnClearWorld(Clear);
        ModClass.I.WorldSaves.World.Register(new SaveSection<MapBox, SaveData>(
            "constructed_weather", "构造产生的持续天气", 1,
            _ => Capture(), (_, data) => Restore(data), Validate));
    }

    /// <summary>在实际地块生成一朵原版雨云，只在生成成功后报告世界改变。</summary>
    /// <param name="tile">真实成云位置。</param><param name="duration">原理声明的总存续秒数。</param>
    /// <param name="source">发起者长期编号，零表示旧来源未提供人物身份。</param>
    /// <param name="ability">实际构造或旧法术编号。</param>
    /// <param name="startedAt">共同接触时点；旧来源不指定时采用当前实际世界时间。</param>
    internal static SkillEffectResult Spawn(WorldTile tile, float duration, long source, string ability,
        double? startedAt = null)
    {
        if (Clouds.Count >= MaximumClouds || EffectsLibrary.spawn("fx_cloud", tile, "cloud_rain") is not Cloud cloud)
            return default;
        cloud.setLifespan(duration);
        Clouds.Add(cloud, new Identity(++nextId, source, ability, startedAt ?? World.world.getCurWorldTime()));
        MusicBox.playSound("event:/SFX/UNIQUE/SpawnCloud", tile.pos.x, tile.pos.y);
        return new SkillEffectResult(SkillEffectOutcomeFlags.RainCloudSummoned);
    }

    /// <summary>原版云龄原来跟随显示帧；模组天气改用已推进的实际世界秒数，不另设天气时钟。</summary>
    /// <param name="cloud">原版当前处理的云。</param><param name="elapsed">交给原版推进的实际世界时间差。</param>
    internal static bool TryAdvance(Cloud cloud, ref float elapsed)
    {
        if (!Clouds.TryGetValue(cloud, out var identity)) return true;
        if (Config.paused || SmoothLoader.isLoading()) return false;
        double now = World.world.getCurWorldTime();
        elapsed = (float)Math.Max(0, now - identity.LastWorldTime);
        identity.LastWorldTime = Math.Max(identity.LastWorldTime, now);
        return elapsed > 0;
    }

    /// <summary>原版回收对象时立即解除跟踪，池中的同一对象再利用时不会继承旧云身份。</summary>
    /// <param name="effect">原版实际回收的对象。</param>
    internal static void Forget(BaseEffect effect)
    {
        if (effect is Cloud cloud) Clouds.Remove(cloud);
    }

    /// <summary>换世界时移除仍在运行的本模组天气，原版自然云由原版管理。</summary>
    private static void Clear()
    {
        foreach (Cloud cloud in Clouds.Keys.ToArray())
            if (cloud != null)
                cloud.kill();
        Clouds.Clear();
        nextId = 0;
    }

    /// <summary>读取实际云状态，不按照预计结束时间重新推算移动或降雨阶段。</summary>
    private static SaveData Capture()
    {
        var records = new List<CloudRecord>();
        foreach (var pair in Clouds)
        {
            Cloud cloud = pair.Key;
            if (cloud == null) continue;
            SpriteRenderer renderer = cloud.GetComponent<SpriteRenderer>();
            Vector3 position = cloud.transform.localPosition;
            records.Add(new CloudRecord
            {
                Id = pair.Value.Id, Source = pair.Value.Source, Ability = pair.Value.Ability,
                X = position.x, Y = position.y, Z = position.z,
                Age = Age(cloud), Lifespan = Lifespan(cloud), Speed = Speed(cloud),
                Action1 = Action1(cloud), Action2 = Action2(cloud), State = State(cloud),
                Alpha = Alpha(cloud), Sprite = Array.IndexOf(cloud.asset.cached_sprites, renderer.sprite),
                Flip = renderer.flipX
            });
        }

        return new SaveData { NextId = nextId, Clouds = records.ToArray() };
    }

    /// <summary>先解除本世界的旧跟踪，再恢复实际位置、移动、行动间隔及消散进度。</summary>
    /// <param name="data">已经检查的正式天气资料。</param>
    private static void Restore(SaveData data)
    {
        Clear();
        nextId = data.NextId;
        var ids = new HashSet<long>();
        foreach (CloudRecord record in data.Clouds)
        {
            try
            {
                ValidateRecord(record, ids);
            }
            catch (InvalidDataException error)
            {
                ModClass.I.WorldSaves.LastReport.Skip(record?.Id.ToString() ?? "未知", "持续雨云", error.Message);
                continue;
            }

            WorldTile tile = World.world.GetTileSimple(Mathf.Clamp((int)record.X, 0, MapBox.width - 1),
                Mathf.Clamp((int)record.Y, 0, MapBox.height - 1));
            if (EffectsLibrary.spawn("fx_cloud", tile, "cloud_rain") is not Cloud cloud)
            {
                ModClass.I.WorldSaves.LastReport.Skip(record.Id.ToString(), "持续雨云", "原版效果池未能建立雨云");
                continue;
            }

            cloud.transform.localPosition = new Vector3(record.X, record.Y, record.Z);
            Speed(cloud) = record.Speed;
            Age(cloud) = record.Age;
            Lifespan(cloud) = record.Lifespan;
            Action1(cloud) = record.Action1;
            Action2(cloud) = record.Action2;
            State(cloud) = record.State;
            SpriteRenderer renderer = cloud.GetComponent<SpriteRenderer>();
            // 原图缺失时沿用当前原版雨云图，不把 Unity 图片对象写进存档。
            if (record.Sprite >= 0 && record.Sprite < cloud.asset.cached_sprites.Length)
                renderer.sprite = cloud.asset.cached_sprites[record.Sprite];
            renderer.flipX = record.Flip;
            cloud.effect_texture_width = renderer.sprite.rect.width * .08f;
            cloud.effect_texture_height = renderer.sprite.rect.height * .04f;
            Alpha(cloud) = record.Alpha;
            Color color = renderer.color;
            color.a = record.Alpha;
            renderer.color = color;
            Clouds.Add(cloud,
                new Identity(record.Id, record.Source, record.Ability, World.world.getCurWorldTime()));
            nextId = Math.Max(nextId, record.Id);
        }
    }

    /// <summary>只在外部资料读取边界检查有限数值和唯一身份，不把损坏位置送进原版天气。</summary>
    /// <param name="data">待恢复的天气集合。</param>
    private static void Validate(SaveData data)
    {
        if (data.Clouds == null || data.Clouds.Length > MaximumClouds || data.NextId < 0)
            throw new InvalidDataException("持续天气数量或编号无效");
    }

    /// <summary>每朵云单独检查；损坏一条不会丢掉其他仍可恢复的天气。</summary>
    /// <param name="record">当前外部记录。</param><param name="ids">已经接受的实际云编号。</param>
    private static void ValidateRecord(CloudRecord record, ISet<long> ids)
    {
        if (record == null || record.Id <= 0 || record.Ability?.Length > 256 ||
            new[]
            {
                record.X, record.Y, record.Z, record.Age, record.Lifespan, record.Speed,
                record.Action1, record.Action2, record.Alpha
            }.Any(value => float.IsNaN(value) || float.IsInfinity(value)) ||
            record.X < -128 || record.X > MapBox.width + 128 ||
            record.Y < -128 || record.Y > MapBox.height + 128 || Math.Abs(record.Z) > 10000 ||
            record.Lifespan <= 0 || record.Lifespan > 3600 ||
            record.Age < 0 || record.Speed < 0 ||
            record.Alpha > 1 || record.State is not (1 or 2) || !ids.Add(record.Id))
            throw new InvalidDataException("持续雨云的身份、位置、时钟或阶段无效");
    }

    /// <summary>不随原版对象池位置变化的实际来源。</summary>
    private sealed class Identity
    {
        /// <summary>实际云及来源的长期编号。</summary>
        internal readonly long Id, Source;

        /// <summary>产生此云的构造。</summary>
        internal readonly string Ability;

        /// <summary>上次已经交给原版推进的世界时点，不需要随存档保存。</summary>
        internal double LastWorldTime;

        /// <summary>只在真正生成或恢复时建立身份。</summary>
        /// <param name="id">云编号。</param><param name="source">来源人物。</param><param name="ability">产生能力。</param>
        /// <param name="time">产生或恢复时已经确认的世界时点。</param>
        internal Identity(long id, long source, string ability, double time)
        {
            Id = id;
            Source = source;
            Ability = ability;
            LastWorldTime = time;
        }
    }

    /// <summary>第一版正式天气集合，不保存回调或原版对象引用。</summary>
    internal sealed class SaveData
    {
        /// <summary>本世界已经分配的最大云编号。</summary>
        public long NextId;

        /// <summary>仍然实际存在的模组雨云。</summary>
        public CloudRecord[] Clouds;
    }

    /// <summary>一朵实际原版雨云的可升级记录。</summary>
    internal sealed class CloudRecord
    {
        /// <summary>云及来源人物的长期编号。</summary>
        public long Id, Source;

        /// <summary>产生此云的原理来源。</summary>
        public string Ability;

        /// <summary>原版云的实际位置，不把地点重新抽取到另一块地。</summary>
        public float X, Y, Z;

        /// <summary>原版实际时钟、寿命、移动速度和两项行动的剩余间隔。</summary>
        public float Age, Lifespan, Speed, Action1, Action2;

        /// <summary>原版淡入或消散的进度。</summary>
        public float Alpha;

        /// <summary>原版生成或消散阶段及原图片编号。</summary>
        public int State, Sprite;

        /// <summary>原版图片横向朝向。</summary>
        public bool Flip;
    }
}