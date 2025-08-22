using LLMUnitySamples;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI管理器：绑定按钮事件，更新播放状态
/// </summary>
public class DancePlayerUIManager : MonoBehaviour
{
    // UI控件引用（在Inspector赋值）
    public TMP_Text CurrentPlayText;       // 当前播放文件名
    public Button PrevBtn;                 // 上一首按钮
    public Button PlayPauseBtn;            // 播放/暂停按钮（当前仅播放）
    public Button NextBtn;                 // 下一首按钮
    public Button StopBtn;                 // 停止按钮
    public Button PlayModeBtn;             // 播放模式按钮
    public TMP_Text PlayModeText;          // 播放模式文本
    public TMP_Text AvatarStatusText;      // 角色状态文本
    public TMP_Dropdown DanceFileDropdown; // 舞蹈文件下拉框（选择播放）
    [Header("Key Prompt")]
    public TMP_Text _toggleKeyText; // 在Inspector中拖入文本组件


    // 引用播放器核心
    public DancePlayerCore playerCore;


    [Header("UI Toggle")]
    public KeyCode toggleKey = KeyCode.K; // 可配置的切换按键


    // 新增：联动游戏菜单逻辑的变量
    private MenuActions _gameMenuActions; // 游戏原有MenuActions实例
    private MenuEntry _myUIMenuEntry;     // 你的UI对应的MenuEntry（用于加入/移除列表）
    private bool _isMyUIAddedToMenuList;  // 标记是否已加入menuEntries（防止重复添加）
    private Canvas _uiCanvas;         // 你的UI的Canvas组件


    void Start()
    {
        if (playerCore != null && playerCore.resourceManager != null)
        {
            playerCore.resourceManager.RefreshDanceFileList();
        }
        // 1. 初始化UI状态
        InitUI();
        // 2. 绑定按钮事件
        BindButtonEvents();


        // 3. 初始化播放器
        playerCore.InitPlayer();
        RefreshDropdown();
        UpdateToggleKeyText(IsUIVisible());
    }

    void Update()
    {

        UpdateUI();

    }

    private void Awake()
    {
        _gameMenuActions = UnityEngine.Object.FindFirstObjectByType<MenuActions>();
        if (_gameMenuActions == null)
        {
            Debug.LogWarning("未找到游戏的MenuActions脚本，UI点击和滚轮控制可能失效！");
            return;
        }

        // 新增：创建你的UI对应的MenuEntry（配置为“打开时禁用moveCanvas和滚轮”）
        _myUIMenuEntry = new MenuEntry
        {
            menu = gameObject,                  // 你的UI面板根对象（脚本挂载的对象）
            blockMovement = true,               // 关键：打开时禁用moveCanvas和滚轮缩放
            blockHandTracking = false,          // 不影响游戏手部追踪（按需调整）
            blockReaction = false,              // 不影响角色反应（按需调整）
            blockChibiMode = false              // 不影响小人模式（按需调整）
        };

        // 获取本UI的Canvas（确保脚本挂在Canvas节点或其子节点）
        _uiCanvas = GetComponentInParent<Canvas>();


    }

    private void OnDestroy()
    {
        // 移除按钮事件监听，防止内存泄漏
        PrevBtn.onClick.RemoveAllListeners();
        PlayPauseBtn.onClick.RemoveAllListeners();
        NextBtn.onClick.RemoveAllListeners();
        StopBtn.onClick.RemoveAllListeners();
        PlayModeBtn.onClick.RemoveAllListeners();
        DanceFileDropdown.onValueChanged.RemoveAllListeners();
    }
    /// <summary>
    /// 初始化UI（设置默认状态）
    /// </summary>
    private void InitUI()
    {
        CurrentPlayText.text = "Now Playing: None";
        PlayModeText.text = playerCore.GetPlayModeText();
        AvatarStatusText.text = "Avatar Status: Not Connected";
    }

    /// <summary>
    /// 绑定所有按钮事件
    /// </summary>
    private void BindButtonEvents()
    {
        PrevBtn.onClick.AddListener(playerCore.PlayPrev);
        PlayPauseBtn.onClick.AddListener(OnPlayPauseBtnClick);
        NextBtn.onClick.AddListener(playerCore.PlayNext);
        StopBtn.onClick.AddListener(OnStopBtnClick);
        PlayModeBtn.onClick.AddListener(OnPlayModeBtnClick);
        // 下拉框选择播放
        DanceFileDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    /// <summary>
    /// 实时更新UI（每帧调用）
    /// </summary>
    private void UpdateUI()
    {
        // 更新当前播放文件名
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

        // 更新播放模式文本
        PlayModeText.text = playerCore.GetPlayModeText();

        // 更新按钮状态（播放中禁用播放按钮，未播放禁用停止/切歌按钮）
        bool isPlayerReady = playerCore.avatarHelper.IsAvatarAvailable() && playerCore.resourceManager.DanceFileList.Count > 0;
        PlayPauseBtn.interactable = isPlayerReady && !playerCore.IsPlaying;
        PrevBtn.interactable = isPlayerReady && playerCore.IsPlaying;
        NextBtn.interactable = isPlayerReady && playerCore.IsPlaying;
        StopBtn.interactable = isPlayerReady && playerCore.IsPlaying;
        DanceFileDropdown.interactable = isPlayerReady && !playerCore.IsPlaying;

        // 无论是否播放，都更新下拉框显示的当前选中项
        if (playerCore.IsPlaying && playerCore.CurrentPlayIndex >= 0
            && playerCore.CurrentPlayIndex < playerCore.resourceManager.DanceFileList.Count)
        {
            string currentFileName = playerCore.resourceManager.DanceFileList[playerCore.CurrentPlayIndex];
            if (currentFileName.EndsWith(".unity3d", StringComparison.OrdinalIgnoreCase))
            {
                currentFileName = currentFileName.Substring(0, currentFileName.Length - ".unity3d".Length);
            }
            // 更新下拉框文本（即使禁用状态）
            DanceFileDropdown.captionText.text = currentFileName;
        }
    }
    /// <summary>
    /// 暴露给父组件：更新按键提示文本（传入当前显示状态）
    /// </summary>
    public void UpdateToggleKeyText(bool isVisible)
    {
        if (_toggleKeyText != null)
        {
            _toggleKeyText.text = isVisible
                ? $"Hide UI (Press {toggleKey})"
                : $"Show UI (Press {toggleKey})";
        }
    }
    /// <summary>
    /// 播放/暂停按钮点击（当前仅支持播放，暂停需额外处理动画暂停）
    /// </summary>
    private void OnPlayPauseBtnClick()
    {
        // 如果未播放，且下拉框有选择，播放选中的舞蹈
        if (!playerCore.IsPlaying && DanceFileDropdown.value >= 0)
        {
            playerCore.PlayDanceByIndex(DanceFileDropdown.value);
        }
       
    }
    /// <summary>
    /// 修复：实时判断UI是否可见（直接读Canvas.enabled，不依赖缓存变量）
    /// </summary>
    public bool IsUIVisible()
    {
        return _uiCanvas != null && _uiCanvas.enabled;
    }
    /// <summary>
    /// 停止按钮点击
    /// </summary>
    private void OnStopBtnClick()
    {
        playerCore.StopPlay();
        Debug.Log("StopBtn is clicked");
        // 重置下拉框选择
        //DanceFileDropdown.value = -1;
        DanceFileDropdown.captionText.text = "Select Dance File";
        RefreshDropdown();
    }

    /// <summary>
    /// 播放模式按钮点击
    /// </summary>
    private void OnPlayModeBtnClick()
    {
        playerCore.TogglePlayMode();
        // 播放模式文本在Update中自动更新
    }

    /// <summary>
    /// 下拉框选择变化（播放选中的舞蹈）
    /// </summary>
    private void OnDropdownValueChanged(int index)
    {
        //if (index >= 0 && index < playerCore.resourceManager.DanceFileList.Count)
        //{
        //    playerCore.PlayDanceByIndex(index);
        //}
    }

    /// <summary>
    /// 刷新下拉框（手动点击刷新按钮时调用）
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
            // 处理：去除.unity3d后缀，仅显示文件名
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


    /// <summary>
    /// 将你的UI加入MenuActions的menuEntries，禁用moveCanvas和滚轮
    /// </summary>
    public void AddMyUIToGameMenuList()
    {
        if (_gameMenuActions == null || _isMyUIAddedToMenuList || _myUIMenuEntry == null)
            return;

        // 防止重复添加（遍历检查是否已存在）
        bool isAlreadyInList = _gameMenuActions.menuEntries.Exists(
            entry => entry.menu == gameObject
        );
        if (!isAlreadyInList)
        {
            _gameMenuActions.menuEntries.Add(_myUIMenuEntry);
            _isMyUIAddedToMenuList = true;
            Debug.Log("你的UI已加入游戏菜单列表，禁用moveCanvas和滚轮缩放");
        }
    }

    /// <summary>
    /// 将你的UI从MenuActions的menuEntries移除，恢复moveCanvas和滚轮
    /// </summary>
    public void RemoveMyUIFromGameMenuList()
    {
        if (_gameMenuActions == null || !_isMyUIAddedToMenuList || _myUIMenuEntry == null)
            return;

        // 从列表中移除你的UI对应的MenuEntry
        _gameMenuActions.menuEntries.RemoveAll(
            entry => entry.menu == gameObject
        );
        _isMyUIAddedToMenuList = false;
        Debug.Log("你的UI已从游戏菜单列表移除，恢复moveCanvas和滚轮缩放");
    }
}