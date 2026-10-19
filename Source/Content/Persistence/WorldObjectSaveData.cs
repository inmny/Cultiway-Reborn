using System.Collections.Generic;
using Cultiway.Core.SkillLibV3.Blueprints;
using Friflo.Engine.ECS;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Persistence;

/// <summary>独立物件跨多次保存仍保持同一编号。</summary>
internal struct WorldSaveIdentity : IComponent
{
    /// <summary>随机生成的稳定编号，与内存实体编号无关。</summary>
    public string Id;
}

/// <summary>世界物件、知识定义和它们的连接。</summary>
internal sealed class WorldObjectSaveData
{
    /// <summary>明确登记的动态定义，先于人物恢复。</summary>
    public string Definitions;
    /// <summary>全部物件；先建立对象，再连接引用。</summary>
    public List<WorldObjectRecord> Objects = new();
    /// <summary>公开魔网的条目及发布时间。</summary>
    public List<MagicWebSaveEntry> MagicWeb = new();
}

/// <summary>一个人物附加实体、物件、技能或状态的稳定记录。</summary>
internal sealed class WorldObjectRecord
{
    /// <summary>人物、书籍和城市使用原版编号，其余对象使用独立编号。</summary>
    public string Id;
    /// <summary>版本独立的组件与关系记录。</summary>
    public string Parts;
    /// <summary>技能保存可重新编译的定义，不保存函数或贴图。</summary>
    public SkillBlueprint Skill;
    /// <summary>器官、境界和武器操控授予的只读技能，引用当前程序生成的定义。</summary>
    public string GrantedSkillAsset;
    /// <summary>来源技能的动画变体。</summary>
    public int GrantedSkillAnimation;
    /// <summary>器官登记处会在换图时重建容器，读取应接到它当前使用的对象。</summary>
    public bool OrganGrantedSkill;
    /// <summary>人物或书籍掌握的动态知识。</summary>
    public KnowledgeSaveData Knowledge;
    /// <summary>技能学习顺序，影响施法选择。</summary>
    public List<string> LearnedSkills;
}

/// <summary>按资产编号保存知识及熟练程度。</summary>
internal sealed class KnowledgeSaveData
{
    /// <summary>已掌握的功法。</summary>
    public Dictionary<string, float> Cultibooks = new();
    /// <summary>已掌握的丹方。</summary>
    public Dictionary<string, float> Elixirs = new();
    /// <summary>继承的骑士血脉。</summary>
    public Dictionary<string, float> Bloodlines = new();
}

/// <summary>公开法术的历史资料；查询索引在读取时重建。</summary>
internal sealed class MagicWebSaveEntry
{
    /// <summary>法术物件的稳定编号。</summary>
    public string Skill;
    /// <summary>是否为默认公开法术。</summary>
    public bool IsDefault;
    /// <summary>发布者编号。</summary>
    public long PublisherActorId;
    /// <summary>发布者姓名。</summary>
    public string PublisherName;
    /// <summary>发布时间。</summary>
    public double PublishedAt;
    /// <summary>最后访问时间。</summary>
    public double AccessedAt;
}

/// <summary>为普通组件提供可独立升级的记录外壳。</summary>
internal sealed class ComponentSaveValue<T> where T : struct
{
    /// <summary>组件的稳定字段。</summary>
    public T Value;
}

/// <summary>动态定义按条目独立恢复，一个失效条目不影响其他条目。</summary>
internal sealed class DynamicDefinitionSaveData
{
    /// <summary>功法定义。</summary>
    public List<JObject> Cultibooks = new();
    /// <summary>丹方定义。</summary>
    public List<JObject> Elixirs = new();
    /// <summary>骑士血脉定义。</summary>
    public List<JObject> Bloodlines = new();
}
