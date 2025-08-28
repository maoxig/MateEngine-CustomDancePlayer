using System.Linq;
using UnityEngine;

public class DanceAvatarHelper : MonoBehaviour
{
    private const string MODEL_PARENT_NAME = "Model";
    private const string CUSTOM_DANCE_AUDIO_NAME = "CustomDanceAudio";
    private const string BODY_NAME = "Body";
    private GameObject _modelParent;

    public GameObject CurrentAvatar { get; private set; }
    public Animator CurrentAnimator { get; private set; }
    public AudioSource CurrentAudioSource { get; private set; }
    public RuntimeAnimatorController DefaultAnimatorController { get; private set; }

    public RuntimeAnimatorController CustomDanceAvatarController;

    public AnimatorOverrideController CurrentOverrideController { get; set; }

    // Japanese MMD blendshape keywords to identify the correct SMR
    private readonly string[] _mmdBlendshapeKeywords = {
        "まばたき", // Blink
        "あ",       // Mouth shape 'A'
        "い",       // Mouth shape 'I'
        "う",       // Mouth shape 'U'
        "え",       // Mouth shape 'E'
        "お"        // Mouth shape 'O'
    };

    void Start()
    {
        _modelParent = GameObject.Find(MODEL_PARENT_NAME);
        CheckAndUpdateCurrentAvatar();
        if (CurrentAnimator != null)
        {
            DefaultAnimatorController = CurrentAnimator.runtimeAnimatorController;
        }
        SetupMMDBlendshapeSMR();
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

    private void CheckAndUpdateCurrentAvatar()
    {
        if (_modelParent == null)
        {

            Debug.LogWarning("Model parent object not found, please check the game scene structure");

            ClearCurrentAvatar();
            return;
        }

        GameObject newAvatar = null;

        // Find the active avatar under the Model
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

    private void UpdateAvatarComponents(GameObject newAvatar)
    {
        if (CurrentAvatar != null && CurrentAvatar != newAvatar)
        {
            DancePlayerCore playerCore = Object.FindFirstObjectByType<DancePlayerCore>();
            if (playerCore != null)
            {
                playerCore.StopPlay();
                playerCore.ResetDanceEndFlag();
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

        CurrentAvatar = newAvatar;
        CurrentAnimator = newAvatar.GetComponent<Animator>();
        if (CurrentAnimator == null)
        {
#if UNITY_EDITOR
            Debug.LogError($"Avatar {newAvatar.name} does not have an Animator component, cannot play dance");
#endif
            CurrentAvatar = null;
            return;
        }

        // Handle SMR with MMD blendshapes
        SetupMMDBlendshapeSMR();

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

        DefaultAnimatorController = CurrentAnimator.runtimeAnimatorController;

#if UNITY_EDITOR
        Debug.Log($"Connected to avatar: {newAvatar.name}");
#endif
    }

    private void SetupMMDBlendshapeSMR()
    {
        if (CurrentAvatar == null)
            return;

        // Find all SkinnedMeshRenderers in the avatar
        SkinnedMeshRenderer[] smrs = CurrentAvatar.GetComponentsInChildren<SkinnedMeshRenderer>();
        SkinnedMeshRenderer targetSMR = null;

        // Search for SMR with MMD blendshapes
        foreach (var smr in smrs)
        {
            if (smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
            {
                for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                {
                    string blendShapeName = smr.sharedMesh.GetBlendShapeName(i);
                    if (_mmdBlendshapeKeywords.Any(keyword => blendShapeName.Contains(keyword)))
                    {
                        targetSMR = smr;
                        break;
                    }
                }
                if (targetSMR != null)
                    break;
            }
        }

        if (targetSMR == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"No SMR with MMD blendshapes found in avatar {CurrentAvatar.name}");
#endif
            return;
        }

        // Check for existing "Body" GameObject
        Transform existingBody = CurrentAvatar.transform.Find(BODY_NAME);
        if (existingBody != null)
        {
            // If the existing Body doesn't contain the target SMR, rename it
            if (existingBody.GetComponent<SkinnedMeshRenderer>() != targetSMR)
            {
                existingBody.name = $"{BODY_NAME}_Old_{Random.Range(0, 10000)}";
#if UNITY_EDITOR
                Debug.Log($"Renamed existing Body to {existingBody.name} as it didn't contain the target SMR");
#endif
            }
        }
        if (targetSMR != null)
        {

            if (targetSMR.transform.parent != CurrentAvatar.transform)
            {
                targetSMR.transform.SetParent(CurrentAvatar.transform, false);
            }

            targetSMR.gameObject.name = BODY_NAME;
#if UNITY_EDITOR
            Debug.Log($"Moved SMR {targetSMR.name} to root and renamed to Body under {CurrentAvatar.name}");
#endif
        }

    }

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

    public bool IsAvatarAvailable()
    {
        return CurrentAvatar != null && CurrentAnimator != null;
    }
}