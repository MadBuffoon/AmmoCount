﻿using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AmmoCount.Util;

public class DragControl : MonoBehaviour, IDragHandler, IEndDragHandler
{
    [SerializeField] private RectTransform dragRectTransform = new();

    private void Start()
    {
        dragRectTransform = GetComponent<RectTransform>();
        dragRectTransform.anchoredPosition = AmmoCountPlugin.UIAnchorDrag;
    }

    public void OnDrag(PointerEventData eventData)
    {
        dragRectTransform.anchoredPosition += eventData.delta;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        AmmoCountPlugin.UIAnchorDrag = dragRectTransform.anchoredPosition;
        AmmoCountPlugin.UIAnchor.Value = RemoveSpecialCharacters(AmmoCountPlugin.UIAnchorDrag.ToString());
    }

    public static string RemoveSpecialCharacters(string str)
    {
        var sb = new StringBuilder();
        foreach (var c in str)
        {
            if ((c >= '0' && c <= '9') || c == '.' || c == ',' || c == '-' || c == ' ')
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}