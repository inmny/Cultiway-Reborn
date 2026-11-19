using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Railways;
using Cultiway.Core;
using NeoModLoader.utils;
using strings;
using UnityEngine;

namespace Cultiway.Content.ActorComponents;

/// <summary>显示火车头和有序车厢，轨道进度由运输系统推进。</summary>
public sealed class Train : BaseActorComponent
{
    /// <summary>按新版约3.96格的车体排列，各节之间保留约0.09格连接缝。</summary>
    public const float SectionLength = 4.05f;
    /// <summary>二维贴图与三维车体使用相同长度，避免收紧间距后旧贴图互相覆盖。</summary>
    private const float SectionBodyLength = 3.96f;
    /// <summary>二维车头、车厢和烟囱统一向下移动半个地图格。</summary>
    private static readonly Vector3 SpriteOffset = Vector3.down * 0.5f;
    /// <summary>一节载客车厢的座位数。</summary>
    public const int SeatsPerCar = 20;
    /// <summary>一节货运车厢的载货量。</summary>
    public const int CargoPerCar = 40;
    /// <summary>第一节为车头的显示器列表。</summary>
    private readonly List<SpriteRenderer> sections = new();
    /// <summary>不包含车头的有序车厢。</summary>
    private readonly List<TrainCarKind> cars = new();
    /// <summary>当前四向连续轨道。</summary>
    private readonly List<WorldTile> track = new();
    /// <summary>车头沿轨道的进度。</summary>
    private float distance;
    /// <summary>下次允许从烟囱喷出烟雾的模拟时刻。</summary>
    private double nextSmoke;
    /// <summary>车节是否可见。</summary>
    private bool visible = true;
    /// <summary>最近一次推进是否遇到断轨。</summary>
    public bool IsBlocked { get; private set; }
    /// <summary>唯一维护实际乘客的原版承载组件。</summary>
    public Boat Carrier { get; private set; }
    /// <summary>代表整列车的原版单位。</summary>
    internal Actor Head => actor;
    /// <summary>当前编组。</summary>
    public IReadOnlyList<TrainCarKind> Cars => cars;
    /// <summary>当前区间轨道。</summary>
    public IReadOnlyList<WorldTile> Track => track;
    /// <summary>车头进度。</summary>
    public float Distance => distance;
    /// <summary>载客车厢提供的座位总数。</summary>
    public int PassengerCapacity => cars.Count(c => c == TrainCarKind.Passenger) * SeatsPerCar;
    /// <summary>货运车厢提供的载货量。</summary>
    public int CargoCapacity => cars.Count(c => c == TrainCarKind.Cargo) * CargoPerCar;
    /// <summary>整列车的长度。</summary>
    public float TrainLength => LengthForCars(cars.Count);

    /// <summary>线路长度与实际车节间距共用同一尺寸。</summary>
    /// <param name="count">不包含车头的车厢数。</param>
    internal static float LengthForCars(int count) => (count + 1) * SectionLength;

    /// <summary>车头和各车厢等距相接，二维与三维共同使用。</summary>
    /// <param name="index">车头为零，后面是实际车厢顺序。</param>
    internal static float SectionOffset(int index) => index * SectionLength;
    /// <summary>车尾完全进入站内后才完成区间。</summary>
    public bool IsSegmentFinished => track.Count > 1 &&
        (distance >= track.Count - 1 + TrainLength || Mathf.Approximately(distance, track.Count - 1 + TrainLength));
    /// <summary>车节脚下变成水、山体或新建筑时须立即疏散。</summary>
    public bool HasUnsafeFooting => track.Count > 1 && Enumerable.Range(0, cars.Count + 1).Any(i =>
    {
        int index = Mathf.Clamp(Mathf.FloorToInt(distance - SectionOffset(i)), 0, track.Count - 1);
        var tile = track[index];
        return !RailTrackRoutePlanner.IsSafeGround(tile) ||
               (tile.building != null && tile.building.asset != Buildings.TrainStation);
    });

    /// <summary>为新建和读档的车头安装承载组件。</summary>
    /// <param name="pActor">代表整列车的单位。</param>
    public override void create(Actor pActor)
    {
        base.create(pActor);
        Carrier = new Boat();
        actor.addChildSimple(Carrier);
        // 共用原版乘客关系，取消和死亡必须先经过铁路行程。
        actor.callbacks_on_death -= Carrier.deathAction;
        actor.callbacks_on_death -= Carrier.spawnBoatExplosion;
        actor.callbacks_landed -= Carrier.cancelWork;
        actor.callbacks_cancel_path_movement -= Carrier.cancelPathfinderMovement;
        actor.callbacks_on_death += OnDisplaced;
        actor.callbacks_landed += OnDisplaced;
        SetCars(new[] { TrainCarKind.Passenger, TrainCarKind.Passenger });
    }

    /// <summary>在空载时设置车厢顺序，读档也使用同一入口。</summary>
    /// <param name="composition">不包含火车头的车厢。</param>
    public void SetCars(IEnumerable<TrainCarKind> composition)
    {
        if (Carrier.hasPassengers() || !RailwayFleetService.CanConfigure(actor))
            throw new InvalidOperationException("只有归属站内空载空闲的列车可以调整车厢");
        var requested = composition.ToArray();
        if (requested.Any(c => c != TrainCarKind.Passenger && c != TrainCarKind.Cargo))
            throw new ArgumentException("车厢只能是载客车厢或货运车厢", nameof(composition));
        cars.Clear();
        cars.AddRange(requested);
        foreach (var section in sections) Destroy(section.gameObject);
        sections.Clear();
        for (int i = 0; i <= cars.Count; i++)
        {
            var obj = new GameObject(i == 0 ? "TrainHead" : "TrainCar_" + i);
            obj.transform.SetParent(transform, false);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = S_SortingLayer.Objects;
            sections.Add(renderer);
        }
        Draw();
    }

    /// <summary>切换区间，不用车长跳过始发轨道。</summary>
    /// <param name="tiles">包含两端且四向相邻的轨道。</param>
    /// <param name="progress">发车为零，读档使用保存进度。</param>
    public void ConfigureTrack(IReadOnlyList<WorldTile> tiles, float progress = 0f)
    {
        track.Clear();
        track.AddRange(tiles);
        distance = Mathf.Clamp(progress, 0f, Math.Max(0, track.Count - 1 + TrainLength));
        IsBlocked = false;
        SyncPosition();
        Draw();
    }

    /// <summary>逐格验证本轮经过的轨道，再推进逻辑位置。</summary>
    /// <param name="elapsed">本轮模拟时长。</param>
    public void Advance(float elapsed)
    {
        if (track.Count < 2 || elapsed <= 0f) return;
        float before = distance;
        float next = Mathf.Min(track.Count - 1 + TrainLength, distance + TrainConfig.TrackSpeed * elapsed);
        IsBlocked = false;
        for (int i = Math.Max(0, Mathf.FloorToInt(distance - TrainLength));
             i <= Math.Min(track.Count - 1, Mathf.CeilToInt(next)); i++)
        {
            if (!IsTrackTile(track[i]) ||
                (i > 0 && !AreAdjacent(track[i - 1], track[i])))
            {
                next = distance;
                IsBlocked = true;
                break;
            }
        }
        distance = next;
        SyncPosition();
        ApplyMovementEffects(before);
    }

    /// <summary>沿原轨道倒退回始发站，车厢位置连续，不把进度套到新线路。</summary>
    /// <param name="elapsed">本轮模拟时长。</param>
    internal void Retreat(float elapsed)
    {
        float before = distance;
        float next = Mathf.Max(0f, distance - TrainConfig.TrackSpeed * elapsed);
        IsBlocked = false;
        for (int i = Math.Max(0, Mathf.FloorToInt(next - TrainLength));
             i <= Math.Min(track.Count - 1, Mathf.FloorToInt(distance)); i++)
        {
            if (!IsTrackTile(track[i]) || (i > 0 && !AreAdjacent(track[i - 1], track[i])))
            {
                IsBlocked = true;
                return;
            }
        }
        distance = next;
        SyncPosition();
        ApplyMovementEffects(before);
    }

    /// <summary>真实移动后扫过车头经过的站外铁轨，并按模拟时间从烟囱喷烟。</summary>
    /// <param name="before">本次推进前的轨道进度。</param>
    private void ApplyMovementEffects(float before)
    {
        if (distance == before) return;
        RailwayMotionEffects.PushTrackUnits(this, before, distance);
        GetSectionPose(0, out var position, out float angle, out bool shown);
        double now = Core.Performance.SimulationTime.Now;
        if (!shown || now < nextSmoke) return;
        nextSmoke = now + .35;
        // 四个方向的烟囱顶端在贴图中的高度不同，转弯时跟随车体的剩余旋转。
        int facing = Mathf.RoundToInt(angle / 90f);
        Vector3 offset = facing == 1 ? new Vector3(0, 5.4f) : facing == -1 ? new Vector3(0, 2.1f) :
            Math.Abs(facing) == 2 ? new Vector3(-.75f, 3.6f) : new Vector3(.75f, 3.6f);
        // 原烟囱偏移按0.3倍贴图标注；车体缩放后，喷烟位置也同步贴回烟囱。
        offset *= SectionSpriteScale(frames["head/" + SectionDirection(facing)][0].bounds.size, facing) / .3f;
        var chimney = position + SpriteOffset + Quaternion.Euler(0, 0, angle - facing * 90f) * offset;
        World.world.particles_smoke.spawn(chimney.x, chimney.y, pRemoveCooldown: true);
    }

    /// <summary>判断轨道或站内端点是否仍合法。</summary>
    /// <param name="tile">待检查格子。</param>
    /// <returns>可通行时为真。</returns>
    internal static bool IsTrackTile(WorldTile tile)
    {
        return RailTrackRoutePlanner.IsSafeGround(tile) &&
               (tile.building == null || (tile.building.asset == Buildings.TrainStation && tile.building.isNormal())) &&
               (tile.top_type == TopTileTypes.TrainTrack ||
                tile.building?.asset == Buildings.TrainStation);
    }

    /// <summary>判断四向相邻。</summary>
    /// <param name="a">前格。</param>
    /// <param name="b">后格。</param>
    /// <returns>四向相邻时为真。</returns>
    internal static bool AreAdjacent(WorldTile a, WorldTile b)
    {
        return a != null && b != null && Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y) == 1;
    }

    /// <summary>同步原版实际坐标、所在格和车内乘客。</summary>
    private void SyncPosition()
    {
        if (track.Count == 0) return;
        actor.current_position = Evaluate(distance);
        actor.dirty_current_tile = true;
        actor.findCurrentTile(false);
        foreach (var passenger in Carrier.getPassengers())
        {
            if (!passenger.isAlive()) continue;
            passenger.setCurrentTilePosition(actor.current_tile);
            passenger.current_position = actor.current_position;
        }
    }

    /// <summary>计算轨迹位置，站内尾部沿入口切线排列。</summary>
    /// <param name="offset">沿轨距离。</param>
    /// <returns>显示位置。</returns>
    private Vector2 Evaluate(float offset)
    {
        if (track.Count < 2) return actor.current_position + Vector2.right * offset;
        if (offset < 0f)
            return (Vector2)track[0].posV3 + ((Vector2)track[1].posV3 - (Vector2)track[0].posV3) * offset;
        int index = Mathf.Clamp(Mathf.FloorToInt(offset), 0, track.Count - 2);
        return Vector2.Lerp(track[index].posV3, track[index + 1].posV3, Mathf.Clamp01(offset - index));
    }

    /// <summary>原版子组件更新只负责显示。</summary>
    /// <param name="pElapsed">动画时长。</param>
    public override void update(float pElapsed)
    {
        base.update(pElapsed);
        Draw();
    }

    /// <summary>各车节独立沿轨道转向。</summary>
    private void Draw()
    {
        for (int i = 0; i < sections.Count; i++)
        {
            var renderer = sections[i];
            GetSectionPose(i, out var head, out float angle, out bool shown);
            renderer.enabled = shown;
            int facing = Mathf.RoundToInt(angle / 90f);
            string direction = SectionDirection(facing);
            string kind = i == 0 ? "head" : cars[i - 1] == TrainCarKind.Passenger ? "passenger" : "cargo";
            var sprites = frames[kind + "/" + direction];
            var frame = sprites[(int)(distance * 2f) % sprites.Length];
            if (kind == "passenger" && actor.kingdom != null)
            {
                // 沿用原版紫色模板换国家色与图集缓存，不给整节车厢叠色。
                var color = actor.kingdom.getColor();
                color.initColor();
                frame = DynamicSprites.getIcon(frame, color);
            }
            renderer.sprite = frame;
            renderer.transform.localScale = Vector3.one * SectionSpriteScale(frame.bounds.size, facing);
            renderer.transform.position = head + SpriteOffset;
            renderer.transform.localEulerAngles = new Vector3(0f, 0f, angle - facing * 90f);
        }
    }

    /// <summary>按各方向贴图的纵向或横向长度等比缩放，不拉伸车体。</summary>
    /// <param name="size">贴图在游戏中的原始尺寸。</param>
    /// <param name="facing">四向朝向，上下一向分别为一和负一。</param>
    /// <returns>让车体长度保持一致的等比缩放。</returns>
    internal static float SectionSpriteScale(Vector2 size, int facing) =>
        SectionBodyLength / (Math.Abs(facing) == 1 ? size.y : size.x);

    /// <summary>让车体贴图与烟囱偏移使用同一个方向。</summary>
    /// <param name="facing">沿轨道取整后的四向朝向。</param>
    /// <returns>实际贴图方向名称。</returns>
    private static string SectionDirection(int facing) => facing == 1 ? "up" : facing == -1 ? "down" :
        Math.Abs(facing) == 2 ? "left" : "right";

    /// <summary>二维和三维共同使用真实进度、方向及进出站边界。</summary>
    /// <param name="index">车头为零，后续为实际编组顺序。</param>
    /// <param name="position">车节位置。</param>
    /// <param name="angle">沿轨方向，单位为度。</param>
    /// <param name="shown">该车节是否已经出站且尚未进入目标站。</param>
    internal void GetSectionPose(int index, out Vector3 position, out float angle, out bool shown)
    {
        float progress = distance - SectionOffset(index);
        position = Evaluate(progress);
        var tail = Evaluate(progress - SectionLength);
        angle = Mathf.Atan2(position.y - tail.y, position.x - tail.x) * Mathf.Rad2Deg;
        shown = visible && (track.Count < 2 || (progress >= 0 && progress <= track.Count - 1 &&
            track[Mathf.FloorToInt(progress)].building?.asset != Buildings.TrainStation));
    }

    /// <summary>显示全部车节。</summary>
    public void Show() => visible = true;
    /// <summary>只隐藏外观，不关闭运输组件。</summary>
    public void Hide() => visible = false;

    /// <summary>毁车或外力落地后终止原行程并疏散。</summary>
    /// <param name="train">本列车车头。</param>
    private void OnDisplaced(Actor train)
    {
        TrainTransportSystem.Instance?.CancelTrain(train);
    }

    /// <summary>初始化时加载的全部方向帧。</summary>
    private static readonly Dictionary<string, Sprite[]> frames = new();

    /// <summary>复用已有车头、车厢并注册头像组件。</summary>
    internal static void Init()
    {
        foreach (string kind in new[] { "head", "passenger", "cargo" })
        foreach (string direction in new[] { "left", "right", "up", "down" })
            frames[kind + "/" + direction] = SpriteTextureLoader.getSpriteList("actors/train/" + kind + "/" + direction);
        var obj = ModClass.NewPrefabPreview("Train", typeof(Train));
        ResourcesPatch.PatchResource("actors/p_train", obj);
        ActorExtend.RegisterPossibleChildren<Train>();
    }
}
