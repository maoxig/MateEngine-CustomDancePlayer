using System;
using System.Linq;
using UnityEngine;

namespace CustomDancePlayer
{
    // Manages avatar-related components and audio setup
    public class DanceAvatarHelper : MonoBehaviour
    {
        private const string MODEL_PARENT_NAME = "Model"; private const string CUSTOM_DANCE_AUDIO_NAME = "CustomDanceAudio"; private const string BODY_NAME = "Body";
        private static readonly string[] MMDBlendshapeKeywords = { "まばたき", "あ", "い", "う", "え", "お" };

        public Mesh DummyBlendshapeMesh;
        public RuntimeAnimatorController CustomDanceAvatarController;
        public DancePlayerCore playerCore;



        public GameObject CurrentAvatar { get; private set; }
        public Animator CurrentAnimator { get; private set; }

        public Transform CurrentAvatarHips { get; private set; }
        public AudioSource CurrentAudioSource { get; private set; }
        public SkinnedMeshRenderer TargetSMR { get; private set; }
        public AnimatorOverrideController CurrentOverrideController { get; private set; }

        public RuntimeAnimatorController DefaultAnimatorController { get; private set; }

        private GameObject _modelParent;
        private DanceSettingsHandler _settingsHandler;
        private Transform _originalBodyTransform;
        private Transform _dummyBodyTransform;
        private string _oldBodyName;


        void Start()
        {

            _modelParent = GameObject.Find(MODEL_PARENT_NAME);
            SetupAudioSource();

            _settingsHandler = DanceSettingsHandler.Instance;
            CheckAndUpdateCurrentAvatar();
            if (CurrentAnimator != null)
            {
                DefaultAnimatorController = CurrentAnimator.runtimeAnimatorController;
            }
            SetupMMDBlendshapeSMR();
            UpdateAudioVolume();
        }

        void Update()
        {
            CheckAndUpdateCurrentAvatar();
        }

        void OnDestroy()
        {
            ClearCurrentAvatar();
            CurrentAvatar = null;
            CurrentAvatarHips = null;
            CurrentAnimator = null;
            CurrentAudioSource = null;
        }


        private void SetupAudioSource()
        {
            GameObject soundFX = GameObject.Find("SoundFX");
            if (soundFX == null) return;

            Transform audioTrans = soundFX.transform.Find(CUSTOM_DANCE_AUDIO_NAME);
            GameObject audioObj;
            if (audioTrans != null)
            {
                audioObj = audioTrans.gameObject;
            }
            else
            {
                audioObj = new GameObject(CUSTOM_DANCE_AUDIO_NAME);
                audioObj.transform.SetParent(soundFX.transform, false);
            }

            CurrentAudioSource = audioObj.GetComponent<AudioSource>();
            if (CurrentAudioSource == null)
            {
                CurrentAudioSource = audioObj.AddComponent<AudioSource>();
            }
        }

        // Updates audio volume from settings
        public void UpdateAudioVolume()
        {
            if (CurrentAudioSource != null)
            {
                CurrentAudioSource.volume = _settingsHandler.data.danceVolume;
            }
        }

        // Checks and updates the active avatar
        private void CheckAndUpdateCurrentAvatar()
        {
            if (_modelParent == null)
            {
                ClearCurrentAvatar();
                return;
            }
            GameObject newAvatar = null;

            foreach (Transform child in _modelParent.transform)
            {
                if (child.gameObject.activeSelf && child.GetComponent<Animator>() != null)
                {
                    newAvatar = child.gameObject;
                    break;
                }
            }

            if (newAvatar != CurrentAvatar)
            {
                UpdateAvatarComponents(newAvatar);
            }
        }

        // Updates avatar components and notifies core
        private void UpdateAvatarComponents(GameObject newAvatar)
        {
            ClearCurrentAvatar();

            if (newAvatar == null) return;

            CurrentAvatar = newAvatar;
            CurrentAnimator = newAvatar.GetComponentInChildren<Animator>();
            if (CurrentAnimator == null)
            {
                CurrentAvatar = null;
                CurrentAvatarHips = null;
                return;
            }
            CurrentAvatarHips = CurrentAnimator.GetBoneTransform(HumanBodyBones.Hips);
            SetupMMDBlendshapeSMR();

            DefaultAnimatorController = CurrentAnimator.runtimeAnimatorController;
            if (playerCore != null)
            {
                playerCore.StopPlay();
            }
            SMRHandler.SetUpdateWhenOffscreen(CurrentAvatar, true);

            var proxy = CurrentAvatar.AddComponent<DancePlayerAvatarProxy>();
            proxy.playerCore = playerCore;

        }

        // Sets up SkinnedMeshRenderer for MMD blendshapes
        private void SetupMMDBlendshapeSMR()
        {
            if (CurrentAvatar == null) return;

            SkinnedMeshRenderer[] smrs = CurrentAvatar.GetComponentsInChildren<SkinnedMeshRenderer>();
            TargetSMR = smrs.FirstOrDefault(smr => smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0 &&
                !smr.sharedMesh.GetBlendShapeName(0).ToLower().Contains("dummy") &&
                MMDBlendshapeKeywords.All(keyword => Enumerable.Range(0, smr.sharedMesh.blendShapeCount)
                    .Any(i => smr.sharedMesh.GetBlendShapeName(i) == keyword)));

            if (TargetSMR != null && TargetSMR.transform.parent != CurrentAvatar.transform)
            {
                TargetSMR.transform.SetParent(CurrentAvatar.transform, false);
                TargetSMR.gameObject.name = BODY_NAME;
            }
        }

        // Clears current avatar state
        private void ClearCurrentAvatar()
        {
            if (CurrentAnimator != null && DefaultAnimatorController != null)
            {
                CurrentAnimator.runtimeAnimatorController = DefaultAnimatorController;
                CurrentAnimator.SetBool("isDancing", false);
            }
        }

        // Checks if avatar is available
        public bool IsAvatarAvailable()
        {
            return CurrentAvatar != null && CurrentAnimator != null;
        }

        // Sets up dummy mesh for dance if needed
        public void SetupDummyForDance()
        {
            if (TargetSMR != null) return;

            Transform existingBody = CurrentAvatar.transform.Find(BODY_NAME);
            if (existingBody != null)
            {
                SkinnedMeshRenderer smr = existingBody.GetComponent<SkinnedMeshRenderer>();
                if (smr != null && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0 &&
                    Enumerable.Range(0, smr.sharedMesh.blendShapeCount).Any(i => smr.sharedMesh.GetBlendShapeName(i).ToLower().Contains("dummy")))
                {
                    return;
                }
                _originalBodyTransform = existingBody;
                _oldBodyName = BODY_NAME + $"_Old_{UnityEngine.Random.Range(0, 10000)}";
                existingBody.name = _oldBodyName;
            }

            GameObject dummyObj = new GameObject(BODY_NAME);
            dummyObj.transform.SetParent(CurrentAvatar.transform, false);
            var dummySmr = dummyObj.AddComponent<SkinnedMeshRenderer>();
            dummySmr.sharedMesh = DummyBlendshapeMesh;
            dummySmr.updateWhenOffscreen = true;
            _dummyBodyTransform = dummyObj.transform;

            if (!CurrentAvatar.TryGetComponent<DummyToUniversalSync>(out var dummySync))
            {
                dummySync = CurrentAvatar.AddComponent<DummyToUniversalSync>();
            }
            dummySync.dummySmr = dummySmr;
            dummySync.enabled = true;
        }

        // Restores original body if dummy was used
        public void RestoreOriginalBody()
        {
            if (TargetSMR != null) return;

            if (_dummyBodyTransform != null)
            {
                Destroy(_dummyBodyTransform.gameObject);
                _dummyBodyTransform = null;
            }

            if (_originalBodyTransform != null)
            {
                _originalBodyTransform.name = BODY_NAME;
                _originalBodyTransform = null;
                _oldBodyName = null;
            }

            if (CurrentAvatar.TryGetComponent<DummyToUniversalSync>(out var sync))
            {
                sync.enabled = false;
            }
        }

        // Sets up animation override controller
        public void SetupAnimation(AnimationClip clip)
        {
            if (CurrentAnimator == null || clip == null) return;

            CurrentOverrideController = new AnimatorOverrideController(CustomDanceAvatarController);
            CurrentOverrideController["CUSTOM_DANCE"] = clip;
            CurrentAnimator.runtimeAnimatorController = CurrentOverrideController;
            CurrentAnimator.SetBool("isDancing", true);
        }
    }
}