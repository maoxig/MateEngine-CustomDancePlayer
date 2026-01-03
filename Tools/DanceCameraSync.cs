using UnityEngine;
using System.Collections;

namespace CustomDancePlayer
{
    public class DanceCameraSync : MonoBehaviour
    {

        // 引用外部摄像机
        public Camera RenderCamera;

        public DanceAvatarHelper AvatarHelper;

        private Camera _danceCamera;

        void OnEnable()
        {

            _danceCamera = AvatarHelper.CurrentAvatar.transform.Find("Camera_root/Camera_root_1/Camera").GetComponent<Camera>();
            // Camera (Camera_root/Camera_root_1/Camera)
        }

        void OnDisable()
        {
            RenderCamera.enabled = false;

        }

        void LateUpdate()
        {
            if (AvatarHelper == null || AvatarHelper.CurrentAvatar == null || RenderCamera == null || DanceSettingsHandler.Instance.data.isPlaying == false)
            {
                RenderCamera.enabled = false;
                return;
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

            float scale = 1;
            Vector3 referencePos = AvatarHelper.CurrentAvatar.transform.position;
            Vector3 localOffset = cameraNode.position - referencePos;
            Vector3 scaledOffset = localOffset * scale;
            Vector3 finalCameraPos = referencePos + scaledOffset;
            Quaternion finalCameraRot = cameraNode.rotation;
            RenderCamera.fieldOfView = _danceCamera.fieldOfView;
            RenderCamera.transform.SetPositionAndRotation(finalCameraPos, finalCameraRot);
        }
    }
}
