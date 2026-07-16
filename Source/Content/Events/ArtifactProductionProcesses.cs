using Cultiway.Core;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Events;
/// <summary>法器能力可识别的生产流程标识。扩展内容可以直接使用自己的稳定字符串。</summary>
public static class ArtifactProductionProcesses
{
    public const string Alchemy = "alchemy";
    public const string ArtifactRefining = "artifact_refining";
    public const string TalismanCrafting = "talisman_crafting";
}
