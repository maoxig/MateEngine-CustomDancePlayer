using UnityEngine;
using UnityEngine.EventSystems;
namespace CustomDancePlayer
{
    public sealed class DanceTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public GameObject Hint;
        private float showAt;
        private bool hovering;
        public void ShowNow() { hovering=true;showAt=Time.unscaledTime;if(Hint!=null)Hint.SetActive(true); }
        public void OnPointerEnter(PointerEventData data)
        {
            if(Hint==null)return;
            var hintRect=Hint.GetComponent<RectTransform>();var label=Hint.GetComponentInChildren<UnityEngine.UI.Text>();
            if(label!=null)hintRect.sizeDelta=new Vector2(hintRect.sizeDelta.x,Mathf.Max(hintRect.sizeDelta.y,label.preferredHeight+16));
            var parent=Hint.transform.parent as RectTransform;var canvas=parent==null?null:parent.GetComponent<Canvas>();Vector2 point;
            if(parent!=null&&RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,data.position,canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null,out point))
            {
                var rect=Hint.GetComponent<RectTransform>();point+=new Vector2(12,-12);
                point.x=Mathf.Clamp(point.x,parent.rect.xMin+6,parent.rect.xMax-rect.rect.width-6);
                point.y=Mathf.Clamp(point.y,parent.rect.yMin+rect.rect.height+6,parent.rect.yMax-6);rect.anchoredPosition=point;
            }
            hovering=true;showAt=Time.unscaledTime+0.25f;
        }
        public void OnPointerExit(PointerEventData data) { hovering=false;if(Hint!=null) Hint.SetActive(false); }
        private void Update() { if(hovering&&Hint!=null&&Time.unscaledTime>=showAt)Hint.SetActive(true); }
        private void OnDisable() { hovering=false;if(Hint!=null)Hint.SetActive(false); }
        private void OnDestroy() { if(Hint!=null)Destroy(Hint); }
    }
}
