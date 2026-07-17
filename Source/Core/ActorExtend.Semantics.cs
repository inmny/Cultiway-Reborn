using Cultiway.Core.Semantics;

namespace Cultiway.Core;

public partial class ActorExtend
{
    private long semanticProfileRevision;
    private long cachedSemanticProfileRevision = -1;
    private int cachedSemanticLibraryRevision = -1;
    private SemanticProfile cachedSemanticProfile;

    /// <summary>
    /// 标记会改变角色长期语义档案的数据已经发生变化。
    /// </summary>
    public void MarkSemanticProfileDirty()
    {
        semanticProfileRevision++;
    }

    /// <summary>
    /// 获取当前角色的派生语义档案。档案只在来源或语义库修订变化后重建。
    /// </summary>
    public SemanticProfile GetSemanticProfile()
    {
        var libraryRevision = ModClass.L.SemanticLibrary.Revision;
        if (cachedSemanticProfile == null || cachedSemanticProfileRevision != semanticProfileRevision ||
            cachedSemanticLibraryRevision != libraryRevision)
        {
            cachedSemanticProfile = SemanticContributorService.Build(this);
            cachedSemanticProfileRevision = semanticProfileRevision;
            cachedSemanticLibraryRevision = libraryRevision;
        }
        return cachedSemanticProfile;
    }
}
