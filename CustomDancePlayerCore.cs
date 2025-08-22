using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 播放器核心：处理播放模式、切歌、自动下一首
/// </summary>
public class DancePlayerCore : MonoBehaviour
{
    // 播放模式枚举（顺序、循环、随机）
    public enum PlayMode
    {
        Sequence,  // 顺序播放
        Loop,      // 循环当前
        Random     // 随机播放
    }

    // 当前播放模式（默认顺序）
    public PlayMode CurrentPlayMode { get; private set; } = PlayMode.Sequence;
    // 播放列表（来自资源管理器）
    private List<string> _playList;
    // 当前播放索引（-1表示未播放）
    public int CurrentPlayIndex { get; set; } = -1;
    // 是否正在播放
    public bool IsPlaying { get; private set; } = false;


    // 引用依赖
    public AvatarHelper avatarHelper;
    public DanceResourceManager resourceManager;
    public DancePlayerUIManager uiManager;


    void Update()
    {
        // 仅在播放中且角色可用时，检查动画是否播放完成（触发自动下一首）
        if (IsPlaying && avatarHelper.IsAvatarAvailable() && resourceManager.IsResourceLoaded())
        {
            CheckAnimationEnd();

        }
    }

    /// <summary>
    /// 初始化播放器（从资源管理器获取播放列表）
    /// </summary>
    public void InitPlayer()
    {
        _playList = resourceManager.DanceFileList;
        CurrentPlayIndex = -1;
        IsPlaying = false;
        Debug.Log("Player initialization completed");
    }

    /// <summary>
    /// 切换播放模式（顺序→循环→随机→顺序）
    /// </summary>
    public void TogglePlayMode()
    {
        CurrentPlayMode = (PlayMode)((int)(CurrentPlayMode + 1) % Enum.GetValues(typeof(PlayMode)).Length);
        // 更新UI时会用到播放模式文本
        Debug.Log($"Switch play mode: {GetPlayModeText()}");
    }

    /// <summary>
    /// 获取播放模式的中文文本（给UI显示用）
    /// </summary>
    public string GetPlayModeText()
    {
        return CurrentPlayMode switch
        {
            PlayMode.Sequence => "Sequence",
            PlayMode.Loop => "Loop",
            PlayMode.Random => "Random",
            _ => "Sequence"
        };
    }

    /// <summary>
    /// 播放指定索引的舞蹈
    /// </summary>
    public bool PlayDanceByIndex(int index)
    {
        // Pre-check: valid index, avatar available, playlist not empty
        if (_playList == null || _playList.Count == 0)
        {
            Debug.LogError("Playlist is empty");
            return false;
        }
        if (index < 0 || index >= _playList.Count)
        {
            Debug.LogError("Invalid play index: " + index);
            return false;
        }
        if (!avatarHelper.IsAvatarAvailable())
        {
            Debug.LogError("Avatar not available, cannot play");
            return false;
        }

        // 1. 记录当前播放索引
        CurrentPlayIndex = index;
        string targetFileName = _playList[index];

        // 2. 加载对应的舞蹈资源
        bool loadSuccess = resourceManager.LoadDanceResource(targetFileName);
        if (!loadSuccess)
        {
            IsPlaying = false;
            return false;
        }

        // 3. 开始播放动画和音频
        Animator animator = avatarHelper.CurrentAnimator;
        AudioSource audioSource = avatarHelper.CurrentAudioSource;


        SafeSetAnimatorBool(animator, "isDancing", false);

        // 停止当前动画，切换控制器
        animator.SetBool("isDancing", false);
        animator.runtimeAnimatorController = resourceManager.CurrentAnimatorCtrl;

        // 开始播放舞蹈（调用角色控制器的StartDancing方法）
        animator.SetBool("isDancing", true);
        // 将 animator.SetFloat("DanceIndex", 0); 替换为如下代码
        animator.SetFloat(Animator.StringToHash("DanceIndex"), 0); // 使用哈希ID而非字符串

        // 播放音频
        audioSource.Play();

        // 标记为播放中
        IsPlaying = true;
        Debug.Log($"Start playing: {targetFileName} (Mode: {GetPlayModeText()})");
        return true;
    }
    /// <summary>
    /// 安全设置Animator的Bool参数（仅在参数存在时设置）
    /// </summary>
    private void SafeSetAnimatorBool(Animator animator, string paramName, bool value)
    {
        if (animator == null) return;

        // 使用哈希值检查参数是否存在（避免字符串重复计算）
        int paramHash = Animator.StringToHash(paramName);
        if (HasAnimatorParameter(animator, paramHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(paramHash, value);
        }
        // 不存在时不操作，避免警告
    }

    /// <summary>
    /// 安全设置Animator的Float参数（仅在参数存在时设置）
    /// </summary>
    private void SafeSetAnimatorFloat(Animator animator, string paramName, float value)
    {
        if (animator == null) return;

        int paramHash = Animator.StringToHash(paramName);
        if (HasAnimatorParameter(animator, paramHash, AnimatorControllerParameterType.Float))
        {
            animator.SetFloat(paramHash, value);
        }
    }

    /// <summary>
    /// 检查Animator是否存在指定类型的参数（不依赖被裁剪的类）
    /// </summary>
    private bool HasAnimatorParameter(Animator animator, int paramHash, AnimatorControllerParameterType type)
    {
        if (animator == null) return false;

        // 遍历所有参数检查（使用基础API，不依赖AnimatorController）
        foreach (var param in animator.parameters)
        {
            // AnimatorControllerParameter 没有 nameHash 属性，需用 Animator.StringToHash(param.name)
            if (Animator.StringToHash(param.name) == paramHash && param.type == type)
            {
                return true;
            }
        }
        return false;
    }
    /// <summary>
    /// 播放下一首
    /// </summary>
    public void PlayNext()
    {
        if (_playList == null || _playList.Count == 0) return;

        int nextIndex = CurrentPlayIndex;
        switch (CurrentPlayMode)
        {
            case PlayMode.Sequence:
                // 顺序：当前索引+1，到末尾则停止
                nextIndex = CurrentPlayIndex + 1;
                if (nextIndex >= _playList.Count)
                {
                    Debug.Log("Reached the last song, stopping playback");

                    StopPlay();
                    return;
                }
                break;
            case PlayMode.Loop:
                // 循环：保持当前索引（重新播放）
                nextIndex = CurrentPlayIndex;
                break;
            case PlayMode.Random:
                // 随机：生成不同于当前的索引（列表长度>1时）
                System.Random random = new System.Random();
                do
                {
                    nextIndex = random.Next(0, _playList.Count);
                } while (_playList.Count > 1 && nextIndex == CurrentPlayIndex);
                break;
        }

        // 播放下一首
        PlayDanceByIndex(nextIndex);
    }

    /// <summary>
    /// 播放上一首
    /// </summary>
    public void PlayPrev()
    {
        if (_playList == null || _playList.Count == 0) return;
        if (CurrentPlayIndex <= 0)
        {
            Debug.Log("Reached the first song, replaying current");
            PlayDanceByIndex(0);
            return;
        }

        // 上一首：当前索引-1
        PlayDanceByIndex(CurrentPlayIndex - 1);
    }

    /// <summary>
    /// 停止播放（恢复默认动画）
    /// </summary>
    public void StopPlay()
    {
        if (!avatarHelper.IsAvatarAvailable())
        {
            Debug.LogWarning("Avatar not available, cannot stop playback");
            return;
        }

        // 1. 停止音频和动画
        var audioSource = avatarHelper.CurrentAudioSource;
        var animator = avatarHelper.CurrentAnimator;
        audioSource.Stop();
        animator.SetBool("isDancing", false);
        SafeSetAnimatorBool(animator, "isDancing", false); // 安全设置
        // 2. 关键修复：恢复默认控制器（确保DefaultAnimatorController已正确保存）
        if (avatarHelper.DefaultAnimatorController != null)
        {
            animator.runtimeAnimatorController = avatarHelper.DefaultAnimatorController;
            Debug.Log("已恢复默认动画控制器");
        }
        else
        {
            Debug.LogWarning("未保存默认控制器，尝试重新获取");

        }

        // 3. 卸载资源+重置状态（保持不变）
        resourceManager.UnloadCurrentResource();
        IsPlaying = false;
        CurrentPlayIndex = -1;

        Debug.Log("Playback stopped and resources unloaded");
    }
    /// <summary>
    /// 检查动画是否播放完成（触发自动下一首）
    /// </summary>
    private void CheckAnimationEnd()
    {
        if (!IsPlaying || !avatarHelper.IsAvatarAvailable()) return;


        Animator animator = avatarHelper.CurrentAnimator;
        float normalizedTime = avatarHelper.GetAnimatorNormalizedTime();

        // 后续判断逻辑同方案1
        bool isUsingDanceController = animator.runtimeAnimatorController == resourceManager.CurrentAnimatorCtrl;
        bool isNotTransition = !animator.IsInTransition(0);
        bool isAnimationEnd = normalizedTime >= 1f;

        if (isUsingDanceController && isNotTransition && isAnimationEnd)
        {
            Debug.Log("舞蹈动画播放完成，自动切换下一首");
            PlayNext();
        }
    }

    /// <summary>
    /// 获取当前播放进度（0~1，给进度条用）
    /// </summary>

    /// <summary>
    /// 获取当前播放的文件名（给UI显示用）
    /// </summary>
    public string GetCurrentPlayFileName()
    {
        if (_playList == null || CurrentPlayIndex < 0 || CurrentPlayIndex >= _playList.Count)
        {
            return "Not Playing";
        }
        string fileName = _playList[CurrentPlayIndex];
        // 只在显示时隐藏.unity3d后缀，但不修改原始数据
        if (fileName.EndsWith(".unity3d", StringComparison.OrdinalIgnoreCase))
        {
            return fileName.Substring(0, fileName.Length - ".unity3d".Length);
        }
        return fileName;
    }

    /// <summary>
    /// 刷新播放列表（当文件夹新增/删除文件时调用）
    /// </summary>
    public void RefreshPlayList()
    {
        resourceManager.RefreshDanceFileList();
        _playList = resourceManager.DanceFileList;
        // 如果当前播放索引超出新列表长度，重置为-1
        if (CurrentPlayIndex >= _playList.Count)
        {
            CurrentPlayIndex = -1;
            IsPlaying = false;
        }
        Debug.Log($"Playlist refreshed: {_playList.Count} files in total");

        if (uiManager != null)
        {
            uiManager.RefreshDropdown();
        }
    }
}