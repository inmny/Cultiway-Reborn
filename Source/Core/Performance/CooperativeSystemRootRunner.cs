using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using HarmonyLib;

namespace Cultiway.Core.Performance;

internal sealed class CooperativeSystemRootRunner
{
    private static readonly FieldInfo TickField = AccessTools.Field(typeof(BaseSystem), "tick");
    private static readonly PropertyInfo CommandBuffersProperty =
        AccessTools.Property(typeof(SystemGroup), "CommandBuffers");
    private static readonly MethodInfo UpdateGroupMethod = AccessTools.Method(typeof(BaseSystem), "OnUpdateGroup");
    private static readonly MethodInfo UpdateGroupBeginMethod =
        AccessTools.Method(typeof(BaseSystem), "OnUpdateGroupBegin");
    private static readonly MethodInfo UpdateGroupEndMethod =
        AccessTools.Method(typeof(BaseSystem), "OnUpdateGroupEnd");

    private readonly Stack<GroupFrame> stack = new();
    private UpdateTick tick;

    public bool Active => stack.Count > 0;

    public void Start(SystemRoot root, UpdateTick updateTick)
    {
        Abort();
        tick = updateTick;
        if (root == null || !root.Enabled)
        {
            return;
        }

        PushGroup(root);
    }

    public string GetNextPhaseName()
    {
        if (stack.Count == 0)
        {
            return "cultiway.root.idle";
        }

        GroupFrame frame = stack.Peek();
        if (frame.ChildIndex >= frame.Children.Length)
        {
            return "cultiway.root.finish." + frame.Group.Name;
        }

        BaseSystem child = frame.Children[frame.ChildIndex];
        if (child is ICooperativeSystemStep cooperative)
        {
            return "cultiway.system." + frame.Group.Name + "." + cooperative.CooperativePhaseName;
        }

        return "cultiway.system." + frame.Group.Name + "." + child.Name;
    }

    public bool Step()
    {
        while (stack.Count > 0)
        {
            GroupFrame frame = stack.Peek();
            if (frame.ChildIndex >= frame.Children.Length)
            {
                FinishGroup(frame);
                stack.Pop();
                return stack.Count == 0;
            }

            BaseSystem child = frame.Children[frame.ChildIndex];
            if (!child.Enabled)
            {
                frame.ChildIndex++;
                continue;
            }

            SetTick(child);
            if (child is ICooperativeSystemStep cooperative)
            {
                if (cooperative.StepCooperatively())
                {
                    frame.ChildIndex++;
                }

                return false;
            }

            frame.ChildIndex++;
            if (child is SystemGroup childGroup)
            {
                PushGroup(childGroup);
            }
            else
            {
                UpdateGroupMethod.Invoke(child, null);
            }

            return false;
        }

        return true;
    }

    public void Abort()
    {
        foreach (GroupFrame frame in stack)
        {
            ClearCommandBuffers(frame.Group);
        }

        stack.Clear();
    }

    private void PushGroup(SystemGroup group)
    {
        SetTick(group);
        ClearCommandBuffers(group);
        BaseSystem[] children = SnapshotChildren(group);
        for (int i = 0; i < children.Length; i++)
        {
            BaseSystem child = children[i];
            if (!child.Enabled)
            {
                continue;
            }

            SetTick(child);
            UpdateGroupBeginMethod.Invoke(child, null);
        }

        stack.Push(new GroupFrame(group, children));
    }

    private void FinishGroup(GroupFrame frame)
    {
        PlayCommandBuffers(frame.Group);
        for (int i = 0; i < frame.Children.Length; i++)
        {
            BaseSystem child = frame.Children[i];
            if (child.Enabled)
            {
                UpdateGroupEndMethod.Invoke(child, null);
            }
        }
    }

    private void SetTick(BaseSystem system)
    {
        TickField.SetValue(system, tick);
    }

    private static BaseSystem[] SnapshotChildren(SystemGroup group)
    {
        var source = group.ChildSystems;
        var result = new BaseSystem[source.Count];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = source[i];
        }

        return result;
    }

    private static void ClearCommandBuffers(SystemGroup group)
    {
        foreach (CommandBuffer commandBuffer in GetCommandBuffers(group))
        {
            commandBuffer.Clear();
        }
    }

    private static void PlayCommandBuffers(SystemGroup group)
    {
        foreach (CommandBuffer commandBuffer in GetCommandBuffers(group))
        {
            commandBuffer.Playback();
        }
    }

    private static IEnumerable<CommandBuffer> GetCommandBuffers(SystemGroup group)
    {
        object value = CommandBuffersProperty.GetValue(group);
        if (value is not IEnumerable enumerable)
        {
            yield break;
        }

        foreach (object item in enumerable)
        {
            if (item is CommandBuffer commandBuffer)
            {
                yield return commandBuffer;
            }
        }
    }

    private sealed class GroupFrame
    {
        public GroupFrame(SystemGroup group, BaseSystem[] children)
        {
            Group = group;
            Children = children;
        }

        public SystemGroup Group { get; }
        public BaseSystem[] Children { get; }
        public int ChildIndex { get; set; }
    }
}
