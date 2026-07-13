using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Content.Sects;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Debug;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 执事整理宗门藏经阁，为宗门典籍维护提供贡献来源。
/// </summary>
public class BehOrganizeSectScripture : BehaviourActionActor
{
    /// <summary>
    /// 完成一次藏经阁整理，并按事务资产配置发放贡献。
    /// </summary>
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        SectAffairAsset affair = SectAffairs.OrganizeScripture;
        if (!SectAffairExecutionPolicy.CanExecute(pObject, affair))
        {
            SectVerifyLog.Log("SectAffairTask", $"affair={affair?.id ?? "null"} actor={SectVerifyLog.Actor(pObject)} result=false");
            return BehResult.Stop;
        }

        Sect sect = pObject.GetExtend().sect;
        int contribution = SectTraitRules.GetAffairContributionReward(sect, affair);
        bool result = sect.AddContribution(pObject, contribution);
        SectVerifyLog.Log("SectAffairTask", $"affair={affair.id} sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(pObject)} books={sect.GetScriptureBookIds().Count} contribution={contribution} result={result}");
        return result ? BehResult.Continue : BehResult.Stop;
    }
}
