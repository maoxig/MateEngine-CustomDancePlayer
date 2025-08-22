using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI管理器：绑定按钮事件，更新播放状态
/// </summary>
public class DancePlayerUIManager : MonoBehaviour
{
    // UI控件引用（在Inspector赋值）
    public TMP_Text CurrentPlayText;       // 当前播放文件名
    public Slider ProgressSlider;          // 播放进度条
    public Button PrevBtn;                 // 上一首按钮
    public Button PlayPauseBtn;            // 播放/暂停按钮（当前仅播放）
    public Button NextBtn;                 // 下一首按钮
    public Button StopBtn;                 // 停止按钮
    public Button PlayModeBtn;             // 播放模式按钮
    public TMP_Text PlayModeText;          // 播放模式文本
    public TMP_Text AvatarStatusText;      // 角色状态文本
    public TMP_Dropdown DanceFileDropdown; // 舞蹈文件下拉框（选择播放）

    // 引用播放器核心
    public DancePlayerCore playerCore;

    private Canvas _uiCanvas; // 本UI的Canvas组件（控制显示/隐藏）

    [Header("UI Toggle")]
    public KeyCode toggleKey = KeyCode.K; // 可配置的切换按键
    private bool _isUIVisible; // UI当前显示状态
    private bool _lastUIVisibleState = false;

    [Header("Bone Follow")]
    public bool followBone = true;
    public HumanBodyBones targetBone = HumanBodyBones.Head;
    [Range(0f, 1f)] public float followSmoothness = 0.8f;
    private RectTransform _uiRect; // 本UI的RectTransform（用于位置更新）
    [Tooltip("UI相对于骨骼的像素偏移：X=左右（正数右移，负数左移），Y=上下（正数上移，负数下移）")]
    public Vector2 followOffset = new Vector2(0, 150); // 默认上移150像素（避免紧贴骨骼）
    private Vector3 _targetScreenPos;

    public float followScale = 1f; // 默认1，可放大到5-20倍测试

    private Camera _mainCamera; // 新增缓存
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
    }

    void Update()
    {
        // 最高优先级：处理moveCanvas状态（确保先于游戏其他逻辑）
        bool currentUIVisible = IsUIVisible();
        if (currentUIVisible != _lastUIVisibleState)
        {
            // 状态变化时才执行，减少性能消耗
            _lastUIVisibleState = currentUIVisible;
        }

        HandleKeyToggleUI();
        UpdateUI();
        // 如果启用跟随且UI可见，更新位置
        if (followBone && _uiRect != null && _uiCanvas != null && _uiCanvas.enabled)
        {
            UpdateUIFollowBone();
        }
    }

    private void Awake()
    {

        // 获取本UI面板的RectTransform（确保脚本挂在UI面板上或其直接父节点）
        _uiRect = GetComponent<RectTransform>();
        if (_uiRect == null)
        {
            // 尝试从父节点获取（防止脚本挂在子节点）
            _uiRect = GetComponentInParent<RectTransform>();
            if (_uiRect == null)
            {
                Debug.LogError("UI面板未找到RectTransform组件！");
                followBone = false; // 禁用跟随功能
            }
        }
        // 获取本UI的Canvas（确保脚本挂在Canvas节点或其子节点）
        _uiCanvas = GetComponentInParent<Canvas>();
        if (_uiCanvas == null)
        {
            Debug.LogWarning("DancePlayerUIManager: UI Canvas not found!");
        }
        else
        {
            _isUIVisible = _uiCanvas.enabled;
        }
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            Debug.LogWarning("MainCamera not found, bone follow may fail");
            followBone = false;
        }
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
        ProgressSlider.value = 0f;
        ProgressSlider.interactable = false; // Progress bar is not draggable by default (simplified)
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

        // 更新进度条
        if (playerCore.IsPlaying && playerCore.avatarHelper.IsAvatarAvailable())
        {
            float normalizedTime = playerCore.avatarHelper.GetAnimatorNormalizedTime(); // 后续可封装到AvatarHelper
            ProgressSlider.value = Mathf.Clamp01(normalizedTime);
        }
        else
        {
            ProgressSlider.value = 0f;
        }

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
    /// 播放/暂停按钮点击（当前仅支持播放，暂停需额外处理动画暂停）
    /// </summary>
    private void OnPlayPauseBtnClick()
    {
        // 如果未播放，且下拉框有选择，播放选中的舞蹈
        if (!playerCore.IsPlaying && DanceFileDropdown.value >= 0)
        {
            playerCore.PlayDanceByIndex(DanceFileDropdown.value);
        }
        // （可选）添加暂停功能：需处理Animator.SetBool("isDancing", false)和音频Pause()
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
    /// 准确判断本UI是否处于可见状态
    /// </summary>
    private bool IsUIVisible()
    {
        // 同时检查Canvas是否启用 + 自身 GameObject 是否激活
        bool canvasEnabled = _uiCanvas != null && _uiCanvas.enabled;
        bool gameObjectActive = gameObject.activeSelf; // 假设脚本挂在UI根节点
        return canvasEnabled && gameObjectActive;
    }

    /// <summary>
    /// K键切换UI显示/隐藏
    /// </summary>
    private void HandleKeyToggleUI()
    {
        if (_uiCanvas == null) return;

        if (Input.GetKeyDown(toggleKey))
        {
            _isUIVisible = !_isUIVisible;
            _uiCanvas.enabled = _isUIVisible;
            _lastUIVisibleState = _isUIVisible;
        }
    }
    private void UpdateUIFollowBone()
    {
        // 1. 基础检查（角色、组件是否可用）
        if (!playerCore.avatarHelper.IsAvatarAvailable() || _uiRect == null || _uiCanvas == null || _mainCamera == null)
            return;

        Animator targetAnimator = playerCore.avatarHelper.CurrentAnimator;
        Transform boneTransform = targetAnimator.GetBoneTransform(targetBone);
        if (boneTransform == null || Camera.main == null)
            return;

        // 2. 骨骼世界位置 → 屏幕坐标
        Vector3 boneScreenPos = _mainCamera.WorldToScreenPoint(boneTransform.position);
        // 放大骨骼自身的移动幅度（可选，根据角色运动幅度调整）
        boneScreenPos = new Vector3(
            boneScreenPos.x * (1 + followScale * 0.1f),  // 轻微放大X方向运动
            boneScreenPos.y * (1 + followScale * 0.1f),  // 轻微放大Y方向运动
            boneScreenPos.z
        );
        // 3. 平滑插值（避免UI抖动）
        _targetScreenPos = Vector3.Lerp(_targetScreenPos, boneScreenPos, 1f - followSmoothness);

        // 4. 关键：将屏幕坐标+偏移量 → UI世界坐标
        RectTransform canvasRect = _uiCanvas.GetComponent<RectTransform>();
        if (canvasRect == null)
            return;

        // 4.1 先给目标屏幕位置添加偏移量（像素单位）
        Vector3 offsetScreenPos = _targetScreenPos + new Vector3(
            followOffset.x * followScale,  // X方向偏移 × 缩放因子
            followOffset.y * followScale,  // Y方向偏移 × 缩放因子
            0
        );

        // 4.2 转换为UI坐标（兼容所有Canvas渲染模式）
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
            canvasRect,          // Canvas的RectTransform
            offsetScreenPos,     // 带偏移的屏幕位置
            _uiCanvas.worldCamera, // Canvas的相机（Overlay模式下为null，自动兼容）
            out Vector3 uiWorldPos))
        {
            // 5. 应用最终位置到UI
            _uiRect.position = uiWorldPos;
        }
        // （可选）调试用：打印当前位置和偏移，方便定位问题
        // Debug.Log($"骨骼屏幕位置：{boneScreenPos} | 偏移后：{offsetScreenPos} | UI位置：{_uiRect.position}");
    }
}