using System;
using Cultiway.Abstract;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.Prefab;

public sealed class WanfaEditorRow : APrefabPreview<WanfaEditorRow>
{
    public Transform Controls { get; private set; }
    private Text _title;
    private Text _detail;
    private Button _action;

    protected override void OnInit()
    {
        _title = transform.Find("Header/Title").GetComponent<Text>();
        _detail = transform.Find("Header/Detail").GetComponent<Text>();
        _action = transform.Find("Header/Action").GetComponent<Button>();
        Controls = transform.Find("Controls");
    }

    public void Setup(string title, string detail, string actionLabel, bool interactable, Action action)
    {
        Init();
        ClearControls();
        _title.text = title;
        _detail.text = detail;
        _action.GetComponentInChildren<Text>().text = actionLabel;
        _action.gameObject.SetActive(action != null);
        _action.interactable = interactable;
        _action.onClick.RemoveAllListeners();
        if (action != null) _action.onClick.AddListener(action.Invoke);
        SetHeight(32f);
    }

    public void SetHeight(float height)
    {
        WanfaUiFactory.SetLayout(transform, 500f, height);
    }

    public void ClearControls()
    {
        for (var i = Controls.childCount - 1; i >= 0; i--)
        {
            var child = Controls.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private static void _init()
    {
        var obj = WanfaUiFactory.CreateLayout(ModClass.I.PrefabLibrary, nameof(WanfaEditorRow), false, 500f, 32f, 2f);
        var background = obj.AddComponent<Image>();
        background.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        background.type = Image.Type.Sliced;
        var header = WanfaUiFactory.CreateLayout(obj.transform, "Header", true, 500f, 28f, 4f);
        WanfaUiFactory.CreateText(header.transform, "Title", string.Empty, 130f, 28f, 8, TextAnchor.MiddleLeft,
            FontStyle.Bold);
        WanfaUiFactory.CreateText(header.transform, "Detail", string.Empty, 278f, 28f, 6);
        WanfaUiFactory.CreateButton(header.transform, "Action", "Cultiway.Wanfa.UI.Action.Select".Localize(),
            80f, 22f, () => { });
        WanfaUiFactory.CreateLayout(obj.transform, "Controls", false, 500f, 0f, 2f);
        Prefab = obj.AddComponent<WanfaEditorRow>();
    }
}
