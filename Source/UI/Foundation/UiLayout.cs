using System;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI;

/// <summary>集中处理 RectTransform 和 LayoutGroup，不包含任何玩法含义。</summary>
internal static class UiLayout
{
    public static GameObject Create(Transform parent, string name, bool horizontal, float width, float height,
        float spacing = 3f, TextAnchor? alignment = null)
    {
        Type layoutType = horizontal ? typeof(HorizontalLayoutGroup) : typeof(VerticalLayoutGroup);
        GameObject obj = new(name, typeof(RectTransform), layoutType, typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        SetSize(obj.transform, width, height);

        if (horizontal)
        {
            HorizontalLayoutGroup layout = obj.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment ?? TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }
        else
        {
            VerticalLayoutGroup layout = obj.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment ?? TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }
        return obj;
    }

    public static void SetSize(Transform transform, float width, float height)
    {
        RectTransform rect = transform.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);
        LayoutElement layout = transform.GetComponent<LayoutElement>() ??
                               transform.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.flexibleWidth = 0f;
        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.flexibleHeight = 0f;
    }

    public static void SetWidth(Transform transform, float width, bool resetHeight = true)
    {
        RectTransform rect = transform.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, resetHeight ? 0f : rect.sizeDelta.y);
        LayoutElement layout = transform.GetComponent<LayoutElement>() ??
                               transform.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.flexibleWidth = 0f;
        if (!resetHeight) return;
        layout.minHeight = -1f;
        layout.preferredHeight = -1f;
        layout.flexibleHeight = -1f;
    }

    public static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float bottom = 0f,
        float top = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    public static void ClearChildren(Transform parent, bool immediate = false,
        Func<Transform, bool> preserve = null)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (preserve != null && preserve(child)) continue;
            child.gameObject.SetActive(false);
            if (immediate) Object.DestroyImmediate(child.gameObject);
            else Object.Destroy(child.gameObject);
        }
    }
}
