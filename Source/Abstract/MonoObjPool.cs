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
        if (obj.transform.parent != _parent)
        {
            obj.transform.SetParent(_parent);
            _pool.Add(obj);
        }
        else
        {
            if (!_idx_dict.TryGetValue(obj.GetInstanceID(), out var idx))
            {
                _pool.Add(obj);
            }
            else
            {
                _pool.Swap(idx, _unused_first_idx - 1);
                _unused_first_idx -= 1;
            }
        }

        obj.gameObject.SetActive(false);
        _deactive_action?.Invoke(obj);
    }

    public T GetNext(int slibing_idx = -1, Transform parent = null)
    {
        T obj;
        var len = _parent.childCount;
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