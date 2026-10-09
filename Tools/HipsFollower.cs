using UnityEngine;

namespace CustomDancePlayer
{
    [DefaultExecutionOrder(12000)]
    public class HipsFollower : MonoBehaviour
    {
        [Tooltip("The smoothness factor for following (0 = instant, 1 = no movement).")]
        [Range(0f, 1f)]
        public float smoothness = 0.9f;
        public Vector2 basePosition;
        public DanceAvatarHelper avatarHelper;

        private RectTransform panelRect;
        private Camera mainCam;
        private Vector3 initialHipsPos;
        private Vector2 currentPosition;
        private bool hasInitialSetup;
        private bool vmdPosePending;
        private Transform boundHips;

        private void Start()
        {
            panelRect = GetComponent<RectTransform>();
            mainCam = Camera.main;
        }

        private void OnEnable()
        {
            UpdateBaseAndInitial();
        }

        private void OnDisable()
        {
            hasInitialSetup = false;
        }

        public void UpdateBaseAndInitial()
        {
            if (panelRect == null) panelRect = GetComponent<RectTransform>();
            if (panelRect == null) return;

            basePosition = panelRect.anchoredPosition;
            currentPosition = basePosition;


            if (avatarHelper != null && avatarHelper.CurrentAvatarHips != null)
            {
                boundHips = avatarHelper.CurrentAvatarHips;
                initialHipsPos = avatarHelper.CurrentAvatarHips.position;
                hasInitialSetup = true;
            }

        }

        public void ApplyAfterVmdPose()
        {
            // HumanPose bone transforms can become observable after the pose
            // callback on some host frames. Defer exactly one follow update to
            // this component's late execution slot instead of applying twice.
            vmdPosePending = true;
        }

        private void LateUpdate()
        {
            bool isVmd = avatarHelper?.playerCore != null && avatarHelper.playerCore.IsPlaying && avatarHelper.playerCore.resourceManager.IsVmdResource;
            if (isVmd)
            {
                if (vmdPosePending) { vmdPosePending = false; ApplyFollow(); }
                return;
            }
            vmdPosePending = false;
            ApplyFollow();
        }
        public void ApplyFollow()
        {
            if (avatarHelper == null) return;
            if (!hasInitialSetup || boundHips != avatarHelper.CurrentAvatarHips) UpdateBaseAndInitial();
            if (!hasInitialSetup || panelRect == null || avatarHelper.CurrentAvatarHips == null) return;

            if (mainCam == null)
            {
                mainCam = Camera.main;
                if (mainCam == null) return;
            }


            Vector3 currentHipsPos = avatarHelper.CurrentAvatarHips.position;
            Vector3 initialScreenPos = mainCam.WorldToScreenPoint(initialHipsPos);
            Vector3 currentScreenPos = mainCam.WorldToScreenPoint(currentHipsPos);

            Vector2 deltaScreen = (Vector2)(currentScreenPos - initialScreenPos);
            var canvas = panelRect.GetComponentInParent<Canvas>();
            if (canvas == null || !canvas.isActiveAndEnabled) return;
            Vector2 targetPosition = basePosition + deltaScreen / (canvas == null ? 1 : canvas.scaleFactor);

            currentPosition = Vector2.Lerp(currentPosition, targetPosition, 1f - smoothness);
            panelRect.anchoredPosition = currentPosition;

            ClampToScreenBounds();
        }

        private void ClampToScreenBounds()
        {
            if (panelRect == null) return;
            var canvas = panelRect.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var bounds = canvas.GetComponent<RectTransform>().rect.size;
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
