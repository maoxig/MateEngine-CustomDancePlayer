using UnityEngine;

namespace CustomDancePlayer
{
    public class DanceShadowFollower : MonoBehaviour
    {
        [Header("Shadow Settings")]
        public string shadowName = "Shadow";
        public float initialZOffset = 2.02f;

        [Header("References")]
        public DanceAvatarHelper avatarHelper;
        public DancePlayerCore dancePlayerCore;

        private Transform _cachedShadowTransform;

        void Update()
        {
            if (dancePlayerCore == null || !dancePlayerCore.IsPlaying)
                return;

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

            return null;
        }
        public void ClearShadowCache()
        {
            _cachedShadowTransform = null;
        }
    }
}