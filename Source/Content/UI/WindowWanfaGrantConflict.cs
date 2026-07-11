using System.Collections.Generic;
using Cultiway.Content.WanfaPavilion;
using NeoModLoader.General.UI.Window;
using NeoModLoader.api;
using Cultiway.Abstract;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI;

public sealed class WindowWanfaGrantConflict : AbstractWindow<WindowWanfaGrantConflict>
{
    public const string Id = "Cultiway.UI.WindowWanfaGrantConflict";
    private static readonly Queue<WanfaGrantConflictRequest> Requests = new();
    private static WindowWanfaGrantConflict _instance;
    private WanfaGrantConflictRequest _current;
    private Text _message;

    internal static void Enqueue(WanfaGrantConflictRequest request)
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
        WanfaUiFactory.CreateButton(actions.transform, "Keep", "Cultiway.Wanfa.UI.Action.KeepOld".Localize(),
            58f, 24f, () => Resolve(false));
        WanfaUiFactory.CreateButton(actions.transform, "Overwrite",
            "Cultiway.Wanfa.UI.Action.OverwriteOld".Localize(), 58f, 24f, () => Resolve(true));
        WanfaUiFactory.CreateButton(actions.transform, "Cancel", "Cultiway.Wanfa.UI.Action.Cancel".Localize(),
            58f, 24f, () => Resolve(false));
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
            _current.Payload.Revision).Replace("\\n", "\n");
    }

    private void Resolve(bool overwrite)
    {
        WanfaDropExportSession.ResolveConflict(_current, overwrite);
        _current = null;
        GetComponent<ScrollWindow>().clickHide();
        if (Requests.Count > 0) ScrollWindow.showWindow(Id);
    }
}
