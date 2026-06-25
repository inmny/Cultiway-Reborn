using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Utils.Extension
{
    public static class TransformTools
    {
        public static void DestroyIfPresent(this Transform root, string path)
        {
            Transform child = root.Find(path);
            if (child == null) return;
            UnityEngine.Object.Destroy(child.gameObject);
        }
        public static void CollapseIfPresent(this Transform root, string path)
        {
            Transform child = root.Find(path);
            if (child == null) return;
            child.gameObject.SetActive(false);
            LayoutElement layout = child.GetComponent<LayoutElement>() ?? child.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            layout.minHeight = 0f;
            layout.preferredHeight = 0f;
        }
    }
}