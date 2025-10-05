using UnityEngine;
namespace CustomDancePlayer
{
    public class DanceShadowFollower : MonoBehaviour
    {
        [Header("配置")]
        public string shadowObjectName = "Shadow";

        private Transform hips;
        private Transform shadowPlane;
        public float initialZOffset = 2.02f;

        public DanceAvatarHelper avatarHelper;
        public DancePlayerCore dancePlayerCore;

        void Start()
        {
            InitReferences();
        }

        void OnEnable()
        {
            InitReferences();
        }

        private void InitReferences()
        {
            if (avatarHelper != null)
            {
                var animator = avatarHelper.CurrentAnimator;
                if (animator != null)
                {
                    hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                }
                else
                {
                    hips = null;
                }
            }
            else
            {
                hips = null;
            }

            GameObject shadowObj = GameObject.Find(shadowObjectName);
            if (shadowObj != null)
            {
                shadowPlane = shadowObj.transform;
            }
            else
            {
                shadowPlane = null;
            }
        }

        void Update()
        {
            if (!dancePlayerCore.IsPlaying || shadowPlane == null)
                return;
            if (avatarHelper != null && hips == null)
            {
                var animator = avatarHelper.CurrentAnimator;
                if (animator != null)
                {
                    hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                }
            }
            if (hips == null)
                return;
            Vector3 pos = shadowPlane.position;

            pos.x = hips.position.x;
            pos.y = hips.position.y;

            pos.z = hips.position.z + initialZOffset;

            shadowPlane.position = pos;
        }
    }
}