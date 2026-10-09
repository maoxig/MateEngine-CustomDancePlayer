using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CustomDancePlayer
{ // Manages UI interactions and updates
    [DefaultExecutionOrder(-20000)]
    public class DancePlayerUIManager : MonoBehaviour
    {
        public Canvas TargetCanvas;
        [Header("Main Panel")]
        public Text CurrentPlayText;
        public Slider ProgressSlider;
        public Dropdown DanceFileDropdown;
        public Button RefreshBtn;
        public Button PrevBtn;
        public Button PlayPauseBtn;
        public Button NextBtn;
        public Button StopBtn;
        public Button PlayModeBtn;
        public TMP_Text PlayModeText;
        public TMP_Text AvatarStatusText;
        public TMP_Text ToggleKeyText;


        public Button AdvancedToggleBtn;
        public TMP_Text AdvancedToggleBtnText;
        public GameObject MainPanelRoot;
        public GameObject SettingsPanelRoot;
        public ScrollRect SettingsScrollRect;

        [Header("Advanced Settings")]
        public Slider VolumeSlider;
        public TMP_Text VolumeValueText;
        public Slider AnimationStartDelaySlider;
        public TMP_Text AnimationStartDelayValueText;
        public Toggle EnableDanceCinematicCamera;
        public Toggle EnableUIPanelFollow;
        public Toggle EnableShadowFollow;
        public Toggle EnableWindowFollow;
        public Toggle EnableCameraDistanceKeep;
        public Toggle AutoPlayOnStartToggle;
        public Toggle HidePanelOnStartToggle;
        public Toggle EnableGlobalHotkey;
        public Toggle EnableMMDCamera;
        public Slider MMDCameraScaleSlider;
        public TMP_Text MMDCameraScaleValueText;

        // Core Components
        [Header("Core Components")]
        public DancePlayerCore playerCore;
        public DanceAvatarHelper avatarHelper;
        public DanceResourceManager resourceManager;

        public DanceCameraSync CameraSync { get; private set; }
        public DanceWindow Window { get; internal set; }
        private bool initialized;
        private bool panelGesture;
        public bool PanelOwnsPointer { get; private set; }
        public static bool SuppressHostDragAnimation() {var ui=DanceBootstrap.Root==null?null:DanceBootstrap.Root.GetComponentInChildren<DancePlayerUIManager>(true);return ui!=null && (ui.PanelOwnsPointer || (ui.playerCore!=null && ui.playerCore.IsPlaying));}
        private MenuActions menus;
        private MenuEntry entry;
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            var settings = DanceSettingsHandler.Instance;
            DanceLocale.Set(settings.data.language);
            if (settings.data.enableMMDCamera && settings.data.enableWindowFollow)
            {
                settings.data.restoreWindowFollowAfterMmdCamera = true;
                settings.data.enableWindowFollow = false;
            }
            playerCore.InitPlayer();
            var root=DanceBootstrap.Root!=null?DanceBootstrap.Root:transform.root.gameObject;
            foreach(var c in root.GetComponentsInChildren<DanceShadowFollower>(true)) { c.enabled=true; c.ApplyVisibility(); }
            foreach(var c in root.GetComponentsInChildren<DanceCameraDistKeeper>(true))c.enabled=settings.data.enableCameraDistanceKeep;
            foreach(var c in root.GetComponentsInChildren<DanceWindowFollower>(true))c.SetEnabled(settings.data.enableWindowFollow);
            var hotkeys = GetComponentsInChildren<GlobalHotkeyListener>(true);
            foreach (var hotkey in hotkeys) hotkey.enabled = DanceHotkeys.HasGlobalBinding;
            DanceCameraDemo.EnsureCreated(this);
            CameraSync = GetComponentInChildren<DanceCameraSync>(true);
            if (CameraSync != null)
            {
                CameraSync.enabled = settings.data.enableMMDCamera;
                if (EnableMMDCamera != null)
                {
                    EnableMMDCamera.isOn = settings.data.enableMMDCamera;
                    EnableMMDCamera.onValueChanged.AddListener(SetCameraEnabled);
                }
                if (MMDCameraScaleSlider != null) MMDCameraScaleSlider.value = settings.data.mmdCameraScale;
            }
            var oldCanvas = TargetCanvas;
            Window = DanceWindow.Create(this);
            if (oldCanvas != null) oldCanvas.gameObject.SetActive(false);
            TargetCanvas = Window.Canvas;
            menus = FindFirstObjectByType<MenuActions>();
            entry = new MenuEntry { menu = TargetCanvas.gameObject, blockMovement = false, blockHandTracking = false, blockReaction = false, blockChibiMode = false };
            if (menus != null) menus.menuEntries.Add(entry);
            SetPanelVisible(!settings.data.hidePanelOnStart);
        }
        void Start()
        {
            if (DanceBootstrap.Root != null && !transform.IsChildOf(DanceBootstrap.Root.transform))
            { if (TargetCanvas != null) TargetCanvas.gameObject.SetActive(false); enabled = false; Debug.LogWarning("[CustomDancePlayer] Duplicate legacy UI disabled."); return; }
            Initialize();
            if (DanceSettingsHandler.Instance.data.autoPlayOnStart) StartCoroutine(TryAutoPlay());
        }
        void Update()
        {
            if (!initialized) return;
            UpdatePointerOwnership(Input.mousePosition,Input.GetMouseButtonDown(0),Input.GetMouseButton(0));
            if (DanceHotkeys.PanelPressed() && !DanceDialogs.Busy && !IsInTextInputState()) SetPanelVisible(!TargetCanvas.gameObject.activeSelf);
        }
        public void UpdatePointerOwnership(Vector2 position,bool pressed,bool held)
        {
            bool inside=Window!=null && Window.ContainsPointer(position);
            if(pressed)panelGesture=inside;
            if(!held)panelGesture=false;
            PanelOwnsPointer=held?panelGesture:inside;
            if(entry!=null)entry.blockMovement=PanelOwnsPointer;
        }
        public void SetCameraEnabled(bool value)
        {
            var data = DanceSettingsHandler.Instance.data;
            bool changed = data.enableMMDCamera != value;
            if (value && changed)
            {
                data.restoreWindowFollowAfterMmdCamera = data.enableWindowFollow;
                ApplyWindowFollow(false);
            }
            data.enableMMDCamera = value;
            if (!value) playerCore.SetRuntimeCamera(null);
            if (CameraSync != null)
            {
                CameraSync.enabled = value;
                if (value)
                {
                    CameraSync.PrepareSwitch();
                    if (playerCore.IsPlaying && playerCore.resourceManager.IsVmdResource)
                        playerCore.SetRuntimeCamera(CameraSync.RenderCamera);
                }
            }
            if (EnableMMDCamera != null && EnableMMDCamera.isOn != value) EnableMMDCamera.isOn = value;
            if (!value && changed)
            {
                ApplyWindowFollow(data.restoreWindowFollowAfterMmdCamera);
                data.restoreWindowFollowAfterMmdCamera = false;
            }
            DanceSettingsHandler.OnSettingChanged();
            Window?.RefreshSettingsState();
        }
        public void SetWindowFollowEnabled(bool value)
        {
            var data = DanceSettingsHandler.Instance.data;
            if (value && data.enableMMDCamera)
            {
                // Selecting window follow explicitly gives it ownership and exits
                // the authored MMD camera view.
                data.restoreWindowFollowAfterMmdCamera = false;
                SetCameraEnabled(false);
            }
            ApplyWindowFollow(value);
            DanceSettingsHandler.OnSettingChanged();
            Window?.RefreshSettingsState();
        }
        private void ApplyWindowFollow(bool value)
        {
            var data = DanceSettingsHandler.Instance.data;
            data.enableWindowFollow = value;
            var root = DanceBootstrap.Root != null ? DanceBootstrap.Root : gameObject;
            foreach (var c in root.GetComponentsInChildren<DanceWindowFollower>(true)) c.SetEnabled(value);
            if (EnableWindowFollow != null && EnableWindowFollow.isOn != value) EnableWindowFollow.isOn = value;
        }
        public void SetPanelVisible(bool visible) { if (TargetCanvas != null) TargetCanvas.gameObject.SetActive(visible); }
        private IEnumerator InitializeLibrary()
        {
            yield return resourceManager.RefreshDanceFileListAsync();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            playerCore.playlistManager.ApplyFilters();
            Window?.RefreshLibrary();
            watch.Stop();
            Debug.Log("[CustomDancePlayer] Library UI refresh completed in " + watch.ElapsedMilliseconds + " ms.");
        }
        public void BeginLibraryInitialization()
        {
            if (!resourceManager.IsRefreshing && resourceManager.DanceFileList.Count == 0)
                StartCoroutine(InitializeLibrary());
        }
        public void RefreshDropdown() { resourceManager.RefreshDanceFileList(); playerCore.playlistManager.ApplyFilters(); Window?.RefreshLibrary(); }
        public void RefreshDropdownAsync() { if (!resourceManager.IsRefreshing) StartCoroutine(InitializeLibrary()); }
        public bool RefreshAndPlayPackage(string path)
        {
            RefreshDropdown(); var playlist = playerCore.playlistManager;
            playlist.Search = ""; playlist.Format = ""; playlist.SetPlaylistType(DancePlaylistManager.PlaylistType.All); playlist.ApplyFilters();
            var descriptor = resourceManager.Descriptors.Values.FirstOrDefault(d => System.IO.Path.GetFullPath(d.Path).Equals(System.IO.Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
            return descriptor != null && playerCore.PlayDanceByIndex(playlist.GetIndexByFile(descriptor.Id));
        }
        public void OnPlayPauseBtnClick()
        {
            if (playerCore.IsPlaying) { playerCore.TogglePause(); return; }
            int index = playerCore.playlistManager.GetIndexByFile(DanceSettingsHandler.Instance.data.lastResourceId);
            if (index < 0) index = DanceSettingsHandler.Instance.data.currentPlayIndex;
            playerCore.PlayDanceByIndex(Math.Max(0, index));
        }
        public void OnPlayStopBtnClick()
        {
            if(playerCore.IsPlaying)playerCore.StopPlay();
            else OnPlayPauseBtnClick();
        }
        public void UpdateDropdownValue() { Window?.RefreshLibrary(); }
        public void AddMyUIToGameMenuList() { }
        public IEnumerator TryAutoPlay()
        {
            yield return new WaitForSecondsRealtime(3);
            float timeout = Time.realtimeSinceStartup + 15;
            while ((!avatarHelper.IsAvatarAvailable() || resourceManager.IsRefreshing) && Time.realtimeSinceStartup < timeout) yield return null;
            if (!avatarHelper.IsAvatarAvailable() || resourceManager.IsRefreshing) yield break;
            int index = playerCore.playlistManager.GetIndexByFile(DanceSettingsHandler.Instance.data.lastResourceId);
            if (index < 0) index = DanceSettingsHandler.Instance.data.currentPlayIndex;
            playerCore.PlayDanceByIndex(index);
        }
        private bool IsInTextInputState()
        {
            var selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            return selected != null && (selected.GetComponent<InputField>() != null || selected.GetComponent<TMP_InputField>() != null);
        }
        void OnDestroy()
        {
            if (menus != null && entry != null) menus.menuEntries.Remove(entry);
            if (Window != null) Destroy(Window.gameObject);
            playerCore?.StopPlay();
        }
    }
}
