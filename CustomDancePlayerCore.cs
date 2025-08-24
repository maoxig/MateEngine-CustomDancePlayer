using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Core player: handles play modes, track switching, and auto-next.
/// </summary>
public class DancePlayerCore : MonoBehaviour
{
    // Play mode enumeration (Sequence, Loop, Random)
    public enum PlayMode
    {
        Sequence,  
        Loop,      
        Random     
    }

    // Current play mode (default is Sequence)
    public PlayMode CurrentPlayMode { get; private set; } = PlayMode.Sequence;
    // Playlist (from resource manager)
    private List<string> _playList;
    // Current play index (-1 means not playing)
    public int CurrentPlayIndex { get; set; } = -1;
    // Whether currently playing
    public bool IsPlaying { get; private set; } = false;
    private float _audioStartTime;


    // Reference dependencies
    public AvatarHelper avatarHelper;
    public DanceResourceManager resourceManager;
    public DancePlayerUIManager uiManager;

    private readonly object _playLock = new object();

    void Update()
    {
        // Only check animation end when playing and avatar is available
        if (IsPlaying && avatarHelper.IsAvatarAvailable() && resourceManager.IsResourceLoaded())
        {
            CheckAnimationEnd();

        }
    }

    /// <summary>
    /// Initializes the player (gets the playlist from the resource manager)
    /// </summary>
    public void InitPlayer()
    {
        RefreshPlayList();
        CurrentPlayIndex = -1;
        IsPlaying = false;
#if UNITY_EDITOR
        Debug.Log("Player initialization completed");
#endif
    }

    /// <summary>
    /// Switch play mode (Sequence → Loop → Random → Sequence)
    /// </summary>
    public void TogglePlayMode()
    {
        CurrentPlayMode = (PlayMode)((int)(CurrentPlayMode + 1) % Enum.GetValues(typeof(PlayMode)).Length);

#if UNITY_EDITOR
        Debug.Log($"Switch play mode: {GetPlayModeText()}");
#endif
    }


    /// <summary>
    /// Get the play mode text (for UI display, supports internationalization)
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
    /// Plays the dance at the specified index
    /// </summary>
    public async Task<bool> PlayDanceByIndex(int index)
    {
        // Lock to prevent concurrent play requests
        lock (_playLock)
        {
            if (resourceManager.IsResourceLoading())
            {
                Debug.LogWarning("Player is busy, skip play request");
                return false;
            }
            if (IsPlaying)
            {
                Debug.Log("Stopping current play to start new one");
                StopPlay(); 
            }
        }

        // Pre-check conditions
        if (_playList == null || _playList.Count == 0)
        {
            Debug.LogError("Playlist is empty");
            return false;
        }
        if (index < 0 || index >= _playList.Count)
        {
            Debug.LogError($"Invalid index: {index} (max: {_playList.Count - 1})");
            return false;
        }
        if (!avatarHelper.IsAvatarAvailable())
        {
            Debug.LogError("Avatar not available");
            return false;
        }

        try
        {
            // 1. Record current index
            CurrentPlayIndex = index;
            string targetFileName = _playList[index];

            // 2. Asynchronously load resources
            bool loadSuccess = await resourceManager.LoadDanceResource(targetFileName);
            if (!loadSuccess)
            {
                IsPlaying = false;
                return false;
            }

            // 3. Make sure the avatar is still available after loading
            Animator animator = avatarHelper.CurrentAnimator;
            AudioSource audioSource = avatarHelper.CurrentAudioSource;

            // Set Parameters safely
            SafeSetAnimatorBool(animator, "isDancing", false);
            animator.runtimeAnimatorController = resourceManager.CurrentAnimatorCtrl;
            SafeSetAnimatorBool(animator, "isDancing", true);
            SafeSetAnimatorFloat(animator, "DanceIndex", 0);

            // Record the start time
            _audioStartTime = Time.time;

            // Play audio
            audioSource.Play();

            // Mark as playing
            IsPlaying = true;
#if UNITY_EDITOR
            Debug.Log($"Playing: {targetFileName} (Mode: {GetPlayModeText()})");
#endif
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to play dance: {e.Message}");
            IsPlaying = false;
            return false;
        }
    }
    /// <summary>
    /// Safely sets the Animator's Bool parameter (only sets if the parameter exists)
    /// </summary>
    private void SafeSetAnimatorBool(Animator animator, string paramName, bool value)
    {
        if (animator == null) return;


        int paramHash = Animator.StringToHash(paramName);
        if (HasAnimatorParameter(animator, paramHash, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(paramHash, value);
        }

    }

    /// <summary>
    /// Safely sets the Animator's Float parameter (only sets if the parameter exists)
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
    /// Checks if the Animator has a parameter of the specified type (does not rely on the trimmed class)
    /// </summary>
    private bool HasAnimatorParameter(Animator animator, int paramHash, AnimatorControllerParameterType type)
    {
        if (animator == null) return false;

        // Check all parameters (use basic API, do not rely on AnimatorController)
        foreach (var param in animator.parameters)
        {
            // AnimatorControllerParameter does not have nameHash property, use Animator.StringToHash(param.name)
            if (Animator.StringToHash(param.name) == paramHash && param.type == type)
            {
                return true;
            }
        }
        return false;
    }
    /// <summary>
    /// Plays the next track
    /// </summary>
    public async void PlayNext()
    {
        if (_playList == null || _playList.Count == 0) return;

        int nextIndex = CurrentPlayIndex;
        switch (CurrentPlayMode)
        {
            case PlayMode.Sequence:
                nextIndex = CurrentPlayIndex + 1;
                if (nextIndex >= _playList.Count)
                {
                    StopPlay();
                    return;
                }
                break;
            case PlayMode.Loop:
                nextIndex = CurrentPlayIndex;
                break;
            case PlayMode.Random:

                System.Random random = new System.Random(Guid.NewGuid().GetHashCode());
                if (_playList.Count == 1)
                {
                    nextIndex = 0;
                }
                else
                {
                    do
                    {
                        nextIndex = random.Next(0, _playList.Count);
                    } while (nextIndex == CurrentPlayIndex);
                }
                break;
        }

        // 异步播放下一曲
        await PlayDanceByIndex(nextIndex);
    }


    /// <summary>
    /// Plays the previous track
    /// </summary>
    public async void PlayPrev()
    {
        if (_playList == null || _playList.Count == 0) return;
        if (CurrentPlayIndex <= 0)
        {
            await PlayDanceByIndex(0);
            return;
        }

        // Plays the previous track
        await PlayDanceByIndex(CurrentPlayIndex - 1);
    }

    /// <summary>
    /// Stops playback (restores default animation)
    /// </summary>
    public void StopPlay()
    {
        if (!avatarHelper.IsAvatarAvailable())
        {
            return;
        }

        // 1. Stops audio and animation
        var audioSource = avatarHelper.CurrentAudioSource;
        var animator = avatarHelper.CurrentAnimator;
        audioSource.Stop();
        animator.SetBool("isDancing", false);
        SafeSetAnimatorBool(animator, "isDancing", false); // Safely set
        // 2. Restore default controller (ensure DefaultAnimatorController is correctly saved)
        if (avatarHelper.DefaultAnimatorController != null)
        {
            animator.runtimeAnimatorController = avatarHelper.DefaultAnimatorController;
            #if UNITY_EDITOR
            Debug.Log("Restored default animator controller");
            #endif
        }
        else
        {
            #if UNITY_EDITOR
            Debug.LogWarning("Default controller not saved, trying to re-fetch");
            #endif
        }

        // 3. Unload resources + reset state (keep unchanged)
        resourceManager.UnloadCurrentResource();
        IsPlaying = false;
        CurrentPlayIndex = -1;

    }
    /// <summary>
    /// Checks if the animation has finished playing (triggers automatic next track)
    /// </summary>

    private void CheckAnimationEnd()
    {
        if (!IsPlaying || !avatarHelper.IsAvatarAvailable() || !resourceManager.IsResourceLoaded())
            return;

        AudioSource audioSource = avatarHelper.CurrentAudioSource;
        AudioClip clip = resourceManager.CurrentAudioClip;

        // Double check: audio is not playin
        if (!audioSource.isPlaying)
        {
            // Invoke next track after a short delay to avoid rapid calls
            Invoke(nameof(PlayNext), 0.2f);
        }
    }
    /// <summary>
    /// Gets the current playing file name (for UI display)
    /// </summary>
    public string GetCurrentPlayFileName()
    {
        if (_playList == null || CurrentPlayIndex < 0 || CurrentPlayIndex >= _playList.Count)
        {
            return "Not Playing";
        }
        string fileName = _playList[CurrentPlayIndex];

        if (fileName.EndsWith(".unity3d", StringComparison.OrdinalIgnoreCase))
        {
            return fileName.Substring(0, fileName.Length - ".unity3d".Length);
        }
        return fileName;
    }

    /// <summary>
    /// Refreshes the playlist (called after adding/removing files)
    /// </summary>
    public async void RefreshPlayList()
    {
        await resourceManager.RefreshDanceFileList();
        _playList = resourceManager.DanceFileList;
        // If current play index exceeds new list length, reset to -1
        if (CurrentPlayIndex >= _playList.Count)
        {
            CurrentPlayIndex = -1;
            IsPlaying = false;
        }
#if UNITY_EDITOR
        Debug.Log($"Playlist refreshed: {_playList.Count} files in total");
#endif
        if (uiManager != null)
        {
            await uiManager.RefreshDropdown();
        }
    }
}