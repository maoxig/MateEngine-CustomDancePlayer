using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace CustomDancePlayer
{
    public class UIDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("Assign UI elements that can be used as drag handles (e.g., Text, Image, etc.).")]
        public RectTransform[] dragHandles;

        [Tooltip("Allow dragging from any non-interactive area inside the panel.")]
        public bool allowNonInteractiveDescendants = true;

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
            if (panelRect == null) panelRect = GetComponent<RectTransform>();
            var canvas = panelRect.GetComponentInParent<Canvas>();
            panelRect.anchoredPosition += eventData.delta / (canvas == null ? 1 : canvas.scaleFactor);
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
                var data=DanceSettingsHandler.Instance.data;
                if (data.miniMode) { if(wasFollowing)data.miniBasePosition=panelRect.anchoredPosition;else data.miniRawPosition=panelRect.anchoredPosition; }
                else { if (wasFollowing) data.uiBasePosition = panelRect.anchoredPosition; else data.uiRawPosition = panelRect.anchoredPosition; }
                DanceSettingsHandler.OnSettingChanged();
            }
        }

        private bool IsValidHandle(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return false;
            if (panelRect == null) panelRect = GetComponent<RectTransform>();

            GameObject hit = eventData.pointerPressRaycast.gameObject;
            if (hit == null) hit = eventData.pointerEnter;
            if (hit == null || panelRect == null) return false;
            Transform hitTransform = hit.transform;
            if (hitTransform != panelRect && !hitTransform.IsChildOf(panelRect)) return false;

            // Buttons, toggles, sliders, input fields and dropdowns all derive
            // from Selectable. Scroll views and custom drag controls keep their
            // own gestures even when nested inside this panel.
            for (Transform current = hitTransform; current != null; current = current.parent)
            {
                if (current.GetComponent<Selectable>() != null ||
                    current.GetComponent<ScrollRect>() != null ||
                    current.GetComponent<Scrollbar>() != null)
                    return false;

                MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
                for (int index = 0; index < behaviours.Length; index++)
                {
                    IDragHandler dragHandler = behaviours[index] as IDragHandler;
                    if (dragHandler != null && !ReferenceEquals(dragHandler, this)) return false;
                }

                if (current == panelRect) break;
            }

            if (allowNonInteractiveDescendants) return true;
            if (dragHandles == null || dragHandles.Length == 0) return false;

            foreach (var handle in dragHandles)
            {
                if (handle == null) continue;
                if (hitTransform == handle || hitTransform.IsChildOf(handle))
                    return true;
            }
            return false;
        }

        private void ClampToScreenBounds()
        {
            var bounds = panelRect.GetComponentInParent<Canvas>().GetComponent<RectTransform>().rect.size;
            Vector2 size = panelRect.rect.size;
            float halfW = size.x / 2f;
            float halfH = size.y / 2f;

            float minX = Mathf.Min(0,-bounds.x / 2f + halfW);
            float maxX = Mathf.Max(0,bounds.x / 2f - halfW);
            float minY = Mathf.Min(0,-bounds.y / 2f + halfH);
            float maxY = Mathf.Max(0,bounds.y / 2f - halfH);

            Vector2 pos = panelRect.anchoredPosition;
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);

            panelRect.anchoredPosition = pos;
        }
    }
}
