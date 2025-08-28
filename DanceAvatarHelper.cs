using System.Reflection;
using UnityEngine;

/// <summary>
/// Avatar utility class: finds the currently active avatar and retrieves necessary components
/// </summary>
public class DanceAvatarHelper : MonoBehaviour
{

    private const string MODEL_PARENT_NAME = "Model";
    private const string CUSTOM_DANCE_AUDIO_NAME = "CustomDanceAudio";
    private GameObject _modelParent;

    public GameObject CurrentAvatar { get; private set; }
    public Animator CurrentAnimator { get; private set; }
    public AudioSource CurrentAudioSource { get; private set; }
    // Store the avatar's default AnimatorController (restore when playback stops)
    public RuntimeAnimatorController DefaultAnimatorController { get; private set; }

    public RuntimeAnimatorController CustomDanceAvatarController;

    public AnimatorOverrideController CurrentOverrideController { get; set; }

    void Start()
    {
        _modelParent = GameObject.Find(MODEL_PARENT_NAME);
        // Initialize and get the current avatar's default AnimatorController
        CheckAndUpdateCurrentAvatar();
        if (CurrentAnimator != null)
        {
            DefaultAnimatorController = CurrentAnimator.runtimeAnimatorController;
        }

        // Initialize AudioSource
        GameObject soundFX = GameObject.Find("SoundFX");
        if (soundFX != null)
        {
            Transform customDanceAudioTrans = soundFX.transform.Find(CUSTOM_DANCE_AUDIO_NAME);
            GameObject customDanceAudioObj;
            if (customDanceAudioTrans == null)
            {
                customDanceAudioObj = new GameObject(CUSTOM_DANCE_AUDIO_NAME);
                customDanceAudioObj.transform.SetParent(soundFX.transform, false);
            }
            else
            {
                customDanceAudioObj = customDanceAudioTrans.gameObject;
            }
            CurrentAudioSource = customDanceAudioObj.GetComponent<AudioSource>();
            if (CurrentAudioSource == null)
            {
                CurrentAudioSource = customDanceAudioObj.AddComponent<AudioSource>();
            }
        }
    }
    void Update()
    {

        CheckAndUpdateCurrentAvatar();
    }
    private void OnDestroy()
    {
        ClearCurrentAvatar(); 
        CurrentAvatar = null;
        CurrentAnimator = null;
        CurrentAudioSource = null;
    }
    /// <summary>
    /// Checks and updates the current avatar
    /// </summary>
    private void CheckAndUpdateCurrentAvatar()
    {

  
        if (_modelParent == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning("Model parent object not found, please check the game scene structure");
#endif
            ClearCurrentAvatar();
            return;
        }

        // 2. Find the active avatar under the Model (could be VRMModel or CustomVRM(Clone))
        GameObject newAvatar = null;


        // If no tag is added, traverse all child objects under Model to find the active object with Animator
        if (newAvatar == null)
        {
            foreach (Transform child in _modelParent.transform)
            {
                if (child.gameObject.activeSelf && child.GetComponent<Animator>() != null)
                {
                    newAvatar = child.gameObject;
                    break;
                }
            }
        }

        // 3. If the avatar has changed, update component references
        if (newAvatar != CurrentAvatar)
        {
            UpdateAvatarComponents(newAvatar);
        }
    }

    /// <summary>
    /// Updates the avatar's Animator
    /// </summary>
    private void UpdateAvatarComponents(GameObject newAvatar)
    {

        if (CurrentAvatar != null && CurrentAvatar != newAvatar)
        {
            DancePlayerCore playerCore = Object.FindFirstObjectByType<DancePlayerCore>();
            if (playerCore != null)
            {
                playerCore.StopPlay();
                playerCore.ResetDanceEndFlag(); // 重置结束标志，避免误触发PlayNext
            }
        }

        ClearCurrentAvatar();

        if (newAvatar == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning("No active avatar found");
#endif
            return;
        }

        // Store the new avatar reference
        CurrentAvatar = newAvatar;

        // Get the Animator (the avatar must have one, otherwise animation cannot be played)
        CurrentAnimator = newAvatar.GetComponent<Animator>();
        if (CurrentAnimator == null)
        {
#if UNITY_EDITOR
            Debug.LogError($"Avatar {newAvatar.name} does not have an Animator component, cannot play dance");
#endif
            CurrentAvatar = null;
            return;
        }

        GameObject soundFX = GameObject.Find("SoundFX");
        if (soundFX != null)
        {
            Transform customDanceAudioTrans = soundFX.transform.Find(CUSTOM_DANCE_AUDIO_NAME);
            GameObject customDanceAudioObj;
            if (customDanceAudioTrans == null)
            {
                customDanceAudioObj = new GameObject(CUSTOM_DANCE_AUDIO_NAME);
                customDanceAudioObj.transform.SetParent(soundFX.transform, false);
            }
            else
            {
                customDanceAudioObj = customDanceAudioTrans.gameObject;
            }
            CurrentAudioSource = customDanceAudioObj.GetComponent<AudioSource>();
            if (CurrentAudioSource == null)
            {
                CurrentAudioSource = customDanceAudioObj.AddComponent<AudioSource>();
            }
        }

        DefaultAnimatorController = CurrentAnimator.runtimeAnimatorController;

#if UNITY_EDITOR
        Debug.Log($"Connected to avatar: {newAvatar.name}");
#endif
    }

    /// <summary>
    /// Clears the current avatar reference (called when switching avatars)
    /// </summary>
    private void ClearCurrentAvatar()
    {
        if (CurrentAnimator != null && DefaultAnimatorController != null)
        {

            CurrentAnimator.runtimeAnimatorController = DefaultAnimatorController;
            CurrentAnimator.SetBool("isDancing", false);
        }

        CurrentAvatar = null;
        CurrentAnimator = null;
        CurrentAudioSource = null;
      
    }

    /// <summary>

    /// </summary>
    public bool IsAvatarAvailable()
    {
        return CurrentAvatar != null && CurrentAnimator != null;
    }
    


}