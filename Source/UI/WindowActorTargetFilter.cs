using Cultiway.Core.ActorFiltering;
using Cultiway.UI.Components;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>供不同世界工具共用的角色目标筛选窗口；每个调用方仍持有自己的筛选配置。</summary>
public sealed class WindowActorTargetFilter : AbstractWideWindow<WindowActorTargetFilter>
{
    public const string Id = "Cultiway.UI.WindowActorTargetFilter";
    public static readonly Vector2 WindowSize = new(600f, 360f);

    private const float RootWidth = 520f;
    private const float RootHeight = 318f;
    private const float ContextHeight = 22f;
    private const float ExpressionHeight = 40f;
    private const float BrowserHeight = 246f;

    private static ActorFilterSettings _pendingSettings;
    private static string _pendingContextKey;
    private static string _pendingEmptyExpressionKey;
    private static string _pendingSemanticsKey;

    private readonly ActorFilterSettings _placeholderSettings = new();
    private ActorFilterEditor _editor;
    private Text _context;

    public static void Open(ActorFilterSettings settings, string contextKey, string emptyExpressionKey,
        string semanticsKey)
    {
        _pendingSettings = settings;
        _pendingContextKey = contextKey;
        _pendingEmptyExpressionKey = emptyExpressionKey;
        _pendingSemanticsKey = semanticsKey;
        ScrollWindow.showWindow(Id);
    }

    protected override void Init()
    {
        ActorFilterCatalog.Initialize();
        UiWindowContext context = UiWindowContext.Bind(BackgroundTransform);
        GameObject root = UiLayout.Create(BackgroundTransform, "ActorTargetFilterRoot", false,
            RootWidth, RootHeight, UiTheme.Current.Metrics.SpacingSm);
        root.transform.localPosition = new Vector3(0f, -8f);

        _context = UiElements.CreateText(root.transform, "Context", string.Empty, RootWidth,
            ContextHeight, 8, TextAnchor.MiddleLeft, FontStyle.Bold);
        _context.color = UiTheme.Current.Palette.AccentText;
        _editor = new ActorFilterEditor(root.transform, RootWidth, ExpressionHeight, BrowserHeight,
            context.ScrollbarTemplate, _placeholderSettings,
            "Cultiway.ActorTargetFilter.UI.Expression.Empty",
            "Cultiway.ActorTargetFilter.UI.Filter.Semantics");
    }

    public override void OnNormalEnable()
    {
        _context.text = _pendingContextKey.Localize();
        _editor.Bind(_pendingSettings, _pendingEmptyExpressionKey, _pendingSemanticsKey);
    }

    private void OnDestroy()
    {
        _editor.Dispose();
    }
}
