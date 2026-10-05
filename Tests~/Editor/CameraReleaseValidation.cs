using System;
using System.IO;
using System.Reflection;
using CustomDancePlayer;
using UnityEditor;
using UnityEngine;

public static class CameraReleaseValidation
{
    private static void Invoke(object target, string method) { target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null); }
    public static void Run()
    {
        try
        {
            var bundle = AssetBundle.LoadFromFile(Path.Combine(Directory.GetCurrentDirectory(), "customdanceplayer.bundle"));
            if (bundle == null) throw new Exception("UI bundle failed to load");
            var prefab = bundle.LoadAllAssets<GameObject>()[0];
            var instance = UnityEngine.Object.Instantiate(prefab);
            var ui = instance.GetComponentInChildren<DancePlayerUIManager>(true);
            if (ui == null) throw new Exception("UI manager missing");
            Invoke(ui.avatarHelper, "Start");
            Invoke(ui.playerCore, "Start");
            Invoke(ui, "Start");
            var sync = ui.GetComponentInChildren<DanceCameraSync>(true);
            if (sync == null || ui.EnableMMDCamera == null || ui.MMDCameraScaleSlider == null)
                throw new Exception("Demo controls/components not created");
            if (sync.RenderCamera == null || sync.RenderCamera.targetTexture == null || sync.PreviewRoot == null)
                throw new Exception("Preview references missing");
            if (sync.enabled) throw new Exception("Camera demo should be opt-in");
            ui.EnableMMDCamera.isOn = true;
            if (!sync.enabled) throw new Exception("Toggle not bound");
            Invoke(sync, "OnEnable");
            Invoke(sync, "LateUpdate");
            if (sync.RenderCamera.enabled) throw new Exception("Camera should be idle without avatar");
            var avatar = new GameObject("CameraValidationAvatar");
            typeof(DanceAvatarHelper).GetProperty("CurrentAvatar").SetValue(ui.avatarHelper, avatar);
            ui.avatarHelper.SetupMMDCameraHierarchy();
            var motionCamera = avatar.transform.Find("Camera_root/Camera_root_1/Camera").GetComponent<Camera>();
            motionCamera.transform.position = new Vector3(1, 2, -4);
            motionCamera.transform.rotation = Quaternion.identity;
            motionCamera.fieldOfView = 42;
            DanceSettingsHandler.Instance.data.isPlaying = true;
            ui.MMDCameraScaleSlider.value = 2;
            Invoke(sync, "LateUpdate");
            if (!sync.RenderCamera.enabled || Vector3.Distance(sync.RenderCamera.transform.position, new Vector3(2,4,-8)) > 0.001f)
                throw new Exception("Camera position/scale not synchronized");
            if (ui.MMDCameraScaleValueText == null || ui.MMDCameraScaleValueText.text != "2.0x") throw new Exception("Scale value label not bound");
            if (Mathf.Abs(sync.RenderCamera.fieldOfView - 42) > 0.001f) throw new Exception("FOV not synchronized");
            if (sync.PreviewStatus.gameObject.activeSelf || !sync.PreviewImage.enabled) throw new Exception("Preview status incorrect");
            DanceSettingsHandler.Instance.data.isPlaying = false;
            Invoke(sync, "LateUpdate");
            if (sync.RenderCamera.enabled || sync.PreviewImage.enabled) throw new Exception("Stop did not clear preview");
            var close = sync.PreviewRoot.transform.Find("PreviewPanel/Close").GetComponent<UnityEngine.UI.Button>();
            close.onClick.Invoke();
            if (sync.enabled || ui.EnableMMDCamera.isOn) throw new Exception("Close not bound");
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../unity-camera-smoke.json"));
            File.WriteAllText(output, "{\"pass\":true,\"checks\":[\"bundle instantiate\",\"runtime controls\",\"toggle binding\",\"idle without avatar\",\"camera position and scale\",\"FOV\",\"stop clears preview\",\"close button\"],\"scope\":\"Unity Editor prefab/component smoke, not official gameplay\"}");
            UnityEngine.Object.DestroyImmediate(instance);
            UnityEngine.Object.DestroyImmediate(avatar);
            bundle.Unload(true);
            Debug.Log("CAMERA_RELEASE_VALIDATION_PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
}
