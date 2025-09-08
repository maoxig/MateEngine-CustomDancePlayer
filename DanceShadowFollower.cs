using UnityEngine;

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
        if (avatarHelper != null)
        {
            var animator = avatarHelper.CurrentAnimator;
            if (animator != null)
            {
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            }
        }

        GameObject shadowObj = GameObject.Find(shadowObjectName);
        if (shadowObj != null)
        {
            shadowPlane = shadowObj.transform;
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
