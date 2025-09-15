using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI Manager: Binds button events and updates playback status
/// Added:
/// - Advanced Settings toggle (Advanced <-> Main)
/// - AnimationStartDelay slider binding and display
/// </summary>
public class DancePlayerUIManager : MonoBehaviour
{
    [Header("UI Components")]
    // UI component references (assign in Inspector)
    public Text CurrentPlayText;       // Currently playing file name
    public Slider ProgressSlider;          // Progress slider (optional, can be null)
    public Button RefreshBtn;              // Refresh button (refresh dance file list)
    public Button PrevBtn;                 // Previous button
    public Button PlayPauseBtn;            // Play/Pause button (currently only play)
    public Button NextBtn;                 // Next button
    public Button StopBtn;                 // Stop button
    public Button PlayModeBtn;             // Play mode button
    public TMP_Text PlayModeText;          // Play mode text
    public Slider VolumeSlider;            // Volume slider (optional, can be null)
    public TMP_Text VolumeValueText;       // Volume value text (optional, can be null)
    public TMP_Text AvatarStatusText;      // Avatar status text
    public Dropdown DanceFileDropdown; // Dance file dropdown (select to play)
    public TMP_Text _toggleKeyText;        // Assign text component in Inspector

    public Canvas targetCanvas;            // UI's Canvas component

    [Header("Advanced UI Components")]
    // Advanced UI elements 
    public Button AdvancedToggleBtn;       // The button that toggles advanced view
    public TMP_Text AdvancedToggleBtnText; // text on the Advanced button (so we can change to "< Back")
    public GameObject MainPanelRoot;       // parent container GameObject for main UI
    public GameObject AdvancedPanelRoot;   // parent container GameObject for advanced UI (should contain a ScrollView)
    public ScrollRect AdvancedScrollRect;  // ScrollRect inside advanced panel (optional but recommended)
    public Toggle AutoPlayOnStartToggle;   // Toggle for auto play on start
    public Toggle HidePanelOnStartToggle;  // Toggle for hide panel on start

    public Slider AnimationStartDelaySlider; // slider for animation start delay (0..1s)
    public TMP_Text AnimationStartDelayValueText; // shows numeric value "0.200s"
    public Toggle EnableUIPanelFollow;      // Toggle for enabling avatar follow (optional, can be null)
    public Toggle EnableShadowFollow;

    [Header("Core Components")]
    // Reference to player core
    public DancePlayerCore playerCore;
    // Reference to HipsFollower (optional, can be null)
    public HipsFollower hipsFollower;

    [Header("Optional Settings")]
    public bool hidePanelOnStart = false; // If true, hide the entire panel on start (can be toggled with key)

    [Header("UI Toggle")]
    public KeyCode toggleKey = KeyCode.H; // Configurable toggle key

    // Variables for linking with game menu logic
    private MenuActions _gameMenuActions; // Game's existing MenuActions instance
    private MenuEntry _myUIMenuEntry;     // Your UI's corresponding MenuEntry (for adding/removing from list)
    private bool _isMyUIAddedToMenuList;  // Flag to prevent duplicate addition to menuEntries
    private Font _defaultLiberationFont;

    // track advanced state
    private bool _isAdvancedOpen = false;

    void Start()
    {
        playerCore.InitPlayer();
        RefreshDropdown();
        UpdateToggleKeyText();

        InitUI();
        BindButtonEvents();

        if (hidePanelOnStart && targetCanvas != null)
        {
            targetCanvas.gameObject.SetActive(false);
            RemoveMyUIFromGameMenuList();
        }

        if (playerCore.autoPlayOnStart&& playerCore != null && playerCore.CurrentPlayIndex >= 0)
        {
            if (DanceFileDropdown.options.Count > 0 && playerCore.CurrentPlayIndex >= 0)
            {
                DanceFileDropdown.value = playerCore.CurrentPlayIndex;
#if DEBUG
                Debug.Log($"Set dropdown to index {playerCore.CurrentPlayIndex}, play on start");
#endif
                StartCoroutine(TryAutoPlay());

            }
        }
    }

    void Update()
    {
        UpdateUI();
        HandleKeyToggleUI();
    }

    private void Awake()
    {
        _defaultLiberationFont = Resources.Load<Font>("LiberationSans.ttf");
        if (_defaultLiberationFont == null)
        {
            _defaultLiberationFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Debug.LogWarning("Failed to load LiberationSans, fall back to LegacyRuntime");
        }
        _gameMenuActions = UnityEngine.Object.FindFirstObjectByType<MenuActions>();
        if (_gameMenuActions == null)
        {
#if DEBUG
            Debug.LogWarning("MenuActions script not found in the game. UI click and scroll controls may not work!");
#endif
            return;
        }

        _myUIMenuEntry = new MenuEntry
        {
            menu = targetCanvas.gameObject,
            blockMovement = true,
            blockHandTracking = false,
            blockReaction = false,
            blockChibiMode = false
        };
        AddMyUIToGameMenuList();


        // Ensure advanced panel hidden initially
        if (AdvancedPanelRoot != null)
            AdvancedPanelRoot.SetActive(false);
        if (MainPanelRoot != null)
            MainPanelRoot.SetActive(true);
    }

    private void OnDestroy()
    {
        PrevBtn.onClick.RemoveAllListeners();
        PlayPauseBtn.onClick.RemoveAllListeners();
        NextBtn.onClick.RemoveAllListeners();
        StopBtn.onClick.RemoveAllListeners();
        PlayModeBtn.onClick.RemoveAllListeners();
        RefreshBtn.onClick.RemoveAllListeners();
        DanceFileDropdown.onValueChanged.RemoveAllListeners();
        VolumeSlider?.onValueChanged.RemoveAllListeners();
       
        EnableUIPanelFollow?.onValueChanged.RemoveAllListeners();
        EnableShadowFollow?.onValueChanged.RemoveAllListeners();
        AdvancedToggleBtn?.onClick.RemoveAllListeners();
        AnimationStartDelaySlider?.onValueChanged.RemoveAllListeners();
        AutoPlayOnStartToggle?.onValueChanged.RemoveAllListeners();
        HidePanelOnStartToggle?.onValueChanged.RemoveAllListeners();
        RemoveMyUIFromGameMenuList();
    }
    /// <summary>
    /// Initialize UI (set default state)
    /// </summary>
    private void InitUI()
    {
        CurrentPlayText.text = "None";
        PlayModeText.text = playerCore.GetPlayModeText();
        AvatarStatusText.text = "Avatar Status: Not Connected";

        // Init animation delay slider if provided
        if (AnimationStartDelaySlider != null && playerCore != null)
        {
            AnimationStartDelaySlider.minValue = 0f;
            AnimationStartDelaySlider.maxValue = 1f;
            AnimationStartDelaySlider.value = playerCore.AnimationStartDelay;
            if (AnimationStartDelayValueText != null)
                AnimationStartDelayValueText.text = $"{playerCore.AnimationStartDelay:0.000}s";
        }
        if (playerCore != null && VolumeSlider != null)
        {
            VolumeSlider.minValue = 0f;
            VolumeSlider.maxValue = 1f;
            VolumeSlider.value = playerCore.avatarHelper.danceVolume;
            if (VolumeValueText != null)
            {
                int percent = Mathf.RoundToInt(VolumeSlider.value * 100);
                VolumeValueText.text = $"{percent}%";
            }
        }

        if (playerCore != null && DanceFileDropdown != null)
        {
            if (playerCore.CurrentPlayIndex >= 0 && playerCore.CurrentPlayIndex < DanceFileDropdown.options.Count)
            {
                DanceFileDropdown.value = playerCore.CurrentPlayIndex;
                DanceFileDropdown.captionText.text = playerCore.GetCurrentPlayFileName();
            }
            else
            {
                DanceFileDropdown.value = 0;
                DanceFileDropdown.captionText.text = DanceFileDropdown.options.Count > 0 ? DanceFileDropdown.options[0].text : "None";
            }
        }

    }

    private System.Collections.IEnumerator TryAutoPlay()
    {
        // 先等待3秒
        yield return new WaitForSeconds(3f);

        // Wait until avatar is available or timeout
        float timeout = 10f; // Max wait time (adjust as needed)
        float elapsed = 0f;

        while (!playerCore.avatarHelper.IsAvatarAvailable() && elapsed < timeout)
        {
            yield return null; // Wait for next frame
            elapsed += Time.deltaTime;
        }

        if (!playerCore.avatarHelper.IsAvatarAvailable())
        {
            Debug.LogWarning("[DancePlayerUIManager] Auto-play failed: Avatar not available after timeout");
            yield break;
        }

        if (playerCore.CurrentPlayIndex >= 0 && DanceFileDropdown.options.Count > 0 && playerCore.CurrentPlayIndex < DanceFileDropdown.options.Count && playerCore.avatarHelper.IsAvatarAvailable())
        {
            DanceFileDropdown.value = playerCore.CurrentPlayIndex;
#if DEBUG
            Debug.Log($"Set dropdown to index {playerCore.CurrentPlayIndex}, triggering auto-play");
#endif
            OnPlayPauseBtnClick();
        }
        else
        {
            Debug.LogWarning("[DancePlayerUIManager] Auto-play skipped: Invalid CurrentPlayIndex or empty dropdown");
        }
    }
    /// <summary>
    /// Bind all button events
    /// </summary>
    private void BindButtonEvents()
    {
        PrevBtn.onClick.AddListener(playerCore.PlayPrev);
        PlayPauseBtn.onClick.AddListener(OnPlayPauseBtnClick);
        NextBtn.onClick.AddListener(playerCore.PlayNext);
        StopBtn.onClick.AddListener(OnStopBtnClick);
        PlayModeBtn.onClick.AddListener(OnPlayModeBtnClick);
        RefreshBtn.onClick.AddListener(RefreshDropdown);
        if (DanceFileDropdown != null)
        {
            DanceFileDropdown.onValueChanged.AddListener(index =>
            {
                if (playerCore != null)
                {
                    playerCore.CurrentPlayIndex = index;
                    DanceSettingsHandler.OnSettingChanged();
                }
            });
        }
        if (VolumeSlider != null)
        {
            VolumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }
        // ADVANCED button
        if (AdvancedToggleBtn != null)
        {
            AdvancedToggleBtn.onClick.AddListener(ToggleAdvancedPanel);
            if (AdvancedToggleBtnText != null)
                AdvancedToggleBtnText.text = "Settings";
        }

        // Animation delay slider binding
        if (AnimationStartDelaySlider != null)
        {
            AnimationStartDelaySlider.onValueChanged.AddListener(OnAnimationDelayChanged);
        }
        // hips follow toggle
        if (EnableUIPanelFollow != null && hipsFollower != null)
        {
            EnableUIPanelFollow.isOn = hipsFollower.followEnabled;
            EnableUIPanelFollow.onValueChanged.AddListener(isOn =>
            {
                if (hipsFollower != null)
                {
                    hipsFollower.followEnabled = isOn;
                    if (isOn)
                    {
                        hipsFollower.UpdateBaseAndInitial();
                    }
                }
            });
        }
        // shadow follow toggle
        DanceShadowFollower danceShadowFollower = FindFirstObjectByType<DanceShadowFollower>();
        if (EnableShadowFollow != null && danceShadowFollower !=null)
        {
            EnableShadowFollow.isOn = danceShadowFollower.isActiveAndEnabled;
            EnableShadowFollow.onValueChanged.AddListener(isOn =>
            {
                if (danceShadowFollower != null)
                {
                    danceShadowFollower.enabled = isOn;
                }
            });

        }

        if (AutoPlayOnStartToggle != null)
        {
            AutoPlayOnStartToggle.isOn =  playerCore.autoPlayOnStart;
            AutoPlayOnStartToggle.onValueChanged.AddListener(isOn =>
            {
                playerCore.autoPlayOnStart = isOn;
            });
        }

        if (HidePanelOnStartToggle != null)
        {
            HidePanelOnStartToggle.isOn = hidePanelOnStart;
            HidePanelOnStartToggle.onValueChanged.AddListener(isOn =>
            {
                hidePanelOnStart = isOn;
            });
        }
    }

    /// <summary>
    /// Update UI in real-time (called every frame)
    /// </summary>
    private void UpdateUI()
    {
        // Update current playing file name
        CurrentPlayText.text = playerCore.GetCurrentPlayFileName();


        // Update avatar status
        if (playerCore.avatarHelper.IsAvatarAvailable())
        {
            AvatarStatusText.text = $"Avatar Status: Connected";
        }
        else
        {
            AvatarStatusText.text = "Avatar Status: Not Connected";
        }

        // Update play mode text
        PlayModeText.text = playerCore.GetPlayModeText();

        // Update button states (disable play button when playing, disable stop/next button when not playing)
        bool isPlayerReady = playerCore.avatarHelper.IsAvatarAvailable() && playerCore.resourceManager.DanceFileList.Count > 0;
        PlayPauseBtn.interactable = isPlayerReady && !playerCore.IsPlaying;
        PrevBtn.interactable = isPlayerReady && playerCore.IsPlaying;
        NextBtn.interactable = isPlayerReady && playerCore.IsPlaying;
        StopBtn.interactable = isPlayerReady && playerCore.IsPlaying;
        DanceFileDropdown.interactable = isPlayerReady && !playerCore.IsPlaying;
        RefreshBtn.interactable = !playerCore.IsPlaying;


        if (playerCore.IsPlaying && playerCore.CurrentPlayIndex >= 0
            && playerCore.CurrentPlayIndex < playerCore.resourceManager.DanceFileList.Count)
        {
            string currentFileName = playerCore.GetCurrentPlayFileName();
            DanceFileDropdown.captionText.text = currentFileName;
        }
        if (playerCore.IsPlaying && playerCore.resourceManager.CurrentAudioClip != null)
        {
            float elapsed = Time.time - playerCore.AudioStartTime;
            float total = playerCore.resourceManager.CurrentAudioClip.length;
            float progress = Mathf.Clamp01(elapsed / total);

            ProgressSlider.value = progress;  // UI slider
        }
        else
        {
            ProgressSlider.value = 0f;
        }
    }
    /// <summary>
    /// Update toggle key text
    /// </summary>
    public void UpdateToggleKeyText()
    {
        if (_toggleKeyText != null)
        {
            _toggleKeyText.text = $"Press [{toggleKey}] to hide UI";
        }
    }
    /// <summary>
    /// Play/Pause button click (currently only supports play, pause requires additional handling)
    /// </summary>
    private void OnPlayPauseBtnClick()
    {
        // If not playing and dropdown has selection, play selected dance
        if (!playerCore.IsPlaying && DanceFileDropdown.value >= 0)
        {
            playerCore.PlayDanceByIndex(DanceFileDropdown.value);
        }

    }


    /// <summary>
    /// Stop button click
    /// </summary>
    private void OnStopBtnClick()
    {
        playerCore.StopPlay();
        // Reset dropdown selection
        DanceFileDropdown.value = playerCore.CurrentPlayIndex;
    }

    /// <summary>
    /// Play mode button click
    /// </summary>
    private void OnPlayModeBtnClick()
    {
        playerCore.TogglePlayMode();
        // Play mode text is automatically updated in Update
    }

    /// <summary>
    /// Volume slider value changed
    /// </summary>
    private void OnVolumeChanged(float value)
    {
        if (playerCore.avatarHelper.IsAvatarAvailable())
        {
            playerCore.avatarHelper.CurrentAudioSource.volume = value;
            if (VolumeValueText != null)
            {
                int percent = Mathf.RoundToInt(value * 100);
                VolumeValueText.text = $"{percent}%";
            }
        }
    }

    /// <summary>
    /// Animation delay slider changed (0..1s)
    /// </summary>
    private void OnAnimationDelayChanged(float seconds)
    {
        if (playerCore != null)
        {
            playerCore.SetAnimationStartDelay(seconds);
        }
        if (AnimationStartDelayValueText != null)
        {
            AnimationStartDelayValueText.text = $"{seconds:0.000}s";
        }
    }

    /// <summary>
    /// Refresh dropdown (called when manually clicking refresh button)
    /// </summary>
    public void RefreshDropdown()
    {
        DanceFileDropdown.ClearOptions();
        playerCore.RefreshPlayList();
        var danceFiles = playerCore.resourceManager.DanceFileList;
        if (danceFiles.Count == 0)
        {
            DanceFileDropdown.options.Add(new Dropdown.OptionData("No dance files (put in CustomDances folder)"));
        }
        else
        {

            var displayNames = new List<string>();
            foreach (var file in danceFiles)
            {
                if (file.EndsWith(".unity3d", System.StringComparison.OrdinalIgnoreCase))
                {
                    displayNames.Add(file.Substring(0, file.Length - ".unity3d".Length));
                }
                else
                {
                    displayNames.Add(file);
                }
            }
            DanceFileDropdown.AddOptions(displayNames);
        }
    }
    private void HandleKeyToggleUI()
    {
        if (targetCanvas == null) return;
        if (IsInTextInputState())
            return;
        if (Input.GetKeyDown(toggleKey))
        {
            GameObject targetCanvasObject = targetCanvas.gameObject;
            bool newVisibleState = !targetCanvasObject.activeSelf;
            targetCanvasObject.SetActive(newVisibleState);

            if (newVisibleState)
            {
                AddMyUIToGameMenuList();
            }
            else
            {
                RemoveMyUIFromGameMenuList();
            }
        }
    }
    /// <summary>
    /// Check if currently in a text input state (e.g., chat box or input field is active)
    /// </summary>
    /// <returns>true = inputting, false = not inputting</returns>
    private bool IsInTextInputState()
    {
        // 1. First check if EventSystem exists (avoid null reference)
        if (EventSystem.current == null)
            return false;

        // 2. Get the currently selected UI object
        GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
        if (selectedObj == null)
            return false;

        // 3. Check if it is an input field type (supports UGUI and TMP)
        bool isUGUIInput = selectedObj.GetComponent<InputField>() != null;
        bool isTMPInput = selectedObj.GetComponent<TMP_InputField>() != null;

        return isUGUIInput || isTMPInput;
    }

    // Toggle advanced panel open/close
    private void ToggleAdvancedPanel()
    {
        _isAdvancedOpen = !_isAdvancedOpen;
        if (MainPanelRoot != null) MainPanelRoot.SetActive(!_isAdvancedOpen);
        if (AdvancedPanelRoot != null) AdvancedPanelRoot.SetActive(_isAdvancedOpen);

        if (AdvancedToggleBtnText != null)
        {
            AdvancedToggleBtnText.text = _isAdvancedOpen ? "Back" : "Settings";
        }

        // reset scroll to top when opened
        if (_isAdvancedOpen && AdvancedScrollRect != null)
        {
            AdvancedScrollRect.verticalNormalizedPosition = 1f;
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


    public void RemoveMyUIFromGameMenuList()
    {
        if (_gameMenuActions == null || !_isMyUIAddedToMenuList || _myUIMenuEntry == null)
            return;

        _gameMenuActions.menuEntries.RemoveAll(
            entry => entry.menu == gameObject
        );
        _isMyUIAddedToMenuList = false;
    }
}
