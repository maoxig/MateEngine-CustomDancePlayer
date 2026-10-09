using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CustomDancePlayer
{
    public static class DanceUi
    {
        public static Font Font;
        public static Sprite PanelSprite;
        public static Sprite ControlSprite;
        private static AssetBundle theme;
        private static GameObject themePrefab;
        public static IEnumerator PrepareTheme(string directory)
        {
            if (theme == null)
            {
                var create = AssetBundle.LoadFromFileAsync(System.IO.Path.Combine(directory, "Assets", "dance-theme.bundle"));
                yield return create;
                theme = create.assetBundle;
            }
            if (theme == null) yield break;
            var assets = theme.LoadAllAssetsAsync();
            yield return assets;
            foreach (var asset in assets.allAssets)
            {
                var sprite = asset as Sprite;
                if (sprite != null) { if (sprite.name == "panel") PanelSprite = sprite; if (sprite.name == "control") ControlSprite = sprite; }
                var prefab = asset as GameObject;
                if (prefab != null && themePrefab == null) themePrefab = prefab;
            }
        }
        public static GameObject CreateCanvas()
        {
            if (theme == null) throw new System.IO.InvalidDataException("Dance theme resources are missing.");
            if (themePrefab == null) themePrefab = theme.LoadAllAssets<GameObject>()[0];
            var root=UnityEngine.Object.Instantiate(themePrefab);root.name="DancePlayerWindow";UnityEngine.Object.DontDestroyOnLoad(root);return root;
        }
        public static readonly Color Background = new Color(0.047f, 0.059f, 0.10f, 0.98f);
        public static readonly Color Surface = new Color(0.095f, 0.115f, 0.17f, 1);
        public static readonly Color Accent = new Color(0.56f, 0.53f, 0.91f, 1);
        public static void Init(Font fallback, Sprite sprite)
        {
            if (Font == null) Font = fallback; PanelSprite = sprite;
            if (theme == null) theme = AssetBundle.LoadFromFile(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(DanceUi).Assembly.Location),"Assets","dance-theme.bundle"));
            if (theme != null && (PanelSprite == null || ControlSprite == null)) foreach (var asset in theme.LoadAllAssets<Sprite>()) { if (asset.name == "panel") PanelSprite = asset; if (asset.name == "control") ControlSprite = asset; }
            EnsureFont(fallback);
        }
        private static void EnsureFont(Font fallback)
        {
            if (Font != null) return;
            Font = fallback;
            var method = typeof(Font).GetMethod("CreateDynamicFontFromOSFont", new[] { typeof(string[]), typeof(int) });
            if (method != null) try { Font = (Font)method.Invoke(null, new object[] { new[] { "Microsoft YaHei", "Segoe UI", "Arial" }, 16 }); } catch { }
            if (Font == null) Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        public static GameObject Node(string name, Transform parent)
        { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go; }
        public static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        public static Image Image(GameObject go, Color color)
        { var image = go.AddComponent<Image>(); image.color = color; var sprite = go.name == "PlayerPanel" ? PanelSprite : ControlSprite; if (sprite != null) { image.sprite = sprite; image.type = UnityEngine.UI.Image.Type.Sliced; } return image; }
        public static GameObject Row(Transform parent, float height = 36)
        {
            var go = Node("Row", parent); var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8; layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
            Size(go, -1, height); return go;
        }
        public static void Size(GameObject go, float width = -1, float height = -1, float flexible = 0)
        {
            var e = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            e.preferredWidth = width; e.preferredHeight = height; e.flexibleWidth = flexible;
            e.flexibleHeight = 0; if (height >= 0) e.minHeight = height;
        }
        public static Text Label(Transform parent, string text, int size = 16, float width = -1)
        {
            var go = Node("Label", parent); var label = go.AddComponent<Text>();
            label.font = Font; label.fontSize = size; label.color = new Color(0.93f, 0.95f, 0.99f);
            label.text = text; label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            Size(go, width, -1, width < 0 ? 1 : 0); return label;
        }
        public static Button Button(Transform parent, string text, Action action, float width = -1, bool primary = false)
        {
            var go = Node("Button", parent); Image(go, primary ? Accent : Surface);
            var button = go.AddComponent<Button>(); var colors = button.colors;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f); colors.pressedColor = new Color(0.8f, 0.85f, 0.9f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f); button.colors = colors;
            var label = Label(go.transform, text, 15); label.alignment = TextAnchor.MiddleCenter;
            Stretch(label.rectTransform); label.rectTransform.offsetMin = new Vector2(8, 0); label.rectTransform.offsetMax = new Vector2(-8, 0);
            Size(go, width, 34, width < 0 ? 1 : 0); button.onClick.AddListener(() => action()); return button;
        }
        public static InputField Input(Transform parent, string placeholder, Action<string> changed)
        {
            var go = Node("Input", parent); Image(go, Surface); Size(go, -1, 36, 1);
            var input = go.AddComponent<InputField>(); var text = Label(go.transform, "", 15); Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(10, 2); text.rectTransform.offsetMax = new Vector2(-10, -2);
            input.textComponent = text; var hint = Label(go.transform, placeholder, 15); Stretch(hint.rectTransform);
            hint.rectTransform.offsetMin = new Vector2(10, 2); hint.rectTransform.offsetMax = new Vector2(-10, -2);
            hint.color = new Color(0.60f, 0.65f, 0.73f); input.placeholder = hint;
            if (changed != null) input.onValueChanged.AddListener(value => changed(value)); return input;
        }
        public static Button ModeButton(Transform parent, bool expand, Action action)
        {
            var button=Button(parent,"",action,32);
            button.name=expand ? "ExpandPlayer" : "MiniPlayer";
            Action<float,float,float,float> stroke=(x,y,w,h)=>
            {
                var line=Node("Icon",button.transform);var image=Image(line,new Color(0.90f,0.90f,0.98f));image.raycastTarget=false;
                var rect=line.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(0.5f,0.5f);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(w,h);
            };
            if(expand)
            {
                foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1}) {stroke(x*7,y*4,2,8);stroke(x*4,y*7,8,2);}
            }
            else
            {
                stroke(0,7,18,2);stroke(-8,0,2,14);stroke(8,0,2,14);stroke(0,-7,18,2);
                stroke(4,-3,8,5);
            }
            AttachTooltip(button, DanceLocale.T(expand?"window.expand":"window.mini"));
            return button;
        }
        public static void AttachTooltip(Button button, string value, float width = 150, float height = 28)
        {
            Canvas parent=button.GetComponentInParent<Canvas>();
            var hint=Node("Tooltip",parent==null?button.transform:parent.transform);Image(hint,Background).raycastTarget=false;
            if(PanelSprite!=null)hint.GetComponent<Image>().sprite=PanelSprite;
            if(parent!=null){var overlay=hint.AddComponent<Canvas>();overlay.overrideSorting=true;overlay.sortingOrder=parent.sortingOrder+10;}
            var box=hint.GetComponent<RectTransform>();box.anchorMin=box.anchorMax=new Vector2(0.5f,0.5f);box.pivot=new Vector2(0,1);box.sizeDelta=new Vector2(width,height);
            var text=Label(hint.transform,value,12);text.alignment=height>28?TextAnchor.UpperLeft:TextAnchor.MiddleCenter;Stretch(text.rectTransform);text.raycastTarget=false;
            text.rectTransform.offsetMin=new Vector2(10,6);text.rectTransform.offsetMax=new Vector2(-10,-6);
            var tooltip=button.gameObject.AddComponent<DanceTooltip>();tooltip.Hint=hint;hint.SetActive(false);
            if(button.GetComponentInChildren<Text>().text=="?")button.onClick.AddListener(tooltip.ShowNow);
        }
        public static Toggle ToggleInRow(Transform parent,bool value,Action<bool> changed)
        {
            var slot=Node("ToggleSlot",parent);Size(slot,28,36);
            var box=Node("Toggle",slot.transform);var rect=box.GetComponent<RectTransform>();
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(28,28);
            var background=Image(box,value?Accent:new Color(.30f,.33f,.43f));
            var toggle=box.AddComponent<Toggle>();toggle.transition=Selectable.Transition.None;
            var mark=Node("Check",box.transform);var check=Label(mark.transform,"✓",21);Stretch(mark.GetComponent<RectTransform>());
            check.alignment=TextAnchor.MiddleCenter;check.color=Color.white;check.raycastTarget=false;
            toggle.targetGraphic=background;toggle.graphic=check;toggle.isOn=value;
            toggle.onValueChanged.AddListener(v=>{background.color=v?Accent:new Color(.30f,.33f,.43f);changed(v);});return toggle;
        }
        public static Toggle Toggle(Transform parent,string label,bool value,Action<bool> changed)
        {
            var row=Row(parent,36);var toggle=ToggleInRow(row.transform,value,changed);
            var caption=Label(row.transform,label);caption.raycastTarget=true;
            var button=caption.gameObject.AddComponent<Button>();button.transition=Selectable.Transition.None;button.onClick.AddListener(()=>toggle.isOn=!toggle.isOn);return toggle;
        }
        public static Slider Slider(Transform parent, float min, float max, float value, Action<float> changed)
        {
            var go = Node("Slider", parent); Size(go, -1, 20, 1);
            var slider = go.AddComponent<Slider>();
            var track = Node("Track", go.transform); Image(track, new Color(0.21f,0.23f,0.32f)); var rect=track.GetComponent<RectTransform>();
            rect.anchorMin=new Vector2(0,0.5f);rect.anchorMax=new Vector2(1,0.5f);rect.sizeDelta=new Vector2(0,4);
            var fill = Node("Fill", track.transform); Image(fill, Accent); Stretch(fill.GetComponent<RectTransform>());
            var handleArea=Node("HandleArea",go.transform);var area=handleArea.GetComponent<RectTransform>();area.anchorMin=new Vector2(0,0.5f);area.anchorMax=new Vector2(1,0.5f);area.sizeDelta=new Vector2(0,10);
            var handle = Node("Handle", handleArea.transform); Image(handle, new Color(0.82f,0.81f,0.98f));
            var handleRect=handle.GetComponent<RectTransform>(); handleRect.sizeDelta = new Vector2(10,0);
            slider.fillRect = fill.GetComponent<RectTransform>(); slider.handleRect = handle.GetComponent<RectTransform>(); slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = min; slider.maxValue = max; slider.value = value; slider.onValueChanged.AddListener(v => changed(v)); return slider;
        }
        public static RectTransform Scroll(Transform parent, out ScrollRect scroll)
        {
            var go = Node("Scroll", parent); var element = go.AddComponent<LayoutElement>(); element.flexibleHeight = 1; element.minHeight = 80;
            scroll = go.AddComponent<ScrollRect>(); scroll.horizontal = false;
            Image(go, new Color(0.14f,0.15f,0.23f));
            scroll.scrollSensitivity = 36; scroll.decelerationRate = 0.08f; scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Node("Viewport", go.transform); Stretch(viewport.GetComponent<RectTransform>()); viewport.AddComponent<RectMask2D>(); Image(viewport, new Color(0,0,0,0));
            viewport.GetComponent<Image>().color = new Color(0.065f,0.075f,0.12f,0.96f);
            viewport.GetComponent<RectTransform>().offsetMin = new Vector2(6,6);
            viewport.GetComponent<RectTransform>().offsetMax = new Vector2(-20,-6);
            var bar=Node("Scrollbar",go.transform);var barRect=bar.GetComponent<RectTransform>();barRect.anchorMin=new Vector2(1,0);barRect.anchorMax=Vector2.one;barRect.pivot=new Vector2(1,0.5f);barRect.sizeDelta=new Vector2(8,0);Image(bar,new Color(0.14f,0.15f,0.23f));
            var handle=Node("Thumb",bar.transform);Stretch(handle.GetComponent<RectTransform>());var handleImage=Image(handle,new Color(0.45f,0.44f,0.64f));
            var scrollbar=bar.AddComponent<Scrollbar>();scrollbar.handleRect=handle.GetComponent<RectTransform>();scrollbar.targetGraphic=handleImage;scrollbar.direction=Scrollbar.Direction.BottomToTop;scroll.verticalScrollbar=scrollbar;
            var content = Node("Content", viewport.transform); var rect = content.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0,1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f,1); rect.sizeDelta = Vector2.zero;
            var layout = content.AddComponent<VerticalLayoutGroup>(); layout.spacing = 6; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            var fitter = content.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport.GetComponent<RectTransform>(); scroll.content = rect; return rect;
        }
    }
}
