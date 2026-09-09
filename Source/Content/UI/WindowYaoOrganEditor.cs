using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.CreatureCompositions.Libraries;
using Cultiway.Content.UI.Prefab;
using Cultiway.Content.YaoBeasts;
using Cultiway.Core;
using Cultiway.UI;
using Cultiway.Utils.Extension;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI;

/// <summary>
///     妖兽器官编辑窗口：仿照万法阁的列表加操作样式，
///     按身体位置列出指定形态的器官，支持更换、升降等级与移除。
/// </summary>
public sealed class WindowYaoOrganEditor : AbstractWideWindow<WindowYaoOrganEditor>
{
    /// <summary>窗口注册编号。</summary>
    public const string Id = "Cultiway.UI.WindowYaoOrganEditor";

    /// <summary>窗口整体尺寸。</summary>
    public static readonly Vector2 WindowSize = new(600f, 360f);

    private const float RootHeight = 318f;
    private const float ListHeight = 262f;

    private static WindowYaoOrganEditor _instance;
    private static Actor _pendingActor;

    private Actor _actor;
    private string _selectedFormId;
    private Text _targetLabel;
    private Text _capacityLabel;
    private Button _formButton;
    private MonoObjPool<YaoOrganSlotRow> _rowPool;
    private UiOptionMenu _optionMenu;
    private CanvasGroup _rootCanvas;

    /// <summary>记住待编辑的妖兽并打开窗口。</summary>
    public static void Open(Actor actor)
    {
        _pendingActor = actor;
        ScrollWindow.showWindow(Id);
    }

    /// <summary>清世界时解除妖兽绑定，避免引用已经销毁的角色。</summary>
    internal static void ClearWorldState()
    {
        _pendingActor = null;
        if (_instance == null) return;
        _instance._actor = null;
        _instance._selectedFormId = null;
        if (_instance.gameObject.activeInHierarchy)
            _instance.GetComponent<ScrollWindow>().clickHide();
        _instance.Refresh();
    }

    protected override void Init()
    {
        _instance = this;
        UiWindowContext context = UiWindowContext.Bind(BackgroundTransform);
        var root = UiLayout.Create(BackgroundTransform, "OrganEditorRoot", false, 520f, RootHeight, 4f);
        root.transform.localPosition = new Vector3(0f, -8f);
        _rootCanvas = root.AddComponent<CanvasGroup>();

        var toolbar = UiLayout.Create(root.transform, "Toolbar", true, 520f, 24f, 4f);
        _targetLabel = UiElements.CreateText(toolbar.transform, "Target", string.Empty, 168f, 22f, 7,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        _formButton = UiElements.CreateIconTextButton(toolbar.transform, "Form", UiIcons.Options, string.Empty,
            108f, 22f, ShowFormMenu);
        UiTooltip.Set(_formButton.gameObject, "Cultiway.YaoOrganEditor.Action.Form",
            "Cultiway.YaoOrganEditor.Tooltip.Form");
        _capacityLabel = UiElements.CreateText(toolbar.transform, "Capacity", string.Empty, 120f, 22f, 7,
            TextAnchor.MiddleRight);

        UiScrollPane list = UiScrollPane.CreateVertical(root.transform, "SlotList", 520f, ListHeight);
        list.AttachOriginalScrollbar(context.ScrollbarTemplate);
        list.SetWindowFrame(UiTheme.Current.Metrics.SpacingMd);
        _rowPool = new MonoObjPool<YaoOrganSlotRow>(YaoOrganSlotRow.Prefab, list.Content);
        _optionMenu = new UiOptionMenu(BackgroundTransform, _rootCanvas, context.ScrollbarTemplate,
            new UiOptionMenuConfig
            {
                SearchPlaceholder = "Cultiway.UI.OptionMenu.Placeholder.Search".Localize(),
                EmptyText = "Cultiway.UI.OptionMenu.State.Empty".Localize(),
                CloseText = "Cultiway.UI.OptionMenu.Action.Close".Localize(),
                CloseTooltipTitle = "Cultiway.UI.OptionMenu.Action.Close",
                CloseTooltipDescription = "Cultiway.UI.OptionMenu.Tooltip.Close",
            });
    }

    public override void OnNormalEnable()
    {
        if (_pendingActor != null)
        {
            _actor = _pendingActor;
            _pendingActor = null;
            _selectedFormId = null;
        }

        Refresh();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>按当前妖兽与选中形态重建整个列表。</summary>
    private void Refresh()
    {
        if (_rowPool == null) return;
        _rowPool.Clear();
        if (!TryReadEditingForm(out YaoBody body, out YaoFormRecord form, out ActorExtend extend))
        {
            _targetLabel.text = string.Empty;
            UiElements.SetButtonLabel(_formButton, string.Empty);
            _capacityLabel.text = string.Empty;
            return;
        }

        ref Yao yao = ref extend.GetCultisys<Yao>();
        _targetLabel.text = string.Format("Cultiway.YaoOrganEditor.Format.Target".Localize(),
            _actor.getName(), Cultisyses.Yao.GetLevelName(yao.CurrLevel));
        UiElements.SetButtonLabel(_formButton, BuildFormLabel(body, form));
        _capacityLabel.text = string.Format("Cultiway.YaoOrganEditor.Format.Capacity".Localize(),
            GetUsedComplexity(form), GetComplexityCapacity(form));

        // 槽位顺序按身体方案声明；每个位置先列出已有器官，容量还有余量时补一个空位行。
        CreatureBodyPlanAsset bodyPlan = Content.Libraries.Manager.CreatureBodyPlanLibrary.get(form.BodyPlanId);
        if (bodyPlan == null) return;
        YaoOrganRecord[] organs = form.Organs ?? Array.Empty<YaoOrganRecord>();
        foreach (string slotId in bodyPlan.SlotIds)
        {
            CreatureBodySlotAsset slot = Content.Libraries.Manager.CreatureBodySlotLibrary.get(slotId);
            int capacity = slot != null ? Mathf.Max(1, slot.Capacity) : 1;
            string slotName = $"Cultiway.CreatureBodySlot.{slotId}".Localize();
            int usedOnSlot = 0;
            for (int i = 0; i < organs.Length; i++)
            {
                if (!string.Equals(organs[i].SlotId, slotId, StringComparison.Ordinal)) continue;
                usedOnSlot++;
                SetupOrganRow(slotName, form.FormId, organs, i);
            }

            if (usedOnSlot < capacity) SetupEmptyRow(slotName, form.FormId, slotId);
        }
    }

    /// <summary>读取当前妖兽身上要编辑的形态；妖兽失效或数据缺失时返回假。</summary>
    private bool TryReadEditingForm(out YaoBody body, out YaoFormRecord form, out ActorExtend extend)
    {
        body = default;
        form = default;
        extend = null;
        if (_actor == null || _actor.isRekt()) return false;
        extend = _actor.GetExtend();
        if (!extend.HasCultisys<Yao>() || !extend.E.TryGetComponent(out body) ||
            body.Forms == null || body.Forms.Length == 0) return false;

        if (string.IsNullOrEmpty(_selectedFormId) || !body.TryGetForm(_selectedFormId, out form))
        {
            _selectedFormId = body.ActiveFormId;
            if (!body.TryGetForm(_selectedFormId, out form)) return false;
        }

        return true;
    }

    private void SetupOrganRow(string slotName, string formId, YaoOrganRecord[] organs, int organIndex)
    {
        YaoOrganRecord organ = organs[organIndex];
        CreatureOrganAsset asset = Content.Libraries.Manager.CreatureOrganLibrary.get(organ.OrganId);
        string organName = asset != null
            ? $"Cultiway.CreatureOrgan.{organ.OrganId}".Localize()
            : organ.OrganId;
        CreatureOrganRankAsset rank = ResolveRank(organ.OrganId, organ.Rank);
        string detail = string.Format("Cultiway.YaoOrganEditor.Format.Detail".Localize(),
            organ.Rank, $"Cultiway.YaoOrganEditor.Origin.{organ.Origin}".Localize(),
            rank != null ? rank.ComplexityCost : 0);
        _rowPool.GetNext().Setup(
            slotName, organName, detail, YaoOrganIcons.ResolvePath(asset), true,
            asset != null && GetNeighborRank(asset, organ.Rank, -1) != organ.Rank,
            asset != null && GetNeighborRank(asset, organ.Rank, 1) != organ.Rank,
            () => ShowOrganPicker(formId, organ.SlotId, organIndex),
            () => ChangeRank(formId, organIndex, -1),
            () => ChangeRank(formId, organIndex, 1),
            () => RemoveOrgan(formId, organIndex));
    }

    private void SetupEmptyRow(string slotName, string formId, string slotId)
    {
        _rowPool.GetNext().Setup(slotName, "Cultiway.YaoOrganEditor.Slot.Empty".Localize(), string.Empty,
            null, false, false, false,
            () => ShowOrganPicker(formId, slotId, -1),
            null, null, null);
    }

    /// <summary>弹出身体位置可用的器官清单；index 指向要替换的器官，负数表示新增空位。</summary>
    private void ShowOrganPicker(string formId, string slotId, int index)
    {
        if (!TryReadEditingForm(out _, out YaoFormRecord form, out _)) return;

        // 前置与冲突按假设写入后的器官集判断；替换时先拿掉目标行上的旧器官。
        var effective = new List<YaoOrganRecord>(form.Organs ?? Array.Empty<YaoOrganRecord>());
        if (index >= 0 && index < effective.Count) effective.RemoveAt(index);

        var candidates = new List<CreatureOrganAsset>();
        foreach (CreatureOrganAsset organ in Content.Libraries.Manager.CreatureOrganLibrary.list)
        {
            if (ContainsSlotOrgan(effective, slotId, organ.id)) continue;
            if (!YaoFormPlanService.IsOrganAllowedInForm(organ.id, slotId, form, effective.ToArray())) continue;
            candidates.Add(organ);
        }

        candidates.Sort((a, b) => string.CompareOrdinal(
            GetOrganName(a.id), GetOrganName(b.id)));

        var options = new List<UiOptionMenuOption>();
        foreach (CreatureOrganAsset organ in candidates)
        {
            CreatureOrganRankAsset lowest = GetLowestRankAsset(organ);
            string label = lowest != null
                ? $"{GetOrganName(organ.id)}（{lowest.Rank}级 耗{lowest.ComplexityCost}）"
                : GetOrganName(organ.id);
            string organId = organ.id;
            options.Add(new UiOptionMenuOption
            {
                Label = label,
                IconPath = YaoOrganIcons.ResolvePath(organ) ?? UiIcons.Edit,
                SearchText = $"{label} {organId}",
                Select = () => ApplyOrganChoice(formId, slotId, index, organId),
            });
        }

        _optionMenu.Show("Cultiway.YaoOrganEditor.Pick.Title".Localize(), options, true);
    }

    /// <summary>把选中的器官写入目标行：替换已有记录或追加到身体总表。</summary>
    private void ApplyOrganChoice(string formId, string slotId, int index, string organId)
    {
        MutateForm(formId, organs =>
        {
            CreatureOrganRankAsset lowest = GetLowestRankAsset(
                Content.Libraries.Manager.CreatureOrganLibrary.get(organId));
            if (lowest == null) return null;
            var record = new YaoOrganRecord
            {
                SlotId = slotId,
                OrganId = organId,
                Rank = lowest.Rank,
                Origin = YaoOrganOrigin.GodEdited,
            };
            if (index >= 0 && index < organs.Length)
            {
                var updated = (YaoOrganRecord[])organs.Clone();
                updated[index] = record;
                return updated;
            }

            var appended = new YaoOrganRecord[organs.Length + 1];
            Array.Copy(organs, appended, organs.Length);
            appended[^1] = record;
            return appended;
        });
    }

    /// <summary>把目标行器官移到器官声明的上一个或下一个可用等级。</summary>
    private void ChangeRank(string formId, int index, int step)
    {
        MutateForm(formId, organs =>
        {
            if (index < 0 || index >= organs.Length) return null;
            CreatureOrganAsset asset =
                Content.Libraries.Manager.CreatureOrganLibrary.get(organs[index].OrganId);
            if (asset == null) return null;
            int target = GetNeighborRank(asset, organs[index].Rank, step);
            if (target == organs[index].Rank) return null;

            var updated = (YaoOrganRecord[])organs.Clone();
            YaoOrganRecord record = updated[index];
            record.Rank = target;
            record.Origin = YaoOrganOrigin.GodEdited;
            updated[index] = record;
            return updated;
        });
    }

    /// <summary>把目标行器官从身体总表移除；必填位置由编译规则兜底拒绝。</summary>
    private void RemoveOrgan(string formId, int index)
    {
        MutateForm(formId, organs =>
        {
            if (index < 0 || index >= organs.Length) return null;
            var updated = new YaoOrganRecord[organs.Length - 1];
            int j = 0;
            for (int i = 0; i < organs.Length; i++)
            {
                if (i == index) continue;
                updated[j++] = organs[i];
            }

            return updated;
        });
    }

    /// <summary>
    ///     编辑动作的统一入口：在最新身体总表上套用一次修改，
    ///     写入与重新表达交给身体服务，被规则拒绝时提示玩家。
    /// </summary>
    private void MutateForm(string formId, Func<YaoOrganRecord[], YaoOrganRecord[]> mutate)
    {
        if (_actor == null || _actor.isRekt()) return;
        ActorExtend extend = _actor.GetExtend();
        if (!extend.E.TryGetComponent(out YaoBody body) || !body.TryGetForm(formId, out YaoFormRecord form)) return;

        YaoOrganRecord[] updated = mutate(form.Organs ?? Array.Empty<YaoOrganRecord>());
        if (updated == null) return;
        if (!YaoFormPlanService.TrySetOrgans(extend, formId, updated))
        {
            WorldTip.showNow("Cultiway.YaoOrganEditor.Tip.Rejected".Localize(), false, "top", 3f);
            return;
        }

        Refresh();
    }

    private void ShowFormMenu()
    {
        if (_actor == null || _actor.isRekt()) return;
        ActorExtend extend = _actor.GetExtend();
        if (!extend.E.TryGetComponent(out YaoBody body) || body.Forms == null) return;

        var options = new List<UiOptionMenuOption>();
        foreach (YaoFormRecord record in body.Forms)
        {
            string formId = record.FormId;
            options.Add(new UiOptionMenuOption
            {
                Label = BuildFormLabel(body, record),
                IconPath = UiIcons.Options,
                Selected = string.Equals(_selectedFormId, formId, StringComparison.Ordinal),
                Select = () =>
                {
                    _selectedFormId = formId;
                    Refresh();
                },
            });
        }

        _optionMenu.Show("Cultiway.YaoOrganEditor.Action.Form".Localize(), options);
    }

    /// <summary>形态按钮与选项共用的显示文本；活动形态追加标记。</summary>
    private static string BuildFormLabel(YaoBody body, YaoFormRecord form)
    {
        string kindName = $"Cultiway.YaoOrganEditor.Form.{form.Kind}".Localize();
        bool active = string.Equals(body.ActiveFormId, form.FormId, StringComparison.Ordinal);
        return active ? kindName + "Cultiway.YaoOrganEditor.Form.ActiveMark".Localize() : kindName;
    }

    private static string GetOrganName(string organId)
    {
        return $"Cultiway.CreatureOrgan.{organId}".Localize();
    }

    private static bool ContainsSlotOrgan(List<YaoOrganRecord> organs, string slotId, string organId)
    {
        foreach (YaoOrganRecord organ in organs)
        {
            if (string.Equals(organ.SlotId, slotId, StringComparison.Ordinal) &&
                string.Equals(organ.OrganId, organId, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    /// <summary>器官声明中不低于当前等级的相邻等级；没有相邻等级时保持原值。</summary>
    private static int GetNeighborRank(CreatureOrganAsset organ, int currentRank, int step)
    {
        var ranks = new List<int>();
        foreach (string rankId in organ.RankIds ?? Array.Empty<string>())
        {
            CreatureOrganRankAsset rank = Content.Libraries.Manager.CreatureOrganRankLibrary.get(rankId);
            if (rank == null || ranks.Contains(rank.Rank)) continue;
            ranks.Add(rank.Rank);
        }

        ranks.Sort();
        int index = ranks.IndexOf(currentRank);
        if (index < 0) return currentRank;
        int neighbor = index + step;
        return neighbor >= 0 && neighbor < ranks.Count ? ranks[neighbor] : currentRank;
    }

    private static CreatureOrganRankAsset GetLowestRankAsset(CreatureOrganAsset organ)
    {
        if (organ == null) return null;
        CreatureOrganRankAsset lowest = null;
        foreach (string rankId in organ.RankIds ?? Array.Empty<string>())
        {
            CreatureOrganRankAsset rank = Content.Libraries.Manager.CreatureOrganRankLibrary.get(rankId);
            if (rank == null) continue;
            if (lowest == null || rank.Rank < lowest.Rank) lowest = rank;
        }

        return lowest;
    }

    private static CreatureOrganRankAsset ResolveRank(string organId, int rank)
    {
        CreatureOrganAsset asset = Content.Libraries.Manager.CreatureOrganLibrary.get(organId);
        if (asset == null) return null;
        foreach (string rankId in asset.RankIds ?? Array.Empty<string>())
        {
            CreatureOrganRankAsset candidate = Content.Libraries.Manager.CreatureOrganRankLibrary.get(rankId);
            if (candidate != null && candidate.Rank == rank) return candidate;
        }

        return null;
    }

    private static int GetUsedComplexity(YaoFormRecord form)
    {
        int used = 0;
        foreach (YaoOrganRecord organ in form.Organs ?? Array.Empty<YaoOrganRecord>())
        {
            CreatureOrganRankAsset rank = ResolveRank(organ.OrganId, organ.Rank);
            if (rank != null) used += rank.ComplexityCost;
        }

        return used;
    }

    private static int GetComplexityCapacity(YaoFormRecord form)
    {
        CreatureBodyPlanAsset bodyPlan =
            Content.Libraries.Manager.CreatureBodyPlanLibrary.get(form.BodyPlanId);
        CreatureMorphAsset morph = Content.Libraries.Manager.CreatureMorphLibrary.get(form.MorphId);
        if (bodyPlan == null || morph == null) return 0;
        return bodyPlan.BaseComplexityCapacity + morph.BaseComplexityModifier;
    }
}
