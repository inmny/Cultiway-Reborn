using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Artifacts.Baibao;
using Cultiway.UI.Components;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>按角色展示法宝库存，并通过检查器和显式多选批量收录。</summary>
public sealed class WindowBaibaoArchive : AbstractWideWindow<WindowBaibaoArchive>
{
    public const string Id = "Cultiway.UI.WindowBaibaoArchive";
    public static readonly Vector2 WindowSize = new(600f, 380f);
    private const float RootHeight = 338f;
    private static Actor _pendingActor;

    private readonly HashSet<int> _selectedArtifactIds = new();
    private Actor _actor;
    private MonoObjPool<BaibaoArchiveRow> _rowPool;
    private BaibaoBlueprintInspector _inspector;
    private Text _actorName;
    private Text _itemCount;
    private Text _selectedCount;
    private Button _selectAll;
    private Button _clearSelection;
    private Button _archiveSelected;
    private int _activeArtifactId;

    public static void Open(Actor actor)
    {
        _pendingActor = actor;
        PowerButtonSelector.instance.unselectAll();
        ScrollWindow.showWindow(Id);
    }

    protected override void Init()
    {
        Transform originalScrollView = BackgroundTransform.Find("Scroll View");
        Transform scrollbarTemplate = originalScrollView.Find("Scrollbar Vertical Mask");
        originalScrollView.gameObject.SetActive(false);
        GameObject root = WanfaUiFactory.CreateLayout(BackgroundTransform, "BaibaoArchiveRoot", false, 520f,
            RootHeight, 4f);
        root.transform.localPosition = new Vector3(0f, -8f);
        CreateToolbar(root.transform);
        CreateSelectionBar(root.transform);
        GameObject body = WanfaUiFactory.CreateLayout(root.transform, "Body", true, 520f, 284f, 4f,
            TextAnchor.UpperLeft);
        Transform content = WanfaUiFactory.CreateScrollContent(body.transform, "ArtifactList", 318f, 284f);
        WanfaUiFactory.AttachOriginalVerticalScrollbar(content, scrollbarTemplate);
        BaibaoUiFactory.AddScrollBackground(content);
        _rowPool = new MonoObjPool<BaibaoArchiveRow>(BaibaoArchiveRow.Prefab, content);
        _inspector = new BaibaoBlueprintInspector(body.transform, 198f, 284f);
    }

    public override void OnNormalEnable()
    {
        _actor = _pendingActor;
        _selectedArtifactIds.Clear();
        _activeArtifactId = 0;
        Refresh();
    }

    private void CreateToolbar(Transform root)
    {
        GameObject toolbar = WanfaUiFactory.CreateLayout(root, "Toolbar", true, 520f, 24f, 4f);
        Button back = WanfaUiFactory.CreateIconButton(toolbar.transform, "Back", BaibaoUiIcons.Pavilion, 28f,
            22f, OpenPavilion);
        WanfaUiFactory.SetTooltip(back.gameObject, "Cultiway.Baibao.UI.Action.BackToPavilion",
            "Cultiway.Baibao.UI.Tooltip.BackToPavilion");
        _actorName = WanfaUiFactory.CreateText(toolbar.transform, "ActorName", string.Empty, 372f, 22f, 8,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        _itemCount = WanfaUiFactory.CreateText(toolbar.transform, "ItemCount", string.Empty, 112f, 22f, 6,
            TextAnchor.MiddleRight);
    }

    private void CreateSelectionBar(Transform root)
    {
        GameObject bar = WanfaUiFactory.CreateLayout(root, "Selection", true, 520f, 22f, 4f);
        _selectedCount = WanfaUiFactory.CreateText(bar.transform, "SelectedCount", string.Empty, 244f, 21f, 6,
            TextAnchor.MiddleLeft);
        _selectAll = WanfaUiFactory.CreateIconTextButton(bar.transform, "SelectAll", BaibaoUiIcons.Confirm,
            "Cultiway.Baibao.UI.Action.SelectAll".Localize(), 88f, 21f, SelectAll);
        WanfaUiFactory.SetTooltip(_selectAll.gameObject, "Cultiway.Baibao.UI.Action.SelectAll",
            "Cultiway.Baibao.UI.Tooltip.SelectAllArchive");
        _clearSelection = WanfaUiFactory.CreateIconTextButton(bar.transform, "Clear", BaibaoUiIcons.Reset,
            "Cultiway.Baibao.UI.Action.ClearSelection".Localize(), 88f, 21f, ClearSelection);
        WanfaUiFactory.SetTooltip(_clearSelection.gameObject, "Cultiway.Baibao.UI.Action.ClearSelection",
            "Cultiway.Baibao.UI.Tooltip.ClearArchiveSelection");
        _archiveSelected = WanfaUiFactory.CreateIconTextButton(bar.transform, "Archive", BaibaoUiIcons.Archive,
            "Cultiway.Baibao.UI.Action.ArchiveSelected".Localize(), 88f, 21f, ArchiveSelected);
        WanfaUiFactory.SetTooltip(_archiveSelected.gameObject, "Cultiway.Baibao.UI.Action.ArchiveSelected",
            "Cultiway.Baibao.UI.Tooltip.ArchiveSelected");
    }

    private void Refresh()
    {
        _rowPool.Clear();
        if (_actor == null || !_actor.isAlive())
        {
            _actorName.text = "Cultiway.Baibao.UI.State.TargetUnavailable".Localize();
            _itemCount.text = string.Empty;
            _selectedCount.text = string.Empty;
            _selectAll.interactable = false;
            _clearSelection.interactable = false;
            _archiveSelected.interactable = false;
            _inspector.Clear();
            return;
        }

        BaibaoPavilionService service = BaibaoPavilionService.Instance;
        List<Entity> artifacts = service.GetArchivableArtifacts(_actor);
        HashSet<int> present = new(artifacts.Select(artifact => artifact.Id));
        _selectedArtifactIds.RemoveWhere(id => !present.Contains(id));
        if (artifacts.All(artifact => artifact.Id != _activeArtifactId))
            _activeArtifactId = artifacts.FirstOrDefault().Id;

        _actorName.text = _actor.getName();
        _itemCount.text = string.Format("Cultiway.Baibao.UI.Format.ArtifactCount".Localize(), artifacts.Count);
        int available = 0;
        for (int i = 0; i < artifacts.Count; i++)
        {
            Entity artifact = artifacts[i];
            bool archived = service.IsArchived(artifact, _actor);
            if (!archived) available++;
            _rowPool.GetNext().Setup(artifact, archived, artifact.Id == _activeArtifactId,
                _selectedArtifactIds.Contains(artifact.Id),
                () => SelectArtifact(artifact.Id),
                () => ToggleSelection(artifact.Id));
        }

        _selectedCount.text = string.Format("Cultiway.Baibao.UI.Format.ArchiveSelectedCount".Localize(),
            _selectedArtifactIds.Count, available);
        _selectAll.interactable = available > 0;
        _clearSelection.interactable = _selectedArtifactIds.Count > 0;
        _archiveSelected.interactable = _selectedArtifactIds.Count > 0;
        RefreshInspector(artifacts);
    }

    private void RefreshInspector(IReadOnlyList<Entity> artifacts)
    {
        Entity artifact = artifacts.FirstOrDefault(item => item.Id == _activeArtifactId);
        if (!artifact.IsAvailable())
        {
            _inspector.Clear();
            return;
        }
        BaibaoPavilionService service = BaibaoPavilionService.Instance;
        bool archived = service.IsArchived(artifact, _actor);
        ArtifactBlueprint blueprint = ArtifactBlueprintCodec.Capture(artifact, _actor);
        _inspector.Show(blueprint, new BaibaoBlueprintInspectorActions
        {
            Archive = archived ? null : () => ArchiveOne(artifact),
            ArchiveVisible = true,
        });
    }

    private void SelectArtifact(int id)
    {
        _activeArtifactId = id;
        Refresh();
    }

    private void ToggleSelection(int id)
    {
        if (!_selectedArtifactIds.Add(id)) _selectedArtifactIds.Remove(id);
        Refresh();
    }

    private void SelectAll()
    {
        BaibaoPavilionService service = BaibaoPavilionService.Instance;
        foreach (Entity artifact in service.GetArchivableArtifacts(_actor))
        {
            if (!service.IsArchived(artifact, _actor)) _selectedArtifactIds.Add(artifact.Id);
        }
        Refresh();
    }

    private void ClearSelection()
    {
        _selectedArtifactIds.Clear();
        Refresh();
    }

    private void ArchiveOne(Entity artifact)
    {
        BaibaoSaveResult result = BaibaoPavilionService.Instance.Archive(artifact, _actor);
        WindowBaibaoPavilion.ShowSaveResult(result, "Cultiway.Baibao.UI.Tip.Archived");
        _selectedArtifactIds.Remove(artifact.Id);
        Refresh();
    }

    private void ArchiveSelected()
    {
        List<Entity> artifacts = BaibaoPavilionService.Instance.GetArchivableArtifacts(_actor)
            .Where(artifact => _selectedArtifactIds.Contains(artifact.Id)).ToList();
        int saved = 0;
        int duplicate = 0;
        int invalid = 0;
        for (int i = 0; i < artifacts.Count; i++)
        {
            BaibaoSaveResult result = BaibaoPavilionService.Instance.Archive(artifacts[i], _actor);
            switch (result.Status)
            {
                case BaibaoSaveStatus.Saved:
                    saved++;
                    break;
                case BaibaoSaveStatus.Duplicate:
                    duplicate++;
                    break;
                default:
                    invalid++;
                    break;
            }
        }
        _selectedArtifactIds.Clear();
        WorldTip.showNow(string.Format("Cultiway.Baibao.UI.Format.ArchiveAllResult".Localize(), saved, duplicate,
            invalid), false, "top", 3f);
        Refresh();
    }

    private void OpenPavilion()
    {
        GetComponent<ScrollWindow>().clickHide();
        ScrollWindow.showWindow(WindowBaibaoPavilion.Id);
    }
}
