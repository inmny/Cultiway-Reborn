using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.ControlledTasks;

public enum ControlledTaskParameterMode
{
    SingleChoice,
    MultipleChoice,
}

public enum ControlledTaskParameterLayout
{
    List,
    CompactList,
    ItemGrid,
}

public sealed class ControlledTaskParameterDefinition
{
    public string Key { get; }
    public ControlledTaskParameterMode Mode { get; }
    public bool Required { get; }
    public int MinSelected { get; }
    public int MaxSelected { get; }
    public string NameLocaleKey { get; }
    public string DescriptionLocaleKey { get; }
    public ControlledTaskParameterLayout Layout { get; }

    public ControlledTaskParameterDefinition(
        string key,
        ControlledTaskParameterMode mode,
        bool required,
        int minSelected,
        int maxSelected,
        string nameLocaleKey,
        string descriptionLocaleKey,
        ControlledTaskParameterLayout layout = ControlledTaskParameterLayout.List)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("Parameter key is required.", nameof(key));
        if (minSelected < 0) throw new ArgumentOutOfRangeException(nameof(minSelected));
        int normalizedMin = required ? Math.Max(1, minSelected) : minSelected;
        if (maxSelected < normalizedMin) throw new ArgumentOutOfRangeException(nameof(maxSelected));
        if (mode == ControlledTaskParameterMode.SingleChoice && maxSelected > 1)
            throw new ArgumentException("A single-choice parameter cannot accept more than one value.", nameof(maxSelected));

        Key = key;
        Mode = mode;
        Required = required;
        MinSelected = normalizedMin;
        MaxSelected = maxSelected;
        NameLocaleKey = nameLocaleKey ?? string.Empty;
        DescriptionLocaleKey = descriptionLocaleKey ?? string.Empty;
        Layout = layout;
    }
}

public sealed class ControlledTaskOption
{
    public string Key { get; }
    public string Label { get; }
    public string Summary { get; }
    public string IconPath { get; }
    public Sprite IconSprite { get; }
    public int SpecialItemId { get; }
    public string SearchText { get; }
    public bool Enabled { get; }
    public string ReasonLocaleKey { get; }

    public ControlledTaskOption(
        string key,
        string label,
        string summary = null,
        string iconPath = null,
        string searchText = null,
        bool enabled = true,
        string reasonLocaleKey = null,
        Sprite iconSprite = null,
        int specialItemId = 0)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("Option key is required.", nameof(key));
        Key = key;
        Label = label ?? string.Empty;
        Summary = summary ?? string.Empty;
        IconPath = iconPath ?? string.Empty;
        IconSprite = iconSprite;
        SpecialItemId = specialItemId;
        SearchText = string.IsNullOrEmpty(searchText) ? Label : searchText;
        Enabled = enabled;
        ReasonLocaleKey = reasonLocaleKey ?? string.Empty;
    }

    public static ControlledTaskOption Disabled(string key, string label, string reasonLocaleKey,
        string summary = null, string iconPath = null)
    {
        return new ControlledTaskOption(key, label, summary, iconPath, null, false, reasonLocaleKey);
    }
}

public readonly struct ControlledTaskInvocation
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EmptyParameters =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    public static ControlledTaskInvocation Empty => new(ControlledTaskTarget.None, EmptyParameters);

    public ControlledTaskTarget Target { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Parameters { get; }

    public ControlledTaskInvocation(
        ControlledTaskTarget target,
        IReadOnlyDictionary<string, IReadOnlyList<string>> parameters)
    {
        Target = target;
        Parameters = parameters ?? EmptyParameters;
    }

    public IReadOnlyList<string> GetSelections(string key)
    {
        if (string.IsNullOrEmpty(key) || Parameters == null || !Parameters.TryGetValue(key, out var values) ||
            values == null)
            return Array.Empty<string>();
        return values;
    }

    public bool HasSelections(string key)
    {
        return GetSelections(key).Count > 0;
    }
}

public interface IControlledTaskCommandConfigurator
{
    IReadOnlyList<ControlledTaskParameterDefinition> Parameters { get; }

    IReadOnlyList<ControlledTaskOption> GetOptions(
        Actor actor,
        string parameterKey,
        ControlledTaskInvocation invocation);

    ControlledTaskAvailability Validate(Actor actor, ControlledTaskInvocation invocation);

    IControlledTaskExecutionContext Prepare(Actor actor, ControlledTaskInvocation invocation);
}

public interface IControlledTaskInvocationSummaryProvider
{
    string GetInvocationSummary(Actor actor, ControlledTaskInvocation invocation);
}

public interface IControlledTaskExecutionContext
{
    void OnOrderFinished(ControlledTaskOrderState state, string reasonLocaleKey);
}

public interface IControlledTaskOrderBoundContext : IControlledTaskExecutionContext
{
    void BindOrder(long orderId);
}

public static class ControlledTaskExecutionContextStore
{
    private static readonly Dictionary<long, IControlledTaskExecutionContext> Contexts = new();

    public static void Put(long orderId, IControlledTaskExecutionContext context)
    {
        if (orderId <= 0) throw new ArgumentOutOfRangeException(nameof(orderId));
        if (context == null) return;
        if (Contexts.ContainsKey(orderId))
            throw new InvalidOperationException($"Controlled task context already exists for order {orderId}.");
        if (context is IControlledTaskOrderBoundContext orderBound) orderBound.BindOrder(orderId);
        Contexts.Add(orderId, context);
    }

    public static bool TryGet<T>(long orderId, out T context) where T : class, IControlledTaskExecutionContext
    {
        if (Contexts.TryGetValue(orderId, out IControlledTaskExecutionContext value) && value is T typed)
        {
            context = typed;
            return true;
        }

        context = null;
        return false;
    }

    public static bool Remove<T>(long orderId, out T context) where T : class, IControlledTaskExecutionContext
    {
        if (Contexts.TryGetValue(orderId, out IControlledTaskExecutionContext value) && value is T typed)
        {
            Contexts.Remove(orderId);
            context = typed;
            return true;
        }

        context = null;
        return false;
    }

    public static bool Remove(long orderId, out IControlledTaskExecutionContext context)
    {
        if (Contexts.TryGetValue(orderId, out context))
        {
            Contexts.Remove(orderId);
            return true;
        }

        context = null;
        return false;
    }

    internal static void Clear()
    {
        Contexts.Clear();
    }
}
