using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>第十五版正式等待请求保留原改造选择，新请求才修复运动开始引用。</summary>
internal sealed class CreationV16ToV17 : ISaveMigration
{
    /// <summary>原正式人物区段。</summary>
    public int FromVersion => 16;
    /// <summary>原样保留冻结输入，既有成功结果及明确失败均不重抽。</summary>
    /// <param name="data">旧请求独立副本。</param>
    public JObject Apply(JObject data) => data;
}
