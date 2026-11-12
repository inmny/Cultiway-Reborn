using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals;
using Cultiway.Content.Skills;
using Cultiway.Patch;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Combat;

/// <summary>原版兵器脱手后仍是同一件物品；地面持有、落地和拾取由器物玩法维护。</summary>
internal sealed partial class GroundWeaponService : BaseSystem
{
    /// <summary>实际地面物品，不借用原持有人的装备槽。</summary>
    private static readonly Dictionary<long, Entry> entries = new();
    /// <summary>读档前已解析的原物品编号，也在人物恢复前阻止原版误删无主物。</summary>
    private static readonly HashSet<long> retained = new();
    /// <summary>等原版物品和人物全部恢复后，再接回同一件物品。</summary>
    private static Archive pending;
    /// <summary>外部模型可从实际物品资料重建，不把网格或图片写入存档。</summary>
    internal static Func<Item, BodySurfacePose> AppearanceProvider;

    /// <summary>同一件物品的完整事实，显示只读这里。</summary>
    internal sealed class Entry
    {
        /// <summary>原版实际物品及正式地面资料。</summary>
        internal Item Item;
        /// <summary>位置、速度和外观大小的稳定资料。</summary>
        internal Record Data;
        /// <summary>本世界复用的实际兵器几何。</summary>
        internal SpatialVisualMesh Model;
        /// <summary>恢复后的实际模型缩放。</summary>
        internal float Scale = 1f;
        /// <summary>当前朝向下的实际最低顶点。</summary>
        internal float Bottom;
        /// <summary>同一实际更新采集的地表，显示不会重复请求地形。</summary>
        internal float Ground;
        /// <summary>已经推进的世界时点。</summary>
        internal double Time;
    }

    /// <summary>地面资料独立升级；实际物品自身仍由原版保存。</summary>
    internal sealed class Archive
    {
        /// <summary>各实际原物品的地面状态。</summary>
        public List<Record> Weapons = new();
    }

    /// <summary>只保存物品编号及数值姿态，不保存人物对象、网格或图片。</summary>
    internal sealed class Record
    {
        /// <summary>原版物品的稳定编号。</summary>
        public long ItemId;
        /// <summary>共同空间绝对位置和实际速度。</summary>
        public float[] Position, Velocity;
        /// <summary>完整朝向，依次为四元数四个分量。</summary>
        public float[] Rotation;
        /// <summary>真实兵器的最长边，恢复时仍按实际物品外观重建。</summary>
        public float Length;
        /// <summary>原版读取物品会重新初始化耐久，地面资料在其后归还实际耐久。</summary>
        public int Durability;
        /// <summary>已经接触地面，不靠显示进度猜测落地。</summary>
        public bool Resting;
        /// <summary>实际外观来源：形体、原版图片、Worldfall原模型或旧版物品重建。</summary>
        public string AppearanceSource;
        /// <summary>主动动作实际借用的稳定形体编号，普通持物为空。</summary>
        public string Form;
        /// <summary>原版有限像素深度所用的身体高度参数，重建同一图片几何时继续使用。</summary>
        public float ImageHeight;
        /// <summary>资源部件顺序和编号，更新资源时可识别已经变化的布局。</summary>
        public string[] PartIds;
        /// <summary>部件相对地面兵器中心和朝向的数值变换，不包含网格。</summary>
        public float[][] PartTransforms;
        /// <summary>实际材质面色，不保存纹理或网格；资源换面后可丢弃这部分色号。</summary>
        public uint[] FaceColors;
    }

    /// <summary>登记真实更新、世界清理及独立保存区段。</summary>
    internal static void Initialize()
    {
        ModClass.I.GeneralLogicSystems.Add(new GroundWeaponService());
        PatchMapBox.RegisterActionOnClearWorld(() => { entries.Clear(); retained.Clear(); pending = null; });
        ModClass.I.WorldSaves.World.Register(new SaveSection<MapBox, Archive>("ground-weapons", "地面兵器", 2,
            _ => new Archive { Weapons = entries.Values.Select(entry =>
            { entry.Data.Durability = entry.Item.data.durability; return entry.Data; }).ToList() },
            (_, archive) =>
            {
                pending = archive;
                foreach (Record record in archive.Weapons) retained.Add(record.ItemId);
            }, Validate, migrations: new ISaveMigration[] { new GroundWeaponsV1ToV2() }) { BeforeActors = true });
        ModClass.I.WorldSaves.WorldRestored += Restore;
    }

    /// <summary>原版清理只让位给明确登记的实际地面物品。</summary>
    /// <param name="item">原版正准备检查的物品。</param>
    internal static bool IsRetained(Item item) => retained.Contains(item.id);

    /// <summary>先核对真实物品和器物规则，再解除原槽位；几何准备失败前不改变归属。</summary>
    /// <param name="owner">原持有人。</param><param name="surface">已经确认接触的实际表面。</param>
    /// <param name="velocity">真实接触带来的速度。</param><param name="time">确认的世界时点。</param>
    /// <param name="form">主动借用的实际形体，普通持物为空。</param>
    /// <param name="reason">未执行时的实际原因。</param>
    internal static bool TryDrop(Actor owner, BodySurfacePose surface, Vector3 velocity, double time, string form, out string reason)
    {
        Item item = owner.getWeapon();
        reason = !EquippedWeaponVisualService.IsCurrent(owner, item) ? "WeaponChanged" :
            !owner.equipment.weapon.canChangeSlot() ? "CursedWeapon" : null;
        if (reason != null) return false;
        Sprite image = EquippedWeaponVisualService.ResolveSprite(owner, item.getAsset());
        SpatialVisualMesh model = Bake(surface, image, out Vector3 position, out Quaternion rotation, form);
        var entry = new Entry
        {
            Item = item, Model = model, Time = time,
            Data = new Record { ItemId = item.id, Position = Values(position), Velocity = Values(velocity),
                Rotation = new[] { rotation.x, rotation.y, rotation.z, rotation.w },
                Length = model.Bounds.size.z, Durability = item.data.durability }
        };
        CaptureAppearance(entry.Data, surface, form);
        entry.Data.FaceColors = model.Colors.Select(color => (uint)(color.r | color.g << 8 | color.b << 16 | color.a << 24)).ToArray();
        if (entry.Data.AppearanceSource == "native") entry.Data.ImageHeight = surface.Height / owner.actor_scale;
        SetBottom(entry);
        entry.Ground = Floor(position.x, position.y);
        entries.Add(item.id, entry);
        retained.Add(item.id);
        owner.equipment.weapon.takeAwayItem();
        owner.setStatsDirty();
        owner.updateStats();
        return true;
    }

    /// <summary>只拾取真实可触及的落地兵器，不能覆盖另一件武器或抢占出招中的手。</summary>
    /// <param name="actor">实际拾取者。</param><param name="itemId">原版物品编号。</param>
    internal static bool TryPickUp(Actor actor, long itemId)
    {
        if (!entries.TryGetValue(itemId, out Entry entry) || !entry.Data.Resting || actor.isRekt() ||
            !actor.isAlive() || actor.hasWeapon() || BodyControlState.IsBusy(actor.id, BodyOccupancy.RightArm) ||
            !actor.asset.use_items || !entry.Item.isAlive() || entry.Item.hasActor() || entry.Item.hasCity()) return false;
        Vector3 foot = SpatialActorPose.Foot(actor);
        float floor = Floor(foot.x, foot.y), height = SpatialActorPose.Height(actor);
        foot.z += floor + height * .5f;
        if (Vector3.Distance(foot, Vector(entry.Data.Position)) > height * .8f) return false;
        actor.equipment.setItem(entry.Item, actor);
        actor.setStatsDirty();
        actor.updateStats();
        entries.Remove(itemId);
        retained.Remove(itemId);
        return true;
    }

    /// <summary>只按世界时间推进重力和首次落地；暂停及绘制不会移动物品。</summary>
    protected override void OnUpdateGroup()
    {
        double now = World.world.getCurWorldTime();
        foreach (Entry entry in entries.Values.ToArray())
        {
            if (!entry.Item.isAlive() || entry.Item.shouldbe_removed || entry.Item.hasActor() || entry.Item.hasCity())
            { entries.Remove(entry.Data.ItemId); retained.Remove(entry.Data.ItemId); continue; }
            Vector3 location = Vector(entry.Data.Position);
            var ground = ConstructedAbilityService.GroundProvider?.Invoke(new Rect(location.x - 2f, location.y - 2f, 4f, 4f));
            entry.Ground = ground?.Sample(location.x, location.y) ?? 0f;
            float remaining = (float)Math.Max(0d, now - entry.Time);
            entry.Time = now;
            if (entry.Data.Resting || remaining <= 0f) continue;
            Vector3 point = Vector(entry.Data.Position), velocity = Vector(entry.Data.Velocity);
            while (remaining > 0f && !entry.Data.Resting)
            {
                float step = Mathf.Min(.02f, remaining);
                point += velocity * step - Vector3.forward * (4.9f * step * step);
                velocity.z -= 9.8f * step;
                remaining -= step;
                point.x = Mathf.Clamp(point.x, 0f, MapBox.width - .001f);
                point.y = Mathf.Clamp(point.y, 0f, MapBox.height - .001f);
                float floor = ground?.Sample(point.x, point.y) ?? 0f;
                entry.Ground = floor;
                if (point.z + entry.Bottom <= floor)
                { point.z = floor - entry.Bottom; velocity = Vector3.zero; entry.Data.Resting = true; }
            }
            entry.Data.Position = Values(point);
            entry.Data.Velocity = Values(velocity);
        }
    }

    /// <summary>两套显示共用同一真实物品位置；不推进落地或拾取。</summary>
    /// <param name="output">本帧已有空间模型列表。</param>
    internal static void Append(List<SpatialVisualMeshInstance> output)
    {
        foreach (Entry entry in entries.Values)
        {
            Vector3 point = Vector(entry.Data.Position);
            point.z -= entry.Ground;
            output.Add(new SpatialVisualMeshInstance(entry.Model, point, Rotation(entry.Data),
                Vector3.one * entry.Scale, Color.white, drawNative: true, additive: false));
        }
    }

    /// <summary>先找回原版物品，再重建当前资源外观；不生成替代品。</summary>
    private static void Restore()
    {
        if (pending == null) return;
        foreach (Record record in pending.Weapons)
        {
            Item item = World.world.items.get(record.ItemId);
            if (item == null || item.hasActor() || item.hasCity())
            {
                retained.Remove(record.ItemId);
                ModClass.I.WorldSaves.LastReport.Skip("地面兵器", record.ItemId.ToString(), "原物品缺失或已有原版持有者");
                continue;
            }
            SpatialVisualMesh model = RestoreAppearance(item, record, out float scale);
            var entry = new Entry { Item = item, Data = record, Model = model,
                Scale = scale, Time = World.world.getCurWorldTime() };
            item.data.durability = record.Durability;
            SetBottom(entry);
            Vector3 position = Vector(record.Position);
            entry.Ground = Floor(position.x, position.y);
            if (record.Resting)
            {
                position.z = entry.Ground - entry.Bottom;
                record.Position = Values(position);
            }
            entries.Add(item.id, entry);
        }
        pending = null;
    }

    /// <summary>地面只从现有共同高度快照采样，普通原版沿原地面零点。</summary>
    /// <param name="x">横向位置。</param><param name="y">纵向位置。</param>
    private static float Floor(float x, float y) =>
        ConstructedAbilityService.GroundProvider?.Invoke(new Rect(x - 1f, y - 1f, 2f, 2f)).Sample(x, y) ?? 0f;

    /// <summary>朝向固定时最低顶点只计算一次，不逐帧扫描兵器网格。</summary>
    /// <param name="entry">实际物品。</param>
    private static void SetBottom(Entry entry)
    {
        Quaternion rotation = Rotation(entry.Data);
        entry.Bottom = entry.Model.Vertices.Min(point => (rotation * (point * entry.Scale)).z);
    }

    /// <summary>存档边界核对稳定编号及有限数字，不改变原物品。</summary>
    /// <param name="archive">待恢复资料。</param>
    private static void Validate(Archive archive)
    {
        if (archive.Weapons == null || archive.Weapons.Select(row => row?.ItemId).Distinct().Count() != archive.Weapons.Count ||
            archive.Weapons.Any(row => row == null || row.ItemId <= 0 || row.Position?.Length != 3 || row.Velocity?.Length != 3 ||
                row.Rotation?.Length != 4 || !(row.Length > 0f) || float.IsInfinity(row.Length) ||
                row.Position.Concat(row.Velocity).Concat(row.Rotation).Any(value => float.IsNaN(value) || float.IsInfinity(value)) ||
                Mathf.Abs(row.Rotation.Sum(value => value * value) - 1f) > .001f ||
                row.AppearanceSource is not ("form" or "native" or "worldfall" or "item") ||
                row.AppearanceSource == "native" && (!(row.ImageHeight > 0f) || float.IsInfinity(row.ImageHeight)) ||
                row.AppearanceSource == "form" && (string.IsNullOrWhiteSpace(row.Form) || row.Form != Path.GetFileName(row.Form)) ||
                (row.PartIds == null) != (row.PartTransforms == null) ||
                row.PartIds != null && (row.PartIds.Length != row.PartTransforms.Length ||
                    row.PartTransforms.Any(matrix => matrix?.Length != 16 || matrix.Any(value => float.IsNaN(value) || float.IsInfinity(value))))))
            throw new InvalidDataException("地面兵器的编号、位置或外观大小无效");
    }

    /// <summary>读取已经核对的三维数字。</summary><param name="value">三个分量。</param>
    private static Vector3 Vector(float[] value) => new(value[0], value[1], value[2]);
    /// <summary>复制三维数字。</summary><param name="value">实际数值。</param>
    private static float[] Values(Vector3 value) => new[] { value.x, value.y, value.z };
    /// <summary>读取已经核对的四元数。</summary><param name="record">实际资料。</param>
    private static Quaternion Rotation(Record record) => new(record.Rotation[0], record.Rotation[1], record.Rotation[2], record.Rotation[3]);
}
