using System;
using System.Collections.Generic;
using Cultiway.Utils.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

internal sealed class ControlledCultivatorPossessionUi : MonoBehaviour
{
    private const string RootName = "CultiwayControlledPossessionUi";
    private const string KeyColor = "#F3961F";
    private const string HintColor = "#A8A8A8";
    private const int HintFontSize = 10;
    private const float RowHeight = 14f;
    private const float HintLineHeight = 14f;

    private static readonly List<RegisteredAction> RegisteredActions = new();
    private static ControlledCultivatorPossessionUi _instance;
    private static bool _structureDirty;

    private readonly Dictionary<string, PossessionTipRow> _rows = new();
    private PossessionUI _boundUi;
    private RectTransform _listRect;
    private Transform _rowsParent;
    private Text _rowTemplate;
    private float _baseListHeight;
    private float _visibleRowsHeight = -1f;

    internal static void Register(string id, Func<bool> visible, Func<string> labelKey, Func<string> hotkeyText,
        Func<string> hintKey = null)
    {
        if (string.IsNullOrEmpty(id)) return;

        var action = new RegisteredAction(id, visible, labelKey, hotkeyText, hintKey);
        var index = RegisteredActions.FindIndex(x => x.Id == id);
        if (index >= 0)
        {
            RegisteredActions[index] = action;
        }
        else
        {
            RegisteredActions.Add(action);
        }

        _structureDirty = true;
        Ensure();
    }

    private static void Ensure()
    {
        if (_instance != null) return;

        var root = new GameObject(RootName, typeof(RectTransform), typeof(ControlledCultivatorPossessionUi));
        var parent = CanvasMain.instance?.canvas_ui?.transform;
        if (parent != null)
        {
            root.transform.SetParent(parent, false);
        }
    }

    private void Awake()
    {
        _instance = this;
    }

    private void Update()
    {
        if (!EnsureBound()) return;

        var visibleRowsHeight = 0f;
        if (_structureDirty)
        {
            RefreshRowOrder();
            _structureDirty = false;
        }

        var visibilityChanged = false;
        foreach (var action in RegisteredActions)
        {
            var row = EnsureRow(action);
            var visible = action.IsVisible();
            visibilityChanged |= row.SetVisible(visible);
            if (!visible) continue;

            var rowHeight = action.GetRowHeight();
            visibleRowsHeight += rowHeight;
            visibilityChanged |= row.SetHeight(rowHeight);
            row.SetText(action.GetLabelKey(), action.GetHotkeyText(), action.GetHintKey());
        }

        if (Mathf.Approximately(_visibleRowsHeight, visibleRowsHeight) && !visibilityChanged) return;

        _visibleRowsHeight = visibleRowsHeight;
        _listRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _baseListHeight + visibleRowsHeight);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_listRect);
    }

    private bool EnsureBound()
    {
        if (PossessionUI.instance == null)
        {
            HideRows();
            return false;
        }

        if (_boundUi == PossessionUI.instance && _listRect != null && _rowsParent != null && _rowTemplate != null)
        {
            return true;
        }

        Bind(PossessionUI.instance);
        return _listRect != null && _rowsParent != null && _rowTemplate != null;
    }

    private void Bind(PossessionUI ui)
    {
        _boundUi = ui;
        _rows.Clear();

        var attack = ui.transform.FindRecursive("attack");
        if (attack == null)
        {
            ModClass.LogWarning($"[{nameof(ControlledCultivatorPossessionUi)}] PossessionUI attack text not found");
            return;
        }

        _rowsParent = attack.parent;
        _rowTemplate = attack.GetComponent<Text>();
        _listRect = _rowsParent.GetComponent<RectTransform>();
        _visibleRowsHeight = -1f;

        var existingRowsHeight = 0f;
        foreach (Transform child in _rowsParent)
        {
            var action = RegisteredActions.Find(x => x.RowName == child.name);
            if (action != null)
            {
                existingRowsHeight += action.GetRowHeight();
            }
        }

        var rawHeight = _listRect.rect.height > 0f ? _listRect.rect.height : _listRect.sizeDelta.y;
        _baseListHeight = Mathf.Max(0f, rawHeight - existingRowsHeight);
        _structureDirty = true;
    }

    private void RefreshRowOrder()
    {
        if (_rowsParent == null || _rowTemplate == null) return;

        var separator = _rowsParent.Find("----");
        var insertIndex = separator == null ? _rowsParent.childCount : separator.GetSiblingIndex();
        foreach (var action in RegisteredActions)
        {
            var row = EnsureRow(action);
            row.SetSiblingIndex(insertIndex++);
        }
    }

    private PossessionTipRow EnsureRow(RegisteredAction action)
    {
        if (_rows.TryGetValue(action.Id, out var row) && row.IsValid)
        {
            return row;
        }

        var existing = _rowsParent.Find(action.RowName);
        var obj = existing == null
            ? Instantiate(_rowTemplate.gameObject, _rowsParent, false)
            : existing.gameObject;
        obj.name = action.RowName;
        obj.SetActive(false);

        var text = obj.GetComponent<Text>();
        text.raycastTarget = false;
        text.supportRichText = true;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        row = new PossessionTipRow(obj, text);
        _rows[action.Id] = row;
        return row;
    }

    private void HideRows()
    {
        foreach (var row in _rows.Values)
        {
            row.SetVisible(false);
        }

        if (_listRect != null && !Mathf.Approximately(_visibleRowsHeight, 0f))
        {
            _visibleRowsHeight = 0f;
            _listRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _baseListHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_listRect);
        }
    }

    private static string BuildText(string labelKey, string key, string hintKey)
    {
        var label = Toolbox.coloredString(LMTools.GetOrFallback(labelKey, labelKey), "white");
        var hotkey = Toolbox.coloredString(key, KeyColor);
        if (string.IsNullOrEmpty(hintKey)) return $"{label} --> [ {hotkey} ]";

        var hint = Toolbox.coloredString(LMTools.GetOrFallback(hintKey, hintKey), HintColor);
        return $"{label} --> [ {hotkey} ]\n<size={HintFontSize}>{hint}</size>";
    }

    private sealed class RegisteredAction
    {
        private readonly Func<bool> _visible;
        private readonly Func<string> _labelKey;
        private readonly Func<string> _hotkeyText;
        private readonly Func<string> _hintKey;

        public string Id { get; }
        public string RowName => $"cultiway_possession_{Id}";

        public RegisteredAction(string id, Func<bool> visible, Func<string> labelKey, Func<string> hotkeyText,
            Func<string> hintKey)
        {
            Id = id;
            _visible = visible;
            _labelKey = labelKey;
            _hotkeyText = hotkeyText;
            _hintKey = hintKey;
        }

        public bool IsVisible()
        {
            return _visible?.Invoke() ?? false;
        }

        public string GetLabelKey()
        {
            return _labelKey?.Invoke() ?? Id;
        }

        public string GetHotkeyText()
        {
            var text = _hotkeyText?.Invoke();
            return string.IsNullOrEmpty(text) ? "?" : text;
        }

        public string GetHintKey()
        {
            return _hintKey?.Invoke();
        }

        public float GetRowHeight()
        {
            return string.IsNullOrEmpty(GetHintKey()) ? RowHeight : RowHeight + HintLineHeight;
        }
    }

    private readonly struct PossessionTipRow
    {
        private readonly GameObject _root;
        private readonly Text _text;

        public bool IsValid => _root != null && _text != null;

        public PossessionTipRow(GameObject root, Text text)
        {
            _root = root;
            _text = text;
        }

        public void SetSiblingIndex(int index)
        {
            if (!IsValid) return;
            _root.transform.SetSiblingIndex(index);
        }

        public bool SetVisible(bool visible)
        {
            if (!IsValid || _root.activeSelf == visible) return false;
            _root.SetActive(visible);
            return true;
        }

        public void SetText(string labelKey, string key, string hintKey)
        {
            if (!IsValid) return;
            _text.text = BuildText(labelKey, key, hintKey);
        }

        public bool SetHeight(float height)
        {
            if (!IsValid) return false;

            var changed = false;
            var rect = _root.GetComponent<RectTransform>();
            if (rect != null && !Mathf.Approximately(rect.rect.height, height))
            {
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                changed = true;
            }

            var layout = _root.GetComponent<LayoutElement>() ?? _root.AddComponent<LayoutElement>();
            if (!Mathf.Approximately(layout.preferredHeight, height))
            {
                layout.minHeight = height;
                layout.preferredHeight = height;
                layout.flexibleHeight = 0f;
                changed = true;
            }

            return changed;
        }
    }
}
