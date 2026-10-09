using CustomDancePlayer;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(11000)]
public class DanceCameraDistKeeper : MonoBehaviour
{
    [Tooltip("Fixed Z-axis distance to maintain between camera and hips")]
    public float fixedZDistance = -3.27f;

    public DanceAvatarHelper avatarHelper;

    private Camera _mainCamera;
    private Vector3 _danceBaseline;
    private bool _hasDanceBaseline;

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

    private void LateUpdate()
    {if(avatarHelper?.playerCore!=null && avatarHelper.playerCore.IsPlaying && avatarHelper.playerCore.resourceManager.IsVmdResource)return;ApplyFollow();}
    public void ApplyFollow()
    {
        if (DanceNativeMenus.IsOpen()) return;
        if (_mainCamera == null || avatarHelper.CurrentAvatarHips == null || avatarHelper.playerCore == null || !avatarHelper.playerCore.IsPlaying) return;


        // Maintain fixed Z distance
        if (avatarHelper.CurrentAvatarHips != null)
        {
            Vector3 newCameraPos = _mainCamera.transform.position;
            // Wheel zoom changes avatar geometry, not camera projection. Keep
            // the front camera outside the body at the current display size.
            float scale=avatarHelper.CurrentAvatar==null?1f:Mathf.Max(0.0001f,Mathf.Abs(avatarHelper.CurrentAvatar.transform.lossyScale.z));
            newCameraPos.z = avatarHelper.CurrentAvatarHips.position.z + fixedZDistance*scale;
            _mainCamera.transform.position = newCameraPos;
        }
    }

    public void BeginDanceTransition()
    {
        var currentMain = Camera.main;
        if (currentMain != null) _mainCamera = currentMain;
        if (_mainCamera == null) return;
        _danceBaseline = _mainCamera.transform.position;
        _hasDanceBaseline = true;
    }

    public void RestoreForDanceTransition()
    {
        if (!_hasDanceBaseline) return;
        if (_mainCamera != null) _mainCamera.transform.position = _danceBaseline;
        _hasDanceBaseline = false;
    }

    private void OnDisable()
    {
        RestoreForDanceTransition();
    }

}
