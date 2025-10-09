using CustomDancePlayer;
using UnityEngine;
using UnityEngine.UI;

public class DanceCameraDistKeeper : MonoBehaviour
{
    [Tooltip("Fixed Z-axis distance to maintain between camera and hips")]
    public float fixedZDistance = -3.27f;

    public DanceAvatarHelper avatarHelper;

    private Camera _mainCamera;

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
        if (_mainCamera == null || avatarHelper.CurrentAvatarHips == null) return;


        // Maintain fixed Z distance
        if (avatarHelper.CurrentAvatarHips != null)
        {
            Vector3 newCameraPos = _mainCamera.transform.position;
            newCameraPos.z = avatarHelper.CurrentAvatarHips.position.z + fixedZDistance;
            _mainCamera.transform.position = newCameraPos;
        }
    }

}
