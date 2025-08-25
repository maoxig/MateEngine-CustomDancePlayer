using System.Reflection;
using UnityEngine;

/// <summary>
/// Avatar utility class: finds the currently active avatar and retrieves necessary components
/// </summary>
public class DanceAvatarHelper : MonoBehaviour
{

    private const string MODEL_PARENT_NAME = "Model";



    public GameObject CurrentAvatar { get; private set; }
    public Animator CurrentAnimator { get; private set; }
    public AudioSource CurrentAudioSource { get; private set; }
    // Store the avatar's default AnimatorController (restore when playback stops)
    public RuntimeAnimatorController DefaultAnimatorController { get; private set; }

    public RuntimeAnimatorController CustomDanceAvatarController;


    void Update()
    {

        CheckAndUpdateCurrentAvatar();
    }

    /// <summary>
    /// Checks and updates the current avatar
    /// </summary>
    private void CheckAndUpdateCurrentAvatar()
    {

        GameObject modelParent = GameObject.Find(MODEL_PARENT_NAME);
        if (modelParent == null)
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
            foreach (Transform child in modelParent.transform)
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
            if (playerCore != null && playerCore.IsPlaying)
            {
                playerCore.StopPlay();
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
            Transform customDanceAudioTrans = soundFX.transform.Find("CustomDanceAudio");
            GameObject customDanceAudioObj;
            if (customDanceAudioTrans == null)
            {
                customDanceAudioObj = new GameObject("CustomDanceAudio");
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
        }

        if (CurrentAudioSource != null && CurrentAudioSource.gameObject.name == "DanceAudio")
        {
            Destroy(CurrentAudioSource.gameObject);
        }
        CurrentAvatar = null;
        CurrentAnimator = null;
        CurrentAudioSource = null;
    }

    /// <summary>

    /// </summary>
    public bool IsAvatarAvailable()
    {
        return CurrentAvatar != null && CurrentAnimator != null && CurrentAudioSource != null;
    }
    void Start()
    {
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
            Transform customDanceAudioTrans = soundFX.transform.Find("CustomDanceAudio");
            GameObject customDanceAudioObj;
            if (customDanceAudioTrans == null)
            {
                customDanceAudioObj = new GameObject("CustomDanceAudio");
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


}