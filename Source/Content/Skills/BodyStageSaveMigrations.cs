using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>正式构造声明内部阶段；缺少阶段的旧动作仍使用原窗口。</summary>
internal sealed class ConstructionsV8ToV9 : ISaveMigration
{
    /// <summary>已有结果恢复的正式构造版本。</summary>
    public int FromVersion => 8;
    /// <summary>保留完整旧资料，缺省阶段由当前编译统一解释。</summary>
    /// <param name="data">保存层提供的独立资料。</param>
    public JObject Apply(JObject data) => data;
}

/// <summary>新请求可以改造内部阶段，正式旧请求仍保留受理时的输入及规则号。</summary>
internal sealed class CreationV5ToV6 : ISaveMigration
{
    /// <summary>支持结果收势但尚无内部阶段选择的请求格式。</summary>
    public int FromVersion => 5;
    /// <summary>只完成相邻升版，不重新受理请求或改变种子。</summary>
    /// <param name="data">保存层提供的旧请求副本。</param>
    public JObject Apply(JObject data) => data;
}
