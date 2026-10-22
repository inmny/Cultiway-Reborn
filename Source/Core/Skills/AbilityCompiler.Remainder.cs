using System;
using System.Collections.Generic;

namespace Cultiway.Core.Skills;

/// <summary>结束后交接继续沿用原接触组和真实来源，不建立新的初始威力。</summary>
public static partial class AbilityCompiler
{
    /// <summary>在关系已经通过无环检查后，建立有限接收列表并统一接触组。</summary>
    /// <param name="nodes">已经完成来源解析的全部部位。</param>
    private static void LinkRemainders(List<CompiledForm> nodes)
    {
        var receivers = new List<int>[nodes.Count];
        var members = new List<int>[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Activation?.InheritRemainder == true)
            {
                nodes[i].RemainderRoot = i;
                (receivers[nodes[i].Activation.Source] ??= new List<int>()).Add(i);
            }
            else if (nodes[i].Parent >= 0) nodes[i].RemainderRoot = nodes[nodes[i].Parent].RemainderRoot;
            if (nodes[i].RemainderRoot >= 0 && nodes[i].FragmentSource < 0)
                (members[nodes[i].RemainderRoot] ??= new List<int>()).Add(i);
        }
        var linked = new bool[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
        {
            nodes[i].RemainderReceivers = receivers[i]?.ToArray() ?? Array.Empty<int>();
            nodes[i].RemainderMembers = members[i]?.ToArray() ?? Array.Empty<int>();
            LinkContact(i);
        }

        // 后段还可碎解或再次交接；所有这些实际后代延续同一命中记录。
        void LinkContact(int index)
        {
            if (linked[index]) return;
            linked[index] = true;
            CompiledForm node = nodes[index];
            int source = node.FragmentSource >= 0 ? node.FragmentSource : node.RemainderRoot < 0 ? -1 :
                node.RemainderRoot == index ? node.Activation.Source : node.RemainderRoot;
            if (source < 0) return;
            LinkContact(source);
            node.ContactKey = nodes[source].ContactKey;
            node.ContactPolicy ??= nodes[source].ContactPolicy;
        }
    }
}
