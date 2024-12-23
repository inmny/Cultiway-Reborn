using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Prefab;

public class MultiLayerIcon : APrefabPreview<MultiLayerIcon>
{
    private List<Tuple<Image, Rect>> _layers;
    private MonoObjPool<Image>       _pool;
    private RectTransform            _rect_transform;
    private Vector2                  _size;

    private void Update()
    {
        Init();
        if (_rect_transform.sizeDelta != _size) SetSize(_rect_transform.sizeDelta);
    }

    protected override void OnInit()
    {
        _rect_transform = GetComponent<RectTransform>();
        _size = _rect_transform.sizeDelta;
        _pool = new MonoObjPool<Image>(
            ModClass.NewPrefabPreview($"{nameof(MultiLayerIcon)}.Layer", typeof(Image)).GetComponent<Image>(),
            transform);
        _layers = new List<Tuple<Image, Rect>>();
    }

    public void Setup(Vector2 size, params (Sprite, Rect)[] layers)
    {
        Init();
        _pool.Clear();
        _layers.Clear();
        foreach ((Sprite sprite, Rect rect) in layers)
        {
            Image img = _pool.GetNext();
            img.sprite = sprite;
            _layers.Add(new Tuple<Image, Rect>(img, rect));
        }

        SetSize(size);
    }

    public override void SetSize(Vector2 pSize)
    {
        base.SetSize(pSize);
        foreach ((Image img, Rect rect) in _layers)
        {
            img.rectTransform.sizeDelta = new Vector2(pSize.x        * rect.width, pSize.y * rect.height);
            img.rectTransform.anchoredPosition = new Vector2(pSize.x * rect.x,     pSize.y * rect.y);
        }
    }

    private static void _init()
    {
        GameObject obj = ModClass.NewPrefabPreview(nameof(MultiLayerIcon), typeof(RectTransform));
        Prefab = obj.AddComponent<MultiLayerIcon>();
    }
}