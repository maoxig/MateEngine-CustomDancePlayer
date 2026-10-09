using UnityEngine;

namespace CustomDancePlayer
{
    [DefaultExecutionOrder(11000)]
    public class DanceShadowFollower : MonoBehaviour
    {
        [Header("Shadow Settings")]
        public string shadowName = "Shadow";
        public float initialZOffset = 2.02f;

        [Header("References")]
        public DanceAvatarHelper avatarHelper;
        public DancePlayerCore dancePlayerCore;

        private Transform _cachedShadowTransform;
        private bool originalVisibility;
        private bool ownsVisibility;
        private float nextSearch;

        public void ApplyVisibility()
        {
            if (_cachedShadowTransform == null)
            {
                _cachedShadowTransform = FindShadowTransform();
                if (_cachedShadowTransform == null) return;
            }
            bool visible = DanceSettingsHandler.Instance.data.showAvatarShadow;
            var shadow = _cachedShadowTransform.gameObject;
            if (!visible)
            {
                if (!ownsVisibility) originalVisibility = shadow.activeSelf;
                else if (shadow.activeSelf) originalVisibility = true;
                ownsVisibility = true;
                if (shadow.activeSelf) shadow.SetActive(false);
            }
            else if (ownsVisibility) RestoreVisibility();
        }

        void LateUpdate()
        {
            if (_cachedShadowTransform != null || Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 1f;
                ApplyVisibility();
            }
            if(dancePlayerCore!=null&&dancePlayerCore.IsPlaying&&dancePlayerCore.resourceManager.IsVmdResource)return;
            ApplyFollow();
        }
        public void ApplyFollow()
        {
            if (!DanceSettingsHandler.Instance.data.enableShadowFollow || !DanceSettingsHandler.Instance.data.showAvatarShadow) return;
            if (avatarHelper.CurrentAvatarHips == null)
                return;

            if (_cachedShadowTransform == null)
            {
                _cachedShadowTransform = FindShadowTransform();
                if (_cachedShadowTransform == null)
                    return;
            }

            Vector3 pos = _cachedShadowTransform.position;
            pos.x = avatarHelper.CurrentAvatarHips.position.x;
            pos.y = avatarHelper.CurrentAvatarHips.position.y;
            pos.z = avatarHelper.CurrentAvatarHips.position.z + initialZOffset;
            _cachedShadowTransform.position = pos;
        }

        private Transform FindShadowTransform()
        {
            GameObject shadowObj = GameObject.Find(shadowName);

            if (shadowObj != null && shadowObj.GetComponent<Renderer>() != null)
            {
                return shadowObj.transform;
            }

            foreach (var node in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (node.name == shadowName && node.GetComponent<Renderer>() != null) return node;

            return null;
        }
        public void ClearShadowCache()
        {
            RestoreVisibility();
            _cachedShadowTransform = null;
        }

        private void RestoreVisibility()
        {
            if (ownsVisibility && _cachedShadowTransform != null) _cachedShadowTransform.gameObject.SetActive(originalVisibility);
            ownsVisibility = false;
        }

        void OnDisable() { RestoreVisibility(); }
        void OnDestroy() { RestoreVisibility(); }
    }
}
