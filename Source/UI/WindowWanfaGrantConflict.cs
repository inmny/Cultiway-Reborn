using System.Collections.Generic;
using Cultiway.Core.SkillLibV3.Wanfa;
using NeoModLoader.General.UI.Window;
using NeoModLoader.api;
using Cultiway.Abstract;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

public sealed class WindowWanfaGrantConflict : AbstractWindow<WindowWanfaGrantConflict>
{
    public const string Id = "Cultiway.UI.WindowWanfaGrantConflict";
    private static readonly Queue<WanfaGrantConflictPrompt> Requests = new();
    private static WindowWanfaGrantConflict _instance;
    private WanfaGrantConflictPrompt _current;
    private Text _message;

    internal static void Enqueue(WanfaGrantConflictPrompt request)
    {
        Requests.Enqueue(request);
        ScrollWindow.showWindow(Id);
    }

    internal static void ClearPending()
    {
        Requests.Clear();
        if (_instance == null) return;
        var wasResolving = _instance._current != null;
        _instance._current = null;
        if (wasResolving && _instance.gameObject.activeInHierarchy)
        {
            _instance.GetComponent<ScrollWindow>().clickHide();
        }
    }

    protected override void Init()
    {
        _instance = this;
        var layout = ContentTransform.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        _message = WanfaUiFactory.CreateText(ContentTransform, "Message", string.Empty, 190f, 62f, 8,
            TextAnchor.MiddleCenter);
        var actions = WanfaUiFactory.CreateLayout(ContentTransform, "Actions", true, 190f, 28f, 5f);
        var keep = WanfaUiFactory.CreateIconTextButton(actions.transform, "Keep", WanfaUiIcons.Cancel,
            "Cultiway.Wanfa.UI.Action.KeepOld".Localize(), 58f, 24f, () => Resolve(false));
        WanfaUiFactory.SetTooltip(keep.gameObject, "Cultiway.Wanfa.UI.Action.KeepOld",
            "Cultiway.Wanfa.UI.Tooltip.KeepOld");
        var overwrite = WanfaUiFactory.CreateIconTextButton(actions.transform, "Overwrite", WanfaUiIcons.Confirm,
            "Cultiway.Wanfa.UI.Action.OverwriteOld".Localize(), 64f, 24f, () => Resolve(true));
        WanfaUiFactory.SetTooltip(overwrite.gameObject, "Cultiway.Wanfa.UI.Action.OverwriteOld",
            "Cultiway.Wanfa.UI.Tooltip.OverwriteOld");
        var cancel = WanfaUiFactory.CreateIconButton(actions.transform, "Cancel", WanfaUiIcons.Cancel, 28f, 24f,
            () => Resolve(false));
        WanfaUiFactory.SetTooltip(cancel.gameObject, "Cultiway.Wanfa.UI.Action.Cancel",
            "Cultiway.Wanfa.UI.Tooltip.CancelConflict");
    }

    public override void OnNormalEnable()
    {
        if (Requests.Count == 0)
        {
            GetComponent<ScrollWindow>().clickHide();
            return;
        }
        _current = Requests.Dequeue();
        _message.text = string.Format("Cultiway.Wanfa.UI.Format.GrantConflict".Localize(), _current.ActorName,
            _current.Revision).Replace("\\n", "\n");
    }

    private void Resolve(bool overwrite)
    {
        _current.Resolve(overwrite);
        _current = null;
        GetComponent<ScrollWindow>().clickHide();
        if (Requests.Count > 0) ScrollWindow.showWindow(Id);
    }
}
