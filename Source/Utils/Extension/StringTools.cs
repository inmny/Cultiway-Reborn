using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine.Pool;

namespace Cultiway.Utils.Extension;

public static class StringTools
{
    private static StringBuilder sb = new();

    [MethodImpl(MethodImplOptions.Synchronized)]
    public static string LeaveDigit(this string a)
    {
        sb.Clear();
        for (int i = 0; i < a.Length; i++)
        {
            if (char.IsDigit(a, i)) sb.Append(a[i]);
        }

        return sb.ToString();
    }
    /// <summary>
    /// 处理 JSON 文本的后处理，去除代码块标记，找到 JSON 的开始和结束，并补齐括号
    /// </summary>
    public static string PostProcessForJSON(this string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        if (!text.StartsWith("{") && !text.StartsWith("["))
        {
            int idx1 = text.IndexOf('{');
            int idx2 = text.IndexOf('[');
            
            if (idx1 != -1 && idx2 != -1)
            {
                text = text.Substring(Math.Min(idx1, idx2));
            }
            else if (idx1 != -1)
            {
                text = text.Substring(idx1);
            }
            else if (idx2 != -1)
            {
                text = text.Substring(idx2);
            }
        }

        // 如果结尾不是 } 或 ]，找到最后一个 } 或 ]
        if (!text.EndsWith("}") && !text.EndsWith("]"))
        {
            int idx1 = text.LastIndexOf('}');
            int idx2 = text.LastIndexOf(']');
            
            if (idx1 != -1 && idx2 != -1)
            {
                text = text.Substring(0, Math.Max(idx1, idx2) + 1);
            }
            else if (idx1 != -1)
            {
                text = text.Substring(0, idx1 + 1);
            }
            else if (idx2 != -1)
            {
                text = text.Substring(0, idx2 + 1);
            }
        }

        var stack = new Stack<char>();
        int cutIndex = text.Length;
        
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '[' || c == '{')
            {
                stack.Push(c);
            }
            else if (c == ']' || c == '}')
            {
                if (stack.Count == 0)
                {
                    cutIndex = i;
                    break;
                }
                else
                {
                    stack.Pop();
                }
            }
        }
        
        text = text.Substring(0, cutIndex);
        
        while (stack.Count > 0)
        {
            char leavePar = stack.Pop();
            if (leavePar == '[')
            {
                text += "]";
            }
            else if (leavePar == '{')
            {
                text += "}";
            }
        }

        return text;
    }
    public static int ToInt(this string a)
    {
        return int.Parse(a);
    }
}