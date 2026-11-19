using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.VillageDuties;
using Cultiway.Content.VillageMilitary;
using Cultiway.Content.KingdomPolicies;
using Cultiway.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.VillageDuties;

/// <summary>按村庄配置职业人数，自动目标按真实劳动力和物资缺口分配。</summary>
public sealed partial class WindowVillageDuties
{
    /// <summary>各职业人数的本地输入草稿。</summary>
    private readonly Dictionary<string, InputField> occupationFields = new();
    /// <summary>各职业当前计划、固定归属和实际在岗人数。</summary>
    private readonly Dictionary<string, Text> occupationCounts = new();
    /// <summary>本村可劳动的普通居民与固定归属总数。</summary>
    private Text occupationSummary;
    /// <summary>实际生效的童工政策及年龄门槛。</summary>
    private Text childLaborSummary;

    /// <summary>职业名使用本功能的中文和英文文本，额外职业保留其原版编号。</summary>
    /// <param name="id">原版普通职业编号。</param>
    internal static string OccupationName(string id)
    {
        if (id == CitizenJobs.HerbCollector.id) return T("Occupation.HerbCollector");
        string text = T("Occupation." + id);
        return text == "Cultiway.Village.Occupation." + id ? id : text;
    }

    /// <summary>职业设置置于独立滚动页，不挤占原有名册和军制页面。</summary>
    private void BuildOccupations()
    {
        var parent = pages[4].transform;
        Text(parent, "OccupationHint", 44);
        occupationSummary = UiElements.CreateText(parent, "Workforce", "", 500, 32, 8, TextAnchor.MiddleLeft);
        childLaborSummary = UiElements.CreateText(parent, "ChildLabor", "", 500, 24, 7, TextAnchor.MiddleLeft);
        Button(parent, "AutomaticOccupations", 220, () =>
        {
            foreach (var input in occupationFields.Values) input.SetTextWithoutNotify("");
            RefreshPreview();
        });
        foreach (var job in VillageOccupationService.Jobs)
        {
            var line = UiLayout.Create(parent, "Occupation_" + job.id, true, 500, 44, UiTheme.Current.Metrics.SpacingSm);
            var name = UiElements.CreateText(line.transform, "Name", OccupationName(job.id), 112, 28, 7, TextAnchor.MiddleLeft);
            translated.Add((name, job == CitizenJobs.HerbCollector ? "Occupation.HerbCollector" : "Occupation." + job.id));
            occupationFields.Add(job.id, Input(line.transform, "Count", 64, "Automatic", true));
            occupationCounts.Add(job.id, UiElements.CreateText(line.transform, "Current", "", 308, 44, 7, TextAnchor.MiddleLeft));
        }
        UiLayout.SetSize(parent, 500, 136 + occupationFields.Count * 48);
    }

    /// <summary>以一次人数统计刷新本页，实际人数不足时显示空缺，不制造虚拟人员。</summary>
    /// <param name="city">设置独立保存的村庄。</param>
    /// <param name="data">本村的已应用设置和职业人员归属。</param>
    private void RefreshOccupations(City city, VillageMilitaryData data)
    {
        var workers = city.units.Where(a => VillageDutyService.IsResident(a) && a.canWork() &&
            VillageDutyService.Classify(a) == VillageResidentKind.Worker).ToList();
        var active = workers.Where(a => a.citizen_job != null).GroupBy(a => a.citizen_job.id).ToDictionary(g => g.Key, g => g.Count());
        var autonomousPeople = city.units.Where(a => VillageDutyService.IsResident(a) && a.citizen_job != null &&
            VillageDutyService.Classify(a) is VillageResidentKind.Special or VillageResidentKind.Manager).ToArray();
        var autonomous = autonomousPeople.GroupBy(a => a.citizen_job.id).ToDictionary(g => g.Key, g => g.Count());
        var assignedPeople = workers.Where(a => data.Occupations.ContainsKey(a.id)).ToArray();
        var assigned = assignedPeople.GroupBy(a => data.Occupations[a.id]).ToDictionary(g => g.Key, g => g.Count());
        var children = workers.Where(a => !a.isAdult()).ToArray();
        var childAssigned = assignedPeople.Where(a => !a.isAdult()).GroupBy(a => data.Occupations[a.id]).ToDictionary(g => g.Key, g => g.Count());
        var childActive = children.Where(a => a.citizen_job != null).GroupBy(a => a.citizen_job.id).ToDictionary(g => g.Key, g => g.Count());
        var childAutonomous = autonomousPeople.Where(a => !a.isAdult() && a.canWork()).GroupBy(a => a.citizen_job.id).ToDictionary(g => g.Key, g => g.Count());
        occupationSummary.text = F("WorkforceSummary", workers.Count, data.Policy.OccupationTargets.Values.Sum(n => (long)n), assignedPeople.Length,
            workers.Count - children.Length, children.Length);
        childLaborSummary.text = KingdomPolicyService.Has(city, KingdomPolicyCard.ChildLabor)
            ? F("ChildLaborEnabled", SimGlobals.m.child_work_age, children.Length) : T("ChildLaborDisabled");
        foreach (var job in VillageOccupationService.Jobs)
        {
            active.TryGetValue(job.id, out int working); assigned.TryGetValue(job.id, out int staff);
            autonomous.TryGetValue(job.id, out int extra);
            occupationCounts[job.id].text = data.Policy.OccupationTargets.TryGetValue(job.id, out int target)
                ? F("FixedOccupation", target, staff, working, extra, VillageOccupationService.TemporaryWorkers(city, job))
                : F("AutomaticOccupation", VillageOccupationService.PlannedWorkers(city, job), staff, working, extra, VillageOccupationService.TemporaryWorkers(city, job));
            childAssigned.TryGetValue(job.id, out int childrenAssigned); childActive.TryGetValue(job.id, out int childrenWorking);
            childAutonomous.TryGetValue(job.id, out int childrenExtra);
            int helpers = children.Count(a => a.citizen_job == job && VillageOccupationService.TemporaryJob(a.id) == job.id);
            occupationCounts[job.id].text += "\n" + F("OccupationChildren", childrenAssigned, childrenWorking, childrenExtra, helpers);
        }
    }
}
