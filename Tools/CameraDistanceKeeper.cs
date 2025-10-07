using CustomDancePlayer;
using UnityEngine;
using UnityEngine.UI;

public class CameraDistanceKeeper : MonoBehaviour
{
    [Tooltip("Fixed Z-axis distance to maintain between camera and hips")]
    public float fixedZDistance = -3.27f;

    public DanceAvatarHelper avatarHelper;
    public Toggle EnableCameraDistanceKeep;

    private Camera _mainCamera;
    private Animator _avatarAnimator;
    private Transform _hipsTransform;

    private void OnEnable()
    {

        _mainCamera = Camera.main;

        // Validate required references
        if (avatarHelper == null)
        {
            Debug.LogError("Missing DanceAvatarHelper component!", this);
            enabled = false;
            return;
        }

        if (_mainCamera == null)
        {
            Debug.LogError("No MainCamera found in scene!", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (_mainCamera == null || avatarHelper.CurrentAvatar == null) return;

        // Update animator reference if avatar changed
        if (_avatarAnimator == null || _avatarAnimator.gameObject != avatarHelper.CurrentAvatar)
        {
            _avatarAnimator = avatarHelper.CurrentAvatar.GetComponent<Animator>();
            UpdateHipsTransform();
        }

        // Update hips reference if needed
        if (_hipsTransform == null && _avatarAnimator != null)
        {
            UpdateHipsTransform();
        }

        // Maintain fixed Z distance
        if (_hipsTransform != null)
        {
            Vector3 newCameraPos = _mainCamera.transform.position;
            newCameraPos.z = _hipsTransform.position.z + fixedZDistance;
            _mainCamera.transform.position = newCameraPos;
        }
    }

    /// <summary>
    /// Gets hips transform using Unity's humanoid bone system
    /// </summary>
    private void UpdateHipsTransform()
    {
        _hipsTransform = null;

        if (_avatarAnimator == null || !_avatarAnimator.isHuman)
        {
            Debug.LogWarning("Current avatar is not a humanoid (no valid Animator with human avatar)", this);
            return;
        }

        // Get hips directly using humanoid bone system
        _hipsTransform = _avatarAnimator.GetBoneTransform(HumanBodyBones.Hips);

        if (_hipsTransform == null)
        {
            Debug.LogWarning("Could not find Hips bone in humanoid avatar", this);
        }
    }
}
