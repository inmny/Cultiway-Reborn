using System;
using UnityEngine;

namespace Cultiway.Utils.Extension;

public static class GameObjectTools
{
    public static GameObject NewChild(this GameObject parent, string name, params Type[] types)
    {
        var go = new GameObject(name, types);
        go.transform.SetParent(parent.transform);
        go.transform.localScale = Vector3.one;
        go.transform.localPosition = Vector3.zero;
        return go;
    }

    public static void SetActiveIfPresent(this Component component, bool active)
    {
        if (component == null) return;
        component.gameObject.SetActive(active);
    }

    public static void HideChildByPath(this Transform root, string path)
    {
        Transform child = root.Find(path);
        if (child == null) return;
        child.gameObject.SetActive(false);
    }

    public static void HideChildrenByPath(this Transform root, params string[] paths)
    {
        for (int i = 0; i < paths.Length; i++)
        {
            root.HideChildByPath(paths[i]);
        }
    }

    public static void SetDescendantsActiveByName(this Component root, bool active, params string[] names)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];
            if (!HasName(current.name, names)) continue;

            current.gameObject.SetActive(active);
        }
    }

    public static bool HasAncestorWithAnyComponent(this Transform child, params Type[] componentTypes)
    {
        Transform current = child.parent;
        while (current != null)
        {
            for (int i = 0; i < componentTypes.Length; i++)
            {
                if (current.GetComponent(componentTypes[i]) != null)
                {
                    return true;
                }
            }

            current = current.parent;
        }

        return false;
    }

    private static bool HasName(string value, string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            if (value == names[i]) return true;
        }

        return false;
    }
}
