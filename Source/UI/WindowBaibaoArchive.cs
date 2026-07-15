using Cultiway.Abstract;
using Cultiway.Content.Artifacts.Baibao;
using Cultiway.UI.Prefab;
using Friflo.Engine.ECS;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>
/// 展示指定角色库存中的法宝实体，并将选中法宝按快照收入百宝阁。
/// </summary>
public sealed class WindowBaibaoArchive : AbstractWideWindow<WindowBaibaoArchive>
{
    public const string Id = "Cultiway.UI.WindowBaibaoArchive";
    public static readonly Vector2 WindowSize = new(600f, 360f);
    private const float RootHeight = 318f;
    private static Actor _pendingActor;

    private Actor _actor;
    private MonoObjPool<BaibaoArchiveRow> _rowPool;
    private Text _actorName;
    private Text _itemCount;
    private Button _archiveAll;

    public static void Open(Actor actor)
    {
        _pendingActor = actor;
        PowerButtonSelector.instance.unselectAll();
        ScrollWindow.showWindow(Id);
    }

    protected override void Init()
    {
        BackgroundTransform.Find("Scroll View").gameObject.SetActive(false);
        GameObject root = WanfaUiFactory.CreateLayout(BackgroundTransform, "BaibaoArchiveRoot", false, 520f,
            RootHeight, 4f);
        root.transform.localPosition = new Vector3(0f, -8f);

        GameObject toolbar = WanfaUiFactory.CreateLayout(root.transform, "Toolbar", true, 520f, 24f, 4f);
        Button back = WanfaUiFactory.CreateIconButton(toolbar.transform, "Back", BaibaoUiIcons.Pavilion, 28f, 22f,
            OpenPavilion);
        WanfaUiFactory.SetTooltip(back.gameObject, "Cultiway.Baibao.UI.Action.BackToPavilion",
            "Cultiway.Baibao.UI.Tooltip.BackToPavilion");
        _actorName = WanfaUiFactory.CreateText(toolbar.transform, "ActorName", string.Empty, 330f, 22f, 8,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        _itemCount = WanfaUiFactory.CreateText(toolbar.transform, "ItemCount", string.Empty, 90f, 22f, 6,
            TextAnchor.MiddleRight);
        _archiveAll = WanfaUiFactory.CreateIconButton(toolbar.transform, "ArchiveAll", BaibaoUiIcons.Archive, 36f,
            22f, ArchiveAll);
        WanfaUiFactory.SetTooltip(_archiveAll.gameObject, "Cultiway.Baibao.UI.Action.ArchiveAll",
            "Cultiway.Baibao.UI.Tooltip.ArchiveAll");

        Transform content = WanfaUiFactory.CreateScrollContent(root.transform, "ArtifactList", 520f, 286f);
        _rowPool = new MonoObjPool<BaibaoArchiveRow>(BaibaoArchiveRow.Prefab, content);
    }

    public override void OnNormalEnable()
    {
        _actor = _pendingActor;
        Refresh();
    }

    private void Refresh()
    {
        _rowPool.Clear();
        if (_actor == null || !_actor.isAlive())
        {
            _actorName.text = "Cultiway.Baibao.UI.State.TargetUnavailable".Localize();
            _itemCount.text = string.Empty;
            _archiveAll.interactable = false;
            return;
        }

        BaibaoPavilionService service = BaibaoPavilionService.Instance;
        var artifacts = service.GetArchivableArtifacts(_actor);
        _actorName.text = _actor.getName();
        _itemCount.text = string.Format("Cultiway.Baibao.UI.Format.ArtifactCount".Localize(), artifacts.Count);
        _archiveAll.interactable = artifacts.Count > 0;
        for (int i = 0; i < artifacts.Count; i++)
        {
            Entity artifact = artifacts[i];
            bool archived = service.IsArchived(artifact, _actor);
            _rowPool.GetNext().Setup(artifact, archived, () => ArchiveOne(artifact));
        }
    }

    private void ArchiveOne(Entity artifact)
    {
        BaibaoSaveResult result = BaibaoPavilionService.Instance.Archive(artifact, _actor);
        WindowBaibaoPavilion.ShowSaveResult(result, "Cultiway.Baibao.UI.Tip.Archived");
        Refresh();
    }

    private void ArchiveAll()
    {
        var artifacts = BaibaoPavilionService.Instance.GetArchivableArtifacts(_actor);
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

        string tip = string.Format("Cultiway.Baibao.UI.Format.ArchiveAllResult".Localize(), saved, duplicate,
            invalid);
        WorldTip.showNow(tip, false, "top", 3f);
        Refresh();
    }

    private void OpenPavilion()
    {
        GetComponent<ScrollWindow>().clickHide();
        ScrollWindow.showWindow(WindowBaibaoPavilion.Id);
    }
}
