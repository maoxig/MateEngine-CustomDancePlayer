using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI Manager: Binds button events and updates playback status
/// </summary>
public class DancePlayerUIManager : MonoBehaviour
{
    // UI component references (assign in Inspector)
    public Text CurrentPlayText;       // Currently playing file name
    public Button RefreshBtn;              // Refresh button (refresh dance file list)
    public Button PrevBtn;                 // Previous button
    public Button PlayPauseBtn;            // Play/Pause button (currently only play)
    public Button NextBtn;                 // Next button
    public Button StopBtn;                 // Stop button
    public Button PlayModeBtn;             // Play mode button
    public TMP_Text PlayModeText;          // Play mode text
    public TMP_Text AvatarStatusText;      // Avatar status text

    public Dropdown DanceFileDropdown;     // Dance file dropdown (select to play)
    public TMP_Text _toggleKeyText;        // Assign text component in Inspector
    public Canvas targetCanvas;            // UI's Canvas component

    private Font _defaultLiberationFont;

    private Text _dropdownItemTemplateText;

    // Reference to player core
    public DancePlayerCore playerCore;
    public DanceResourceManager resourceManager;


    [Header("UI Toggle")]
    public KeyCode toggleKey = KeyCode.K; // Configurable toggle key

    // Variables for linking with game menu logic
    private MenuActions _gameMenuActions; // Game's existing MenuActions instance
    private MenuEntry _myUIMenuEntry;     // Your UI's corresponding MenuEntry (for adding/removing from list)
    private bool _isMyUIAddedToMenuList;  // Flag to prevent duplicate addition to menuEntries
    private bool _isRefreshingDropdown = false; // Flag to prevent multiple simultaneous refreshes


    void Start()
    {
        if (resourceManager != null)
        {
            _ = resourceManager.RefreshDanceFileList();
        }

        InitUI();

        BindButtonEvents();
        if (playerCore != null)
        {
            playerCore.InitPlayer();
            _ = RefreshDropdown();
        }
        UpdateToggleKeyText();

    }

    void Update()
    {

        UpdateUI();
        HandleKeyToggleUI();


    }

private void Awake()
    {
        //1. Load default font for fallback
        _defaultLiberationFont = Resources.Load<Font>("LiberationSans.ttf");
        // 2. 回退字体改为Legacy字体
        if (_defaultLiberationFont == null)
        {
            _defaultLiberationFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Debug.LogWarning("LiberationSans字体加载失败，已回退到Legacy字体");
        }

        //2. Find MenuActions instance in the scene
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

        //3. Set dropdown item font to support CJK characters
        if (DanceFileDropdown != null && DanceFileDropdown.template != null)
        {

            _dropdownItemTemplateText = DanceFileDropdown.itemText;
            _dropdownItemTemplateText.font = _defaultLiberationFont;
            if (_dropdownItemTemplateText != null)
            {

                _dropdownItemTemplateText.font = _defaultLiberationFont;
            }

        }

    }

    void OnDestroy()
    {
        // Remove all button listeners to prevent memory leaks
        PrevBtn?.onClick.RemoveAllListeners();
        PlayPauseBtn?.onClick.RemoveAllListeners();
        NextBtn?.onClick.RemoveAllListeners();
        StopBtn?.onClick.RemoveAllListeners();
        PlayModeBtn?.onClick.RemoveAllListeners();
        RefreshBtn?.onClick.RemoveAllListeners();
        DanceFileDropdown?.onValueChanged.RemoveAllListeners();

        // Remove from game menu list
        RemoveMyUIFromGameMenuList();
    }
    /// <summary>
    /// <summary>
    /// Initialize UI (set default state)
    /// </summary>
    private void InitUI()
    {
        CurrentPlayText.font = _defaultLiberationFont;
        CurrentPlayText.text = "Now Playing: None";

        PlayModeText.text = playerCore.GetPlayModeText();
        AvatarStatusText.text = "Avatar Status: Not Connected";

        if (DanceFileDropdown != null && DanceFileDropdown.captionText != null)
        {
            DanceFileDropdown.captionText.font = _defaultLiberationFont;
            DanceFileDropdown.captionText.text = "Select Dance File";
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
        DanceFileDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    /// <summary>
    /// Update UI in real-time (called every frame)
    /// </summary>
    private void UpdateUI()
    {
        if (playerCore == null) return;
        // Update current playing file name
        CurrentPlayText.text = $"Now Playing: {playerCore.GetCurrentPlayFileName()}";


        // Update avatar status
        string avatarStatus = playerCore.avatarHelper.IsAvatarAvailable()
            ? "Avatar Status: Connected"
            : "Avatar Status: Not Connected (auto retry)";
        AvatarStatusText.text = avatarStatus;

        // Update play mode text
        PlayModeText.text = playerCore.GetPlayModeText();

        // Update button states (disable play button when playing, disable stop/next button when not playing)
        bool isPlayerReady = playerCore.avatarHelper.IsAvatarAvailable() && resourceManager.DanceFileList.Count > 0;
        PlayPauseBtn.interactable = isPlayerReady && !playerCore.IsPlaying;
        PrevBtn.interactable = isPlayerReady && playerCore.IsPlaying;
        NextBtn.interactable = isPlayerReady && playerCore.IsPlaying;
        StopBtn.interactable = isPlayerReady && playerCore.IsPlaying;
        DanceFileDropdown.interactable = isPlayerReady && !playerCore.IsPlaying;


        if (playerCore.IsPlaying && playerCore.CurrentPlayIndex >= 0
            && playerCore.CurrentPlayIndex < playerCore.resourceManager.DanceFileList.Count)
        {
            string currentFile = resourceManager.DanceFileList[playerCore.CurrentPlayIndex];
            string displayName = currentFile.EndsWith(".unity3d", StringComparison.OrdinalIgnoreCase)
                ? currentFile.Substring(0, currentFile.Length - ".unity3d".Length)
                : currentFile;
            DanceFileDropdown.captionText.text = displayName;
        }
    }
    /// <summary>
    /// Update toggle key text
    /// </summary>
    public void UpdateToggleKeyText()
    {
        if (_toggleKeyText != null)
        {
            _toggleKeyText.text = $"Press [{toggleKey}] to show/hide UI";
        }
    }
    // 将 OnPlayPauseBtnClick 方法修改为 async，并在调用 PlayDanceByIndex 时加上 await
    private async void OnPlayPauseBtnClick()
    {
        // If not playing and dropdown has selection, play selected dance
        if (!playerCore.IsPlaying && DanceFileDropdown.value >= 0)
        {
            await playerCore.PlayDanceByIndex(DanceFileDropdown.value);
        }
    }

    /// <summary>
    /// Stop button click
    /// </summary>
    private void OnStopBtnClick()
    {
        playerCore.StopPlay();
        // Reset dropdown selection
        //DanceFileDropdown.value = -1;
        DanceFileDropdown.captionText.text = "Select Dance File";
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
    /// Dropdown selection change (play selected dance)
    /// </summary>
    private void OnDropdownValueChanged(int index)
    {
        //if (index >= 0 && index < playerCore.resourceManager.DanceFileList.Count)
        //{
        //    playerCore.PlayDanceByIndex(index);
        //}
    }

    /// <summary>
    /// Refresh dropdown (called when manually clicking refresh button)
    /// </summary>
    public async System.Threading.Tasks.Task RefreshDropdown()
    {
        if (_isRefreshingDropdown) return; // Prevent multiple simultaneous refreshes
        _isRefreshingDropdown = true;
        try
        {
            DanceFileDropdown.interactable = false;
            DanceFileDropdown.ClearOptions();
            DanceFileDropdown.captionText.text = "Loading...";
            await System.Threading.Tasks.Task.Run(() => playerCore.RefreshPlayList());
            List<string> danceFiles = resourceManager.DanceFileList;

            if (danceFiles.Count == 0)
            {
                DanceFileDropdown.options.Add(new Dropdown.OptionData("No dance files (put in CustomDances folder)"));
            }
            else
            {
                List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();
                foreach (var file in danceFiles)
                {
                    string displayName = file.EndsWith(".unity3d", StringComparison.OrdinalIgnoreCase)
                        ? file.Substring(0, file.Length - ".unity3d".Length)
                        : file;
                    options.Add(new Dropdown.OptionData(displayName));
                }
                DanceFileDropdown.AddOptions(options);
            }
            DanceFileDropdown.captionText.text = danceFiles.Count == 0 ? "No Files" : "Select Dance File";
        }
        catch (Exception e)
        {
            Debug.LogError($"下拉框刷新失败：{e.Message}");
            DanceFileDropdown.options.Add(new Dropdown.OptionData("Refresh Failed"));
        }
        finally
        {
            DanceFileDropdown.interactable = true;
            _isRefreshingDropdown = false;
        }
    }

    private void HandleKeyToggleUI()
    {
        if (targetCanvas == null) return;
        if (Input.GetKeyDown(toggleKey))
        {
            bool isActive = !targetCanvas.gameObject.activeSelf;
            targetCanvas.gameObject.SetActive(isActive);

            if (isActive) AddMyUIToGameMenuList();
            else RemoveMyUIFromGameMenuList();
        }
    }

    public void AddMyUIToGameMenuList()
    {
        if (_gameMenuActions == null || _isMyUIAddedToMenuList || _myUIMenuEntry == null) return;
        if (!_gameMenuActions.menuEntries.Exists(e => e.menu == gameObject))
        {
            _gameMenuActions.menuEntries.Add(_myUIMenuEntry);
            _isMyUIAddedToMenuList = true;
        }
    }


    public void RemoveMyUIFromGameMenuList()
    {
        if (_gameMenuActions == null || !_isMyUIAddedToMenuList || _myUIMenuEntry == null) return;
        _gameMenuActions.menuEntries.RemoveAll(e => e.menu == gameObject);
        _isMyUIAddedToMenuList = false;
    }
}