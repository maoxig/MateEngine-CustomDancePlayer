using UnityEngine;

namespace CustomDancePlayer
{
    public class HipsFollower : MonoBehaviour
    {
        [Tooltip("The smoothness factor for following (0 = instant, 1 = no movement).")]
        [Range(0f, 1f)]
        public float smoothness = 0.9f;

        public Vector2 basePosition;
        public bool followEnabled = true;

        private RectTransform panelRect;
        public DanceAvatarHelper avatarHelper;
        private Camera mainCam;
        private Vector3 initialHipsPos;
        private Vector2 currentPosition;
        private bool hasInitialSetup;

        private void Awake()
        {
            panelRect = GetComponent<RectTransform>();
            mainCam = Camera.main;
        }

        private void OnEnable()
        {
            if (followEnabled)
            {
                UpdateBaseAndInitial();
            }
        }

        public void UpdateBaseAndInitial()
        {
            if (panelRect == null) return;

            basePosition = panelRect.anchoredPosition;
            currentPosition = basePosition;

            var animator = avatarHelper?.CurrentAnimator;
            if (animator != null)
            {
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null)
                {
                    initialHipsPos = hips.position;
                    hasInitialSetup = true;
                }
            }
        }

        private void LateUpdate()
        {
            if (!followEnabled || !hasInitialSetup || panelRect == null || avatarHelper?.CurrentAnimator == null) return;

            if (mainCam == null)
            {
                mainCam = Camera.main;
                if (mainCam == null) return;
            }

            Transform hips = avatarHelper.CurrentAnimator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null) return;

            Vector3 currentHipsPos = hips.position;
            Vector3 initialScreenPos = mainCam.WorldToScreenPoint(initialHipsPos);
            Vector3 currentScreenPos = mainCam.WorldToScreenPoint(currentHipsPos);

            Vector2 deltaScreen = (Vector2)(currentScreenPos - initialScreenPos);
            Vector2 targetPosition = basePosition + deltaScreen;

            currentPosition = Vector2.Lerp(currentPosition, targetPosition, 1f - smoothness);
            panelRect.anchoredPosition = currentPosition;

            ClampToScreenBounds();
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