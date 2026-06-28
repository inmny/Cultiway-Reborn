using System.Collections;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.UI.Components;

internal class SectLeaderElement : WindowMetaElement<Sect, SectData>
{
    private GameObject _titleElement;
    private PrefabUnitElement _leaderElement;

    internal void Initialize(GameObject titleElement, PrefabUnitElement leaderElement)
    {
        _titleElement = titleElement;
        _leaderElement = leaderElement;
        RenameTitle();
        clear();
    }

    public override IEnumerator showContent()
    {
        if (_titleElement == null || _leaderElement == null) yield break;

        Actor leader = meta_object?.GetLeaderActor();
        if (leader.isRekt()) yield break;

        track_objects.Add(leader);
        _titleElement.SetActive(true);
        _leaderElement.gameObject.SetActive(true);
        _leaderElement.show(leader);
        yield break;
    }

    public override void clear()
    {
        if (_titleElement != null)
        {
            _titleElement.SetActive(false);
        }

        if (_leaderElement != null)
        {
            _leaderElement.gameObject.SetActive(false);
        }

        base.clear();
    }

    public override bool checkRefreshWindow()
    {
        Sect sect = meta_object;
        bool lostVisibleLeader = _leaderElement != null &&
                                 _leaderElement.gameObject.activeSelf &&
                                 (sect == null || sect.isRekt() || sect.GetLeaderActor().isRekt());
        return lostVisibleLeader
               || base.checkRefreshWindow();
    }

    private void RenameTitle()
    {
        if (_titleElement == null) return;

        LocalizedText localizedTitle = _titleElement.GetComponentInChildren<LocalizedText>(true);
        localizedTitle?.setKeyAndUpdate("Cultiway.Sect.Leader");
    }
}
