using UnityEngine;
using System.Collections;

namespace CustomDancePlayer
{
    public class DanceCameraSync : MonoBehaviour
    {

        // 引用外部摄像机
        public Camera RenderCamera;

        public DanceAvatarHelper AvatarHelper;
        public GameObject PreviewRoot;
        public UnityEngine.UI.RawImage PreviewImage;
        public UnityEngine.UI.Text PreviewStatus;
        public RenderTexture OwnedPreviewTexture;

        private Camera _danceCamera;
        private GameObject _cameraAvatar;

        void OnEnable()
        {
            _danceCamera = null;
            _cameraAvatar = null;
            if (PreviewRoot != null) PreviewRoot.SetActive(true);
        }

        void OnDisable()
        {
            if (RenderCamera != null) RenderCamera.enabled = false;
            if (PreviewRoot != null) PreviewRoot.SetActive(false);

        }

        void LateUpdate()
        {
            if (RenderCamera == null) return;
            // No stale image while idle, paused, or waiting for a camera track.
            if (PreviewImage != null) PreviewImage.enabled = false;
            if (PreviewStatus != null) PreviewStatus.gameObject.SetActive(true);
            if (AvatarHelper == null || AvatarHelper.CurrentAvatar == null || DanceSettingsHandler.Instance.data.isPlaying == false)
            {
                RenderCamera.enabled = false;
                return;
            }

            if (_cameraAvatar != AvatarHelper.CurrentAvatar)
            {
                _cameraAvatar = AvatarHelper.CurrentAvatar;
                _danceCamera = null;
            }
            if (_danceCamera == null)
            {
                Transform cameraTransform = AvatarHelper.CurrentAvatar.transform.Find("Camera_root/Camera_root_1/Camera");
                if (cameraTransform != null)
                {
                    _danceCamera = cameraTransform.GetComponent<Camera>();
                }
            }

            if (_danceCamera == null)
            {
                RenderCamera.enabled = false;
                return;
            }

            Transform cameraNode = _danceCamera.transform;
            if (cameraNode == null)
            {
                RenderCamera.enabled = false;
                return;
            }

            Transform cameraRoot1 = cameraNode.parent;
            Transform cameraRoot = cameraRoot1 != null ? cameraRoot1.parent : null;

            bool isDefaultResolved = true;
            if (cameraRoot != null && (cameraRoot.localPosition != Vector3.zero || cameraRoot.localRotation != Quaternion.Euler(0, 180, 0)))
                isDefaultResolved = false;
            if (cameraRoot1 != null && (cameraRoot1.localPosition != Vector3.zero || cameraRoot1.localRotation != Quaternion.identity))
                isDefaultResolved = false;
            if (cameraNode.localPosition != Vector3.zero || cameraNode.localRotation != Quaternion.identity)
                isDefaultResolved = false;

            if (isDefaultResolved)
            {
                RenderCamera.enabled = false;
                return;
            }
            RenderCamera.enabled = true;
            if (PreviewImage != null) PreviewImage.enabled = true;
            if (PreviewStatus != null) PreviewStatus.gameObject.SetActive(false);

            float scale = Mathf.Clamp(DanceSettingsHandler.Instance.data.mmdCameraScale, 0.1f, 10f);
            Vector3 referencePos = AvatarHelper.CurrentAvatar.transform.position;
            Vector3 localOffset = cameraNode.position - referencePos;
            Vector3 scaledOffset = localOffset * scale;
            Vector3 finalCameraPos = referencePos + scaledOffset;
            Quaternion finalCameraRot = cameraNode.rotation;
            RenderCamera.fieldOfView = _danceCamera.fieldOfView;
            RenderCamera.transform.SetPositionAndRotation(finalCameraPos, finalCameraRot);
        }

        void OnDestroy()
        {
            if (RenderCamera != null) RenderCamera.targetTexture = null;
            if (OwnedPreviewTexture != null)
            {
                OwnedPreviewTexture.Release();
                Destroy(OwnedPreviewTexture);
            }
        }
    }
}
