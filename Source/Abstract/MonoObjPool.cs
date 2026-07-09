using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cultiway.Abstract;

public class MonoObjPool<T> where T : MonoBehaviour
{
    private readonly Action<T>            _active_action;
    private readonly Action<T>            _create_action;
    private readonly Action<T>            _deactive_action;
    private readonly Dictionary<int, int> _idx_dict;
    private readonly Transform            _parent;

    private List<T> _pool;
    private T       _prefab;
    private int     _unused_first_idx;

    public MonoObjPool(T prefab, Transform parent, Action<T> create_action = null, Action<T> active_action = null,
                       Action<T> deactive_action = null)
    {
        _prefab = prefab;
        _parent = parent;
        _create_action = create_action;
        _active_action = active_action;
        _deactive_action = deactive_action;
        _pool = new();
        _idx_dict = new Dictionary<int, int>();
        _unused_first_idx = 0;
    }

    public IEnumerable<T> ActiveObjs => _pool.Take(_unused_first_idx);

    public void ResetToStart()
    {
        _unused_first_idx = 0;
    }

    public void ClearUnsed()
    {
        for (int i = _unused_first_idx; i < _pool.Count; i++)
        {
            var obj = _pool[i];
            obj.gameObject.SetActive(false);
            _deactive_action?.Invoke(obj);
        }
    }

    public void Clear()
    {
        foreach (var obj in _pool)
        {
            obj.gameObject.SetActive(false);
            _deactive_action?.Invoke(obj);
        }

        _unused_first_idx = 0;
    }

    public void Return(T obj)
    {
        if (obj == null) return;

        var objId = obj.GetInstanceID();
        if (!TryResolveIndex(obj, out var idx))
        {
            obj.transform.SetParent(_parent);
            _pool.Add(obj);
            _idx_dict[objId] = _pool.Count - 1;
            Deactivate(obj);
            return;
        }

        if (obj.transform.parent != _parent)
        {
            obj.transform.SetParent(_parent);
        }

        if (idx >= _unused_first_idx)
        {
            Deactivate(obj);
            return;
        }

        var lastActiveIdx = _unused_first_idx - 1;
        if (idx != lastActiveIdx)
        {
            var swapped = _pool[lastActiveIdx];
            _pool[idx] = swapped;
            _pool[lastActiveIdx] = obj;
            _idx_dict[swapped.GetInstanceID()] = idx;
            _idx_dict[objId] = lastActiveIdx;
        }

        _unused_first_idx -= 1;
        Deactivate(obj);
    }

    private bool TryResolveIndex(T obj, out int idx)
    {
        var objId = obj.GetInstanceID();
        if (_idx_dict.TryGetValue(objId, out idx) && idx >= 0 && idx < _pool.Count && _pool[idx] == obj)
        {
            return true;
        }

        idx = _pool.IndexOf(obj);
        if (idx < 0)
        {
            _idx_dict.Remove(objId);
            return false;
        }

        _idx_dict[objId] = idx;
        return true;
    }

    private void Deactivate(T obj)
    {
        obj.gameObject.SetActive(false);
        _deactive_action?.Invoke(obj);
    }

    public T GetNext(int slibing_idx = -1, Transform parent = null)
    {
        T obj;
        var len = (parent ?? _parent).childCount;
        if (_pool.Count == 0 || _unused_first_idx == _pool.Count)
        {
            obj = Object.Instantiate(_prefab, parent ?? _parent);
            len++;
            obj.transform.SetSiblingIndex((slibing_idx + len) % len);
            _create_action?.Invoke(obj);
            if (!obj.gameObject.activeSelf)
            {
                obj.gameObject.SetActive(true);
            }

            _pool.Add(obj);
        }
        else
        {
            obj = _pool[_unused_first_idx];
            if (parent != null) obj.transform.SetParent(parent);

            obj.transform.SetSiblingIndex((slibing_idx + len) % len);
            obj.gameObject.SetActive(true);
        }

        _idx_dict[obj.GetInstanceID()] = _unused_first_idx;

        _unused_first_idx++;

        _active_action?.Invoke(obj);
        return obj;
    }
}
