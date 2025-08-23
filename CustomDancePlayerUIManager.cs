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
    public TMP_Text CurrentPlayText;       // Currently playing file name
    public Button RefreshBtn;              // Refresh button (refresh dance file list)
    public Button PrevBtn;                 // Previous button
    public Button PlayPauseBtn;            // Play/Pause button (currently only play)
    public Button NextBtn;                 // Next button
    public Button StopBtn;                 // Stop button
    public Button PlayModeBtn;             // Play mode button
    public TMP_Text PlayModeText;          // Play mode text
    public TMP_Text AvatarStatusText;      // Avatar status text
    public TMP_Dropdown DanceFileDropdown; // Dance file dropdown (select to play)
    public TMP_Text _toggleKeyText;        // Assign text component in Inspector
    public Canvas targetCanvas;            // UI's Canvas component

    // Reference to player core
    public DancePlayerCore playerCore;

    public CustomDancePlayerFontHelper FontHelper; // Font helper for custom fonts

    [Header("UI Toggle")]
    public KeyCode toggleKey = KeyCode.K; // Configurable toggle key

    // Variables for linking with game menu logic
    private MenuActions _gameMenuActions; // Game's existing MenuActions instance
    private MenuEntry _myUIMenuEntry;     // Your UI's corresponding MenuEntry (for adding/removing from list)
    private bool _isMyUIAddedToMenuList;  // Flag to prevent duplicate addition to menuEntries

    

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

        FontHelper.Apply(CurrentPlayText);                 
        FontHelper.ApplyToDropdown(DanceFileDropdown);    
    }

    void Update()
    {

        UpdateUI();
        HandleKeyToggleUI();


    }

private void Awake()
    {
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
    }
    /// <summary>
    /// <summary>
    /// Initialize UI (set default state)
    /// </summary>
    private void InitUI()
    {
        CurrentPlayText.text = "Now Playing: None";
        PlayModeText.text = playerCore.GetPlayModeText();
        AvatarStatusText.text = "Avatar Status: Not Connected";
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

        // Dropdown selection for playback
        DanceFileDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    /// <summary>
    /// Update UI in real-time (called every frame)
    /// </summary>
    private void UpdateUI()
    {
        // Update current playing file name
        CurrentPlayText.text = $"Now Playing: {playerCore.GetCurrentPlayFileName()}";


        // Update avatar status
        if (playerCore.avatarHelper.IsAvatarAvailable())
        {
            AvatarStatusText.text = $"Avatar Status: Connected";
        }
        else
        {
            AvatarStatusText.text = "Avatar Status: Not Connected (auto retry after switching avatar)";
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
    private void RefreshFonts()
    {
        FontHelper.Apply(CurrentPlayText);
        FontHelper.ApplyToDropdown(DanceFileDropdown);
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
            DanceFileDropdown.options.Add(new TMP_Dropdown.OptionData("No dance files (put in CustomDances folder)"));
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
        RefreshFonts();
    }
    private void HandleKeyToggleUI()
    {
        if (targetCanvas == null) return;
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