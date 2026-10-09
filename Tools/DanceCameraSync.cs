using UnityEngine;

namespace CustomDancePlayer
{
    [DefaultExecutionOrder(11000)]
    public class DanceCameraSync : MonoBehaviour
    {
        public Camera RenderCamera;
        public DanceAvatarHelper AvatarHelper;

        // Retained for serialized compatibility with the short-lived 0.2
        // preview implementation. No overlay objects are created or displayed.
        public GameObject PreviewRoot;
        public UnityEngine.UI.RawImage PreviewImage;
        public UnityEngine.UI.Text PreviewStatus;
        public UnityEngine.UI.Text PreviewTitle;
        public RenderTexture OwnedPreviewTexture;

        private Camera danceCamera;
        private GameObject cameraAvatar;
        private Renderer[] cameraAvatarRenderers;
        private Camera frontCamera;
        private Vector3 frontPosition;
        private Quaternion frontRotation;
        private float frontFieldOfView;
        private float frontOrthographicSize;
        private float frontNearClip,frontFarClip;
        private Matrix4x4 frontProjection;
        private bool frontProjectionWasCustom;
        private bool frontEnabled;
        private bool hasFrontState;

        public bool IsUsingDanceView { get; private set; }

        void Update()
        {
            var core = AvatarHelper?.playerCore;
            if (core == null || !core.IsPlaying) return;
            if (DanceNativeMenus.IsOpen())
            {
                core.CurrentVmdPlayer?.CheckCameraOwnership();
                RestoreFrontView();
            }
            else
            {
                ResolveRenderCamera();
                CaptureFrontView();
            }
        }

        public void ConfigureSwitch(DanceAvatarHelper avatarHelper)
        {
            AvatarHelper = avatarHelper;
            HideLegacyPreview();
            ResolveRenderCamera();
        }

        void OnEnable()
        {
            danceCamera = null;
            cameraAvatar = null;
            HideLegacyPreview();
            ResolveRenderCamera();
        }

        void OnDisable()
        {
            RestoreFrontView();
            HideLegacyPreview();
        }

        public void PrepareSwitch()
        {
            ResolveRenderCamera();
            CaptureFrontView();
        }

        public void RestoreFrontView()
        {
            if (hasFrontState && frontCamera != null)
            {
                frontCamera.transform.SetPositionAndRotation(frontPosition, frontRotation);
                frontCamera.fieldOfView = frontFieldOfView;
                frontCamera.orthographicSize = frontOrthographicSize;
                frontCamera.nearClipPlane=frontNearClip;frontCamera.farClipPlane=frontFarClip;
                // Assigning even the original matrix leaves Unity's camera in
                // custom-projection mode. Official world-space menus later
                // change FOV/orthographicSize and expect the matrix to update.
                frontCamera.ResetProjectionMatrix();
                if (frontProjectionWasCustom) frontCamera.projectionMatrix = frontProjection;
                frontCamera.enabled = frontEnabled;
            }
            hasFrontState = false;
            frontCamera = null;
            IsUsingDanceView = false;
        }

        void LateUpdate()
        {
            if (AvatarHelper?.playerCore != null && AvatarHelper.playerCore.IsPlaying &&
                AvatarHelper.playerCore.resourceManager.IsVmdResource) return;
            ApplyPose();
        }

        public void ApplyPose()
        {
            if (DanceNativeMenus.IsOpen())
            {
                AvatarHelper?.playerCore?.CurrentVmdPlayer?.CheckCameraOwnership();
                RestoreFrontView();
                return;
            }
            ResolveRenderCamera();
            if (RenderCamera == null) return;
            var core = AvatarHelper == null ? null : AvatarHelper.playerCore;
            if (AvatarHelper == null || AvatarHelper.CurrentAvatar == null || core == null || !core.IsPlaying)
            {
                RestoreFrontView();
                return;
            }

            CaptureFrontView();
            if (core.resourceManager.IsVmdResource)
            {
                if (!core.HasRuntimeCameraPose) { IsUsingDanceView = false; return; }
                if(core.CurrentVmdPlayer!=null)core.CurrentVmdPlayer.ApplyLastCameraProjectionForRender();
                else ApplyAuthoredProjection(core.RuntimeCameraPerspective);
                IsUsingDanceView = true;
                return;
            }

            if (cameraAvatar != AvatarHelper.CurrentAvatar)
            {
                cameraAvatar = AvatarHelper.CurrentAvatar;
                cameraAvatarRenderers=cameraAvatar.GetComponentsInChildren<Renderer>(true);
                danceCamera = null;
            }
            if (danceCamera == null)
            {
                var cameraTransform = AvatarHelper.CurrentAvatar.transform.Find("Camera_root/Camera_root_1/Camera");
                if (cameraTransform != null) danceCamera = cameraTransform.GetComponent<Camera>();
            }
            if (danceCamera == null || IsLegacyCameraAtRest(danceCamera.transform))
            {
                IsUsingDanceView = false;
                return;
            }

            float scale = core.GetEffectiveCameraScale();
            Vector3 origin = AvatarHelper.CurrentAvatar.transform.position;
            RenderCamera.transform.position = origin + (danceCamera.transform.position - origin) * scale + AvatarHelper.CurrentAvatar.transform.TransformVector(Vector3.up*AvatarHelper.MeasureAvatarGroundHeight());
            RenderCamera.transform.rotation = danceCamera.transform.rotation;
            RenderCamera.fieldOfView = danceCamera.fieldOfView;
            float near=Mathf.Max(.0001f,danceCamera.nearClipPlane);
            float far=Mathf.Max(near+1f,danceCamera.farClipPlane);
            float required=Vector3.Distance(RenderCamera.transform.position,origin);
            if(cameraAvatarRenderers!=null)foreach(var renderer in cameraAvatarRenderers){
                if(renderer==null||!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                var bounds=renderer.bounds;float extent=Vector3.Distance(RenderCamera.transform.position,bounds.center)+bounds.extents.magnitude;
                if(!float.IsNaN(extent)&&!float.IsInfinity(extent))required=Mathf.Max(required,extent);
            }
            far=Mathf.Max(far,required+Mathf.Max(1f,AvatarHelper.MeasureAvatarHeight()*Mathf.Abs(cameraAvatar.transform.lossyScale.y)*2f));
            RenderCamera.nearClipPlane=near;RenderCamera.farClipPlane=far;
            // Copying only the matrix leaves the host culling/shader clip
            // parameters at the front camera's 10 m far plane.
            var projection=danceCamera.projectionMatrix;
            if(Mathf.Abs(projection.m33)>.5f&&Mathf.Abs(projection.m32)<.5f){projection.m22=-2f/(far-near);projection.m23=-(far+near)/(far-near);}
            else{projection.m22=-(far+near)/(far-near);projection.m23=-2f*far*near/(far-near);}
            RenderCamera.projectionMatrix = projection;
            RenderCamera.enabled = true;
            IsUsingDanceView = true;
        }

        private void ResolveRenderCamera()
        {
            Camera current = Camera.main;
            if (current == null) current = AvatarHelper?.ResolveRuntimeVmdCamera();
            if (current == null || current == RenderCamera) return;
            RestoreFrontView();
            RenderCamera = current;
        }

        private void CaptureFrontView()
        {
            if (RenderCamera == null || hasFrontState && frontCamera == RenderCamera) return;
            RestoreFrontView();
            frontCamera = RenderCamera;
            frontPosition = RenderCamera.transform.position;
            frontRotation = RenderCamera.transform.rotation;
            frontFieldOfView = RenderCamera.fieldOfView;
            frontOrthographicSize = RenderCamera.orthographicSize;
            frontNearClip=RenderCamera.nearClipPlane;frontFarClip=RenderCamera.farClipPlane;
            frontProjection = RenderCamera.projectionMatrix;
            // Detect a host-provided custom projection without assuming every
            // main camera uses Unity's automatic matrix. Restore the captured
            // custom matrix immediately when one was present.
            RenderCamera.ResetProjectionMatrix();
            frontProjectionWasCustom = MatrixDifference(frontProjection, RenderCamera.projectionMatrix) > 0.0001f;
            if (frontProjectionWasCustom) RenderCamera.projectionMatrix = frontProjection;
            frontEnabled = RenderCamera.enabled;
            hasFrontState = true;
        }

        private static float MatrixDifference(Matrix4x4 left, Matrix4x4 right)
        {
            float total = 0f;
            for (int row = 0; row < 4; row++)
                for (int column = 0; column < 4; column++)
                    total += Mathf.Abs(left[row, column] - right[row, column]);
            return total;
        }

        private void ApplyAuthoredProjection(bool perspective)
        {
            if (!perspective)
            {
                float distance = AvatarHelper == null || AvatarHelper.CurrentAvatar == null
                    ? 5f : Vector3.Distance(RenderCamera.transform.position, AvatarHelper.CurrentAvatar.transform.position);
                float halfHeight = Mathf.Max(0.01f, distance * 0.5f);
                float halfWidth = halfHeight * Mathf.Max(0.01f, RenderCamera.aspect);
                RenderCamera.projectionMatrix = CreateOrthographic(halfWidth, halfHeight,
                    RenderCamera.nearClipPlane, RenderCamera.farClipPlane);
            }
            else
            {
                RenderCamera.projectionMatrix = CreatePerspective(RenderCamera.fieldOfView,
                    Mathf.Max(0.01f, RenderCamera.aspect), RenderCamera.nearClipPlane, RenderCamera.farClipPlane);
            }
            RenderCamera.enabled = true;
        }

        // MateEngine's generated Unity reference omits Matrix4x4.Perspective /
        // Ortho and Camera.orthographic setters. Custom projection matrices let
        // the real host camera switch projection without relying on those APIs.
        private static Matrix4x4 CreatePerspective(float fieldOfView, float aspect, float near, float far)
        {
            float cotangent = 1f / Mathf.Tan(fieldOfView * (float)System.Math.PI / 360f);
            var matrix = new Matrix4x4();
            matrix.m00 = cotangent / aspect;
            matrix.m11 = cotangent;
            matrix.m22 = -(far + near) / (far - near);
            matrix.m23 = -(2f * far * near) / (far - near);
            matrix.m32 = -1f;
            return matrix;
        }

        private static Matrix4x4 CreateOrthographic(float halfWidth, float halfHeight, float near, float far)
        {
            var matrix = new Matrix4x4();
            matrix.m00 = 1f / halfWidth;
            matrix.m11 = 1f / halfHeight;
            matrix.m22 = -2f / (far - near);
            matrix.m23 = -(far + near) / (far - near);
            matrix.m33 = 1f;
            return matrix;
        }

        private static bool IsLegacyCameraAtRest(Transform node)
        {
            Transform first = node.parent;
            Transform root = first == null ? null : first.parent;
            return (root == null || root.localPosition == Vector3.zero && root.localRotation == Quaternion.Euler(0, 180, 0)) &&
                (first == null || first.localPosition == Vector3.zero && first.localRotation == Quaternion.identity) &&
                node.localPosition == Vector3.zero && node.localRotation == Quaternion.identity;
        }

        private void HideLegacyPreview()
        {
            if (PreviewRoot != null) PreviewRoot.SetActive(false);
            if (RenderCamera != null && RenderCamera.targetTexture == OwnedPreviewTexture) RenderCamera.targetTexture = null;
            if (OwnedPreviewTexture != null)
            {
                OwnedPreviewTexture.Release();
                Destroy(OwnedPreviewTexture);
                OwnedPreviewTexture = null;
            }
        }

        void OnDestroy()
        {
            RestoreFrontView();
            HideLegacyPreview();
        }
    }
}
