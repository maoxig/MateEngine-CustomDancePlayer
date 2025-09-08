using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Allows dragging a panel by one or more assigned "drag handles".
/// Attach to the root panel that you want to move.
/// </summary>
public class UIDragPanel : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [Tooltip("Assign UI elements that can be used as drag handles (e.g., Text, Image, etc.).")]
    public RectTransform[] dragHandles;

    private RectTransform panelRect;
    private bool isDragging;

    private void Awake()
    {
        panelRect = GetComponent<RectTransform>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsValidHandle(eventData))
        {
            isDragging = true;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        // 直接用 pointer 的 delta
        panelRect.anchoredPosition += eventData.delta;
    }

    private bool IsValidHandle(PointerEventData eventData)
    {
        if (dragHandles == null || dragHandles.Length == 0) return false;

        foreach (var handle in dragHandles)
        {
            if (handle == null) continue;
            if (eventData.pointerEnter == handle.gameObject)
                return true;
        }
        return false;
    }

    private void LateUpdate()
    {
        if (isDragging && Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }
    }
}
