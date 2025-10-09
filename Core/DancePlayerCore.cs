using System;
using System.Collections;
using UnityEngine;

namespace CustomDancePlayer
{ // Manages dance playback logic and state updates

    public class DancePlayerCore : MonoBehaviour
    {

        [Header("Dependency References")]
        public DanceAvatarHelper avatarHelper;
        public DanceResourceManager resourceManager;

        public enum PlayMode { Sequence, Loop, Random }


        private DanceSettingsHandler _settingsHandler;
        private Coroutine _startAnimationCoroutine;

        public bool IsPlaying
        {
            get => _settingsHandler.data.isPlaying;
            set
            {
                _settingsHandler.data.isPlaying = value;
                DanceSettingsHandler.OnSettingChanged();
            }
        }

        void Start()
        {

            _settingsHandler = DanceSettingsHandler.Instance;
        }

        // Initializes player with playlist
        public void InitPlayer()
        {
            if (_settingsHandler  == null)
            {
                _settingsHandler = DanceSettingsHandler.Instance;
            }
            if (_settingsHandler.data.autoPlayOnStart && resourceManager.DanceFileList.Count > 0 && _settingsHandler.data.currentPlayIndex >= 0)
            {
                PlayDanceByIndex(_settingsHandler.data.currentPlayIndex);
            }
        }

        // Plays dance at specified index
        public bool PlayDanceByIndex(int index)
        {
            if (resourceManager.DanceFileList.Count == 0 || index < 0 || index >= resourceManager.DanceFileList.Count || !avatarHelper.IsAvatarAvailable())
            {
                return false;
            }

            _settingsHandler.data.currentPlayIndex = index;
            string targetFileName = resourceManager.DanceFileList[index];

            if (!resourceManager.LoadDanceResource(targetFileName))
            {
                _settingsHandler.data.isPlaying = false;
                DanceSettingsHandler.OnSettingChanged();
                return false;
            }

            if (_startAnimationCoroutine != null)
            {
                StopCoroutine(_startAnimationCoroutine);
                _startAnimationCoroutine = null;
            }

            if (avatarHelper.CurrentOverrideController != null)
            {
                Destroy(avatarHelper.CurrentOverrideController);

            }

            if (avatarHelper.TargetSMR != null)
            {
                var proxy = avatarHelper.CurrentAnimator.GetComponent<UniversalBlendshapes>();
                if (proxy != null) proxy.enabled = false;
            }
            else
            {
                avatarHelper.SetupDummyForDance();
            }

            _settingsHandler.data.audioStartTime = Time.time;
            avatarHelper.CurrentAudioSource.Play();
            _settingsHandler.data.isPlaying = true;

            float delay = Mathf.Clamp(_settingsHandler.data.animationStartDelay, 0f, 1f);
            if (delay <= 0.0001f)
            {
                ApplyAnimationImmediately(avatarHelper.CurrentAnimator, resourceManager.CurrentAnimationClip);
            }
            else
            {
                _startAnimationCoroutine = StartCoroutine(StartAnimationAfterDelay(avatarHelper.CurrentAnimator, resourceManager.CurrentAnimationClip, delay));
            }

            DanceSettingsHandler.OnSettingChanged();
            return true;
        }

        // Applies animation immediately
        private void ApplyAnimationImmediately(Animator animator, AnimationClip clip)
        {
            if (animator == null || clip == null) return;

            avatarHelper.SetupAnimation(clip);
        }

        // Starts animation after delay
        private IEnumerator StartAnimationAfterDelay(Animator animator, AnimationClip clip, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (!_settingsHandler.data.isPlaying || animator == null || clip == null)
            {
                _startAnimationCoroutine = null;
                yield break;
            }

            ApplyAnimationImmediately(animator, clip);
            _startAnimationCoroutine = null;
        }

        // Plays next track based on play mode
        public void PlayNext()
        {
            if (resourceManager.DanceFileList.Count == 0) return;

            int nextIndex = _settingsHandler.data.currentPlayIndex;

            switch (_settingsHandler.data.currentPlayMode)
            {
                case PlayMode.Sequence:
                    nextIndex = _settingsHandler.data.currentPlayIndex + 1;
                    if (nextIndex >= resourceManager.DanceFileList.Count)
                    {
                        StopPlay();
                        return;
                    }
                    break;
                case PlayMode.Loop:
                    nextIndex = _settingsHandler.data.currentPlayIndex;
                    break;
                case PlayMode.Random:
                    System.Random random = new System.Random();
                    do
                    {
                        nextIndex = random.Next(0, resourceManager.DanceFileList.Count);
                    } while (resourceManager.DanceFileList.Count > 1 && nextIndex == _settingsHandler.data.currentPlayIndex);
                    break;
            }

            PlayDanceByIndex(nextIndex);
        }

        // Plays previous track
        public void PlayPrev()
        {
            if (resourceManager.DanceFileList.Count == 0) return;
            PlayDanceByIndex(Mathf.Max(0, _settingsHandler.data.currentPlayIndex - 1));
        }

        // Stops playback and resets state
        public void StopPlay()
        {
            if (!avatarHelper.IsAvatarAvailable()) return;

            if (_startAnimationCoroutine != null)
            {
                StopCoroutine(_startAnimationCoroutine);
                _startAnimationCoroutine = null;
            }

            avatarHelper.CurrentAudioSource.Stop();
            avatarHelper.CurrentAnimator.SetBool("isDancing", false);

            if (avatarHelper.DefaultAnimatorController != null)
            {
                avatarHelper.CurrentAnimator.runtimeAnimatorController = avatarHelper.DefaultAnimatorController;
            }

            if (avatarHelper.TargetSMR != null)
            {
                var proxy = avatarHelper.CurrentAnimator.GetComponent<UniversalBlendshapes>();
                if (proxy != null) proxy.enabled = true;
            }
            else
            {
                avatarHelper.RestoreOriginalBody();
            }

            resourceManager.UnloadCurrentResource();
            _settingsHandler.data.isPlaying = false;
            DanceSettingsHandler.OnSettingChanged();
        }



        // Gets current playing file name
        public string GetCurrentPlayFileName()
        {
            if (resourceManager.DanceFileList.Count == 0 || _settingsHandler.data.currentPlayIndex < 0 || _settingsHandler.data.currentPlayIndex >= resourceManager.DanceFileList.Count)
            {
                return "Not Playing";
            }
            string fileName = resourceManager.DanceFileList[_settingsHandler.data.currentPlayIndex];
            return fileName.EndsWith(".unity3d", StringComparison.OrdinalIgnoreCase) ? fileName.Substring(0, fileName.Length - ".unity3d".Length) : fileName;
        }
    }
}