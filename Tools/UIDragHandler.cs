using UnityEngine;
using UnityEngine.EventSystems;
namespace CustomDancePlayer
{
    public class UIDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("Assign UI elements that can be used as drag handles (e.g., Text, Image, etc.).")]
        public RectTransform[] dragHandles;

        public HipsFollower hipsFollower;

        private RectTransform panelRect;
        private bool isDragging;
        private bool wasFollowing;

        private void Start()
        {
            panelRect = GetComponent<RectTransform>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!IsValidHandle(eventData)) return;

            isDragging = true;
            if (hipsFollower != null)
            {
                wasFollowing = hipsFollower.enabled;
                hipsFollower.enabled = false;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            panelRect.anchoredPosition += eventData.delta;
            ClampToScreenBounds();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (isDragging)
            {
                isDragging = false;
                if (hipsFollower != null && wasFollowing)
                {
                    hipsFollower.enabled = true; // Re-enable following only if it was enabled before
                    hipsFollower.UpdateBaseAndInitial(); // Update base position for new follow start
                }
            }
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

        private void ClampToScreenBounds()
        {
            Vector2 size = panelRect.rect.size * panelRect.lossyScale;
            float halfW = size.x / 2f;
            float halfH = size.y / 2f;

            float minX = -Screen.width / 2f + halfW;
            float maxX = Screen.width / 2f - halfW;
            float minY = -Screen.height / 2f + halfH;
            float maxY = Screen.height / 2f - halfH;

            Vector2 pos = panelRect.anchoredPosition;
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);

            panelRect.anchoredPosition = pos;
        }
    }
}