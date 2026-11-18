using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>旧正式构造不追补映像对应，完整原资料与原行为保持。</summary>
internal sealed class ConstructionsV12ToV13 : ISaveMigration
{
    /// <summary>原正式世界构造区段。</summary>
    public int FromVersion => 12;

    /// <summary>只建立正式版本边界，不改写旧构造。</summary>
    /// <param name="data">旧区段的独立副本。</param>
    public JObject Apply(JObject data) => data;
}

/// <summary>原映像保留传伤和完整资料，附带作用默认关闭。</summary>
internal sealed class ConstructionsV13ToV14 : ISaveMigration
{
    /// <summary>原映像所在的正式区段版本。</summary>
    public int FromVersion => 13;

    /// <summary>保留旧构造完整资料。</summary>
    /// <param name="data">旧区段的独立副本。</param>
    public JObject Apply(JObject data) => data;
}
