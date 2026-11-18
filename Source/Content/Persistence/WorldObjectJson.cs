using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.Semantics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Persistence;

/// <summary>只处理已登记的数据；把实体引用改写为稳定编号，把属性表写成数值表。</summary>
internal sealed class WorldObjectJson : JsonConverter
{
    /// <summary>保存时登记引用对象。</summary>
    private readonly Func<Entity, string> reference;
    /// <summary>读取时从稳定编号解析本次内存对象。</summary>
    private readonly Func<string, Entity> resolve;
    /// <summary>组件专用的字段规则与引用转换。</summary>
    internal readonly JsonSerializerSettings Settings;

    /// <summary>创建本次保存或读取专用的转换器。</summary>
    /// <param name="reference">把对象转换为存档编号的方法。</param>
    /// <param name="resolve">从存档编号取得当前对象的方法。</param>
    internal WorldObjectJson(Func<Entity, string> reference, Func<string, Entity> resolve)
    {
        this.reference = reference;
        this.resolve = resolve;
        Settings = new JsonSerializerSettings
        {
            ContractResolver = new RecordFields(new EntityNumberJson(reference, resolve)),
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.None,
            Converters = { this }
        };
    }

    /// <summary>只接管包含缓存、引用或非字段资料的明确类型。</summary>
    /// <param name="type">需要转换的类型。</param>
    public override bool CanConvert(Type type) => type == typeof(Entity) || type == typeof(BaseStats) ||
        type == typeof(ElementRoot) || type == typeof(Cultibook) || type == typeof(Elixirbook) ||
        type == typeof(YuanshenAnchorHandle) || type == typeof(ActorCultibookState) || type == typeof(SkillCooldownEntry) ||
        type == typeof(SemanticAsset) || typeof(BaseSimObject).IsAssignableFrom(type) ||
        type == typeof(Item) || type == typeof(Libraries.KnightTechniqueAsset);

    /// <summary>把运行期引用改成明确的稳定资料。</summary>
    /// <param name="writer">写入当前记录的工具。</param>
    /// <param name="value">当前值。</param>
    /// <param name="serializer">本次资料转换器。</param>
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        switch (value)
        {
            case Item item:
                writer.WriteValue(item.id);
                return;
            case Libraries.KnightTechniqueAsset technique:
                writer.WriteValue(technique.id);
                return;
            case Actor actor:
                serializer.Serialize(writer, new SourceObject { Kind = "actor", Id = actor.id });
                return;
            case Building building:
                serializer.Serialize(writer, new SourceObject { Kind = "building", Id = building.id });
                return;
            case SemanticAsset semantic:
                writer.WriteValue(semantic.id);
                return;
            case SkillCooldownEntry cooldown:
                serializer.Serialize(writer, new CooldownReference
                {
                    Skill = ModClass.I.W.TryGetEntityByPid(cooldown.SkillContainerPid, out Entity skill) ? reference(skill) : null,
                    Remaining = cooldown.Remaining
                });
                return;
            case Entity entity:
                writer.WriteValue(reference(entity));
                return;
            case BaseStats stats:
                serializer.Serialize(writer, stats.getList().ToDictionary(entry => entry.id, entry => entry.value));
                return;
            case ElementRoot root:
                serializer.Serialize(writer, Enumerable.Range(0, 8).Select(index => root[index]).ToArray());
                return;
            case Cultibook book:
                writer.WriteValue(book.ID);
                return;
            case Elixirbook book:
                writer.WriteValue(book.ID);
                return;
            case YuanshenAnchorHandle handle:
                serializer.Serialize(writer, new AnchorReference
                {
                    Object = ReferenceNumber(handle.EntityId), Generation = handle.Generation, Owner = handle.OwnerActorId
                });
                return;
            case ActorCultibookState state:
                var progress = new Dictionary<string, float>();
                if (state.SkillProgress != null)
                    foreach (var entry in state.SkillProgress)
                    {
                        string id = ReferenceNumber(entry.Key);
                        if (id != null) progress[id] = entry.Value;
                    }
                serializer.Serialize(writer, new CultibookProgress
                {
                    Id = state.MainCultibookId, Mastery = state.MainMastery,
                    Time = state.AccumulatedTime, Skills = progress, Constructions = state.ConstructionProgress
                });
                return;
        }
        throw new InvalidDataException("未登记的存档类型");
    }

    /// <summary>先核对外部记录，再建立当前运行期对象。</summary>
    /// <param name="reader">读取当前记录的工具。</param>
    /// <param name="objectType">当前记录的类型。</param>
    /// <param name="existingValue">读取之前已有的值。</param>
    /// <param name="serializer">本次资料转换器。</param>
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        JToken token = JToken.Load(reader);
        if (objectType == typeof(Item)) return token.Type == JTokenType.Null ? null : World.world.items.get(token.Value<long>());
        if (objectType == typeof(Libraries.KnightTechniqueAsset))
            return token.Type == JTokenType.Null ? null : Libraries.Manager.KnightTechniqueLibrary.get(token.Value<string>());
        if (typeof(BaseSimObject).IsAssignableFrom(objectType))
        {
            if (token.Type == JTokenType.Null) return null;
            var source = token.ToObject<SourceObject>(serializer);
            // 施加者可以已经死亡；强度单独保存，剩余状态仍正常生效。
            return source.Kind switch
            {
                "actor" => World.world.units.get(source.Id),
                "building" => World.world.buildings.get(source.Id),
                _ => throw new InvalidDataException("状态来源类型无效")
            };
        }
        if (objectType == typeof(SemanticAsset))
        {
            if (token.Type == JTokenType.Null) return null;
            if (!ModClass.L.SemanticLibrary.TryResolve(token.Value<string>(), out SemanticAsset semantic))
                throw new InvalidDataException("语义定义已移除：" + token);
            return semantic;
        }
        if (objectType == typeof(SkillCooldownEntry))
        {
            var record = token.ToObject<CooldownReference>(serializer);
            try
            {
                return new SkillCooldownEntry { SkillContainerPid = record.Skill == null ? 0 : resolve(record.Skill).Pid, Remaining = record.Remaining };
            }
            catch (InvalidDataException error)
            {
                ModClass.I.WorldSaves.LastReport.Skip("技能冷却", record.Skill, error.Message);
                return default(SkillCooldownEntry);
            }
        }
        if (objectType == typeof(Entity)) return token.Type == JTokenType.Null ? default(Entity) : resolve(token.Value<string>());
        if (objectType == typeof(BaseStats))
        {
            if (token.Type == JTokenType.Null) return null;
            var stats = new BaseStats();
            foreach (var entry in token.ToObject<Dictionary<string, float>>())
            {
                if (AssetManager.base_stats_library.get(entry.Key) == null || float.IsNaN(entry.Value) || float.IsInfinity(entry.Value))
                    throw new InvalidDataException($"属性已移除或数值无效：{entry.Key}");
                stats[entry.Key] = entry.Value;
            }
            return stats;
        }
        if (objectType == typeof(ElementRoot))
        {
            float[] values = token.ToObject<float[]>();
            if (values == null || values.Length != 8 || values.Any(value => value < 0 || float.IsNaN(value) || float.IsInfinity(value)))
                throw new InvalidDataException("灵根资料无效");
            return new ElementRoot(values);
        }
        if (objectType == typeof(Cultibook))
        {
            string id = token.Value<string>();
            if (Libraries.Manager.CultibookLibrary.get(id) == null) throw new InvalidDataException($"功法已移除：{id}");
            return new Cultibook(id);
        }
        if (objectType == typeof(Elixirbook))
        {
            string id = token.Value<string>();
            if (Libraries.Manager.ElixirLibrary.get(id) == null) throw new InvalidDataException($"丹方已移除：{id}");
            return new Elixirbook(id);
        }
        if (objectType == typeof(YuanshenAnchorHandle))
        {
            AnchorReference record = token.ToObject<AnchorReference>(serializer);
            if (record.Object == null) return default(YuanshenAnchorHandle);
            Entity entity = resolve(record.Object);
            var state = new YuanshenAnchorState { generation = record.Generation, owner_actor_id = record.Owner };
            return new YuanshenAnchorHandle(entity.Id, in state);
        }
        if (objectType == typeof(ActorCultibookState))
        {
            CultibookProgress record = token.ToObject<CultibookProgress>(serializer);
            if (!string.IsNullOrEmpty(record.Id) && Libraries.Manager.CultibookLibrary.get(record.Id) == null)
                throw new InvalidDataException($"主修功法已移除：{record.Id}");
            var state = new ActorCultibookState
            {
                MainCultibookId = record.Id, MainMastery = record.Mastery, AccumulatedTime = record.Time,
                SkillProgress = new Dictionary<int, float>(),
                ConstructionProgress = record.Constructions ?? new Dictionary<string, float>()
            };
            foreach (var entry in record.Skills) state.SkillProgress[resolve(entry.Key).Id] = entry.Value;
            return state;
        }
        throw new InvalidDataException("未登记的存档类型");
    }

    /// <summary>仅在存档边界把少量历史上使用整数的实体引用改成稳定编号。</summary>
    /// <param name="id">长期保持稳定的编号。</param>
    private string ReferenceNumber(int id) => id <= 0 ? null : reference(ModClass.I.W.GetEntityById(id));

    /// <summary>元神锚点保存稳定物件编号，保留原有归属与代次校验。</summary>
    private sealed class AnchorReference
    {
        /// <summary>锚点物件编号。</summary>
        public string Object;
        /// <summary>创建代次。</summary>
        public int Generation;
        /// <summary>人物编号。</summary>
        public long Owner;
    }

    /// <summary>技能冷却关联稳定法术编号，而非本次实体编号。</summary>
    private sealed class CooldownReference
    {
        /// <summary>技能稳定编号。</summary>
        public string Skill;
        /// <summary>剩余秒数。</summary>
        public float Remaining;
    }

    /// <summary>持续状态的施加者，使用原版人物或建筑编号。</summary>
    private sealed class SourceObject
    {
        /// <summary>原版对象种类。</summary>
        public string Kind;
        /// <summary>原版稳定编号。</summary>
        public long Id;
    }

    /// <summary>功法领悟进度按法术稳定编号保存。</summary>
    private sealed class CultibookProgress
    {
        /// <summary>主修功法编号。</summary>
        public string Id;
        /// <summary>掌握程度。</summary>
        public float Mastery;
        /// <summary>累计时间。</summary>
        public float Time;
        /// <summary>各法术领悟进度。</summary>
        public Dictionary<string, float> Skills = new();

        /// <summary>新增构造采用本来就是长期编号的进度；旧资料缺少时为空。</summary>
        public Dictionary<string, float> Constructions = new();
    }

    /// <summary>字段规则排除缓存与函数，只为已登记的历史整数引用安装转换。</summary>
    private sealed class RecordFields : DefaultContractResolver
    {
        /// <summary>整数引用转换器。</summary>
        private readonly JsonConverter entityNumber;
        /// <summary>共享本次保存上下文。</summary>
        /// <param name="entityNumber">历史整数引用的转换器。</param>
        internal RecordFields(JsonConverter entityNumber) => this.entityNumber = entityNumber;
        /// <summary>读取公共字段，不触发资产或计算属性。</summary>
        /// <param name="type">需要转换的类型。</param>
        /// <param name="memberSerialization">当前资料的成员读取规则。</param>
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            => type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(field => (!field.IsDefined(typeof(Friflo.Json.Fliox.IgnoreAttribute), true) ||
                                field.DeclaringType == typeof(ArtifactAbilityRuntimeEntry) &&
                                field.Name is "activity_carrier_actor_id" or "activity_effect_scale" ||
                                field.DeclaringType == typeof(StatusComponent) &&
                                field.Name is "Source" or "SourcePowerLevel" or "SourceActorId") &&
                                !typeof(Delegate).IsAssignableFrom(field.FieldType))
                .Select(field =>
                {
                    JsonProperty property = base.CreateProperty(field, MemberSerialization.Fields);
                    if (field.DeclaringType == typeof(StatusComponent) && field.Name == nameof(StatusComponent.SourceActorId))
                        property.DefaultValueHandling = DefaultValueHandling.Ignore;
                    if (field.Name is "artifact_entity_id" or "anchor_artifact_entity_id") property.Converter = entityNumber;
                    return property;
                }).ToList();
    }

    /// <summary>把历史整数引用写成稳定编号，读取时重新分配到本次实体。</summary>
    private sealed class EntityNumberJson : JsonConverter
    {
        /// <summary>保存引用。</summary>
        private readonly Func<Entity, string> reference;
        /// <summary>恢复引用。</summary>
        private readonly Func<string, Entity> resolve;
        /// <summary>绑定本次上下文。</summary>
        /// <param name="reference">把对象转换为存档编号的方法。</param>
        /// <param name="resolve">从存档编号取得当前对象的方法。</param>
        internal EntityNumberJson(Func<Entity, string> reference, Func<string, Entity> resolve)
        { this.reference = reference; this.resolve = resolve; }
        /// <summary>只适用于明确登记的整数引用字段。</summary>
        /// <param name="type">需要转换的类型。</param>
        public override bool CanConvert(Type type) => type == typeof(int);
        /// <summary>写入物件编号。</summary>
        /// <param name="writer">写入当前记录的工具。</param>
        /// <param name="value">当前值。</param>
        /// <param name="serializer">本次资料转换器。</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            => writer.WriteValue((int)value <= 0 ? null : reference(ModClass.I.W.GetEntityById((int)value)));
        /// <summary>读取本次内存编号。</summary>
        /// <param name="reader">读取当前记录的工具。</param>
        /// <param name="type">需要转换的类型。</param>
        /// <param name="existing">读取之前已有的值。</param>
        /// <param name="serializer">本次资料转换器。</param>
        public override object ReadJson(JsonReader reader, Type type, object existing, JsonSerializer serializer)
            => reader.TokenType == JsonToken.Null ? 0 : resolve((string)reader.Value).Id;
    }
}
