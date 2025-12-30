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
        public DancePlayerUIManager uiManager;
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
            if (_settingsHandler == null)
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

            _settingsHandler.data.isPlaying = true;

            float delay = Mathf.Clamp(_settingsHandler.data.animationStartDelay, 0f, 1f);
            _startAnimationCoroutine = StartCoroutine(StartDanceSequence(avatarHelper.CurrentAnimator, resourceManager.CurrentAnimationClip, delay));

            if (uiManager != null)
            {
                uiManager.UpdateDropdownValue();
            }
            DanceSettingsHandler.OnSettingChanged();
            return true;
        }

        // Starts dance sequence with audio warm-up for synchronization
        private IEnumerator StartDanceSequence(Animator animator, AnimationClip clip, float delay)
        {
            if (animator == null || clip == null || avatarHelper.CurrentAudioSource == null)
            {
                _startAnimationCoroutine = null;
                yield break;
            }

            // 1. Setup Animation immediately but pause it
            avatarHelper.SetupAnimation(clip);
            animator.speed = 0f;

            // 2. Warm up audio to ensure DSP buffer is ready
            AudioSource audio = avatarHelper.CurrentAudioSource;
            audio.volume = 0f; // Mute for warm-up
            audio.Play();

            float timeout = Time.time + 1f;
            // Wait for audio to actually start processing
            while (audio.time < 0.05f && Time.time < timeout)
            {
                yield return null;
            }

            // 3. Reset and prepare for actual playback
            audio.Pause();
            audio.time = 0f;
            avatarHelper.UpdateAudioVolume(); // Restore volume

            // 4. Handle user-defined delay
            if (delay > 0.0001f)
            {
                yield return new WaitForSeconds(delay);
            }

            // Check if we should still be playing
            if (!_settingsHandler.data.isPlaying)
            {
                _startAnimationCoroutine = null;
                yield break;
            }

            // 5. Start playback synchronized
            _settingsHandler.data.audioStartTime = Time.time;
            audio.Play();
            animator.speed = 1f;

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

            avatarHelper.CurrentAnimator.speed = 1f;
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