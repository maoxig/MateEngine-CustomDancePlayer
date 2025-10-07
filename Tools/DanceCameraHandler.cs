//using System;
//using System.Collections;
//using UnityEngine;

//namespace CustomDancePlayer
//{
//    public class DanceCameraHandler : MonoBehaviour
//    {
//        [Header("References")]
//        public DanceAvatarHelper avatarHelper; // 用于获取 CurrentAvatar
//        public DancePlayerCore playerCore;    // 用于获取 IsPlaying
//        public Camera mainCamera;              // 主相机引用
//        public Camera perspectiveCamera;       // 透视相机引用，用于属性模板

//        // Cached camera properties
//        private Vector3 cachedPosition;
//        private Quaternion cachedRotation;
//        private bool cachedOrthographic;
//        private float cachedOrthographicSize;
//        private float cachedFieldOfView;
//        private float cachedNearClipPlane;
//        private float cachedFarClipPlane;
//        private bool hasCachedProperties = false;

//        private void Start()
//        {
//            if (mainCamera == null)
//            {
//                mainCamera = Camera.main;
//                if (mainCamera == null)
//                {
//                    Debug.LogError("[DanceCameraHandler] No main camera assigned and Camera.main is null. Disabling.");
//                    enabled = false;
//                    return;
//                }
//            }

//            if (avatarHelper == null)
//            {
//                Debug.LogError("[DanceCameraHandler] avatarHelper is not assigned. Disabling.");
//                enabled = false;
//                return;
//            }

//            if (playerCore == null)
//            {
//                Debug.LogError("[DanceCameraHandler] playerCore is not assigned. Disabling.");
//                enabled = false;
//                return;
//            }

//            if (perspectiveCamera == null)
//            {
//                Debug.LogWarning("[DanceCameraHandler] perspectiveCamera not assigned. Perspective properties may not be set correctly.");
//            }

//            // Cache main camera properties on start
//            CacheMainCameraProperties();
//        }

//        // 将 StartCoroutine(SynchronizeCamera()); 替换为 SynchronizeCamera();
//        private void OnEnable()
//        {
//            // When enabled, synchronize if dancing
//            if (playerCore != null && playerCore.IsPlaying)
//            {
//                SynchronizeCamera();
//            }
//        }

//        private void OnDisable()
//        {
//            // Restore camera when disabled
//            RestoreMainCamera();
//        }

//        // Cache main camera's transform and properties
//        private void CacheMainCameraProperties()
//        {
//            if (mainCamera != null)
//            {
//                cachedPosition = mainCamera.transform.position;
//                cachedRotation = mainCamera.transform.rotation;
//                cachedOrthographic = mainCamera.orthographic;
//                cachedOrthographicSize = mainCamera.orthographicSize;
//                cachedFieldOfView = mainCamera.fieldOfView;
//                cachedNearClipPlane = mainCamera.nearClipPlane;
//                cachedFarClipPlane = mainCamera.farClipPlane;
//                hasCachedProperties = true;
//#if DEBUG
//                Debug.Log("[DanceCameraHandler] Main camera properties cached.");
//#endif
//            }
//        }

//        // Synchronize main camera with cinematic camera properties
//        public void SynchronizeCamera()
//        {
//            if (!enabled || !isActiveAndEnabled) return;

//            if (playerCore.IsPlaying && avatarHelper.IsAvatarAvailable() && avatarHelper.CurrentAvatar != null)
//            {
//                Transform cinematicCameraTransform = avatarHelper.CurrentAvatar.transform.Find("Camera_root/Camera_root_1/Camera");
//                if (cinematicCameraTransform != null)
//                {
//                    Camera cinematicCamera = cinematicCameraTransform.GetComponent<Camera>();
//                    if (cinematicCamera != null)
//                    {

//                        mainCamera.orthographic = false;
//                        mainCamera.fieldOfView = perspectiveCamera != null ? perspectiveCamera.fieldOfView : cinematicCamera.fieldOfView;
//                        mainCamera.nearClipPlane = perspectiveCamera != null ? perspectiveCamera.nearClipPlane : cinematicCamera.nearClipPlane;
//                        mainCamera.farClipPlane = perspectiveCamera != null ? perspectiveCamera.farClipPlane : cinematicCamera.farClipPlane;

//                        mainCamera.transform.position = cinematicCameraTransform.position;
//                        mainCamera.transform.rotation = cinematicCameraTransform.rotation;

//#if DEBUG
//                        Debug.Log("[DanceCameraHandler] Main camera synchronized with cinematic camera.");
//#endif
//                    }
//                    else
//                    {
//                        Debug.LogWarning("[DanceCameraHandler] Cinematic camera component not found.");
//                    }
//                }
//                else
//                {
//                    Debug.LogWarning("[DanceCameraHandler] Cinematic camera transform not found at Camera_root/Camera_root_1/Camera.");
//                }
//            }
//            else
//            {
//                // Not dancing, restore main camera
//                RestoreMainCamera();
//            }
//        }

//        // Restore main camera to its cached properties
//        public void RestoreMainCamera()
//        {
//            if (hasCachedProperties && mainCamera != null)
//            {
//                mainCamera.transform.position = cachedPosition;
//                mainCamera.transform.rotation = cachedRotation;
//                mainCamera.orthographic = cachedOrthographic;
//                mainCamera.orthographicSize = cachedOrthographicSize;
//                mainCamera.fieldOfView = cachedFieldOfView;
//                mainCamera.nearClipPlane = cachedNearClipPlane;
//                mainCamera.farClipPlane = cachedFarClipPlane;

//#if DEBUG
//                Debug.Log("[DanceCameraHandler] Main camera restored to original properties.");
//#endif
//            }
//        }

//        private void Update()
//        {
//            // Check dance state and synchronize or restore accordingly
//            if (playerCore.IsPlaying)
//            {
//                SynchronizeCamera();
//            }
//            else
//            {
//                RestoreMainCamera();
//            }
//        }

//        // Existing method from reference code
//        public static void AddCinematicCamera(Transform modelTransform)
//        {
//            // Step 1: 查找并移除已有的Camera_root/Camera_root_1/Camera层级
//            Transform cameraNode = modelTransform.Find("Camera_root/Camera_root_1/Camera");
//            if (cameraNode != null)
//            {
//                Transform cameraRoot = cameraNode.parent?.parent;
//                if (cameraRoot != null && cameraRoot.name == "Camera_root")
//                {
//                    UnityEngine.Object.DestroyImmediate(cameraRoot.gameObject);
//#if DEBUG
//                    Debug.Log("[DanceCameraHandler] 已移除旧的Camera_root层级。");
//#endif
//                }
//                cameraNode = null;
//            }

//            // Step 2: 创建新的Camera_root层级
//            Transform root = new GameObject("Camera_root").transform;
//            root.SetParent(modelTransform);
//            root.localPosition = Vector3.zero;
//            root.localRotation = Quaternion.Euler(0, 180, 0);

//            Transform child1 = new GameObject("Camera_root_1").transform;
//            child1.SetParent(root);
//            child1.localPosition = Vector3.zero;
//            child1.localRotation = Quaternion.identity;

//            cameraNode = new GameObject("Camera").transform;
//            cameraNode.SetParent(child1);
//            cameraNode.localPosition = Vector3.zero;
//            cameraNode.localRotation = Quaternion.identity;

//            // Step 3: 添加并禁用Camera组件
//            Camera cameraComponent = cameraNode.gameObject.AddComponent<Camera>();
//            cameraComponent.enabled = false;
//#if DEBUG
//            Debug.Log("[DanceCameraHandler] 已创建新的Camera_root层级并添加相机。");
//#endif
//        }
//    }
//}


using System;
using System.Reflection;
using UnityEngine;

namespace CustomDancePlayer
{
    public class DanceCameraHandler : MonoBehaviour
    {
        void Start()
        {
            try
            {
                Camera cam = Camera.main;
                if (cam == null)
                {
                    Debug.LogError("[DanceCameraHandler] 未找到主相机（Camera.main == null）");
                    return;
                }

                Debug.Log("[DanceCameraHandler] 检测到主相机: " + cam.name);

                // 反射尝试修改私有字段 m_Orthographic
                FieldInfo fOrthographic = typeof(Camera).GetField("m_Orthographic", BindingFlags.NonPublic | BindingFlags.Instance);
                if (fOrthographic != null)
                {
                    fOrthographic.SetValue(cam, false);
                    Debug.Log("[DanceCameraHandler] 已通过字段反射修改 orthographic = false");
                }
                else
                {
                    Debug.LogWarning("[DanceCameraHandler] 找不到 m_Orthographic 字段，尝试调用 set_orthographic_Injected 方法...");

                    MethodInfo setMethod = typeof(Camera).GetMethod("set_orthographic_Injected", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (setMethod != null)
                    {
                        // 获取底层指针（类似 cam.GetCachedPtr()）
                        var ptr = typeof(UnityEngine.Object)
                            .GetMethod("GetCachedPtr", BindingFlags.NonPublic | BindingFlags.Instance)
                            ?.Invoke(cam, null);

                        if (ptr != null)
                        {
                            setMethod.Invoke(cam, new object[] { ptr, false });
                            Debug.Log("[DanceCameraHandler] 已通过 set_orthographic_Injected 成功修改 orthographic = false");
                        }
                        else
                        {
                            Debug.LogError("[DanceCameraHandler] 无法获取底层指针 ptr");
                        }
                    }
                    else
                    {
                        Debug.LogError("[DanceCameraHandler] 没找到可用的修改方法（此 UnityEngine.CoreModule 版本可能已去除 setter）");
                    }
                }

                // 输出最终状态
                Debug.Log("[DanceCameraHandler] 修改完成，当前 orthographic = " + cam.orthographic);
            }
            catch (Exception ex)
            {
                Debug.LogError("[DanceCameraHandler] 修改相机时出错: " + ex);
            }
        }

        public void Switch()
        {
            try
            {
                Camera cam = Camera.main;
                if (cam == null)
                {
                    Debug.LogError("[DanceCameraHandler] 未找到主相机（Camera.main == null）");
                    return;
                }

                Debug.Log("[DanceCameraHandler] 检测到主相机: " + cam.name);

                // 反射尝试修改私有字段 m_Orthographic
                FieldInfo fOrthographic = typeof(Camera).GetField("m_Orthographic", BindingFlags.NonPublic | BindingFlags.Instance);
                if (fOrthographic != null)
                {
                    fOrthographic.SetValue(cam, false);
                    Debug.Log("[DanceCameraHandler] 已通过字段反射修改 orthographic = false");
                }
                else
                {
                    Debug.LogWarning("[DanceCameraHandler] 找不到 m_Orthographic 字段，尝试调用 set_orthographic_Injected 方法...");

                    MethodInfo setMethod = typeof(Camera).GetMethod("set_orthographic_Injected", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (setMethod != null)
                    {
                        // 获取底层指针（类似 cam.GetCachedPtr()）
                        var ptr = typeof(UnityEngine.Object)
                            .GetMethod("GetCachedPtr", BindingFlags.NonPublic | BindingFlags.Instance)
                            ?.Invoke(cam, null);

                        if (ptr != null)
                        {
                            setMethod.Invoke(cam, new object[] { ptr, false });
                            Debug.Log("[DanceCameraHandler] 已通过 set_orthographic_Injected 成功修改 orthographic = false");
                        }
                        else
                        {
                            Debug.LogError("[DanceCameraHandler] 无法获取底层指针 ptr");
                        }
                    }
                    else
                    {
                        Debug.LogError("[DanceCameraHandler] 没找到可用的修改方法（此 UnityEngine.CoreModule 版本可能已去除 setter）");
                    }
                }

                // 输出最终状态
                Debug.Log("[DanceCameraHandler] 修改完成，当前 orthographic = " + cam.orthographic);
            }
            catch (Exception ex)
            {
                Debug.LogError("[DanceCameraHandler] 修改相机时出错: " + ex);
            }
        }
    }
}
