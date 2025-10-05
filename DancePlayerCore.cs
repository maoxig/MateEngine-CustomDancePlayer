using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace CustomDancePlayer
{
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


        [Header("Playback Settings")]
        [Tooltip("Current play mode (default is Sequence)")]
        public PlayMode CurrentPlayMode { get; set; } = PlayMode.Sequence;

        public bool autoPlayOnStart = false;


        // Playlist (from resource manager)
        private List<string> _playList;
        // Current play index (-1 means not playing)
        [Tooltip("Current play index (-1 means not playing)")]
        public int CurrentPlayIndex { get; set; } = -1;
        // Whether currently playing
        [Tooltip("Whether currently playing")]
        public bool IsPlaying { get; private set; } = false;

        [Tooltip("Audio start time (for sync/debug)")]
        public float AudioStartTime;

        // Whether the dance has ended
        private bool _danceEnded = false;

        // Reference dependencies
        [Header("References")]
        public DanceAvatarHelper avatarHelper;
        public DanceResourceManager resourceManager;
        public DancePlayerUIManager uiManager;

        // animation start delay (seconds). default 0.3s
        [Tooltip("Animation start delay (seconds). Default 0.3s")]
        [Range(0f, 1f)]
        public float AnimationStartDelay = 0.3f;

        // Coroutine handle for delayed animation start (so we can cancel)
        private Coroutine _startAnimationCoroutine = null;

        // Suppress end-check while waiting for animation to be applied (avoid false-positive "End")
        private bool _suppressEndCheck = false;


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

            _playList = resourceManager.DanceFileList ?? new List<string>();
            IsPlaying = false;
#if DEBUG
        Debug.Log("Player initialization completed");
#endif
            if (autoPlayOnStart && _playList.Count > 0 && CurrentPlayIndex >= 0)
            {
                PlayDanceByIndex(CurrentPlayIndex);
            }
        }

        /// <summary>
        /// Set and persist animation start delay (clamped 0..1)
        /// </summary>
        public void SetAnimationStartDelay(float seconds)
        {
            AnimationStartDelay = Mathf.Clamp(seconds, 0f, 1f);
        }

        /// <summary>
        /// Switch play mode (Sequence → Loop → Random → Sequence)
        /// </summary>
        public void TogglePlayMode()
        {
            CurrentPlayMode = (PlayMode)((int)(CurrentPlayMode + 1) % Enum.GetValues(typeof(PlayMode)).Length);

#if DEBUG
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
        /// Behavior: audio starts immediately; animation is applied after AnimationStartDelay (can be 0).
        /// </summary>
        public bool PlayDanceByIndex(int index)
        {
            // Pre-check: valid index, avatar available, playlist not empty
            if (_playList == null || _playList.Count == 0)
            {
#if DEBUG
            Debug.LogError("Playlist is empty");
#endif
                return false;
            }
            if (index < 0 || index >= _playList.Count)
            {
                return false;
            }
            if (!avatarHelper.IsAvatarAvailable())
            {
                return false;
            }

            // 1. Record the current play index
            CurrentPlayIndex = index;
            string targetFileName = _playList[index];

            // 2. Load the corresponding dance resource
            bool loadSuccess = resourceManager.LoadDanceResource(targetFileName);
            if (!loadSuccess)
            {
                IsPlaying = false;
                return false;
            }

            // 3. Start playing the animation and audio
            Animator animator = avatarHelper.CurrentAnimator;
            AudioSource audioSource = avatarHelper.CurrentAudioSource;

            // Stop any pending animation-start coroutine from previous track
            if (_startAnimationCoroutine != null)
            {
                StopCoroutine(_startAnimationCoroutine);
                _startAnimationCoroutine = null;
            }

            if (avatarHelper.CurrentOverrideController != null)
            {
                Destroy(avatarHelper.CurrentOverrideController);
                avatarHelper.CurrentOverrideController = null;
            }

            if (avatarHelper.TargetSMR != null)
            {
                // set  BlendShapeProxy to disable 
                var proxy = avatarHelper.CurrentAnimator.GetComponent<UniversalBlendshapes>();
                if (proxy != null) proxy.enabled = false;
            }
            else
            {
                avatarHelper.SetupDummyForDance();
            }

            // Important: reset flags and mark that animation is pending (suppress "End" checks)
            ResetDanceEndFlag();
            _suppressEndCheck = true;

            // Start audio immediately and record start time
            AudioStartTime = Time.time;
            audioSource.Play();

            // Mark as playing (so UI and Update know)
            IsPlaying = true;

            // If delay is effectively zero -> start animation immediately; else start coroutine
            float delay = Mathf.Clamp(AnimationStartDelay, 0f, 1f);
            if (delay <= 0.0001f)
            {
                ApplyAnimationImmediately(animator, resourceManager.CurrentAnimationClip);
            }
            else
            {
                _startAnimationCoroutine = StartCoroutine(StartAnimationAfterDelay(animator, resourceManager.CurrentAnimationClip, delay));
            }

#if DEBUG
        Debug.Log($"Start playing: {targetFileName} (Mode: {GetPlayModeText()}, anim delay {AnimationStartDelay:F3}s)");
#endif
            return true;
        }

        private void ApplyAnimationImmediately(Animator animator, AnimationClip clip)
        {
            if (animator == null || clip == null) return;

            var overrideController = new AnimatorOverrideController(avatarHelper.CustomDanceAvatarController);
            overrideController["CUSTOM_DANCE"] = clip;
            animator.runtimeAnimatorController = overrideController;
            avatarHelper.CurrentOverrideController = overrideController;

            animator.SetBool("isDancing", true);

            // allow checking for End now
            _suppressEndCheck = false;
        }

        private IEnumerator StartAnimationAfterDelay(Animator animator, AnimationClip clip, float delay)
        {
            yield return new WaitForSeconds(delay);

            // if we have been stopped in the meantime, abort
            if (!IsPlaying || animator == null || clip == null)
            {
                _startAnimationCoroutine = null;
                _suppressEndCheck = false;
                yield break;
            }

            ApplyAnimationImmediately(animator, clip);

            _startAnimationCoroutine = null;
        }

        /// <summary>
        /// Plays the next track
        /// </summary>
        public void PlayNext()
        {
            if (_playList == null || _playList.Count == 0) return;

            int nextIndex = CurrentPlayIndex;

            switch (CurrentPlayMode)
            {
                case PlayMode.Sequence:
                    // Sequence: Current index +1, stop at the end
                    nextIndex = CurrentPlayIndex + 1;
                    if (nextIndex >= _playList.Count)
                    {

                        StopPlay();
                        return;
                    }
                    break;
                case PlayMode.Loop:
                    // Loop: Keep current index (replay)
                    nextIndex = CurrentPlayIndex;
                    break;
                case PlayMode.Random:
                    // Random: Generate an index different from the current one (when list length > 1)
                    System.Random random = new System.Random();
                    do
                    {
                        nextIndex = random.Next(0, _playList.Count);
                    } while (_playList.Count > 1 && nextIndex == CurrentPlayIndex);
                    break;
            }

            // Plays the next track
            PlayDanceByIndex(nextIndex);
        }

        /// <summary>
        /// Plays the previous track
        /// </summary>
        public void PlayPrev()
        {
            if (_playList == null || _playList.Count == 0) return;
            if (CurrentPlayIndex <= 0)
            {
                PlayDanceByIndex(0);
                return;
            }

            // Plays the previous track
            PlayDanceByIndex(CurrentPlayIndex - 1);
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

            // 1. Stops any pending animation-start coroutine
            if (_startAnimationCoroutine != null)
            {
                StopCoroutine(_startAnimationCoroutine);
                _startAnimationCoroutine = null;
            }
            _suppressEndCheck = false;

            // 2. Stops audio and animation
            var audioSource = avatarHelper.CurrentAudioSource;
            var animator = avatarHelper.CurrentAnimator;
            audioSource.Stop();
            animator.SetBool("isDancing", false);

            // 3. Restore default controller (ensure DefaultAnimatorController is correctly saved)
            if (avatarHelper.DefaultAnimatorController != null)
            {
                animator.runtimeAnimatorController = avatarHelper.DefaultAnimatorController;
#if DEBUG
            Debug.Log("Restored default animator controller");
#endif
            }
            else
            {
#if DEBUG
            Debug.LogWarning("Default controller not saved, trying to re-fetch");
#endif
            }
            if (avatarHelper.TargetSMR != null)
            {
                // set  BlendShapeProxy to enable
                var proxy = avatarHelper.CurrentAnimator.GetComponent<UniversalBlendshapes>();
                if (proxy != null) proxy.enabled = true;
            }
            else
            {
                avatarHelper.RestoreOriginalBody();
            }

            // 4. Unload resources + reset state (keep unchanged)
            resourceManager.UnloadCurrentResource();
            IsPlaying = false;
            _danceEnded = false;
        }

        /// <summary>
        /// Checks if the animation has finished playing (triggers automatic next track)
        /// </summary>

        private void CheckAnimationEnd()
        {
            if (!IsPlaying || !avatarHelper.IsAvatarAvailable() || !resourceManager.IsResourceLoaded())
                return;

            // while animation is pending to be applied, skip end-check
            if (_suppressEndCheck)
                return;

            var animator = avatarHelper.CurrentAnimator;
            var audioSource = avatarHelper.CurrentAudioSource;

            if (animator != null)
            {
                var stateInfo = animator.GetCurrentAnimatorStateInfo(0); // 默认 layer 0
                if (!_danceEnded && stateInfo.IsName("End"))
                {
                    _danceEnded = true;
                    PlayNext();
                    return;
                }
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
        public void ResetDanceEndFlag()
        {
            _danceEnded = false;
        }
        /// <summary>
        /// Refreshes the playlist (called after adding/removing files)
        /// </summary>
        public void RefreshPlayList()
        {
            resourceManager.RefreshDanceFileList();
            _playList = resourceManager.DanceFileList;
            // If current play index exceeds new list length, reset to -1
            if (CurrentPlayIndex >= _playList.Count)
            {
                CurrentPlayIndex = -1;
                IsPlaying = false;
            }
#if DEBUG
        Debug.Log($"Playlist refreshed: {_playList.Count} files in total");
#endif
        }
    }
}