using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CustomDancePlayer
{ // Manages UI interactions and updates
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
        public Toggle AutoPlayOnStartToggle;
        public Toggle HidePanelOnStartToggle;
        public Toggle EnableUIPanelFollow;
        public Toggle EnableShadowFollow;
        public Toggle EnableWindowFollow;
        public Toggle EnableCameraDistanceKeep;
        public Slider AnimationStartDelaySlider;
        public TMP_Text AnimationStartDelayValueText;

        // Core Components
        [Header("Core Components")]
        public DancePlayerCore playerCore;
        public DanceAvatarHelper avatarHelper;
        public DanceResourceManager resourceManager;

        private DanceSettingsHandler _settingsHandler;
        // Other Components
        private HipsFollower _hipsFollower;
        private DanceShadowFollower _shadowFollower;
        private DanceWindowFollower _danceWindowFollower;
        private DanceCameraDistKeeper _danceCameraDistKeeper;


        private bool _isAdvancedOpen;

        // TODO remove them
        private MenuActions _gameMenuActions;
        private MenuEntry _myUIMenuEntry;
        private bool _isMyUIAddedToMenuList;


        void Start()
        {
            // Other Components
            _danceWindowFollower = FindFirstObjectByType<DanceWindowFollower>();
            _danceCameraDistKeeper = FindFirstObjectByType<DanceCameraDistKeeper>();
            _hipsFollower = FindFirstObjectByType<HipsFollower>();
            _shadowFollower = FindFirstObjectByType<DanceShadowFollower>();

            _settingsHandler = DanceSettingsHandler.Instance;
            RefreshDropdown();
            playerCore.InitPlayer();
            UpdateToggleKeyText();
            InitUI();
            BindButtonEvents();

            if (_settingsHandler.data.hidePanelOnStart && TargetCanvas != null)
            {
                TargetCanvas.gameObject.SetActive(false);
            }

            if (_settingsHandler.data.autoPlayOnStart && _settingsHandler.data.currentPlayIndex >= 0)
            {
                StartCoroutine(TryAutoPlay());
            }

            // TODO remove them
            _gameMenuActions = UnityEngine.Object.FindFirstObjectByType<MenuActions>();
            _myUIMenuEntry = new MenuEntry
            {
                menu = TargetCanvas.gameObject,
                blockMovement = true,
                blockHandTracking = false,
                blockReaction = false,
                blockChibiMode = false
            };
            AddMyUIToGameMenuList();
        }

        void Update()
        {
            UpdateUI();
            HandleKeyToggleUI();
        }

        // Initializes UI elements
        private void InitUI()
        {
            CurrentPlayText.text = playerCore.GetCurrentPlayFileName();
            PlayModeText.text = GetPlayModeText();
            AvatarStatusText.text = "Avatar Status: Not Connected";
            UpdateDropdownValue();
        }

        // Binds UI events to handlers
        private void BindButtonEvents()
        {
            PrevBtn.onClick.AddListener(playerCore.PlayPrev);
            PlayPauseBtn.onClick.AddListener(OnPlayPauseBtnClick);
            NextBtn.onClick.AddListener(playerCore.PlayNext);
            StopBtn.onClick.AddListener(playerCore.StopPlay);
            PlayModeBtn.onClick.AddListener(OnPlayModeBtnClick);
            RefreshBtn.onClick.AddListener(RefreshDropdown);

            if (DanceFileDropdown != null)
            {
                DanceFileDropdown.onValueChanged.AddListener(index =>
                {
                    _settingsHandler.data.currentPlayIndex = index;
                    DanceSettingsHandler.OnSettingChanged();
                });
            }

            if (VolumeSlider != null)
            {
                VolumeSlider.value = _settingsHandler.data.danceVolume;
                VolumeValueText.text = $"{Mathf.RoundToInt(_settingsHandler.data.danceVolume * 100)}%";
                VolumeSlider.onValueChanged.AddListener(value =>
                {
                    _settingsHandler.data.danceVolume = value;
                    avatarHelper.UpdateAudioVolume();
                    VolumeValueText.text = $"{Mathf.RoundToInt(value * 100)}%";
                    DanceSettingsHandler.OnSettingChanged();
                });
            }

            if (AdvancedToggleBtn != null)
            {
                AdvancedToggleBtn.onClick.AddListener(ToggleAdvancedPanel);
                AdvancedToggleBtnText.text = "Settings";
            }

            if (AnimationStartDelaySlider != null)
            {
                AnimationStartDelaySlider.value = _settingsHandler.data.animationStartDelay;
                AnimationStartDelayValueText.text = $"{_settingsHandler.data.animationStartDelay:0.000}s";
                AnimationStartDelaySlider.onValueChanged.AddListener(value =>
                {
                    _settingsHandler.data.animationStartDelay = value;
                    AnimationStartDelayValueText.text = $"{value:0.000}s";
                    DanceSettingsHandler.OnSettingChanged();
                });
            }
            if (AutoPlayOnStartToggle != null)
            {
                AutoPlayOnStartToggle.isOn = _settingsHandler.data.autoPlayOnStart;
                AutoPlayOnStartToggle.onValueChanged.AddListener(isOn =>
                {
                    _settingsHandler.data.autoPlayOnStart = isOn;
                    DanceSettingsHandler.OnSettingChanged();
                });
            }

            if (HidePanelOnStartToggle != null)
            {
                HidePanelOnStartToggle.isOn = _settingsHandler.data.hidePanelOnStart;
                HidePanelOnStartToggle.onValueChanged.AddListener(isOn =>
                {
                    _settingsHandler.data.hidePanelOnStart = isOn;
                    DanceSettingsHandler.OnSettingChanged();
                });
            }

            // Other Components related toggles
            if (EnableUIPanelFollow != null && _hipsFollower != null)
            {
                EnableUIPanelFollow.isOn = _settingsHandler.data.enableDanceUIFollow;
                _hipsFollower.enabled = _settingsHandler.data.enableDanceUIFollow;
                EnableUIPanelFollow.onValueChanged.AddListener(isOn =>
                {
                    _settingsHandler.data.enableDanceUIFollow = isOn;
                    _hipsFollower.enabled = isOn;
                    DanceSettingsHandler.OnSettingChanged();
                });
            }

            if (EnableShadowFollow != null && _shadowFollower != null)
            {
                EnableShadowFollow.isOn = _settingsHandler.data.enableShadowFollow;
                _shadowFollower.enabled = _settingsHandler.data.enableShadowFollow;
                EnableShadowFollow.onValueChanged.AddListener(isOn =>
                {
                    _settingsHandler.data.enableShadowFollow = isOn;
                    _shadowFollower.enabled = isOn;
                    DanceSettingsHandler.OnSettingChanged();
                });
            }



            if (EnableWindowFollow != null && _danceWindowFollower != null)
            {
                EnableWindowFollow.isOn = _settingsHandler.data.enableWindowFollow;
                _danceWindowFollower.SetEnabled(_settingsHandler.data.enableWindowFollow);
                EnableWindowFollow.onValueChanged.AddListener(isOn =>
                {
                    _settingsHandler.data.enableWindowFollow = isOn;
                    _danceWindowFollower.SetEnabled(isOn);
                    DanceSettingsHandler.OnSettingChanged();
                });
            }

            if (EnableCameraDistanceKeep != null & _danceCameraDistKeeper != null)
            {
                EnableCameraDistanceKeep.isOn = _settingsHandler.data.enableCameraDistanceKeep;
                _danceCameraDistKeeper.enabled = _settingsHandler.data.enableCameraDistanceKeep;
                EnableCameraDistanceKeep.onValueChanged.AddListener(isOn =>
                {
                    _settingsHandler.data.enableCameraDistanceKeep = isOn;
                    _danceCameraDistKeeper.enabled = isOn;
                    DanceSettingsHandler.OnSettingChanged();
                });
            }
        }

        // Updates UI elements in real-time
        private void UpdateUI()
        {
            CurrentPlayText.text = playerCore.GetCurrentPlayFileName();
            AvatarStatusText.text = avatarHelper.IsAvatarAvailable() ? "Avatar Status: Connected" : "Avatar Status: Not Connected";
            PlayModeText.text = GetPlayModeText();

            bool isPlayerReady = avatarHelper.IsAvatarAvailable() && resourceManager.DanceFileList.Count > 0;
            PlayPauseBtn.interactable = isPlayerReady && !_settingsHandler.data.isPlaying;
            PrevBtn.interactable = isPlayerReady && _settingsHandler.data.isPlaying;
            NextBtn.interactable = isPlayerReady && _settingsHandler.data.isPlaying;
            StopBtn.interactable = isPlayerReady && _settingsHandler.data.isPlaying;
            DanceFileDropdown.interactable = isPlayerReady && !_settingsHandler.data.isPlaying;
            RefreshBtn.interactable = !_settingsHandler.data.isPlaying;


            if (_settingsHandler.data.isPlaying && resourceManager.CurrentAudioClip != null)
            {
                float elapsed = Time.time - _settingsHandler.data.audioStartTime;
                float total = resourceManager.CurrentAudioClip.length;
                ProgressSlider.value = Mathf.Clamp01(elapsed / total);
            }
            else
            {
                ProgressSlider.value = 0f;
            }
        }

        // Handles UI toggle key press
        private void HandleKeyToggleUI()
        {
            if (TargetCanvas == null) return;
            if (IsInTextInputState())
                return;
            if (Input.GetKeyDown(_settingsHandler.data.toggleKey))
            {
                GameObject targetCanvasObject = TargetCanvas.gameObject;
                bool newVisibleState = !targetCanvasObject.activeSelf;
                targetCanvasObject.SetActive(newVisibleState);

                if (newVisibleState)
                {
                    AddMyUIToGameMenuList();
                }
            }
        }

        // Attempts auto-play with timeout
        private IEnumerator TryAutoPlay()
        {
            yield return new WaitForSeconds(3f);
            float timeout = 10f;
            float elapsed = 0f;

            while (!avatarHelper.IsAvatarAvailable() && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            if (avatarHelper.IsAvatarAvailable() &&
                _settingsHandler.data.currentPlayIndex >= 0 && DanceFileDropdown.options.Count > 0 &&
                _settingsHandler.data.currentPlayIndex < DanceFileDropdown.options.Count)
            {
                OnPlayPauseBtnClick();
            }
        }

        // Handles play/pause button click
        private void OnPlayPauseBtnClick()
        {
            if (!_settingsHandler.data.isPlaying && DanceFileDropdown.value >= 0)
            {
                playerCore.PlayDanceByIndex(DanceFileDropdown.value);
            }
        }

        // Toggles play mode
        private void OnPlayModeBtnClick()
        {
            _settingsHandler.data.currentPlayMode = (DancePlayerCore.PlayMode)(((int)_settingsHandler.data.currentPlayMode + 1) % Enum.GetValues(typeof(DancePlayerCore.PlayMode)).Length);
            DanceSettingsHandler.OnSettingChanged();
        }

        // Gets play mode text for display
        private string GetPlayModeText()
        {
            return _settingsHandler.data.currentPlayMode switch
            {
                DancePlayerCore.PlayMode.Sequence => "Sequence",
                DancePlayerCore.PlayMode.Loop => "Loop",
                DancePlayerCore.PlayMode.Random => "Random",
                _ => "Sequence"
            };
        }
        public void UpdateDropdownValue()
        {
            if (DanceFileDropdown == null || DanceFileDropdown.options.Count == 0)
                return; 

            int targetIndex = _settingsHandler.data.currentPlayIndex;
            if (targetIndex < 0 || targetIndex >= DanceFileDropdown.options.Count)
            {
                targetIndex = 0;
                _settingsHandler.data.currentPlayIndex = targetIndex; 
                DanceSettingsHandler.OnSettingChanged();
            }

            if (DanceFileDropdown.value != targetIndex)
            {
                DanceFileDropdown.value = targetIndex;
                DanceFileDropdown.captionText.text = DanceFileDropdown.options[targetIndex].text;
            }
        }
        // Refreshes dropdown with dance files
        public void RefreshDropdown()
        {
            DanceFileDropdown.ClearOptions();
            resourceManager.RefreshDanceFileList();
            var danceFiles = resourceManager.DanceFileList;

            if (danceFiles.Count == 0)
            {
                DanceFileDropdown.options.Add(new Dropdown.OptionData("No dance files (put in CustomDances folder)"));
            }
            else
            {
                DanceFileDropdown.AddOptions(danceFiles.Select(file => file.EndsWith(".unity3d", StringComparison.OrdinalIgnoreCase)
                    ? file.Substring(0, file.Length - ".unity3d".Length) : file).ToList());
            }
        }

        // Updates toggle key text
        public void UpdateToggleKeyText()
        {
            if (ToggleKeyText != null)
            {
                ToggleKeyText.text = $"Press {_settingsHandler.data.toggleKey} to hide UI";
            }
        }

        // Checks if in text input state
        private bool IsInTextInputState()
        {
            if (EventSystem.current == null) return false;
            GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
            return selectedObj != null && (selectedObj.GetComponent<InputField>() != null || selectedObj.GetComponent<TMP_InputField>() != null);
        }

        // Toggles advanced panel visibility
        private void ToggleAdvancedPanel()
        {
            _isAdvancedOpen = !_isAdvancedOpen;
            MainPanelRoot.SetActive(!_isAdvancedOpen);
            SettingsPanelRoot.SetActive(_isAdvancedOpen);
            AdvancedToggleBtnText.text = _isAdvancedOpen ? "Back" : "Settings";

            if (_isAdvancedOpen && SettingsScrollRect != null)
            {
                SettingsScrollRect.verticalNormalizedPosition = 1f;
            }
        }
        public void AddMyUIToGameMenuList()
        {
            if (_gameMenuActions == null || _isMyUIAddedToMenuList || _myUIMenuEntry == null)
                return;


            bool isAlreadyInList = _gameMenuActions.menuEntries.Exists(
                entry => entry.menu == gameObject
            );
            if (!isAlreadyInList)
            {
                _gameMenuActions.menuEntries.Add(_myUIMenuEntry);
                _isMyUIAddedToMenuList = true;
            }
        }

    }
}

