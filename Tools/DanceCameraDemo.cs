using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CustomDancePlayer
{
    // Extends the paired legacy prefab at runtime, preserving its script GUIDs.
    // This also makes the camera demo available with the existing UI bundle.
    public static class DanceCameraDemo
    {
        public static void EnsureCreated(DancePlayerUIManager ui)
        {
            if (ui.GetComponentInChildren<DanceCameraSync>(true) != null) return;
            if (ui.TargetCanvas == null || ui.SettingsScrollRect == null ||
                ui.SettingsScrollRect.content == null || ui.EnableWindowFollow == null)
            {
                Debug.LogWarning("[CustomDancePlayer] Camera demo requires the paired UI prefab.");
                return;
            }

            var content = ui.SettingsScrollRect.content;
            float bottom = content.rect.height;
            var toggleObject = Object.Instantiate(ui.EnableWindowFollow.gameObject, content, false);
            toggleObject.name = "EnableMMDCamera";
            var toggleRect = toggleObject.GetComponent<RectTransform>();
            toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(0, 1);
            toggleRect.pivot = new Vector2(0.5f, 0.5f);
            toggleRect.anchoredPosition = new Vector2(130, -bottom - 16);
            SetLabel(toggleObject, "MMD camera demo");
            ui.EnableMMDCamera = toggleObject.GetComponent<Toggle>();
            ui.EnableMMDCamera.onValueChanged.RemoveAllListeners();

            if (ui.AnimationStartDelaySlider != null)
            {
                var sliderObject = Object.Instantiate(ui.AnimationStartDelaySlider.gameObject, content, false);
                sliderObject.name = "MMDCameraScale";
                var sliderRect = sliderObject.GetComponent<RectTransform>();
                sliderRect.anchorMin = sliderRect.anchorMax = new Vector2(0, 1);
                sliderRect.pivot = new Vector2(0.5f, 0.5f);
                sliderRect.anchoredPosition = new Vector2(130, -bottom - 76);
                ui.MMDCameraScaleSlider = sliderObject.GetComponent<Slider>();
                ui.MMDCameraScaleSlider.onValueChanged.RemoveAllListeners();
                ui.MMDCameraScaleSlider.minValue = 0.1f;
                ui.MMDCameraScaleSlider.maxValue = 10f;
                ui.MMDCameraScaleSlider.wholeNumbers = false;
                var originalText = ui.AnimationStartDelayValueText;
                if (originalText != null && originalText.transform.parent != content)
                {
                    var originalHeader = originalText.transform.parent;
                    var header = Object.Instantiate(originalHeader.gameObject, content, false);
                    header.name = "MMDCameraScaleLabel";
                    var headerRect = header.GetComponent<RectTransform>();
                    headerRect.anchorMin = headerRect.anchorMax = new Vector2(0, 1);
                    headerRect.pivot = new Vector2(0.5f, 0.5f);
                    headerRect.anchoredPosition = new Vector2(130, -bottom - 48);
                    string path = GetChildPath(originalHeader, originalText.transform);
                    ui.MMDCameraScaleValueText = header.transform.Find(path)?.GetComponent<TMP_Text>();
                    foreach (var label in header.GetComponentsInChildren<TMP_Text>(true))
                        if (label != ui.MMDCameraScaleValueText) label.text = "Camera scale";
                    foreach (var label in header.GetComponentsInChildren<Text>(true)) label.text = "Camera scale";
                }
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bottom + 96);
            }
            else content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bottom + 40);

            var root = new GameObject("DanceCameraDemo");
            root.SetActive(false);
            root.transform.SetParent(ui.transform, false);
            var sync = root.AddComponent<DanceCameraSync>();
            sync.AvatarHelper = ui.avatarHelper;
            var cameraObject = new GameObject("DancePreviewCamera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            if (Camera.main != null)
            {
                camera.nearClipPlane = Camera.main.nearClipPlane;
                camera.farClipPlane = Camera.main.farClipPlane;
                camera.backgroundColor = Camera.main.backgroundColor;
                camera.clearFlags = Camera.main.clearFlags;
            }
            camera.enabled = false;
            var texture = new RenderTexture(960, 540, 24) { name = "DanceCameraDemoTexture" };
            texture.Create();
            camera.targetTexture = texture;
            sync.RenderCamera = camera;
            sync.OwnedPreviewTexture = texture;

            var preview = new GameObject("DanceCameraPreview", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            preview.transform.SetParent(root.transform, false);
            var canvas = preview.GetComponent<Canvas>();
            // New Canvas defaults to ScreenSpaceOverlay. X3.3 strips its setter.
            canvas.sortingOrder = 200;
            var scaler = preview.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var panel = new GameObject("PreviewPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(preview.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-16, -16);
            rect.sizeDelta = new Vector2(448, 284);
            panel.GetComponent<Image>().color = new Color(0.10f, 0.10f, 0.14f, 0.96f);
            var imageObject = new GameObject("CameraImage", typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(panel.transform, false);
            var imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero; imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = new Vector2(8, 8); imageRect.offsetMax = new Vector2(-8, -34);
            var image = imageObject.GetComponent<RawImage>();
            image.texture = texture; image.raycastTarget = false;
            sync.PreviewImage = image;
            var font = ui.CurrentPlayText != null ? ui.CurrentPlayText.font : null;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var title = CreateText(panel.transform, "MMD camera demo", font);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0, 1); titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(12, -30); titleRect.offsetMax = new Vector2(-44, -4);
            var status = CreateText(imageObject.transform, "Waiting for dance camera motion...", font);
            status.alignment = TextAnchor.MiddleCenter;
            status.rectTransform.anchorMin = Vector2.zero; status.rectTransform.anchorMax = Vector2.one;
            status.rectTransform.offsetMin = Vector2.zero; status.rectTransform.offsetMax = Vector2.zero;
            sync.PreviewStatus = status;
            var close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            close.transform.SetParent(panel.transform, false);
            var closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1, 1);
            closeRect.pivot = new Vector2(1, 1); closeRect.anchoredPosition = new Vector2(-8, -4);
            closeRect.sizeDelta = new Vector2(28, 26);
            close.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f);
            close.GetComponent<Button>().onClick.AddListener(() => ui.EnableMMDCamera.isOn = false);
            var closeText = CreateText(close.transform, "×", font);
            closeText.alignment = TextAnchor.MiddleCenter;
            closeText.rectTransform.anchorMin = Vector2.zero; closeText.rectTransform.anchorMax = Vector2.one;
            closeText.rectTransform.offsetMin = closeText.rectTransform.offsetMax = Vector2.zero;
            sync.PreviewRoot = preview;
            sync.enabled = false;
            preview.SetActive(false);
            root.SetActive(true);
            Debug.Log("[CustomDancePlayer] MMD camera demo ready (toggle, scale, preview camera).");
        }

        private static Text CreateText(Transform parent, string value, Font font)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font; text.fontSize = 14; text.color = Color.white;
            text.text = value; text.raycastTarget = false;
            return text;
        }

        private static void SetLabel(GameObject root, string value)
        {
            var tmp = root.GetComponentsInChildren<TMP_Text>(true);
            var text = root.GetComponentsInChildren<Text>(true);
            if (tmp.Length > 0) tmp[0].text = value;
            else if (text.Length > 0) text[0].text = value;
        }

        private static string GetChildPath(Transform root, Transform child)
        {
            var path = child.name;
            while (child.parent != root) { child = child.parent; path = child.name + "/" + path; }
            return path;
        }
    }
}
