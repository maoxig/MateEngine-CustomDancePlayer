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
    public Toggle EnableUIPanelFollow;      // Toggle for enabling avatar follow (optional, can be null)
    public Canvas targetCanvas;            // UI's Canvas component

    // Advanced UI elements 
    public Button AdvancedToggleBtn;       // The button that toggles advanced view
    public TMP_Text AdvancedToggleBtnText; // text on the Advanced button (so we can change to "< Back")
    public GameObject MainPanelRoot;       // parent container GameObject for main UI
    public GameObject AdvancedPanelRoot;   // parent container GameObject for advanced UI (should contain a ScrollView)
    public ScrollRect AdvancedScrollRect;  // ScrollRect inside advanced panel (optional but recommended)
    public Slider AnimationStartDelaySlider; // slider for animation start delay (0..1s)
    public TMP_Text AnimationStartDelayValueText; // shows numeric value "0.200s"

    // Reference to player core
    public DancePlayerCore playerCore;


    [Header("UI Toggle")]
    public KeyCode toggleKey = KeyCode.K; // Configurable toggle key

    // Variables for linking with game menu logic
    private MenuActions _gameMenuActions; // Game's existing MenuActions instance
    private MenuEntry _myUIMenuEntry;     // Your UI's corresponding MenuEntry (for adding/removing from list)
    private bool _isMyUIAddedToMenuList;  // Flag to prevent duplicate addition to menuEntries
    private Font _defaultLiberationFont;
    private SwingController swingController;

    // track advanced state
    private bool _isAdvancedOpen = false;

    void Start()
    {
        if (playerCore != null && playerCore.resourceManager != null)
        {
            playerCore.resourceManager.RefreshDanceFileList();
        }

        InitUI();

        BindButtonEvents();

        playerCore.InitPlayer();
        playerCore.RefreshPlayList();
        UpdateToggleKeyText();
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
#if UNITY_EDITOR
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

        swingController = GetComponent<SwingController>();

        // restore follow toggle state from swingController if exists
        if (swingController != null && EnableUIPanelFollow != null)
        {
            EnableUIPanelFollow.isOn = swingController.enabled;
        }

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

        // NEW: remove listeners for advanced
        AdvancedToggleBtn?.onClick.RemoveAllListeners();
        AnimationStartDelaySlider?.onValueChanged.RemoveAllListeners();
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
        RefreshBtn.onClick.AddListener(playerCore.RefreshPlayList);
        if (VolumeSlider != null)
        {
            VolumeSlider.value = 0.25f;
            VolumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }
        if (EnableUIPanelFollow != null)
        {
            EnableUIPanelFollow.onValueChanged.AddListener(OnUIPanelFollowToggleChanged);
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
            string currentFileName = playerCore.resourceManager.DanceFileList[playerCore.CurrentPlayIndex];
            if (currentFileName.EndsWith(".unity3d", StringComparison.OrdinalIgnoreCase))
            {
                currentFileName = currentFileName.Substring(0, currentFileName.Length - ".unity3d".Length);
            }

            DanceFileDropdown.captionText.text = currentFileName;
        }
        if (playerCore.IsPlaying && playerCore.resourceManager.CurrentAudioClip != null)
        {
            float elapsed = Time.time - playerCore.AudioStartTime; // 你已经存了开始时间
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
    /// Handle avatar follow toggle change
    /// </summary>
    private void OnUIPanelFollowToggleChanged(bool isOn)
    {
        if (swingController != null)
        {
            swingController.enabled = isOn;
#if UNITY_EDITOR
            Debug.Log($"Avatar follow {(isOn ? "enabled" : "disabled")}");
#endif
        }
        else
        {
#if UNITY_EDITOR
            Debug.LogWarning("SwingController component not found on the same GameObject");
#endif
            if (EnableUIPanelFollow != null)
            {
                EnableUIPanelFollow.interactable = false;
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
        playerCore.resourceManager.RefreshDanceFileList();
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
