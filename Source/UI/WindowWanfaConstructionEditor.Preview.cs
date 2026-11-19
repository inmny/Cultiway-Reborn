using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cultiway.Content.Skills;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.Visuals.Forms;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

public sealed partial class WindowWanfaConstructionEditor
{
    private UiModal performanceModal;
    private RawImage performanceImage;
    private Text performanceStatus;
    private InputField performanceX, performanceY;
    private Button performanceTarget, performancePause, performanceContacts;
    private long performanceTargetId;
    private bool previewPaused, showContacts;
    private FormPreviewViewport performanceViewport;
    private ConstructedAbilityService.PerformancePreview performance;
    private float? performanceRange;
    private readonly List<FormVisualInstance> performanceVisuals = new();
    private readonly List<AbilityCapsule> performanceShapes = new();

    /// <summary>只在当前草稿内播放；输入不进入草稿历史或正式存档。</summary>
    private void InitPerformancePreview(CanvasGroup canvas)
    {
        var panel = UiWindowFrame.CreateContentSize(BackgroundTransform, "PerformancePreview", 386f, 314f);
        var layout = UiLayout.Create(panel.Content, "PreviewBody", false, 386f, 314f, 4f).transform;
        BindText(UiElements.CreateText(layout, "Title", T("PreviewPerformance"), 378f, 17f, 9), "PreviewPerformance");
        BindText(UiElements.CreateText(layout, "Hint", T("PreviewSimulationHint"), 378f, 27f, 7), "PreviewSimulationHint");
        var inputs = UiLayout.Create(layout, "PreviewInputs", true, 378f, 23f, 4f).transform;
        performanceX = UiElements.CreateInput(inputs, "PreviewX", "", "X", 62f, 23f);
        performanceY = UiElements.CreateInput(inputs, "PreviewY", "", "Y", 62f, 23f);
        performanceTarget = UiElements.CreateButton(inputs, "PreviewTarget", T("PreviewTarget"), 180f, 23f, ChoosePerformanceTarget);
        performanceImage = new GameObject("PreviewImage", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement)).GetComponent<RawImage>();
        performanceImage.transform.SetParent(layout, false);
        UiLayout.SetSize(performanceImage.transform, 378f, 189f);
        performanceImage.raycastTarget = false;
        performanceImage.enabled = false;
        performanceStatus = UiElements.CreateText(layout, "PreviewStatus", "", 378f, 20f, 8);
        var controls = UiLayout.Create(layout, "PreviewControls", true, 378f, 23f, 4f).transform;
        BindText(UiElements.CreateButton(controls, "PreviewReplay", T("PreviewReplay"), 88f, 23f, RestartPerformancePreview).GetComponentInChildren<Text>(), "PreviewReplay");
        performancePause = UiElements.CreateButton(controls, "PreviewPause", T("PreviewPause"), 82f, 23f, () =>
        {
            previewPaused = !previewPaused;
            UiElements.SetButtonLabel(performancePause, T(previewPaused ? "PreviewResume" : "PreviewPause"));
        });
        performanceContacts = UiElements.CreateButton(controls, "PreviewContacts", T("PreviewShowContacts"), 111f, 23f, () =>
        {
            showContacts = !showContacts;
            UiElements.SetButtonLabel(performanceContacts, T(showContacts ? "PreviewHideContacts" : "PreviewShowContacts"));
            DrawPerformancePreview();
        });
        BindText(UiElements.CreateButton(controls, "PreviewClose", T("Close"), 76f, 23f, ClosePerformancePreview).GetComponentInChildren<Text>(), "Close");
        performanceModal = new UiModal(panel.Root.gameObject, canvas);
    }

    private void OpenPerformancePreview()
    {
        if (previewPlan == null) return;
        if (HasLearner)
        {
            Vector2 point = learner.GetSimPos();
            performanceX.SetTextWithoutNotify(point.x.ToString("0.###", CultureInfo.CurrentCulture));
            performanceY.SetTextWithoutNotify(point.y.ToString("0.###", CultureInfo.CurrentCulture));
        }
        performanceTargetId = 0;
        showContacts = false;
        UiElements.SetButtonLabel(performanceContacts, T("PreviewShowContacts"));
        ShowPerformancePreview();
    }

    private void ShowPerformancePreview()
    {
        performanceModal.Show();
        RestartPerformancePreview();
    }

    private void ChoosePerformanceTarget()
    {
        if (!HasLearner) { RestartPerformancePreview(); return; }
        ClosePerformancePreview();
        var options = new List<UiOptionMenuOption>();
        if (previewPlan.Use.Target == ActiveAbilityTargetMode.ObjectOrPoint)
            options.Add(new UiOptionMenuOption { Label = T("PreviewPoint"), Selected = performanceTargetId == 0,
                Select = () => { performanceTargetId = 0; ShowPerformancePreview(); } });
        var owner = learner.GetExtend();
        options.AddRange(World.world.units.getSimpleList().Where(actor =>
            ConstructedAbilityService.PreviewTargetMatches(owner, previewPlan, actor)).Select(actor =>
        {
            long id = actor.id;
            return new UiOptionMenuOption
            {
                Label = actor.getName(), SearchText = actor.getName() + " " + LMTools.GetOrFallback(actor.asset.id, "") + " " + actor.asset.id,
                Selected = id == performanceTargetId,
                Select = () => { performanceTargetId = id; ShowPerformancePreview(); } };
        }));
        menu.Show(T("PreviewTarget"), options.ToArray(), true, "Cultiway.Construction.EmptyPreviewTargets");
    }

    private void RestartPerformancePreview()
    {
        ReleasePerformanceViewport();
        if (previewPlan == null) { performanceStatus.text = T("InvalidSummary"); return; }
        if (!HasLearner)
        {
            performanceX.interactable = performanceY.interactable = performanceTarget.interactable = false;
            performanceStatus.text = T("PreviewNeedsLearner");
            return;
        }
        Actor target = performanceTargetId == 0 ? null : World.world.units.get(performanceTargetId);
        ActiveAbilityTargetMode mode = previewPlan.Use.Target;
        bool coordinates = mode is ActiveAbilityTargetMode.Point or ActiveAbilityTargetMode.ObjectOrPoint && target == null;
        performanceX.interactable = performanceY.interactable = coordinates;
        performanceTarget.interactable = mode is ActiveAbilityTargetMode.Object or ActiveAbilityTargetMode.ObjectOrPoint;
        UiElements.SetButtonLabel(performanceTarget, target == null ? T("PreviewTarget") : target.getName());
        if (performanceTargetId != 0 && (target == null || !target.isAlive() || !target.hasHealth()))
        { performanceStatus.text = T("PreviewTargetGone"); return; }
        var point = Vector3.zero;
        if (coordinates && (!float.TryParse(performanceX.text, NumberStyles.Float, CultureInfo.CurrentCulture, out point.x) ||
            !float.TryParse(performanceY.text, NumberStyles.Float, CultureInfo.CurrentCulture, out point.y)))
        { performanceStatus.text = T("PreviewInvalidCoordinates"); return; }

        string reason = ConstructedAbilityService.TryBuildPreview(learner, previewPlan, target, point, out performance, out performanceRange);
        if (reason != null)
        {
            performanceStatus.text = performanceRange.HasValue ? string.Format(T(reason), performanceRange.Value) : T(reason);
            return;
        }
        performanceViewport = new FormPreviewViewport(performance.Source, performance.Target);
        performanceImage.texture = performanceViewport.Image;
        performanceImage.enabled = true;
        performancePause.interactable = performanceContacts.interactable = true;
        previewPaused = false;
        UiElements.SetButtonLabel(performancePause, T("PreviewPause"));
        DrawPerformancePreview();
    }

    private void Update()
    {
        if (previewPaused || performance == null || performance.Cast.Finished) return;
        performance.Advance(Mathf.Min(.05f, Time.unscaledDeltaTime));
        DrawPerformancePreview();
    }

    private void DrawPerformancePreview()
    {
        if (performanceViewport == null) return;
        performanceVisuals.Clear(); performanceShapes.Clear();
        performance.Sample(performanceVisuals, performanceShapes);
        performanceViewport.Draw(performanceVisuals, performanceShapes, performance.Cast.Age, showContacts);
        performanceStatus.text = string.Format(T("PreviewTime"), performance.Cast.Age, performance.Cast.Plan.Lifetime, performanceRange.Value);
    }

    private void ClosePerformancePreview()
    {
        if (performanceModal?.Panel.activeSelf == true) performanceModal.Hide();
        ReleasePerformanceViewport();
    }

    private void ReleasePerformanceViewport()
    {
        performance = null;
        if (performanceImage != null)
        {
            performanceImage.enabled = false;
            performanceImage.texture = null;
        }
        if (performancePause != null)
        {
            performancePause.interactable = performanceContacts.interactable = false;
            UiElements.SetButtonLabel(performancePause, T("PreviewPause"));
        }
        performanceViewport?.Dispose(); performanceViewport = null;
        performanceVisuals.Clear(); performanceShapes.Clear();
    }
}
