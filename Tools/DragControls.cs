using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AmmoCount.Tools;

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
        AmmoCountPlugin.AmmoUIDrage = true;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        AmmoCountPlugin.UIAnchorDrag = dragRectTransform.anchoredPosition;
        AmmoCountPlugin.UIPosition.Value = RemoveSpecialCharacters(AmmoCountPlugin.UIAnchorDrag.ToString());
        AmmoCountPlugin.AmmoUIDrage = false;
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