using System.Collections.Generic;
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

    public Mesh DummyBlendshapeMesh;
    public RuntimeAnimatorController DefaultAnimatorController { get; private set; }

    public RuntimeAnimatorController CustomDanceAvatarController;

    public AnimatorOverrideController CurrentOverrideController { get; set; }

    public SkinnedMeshRenderer TargetSMR {  get; private set; } = null;

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
        if (CurrentAnimator.GetComponent<DummyToUniversalSync>() != null)
            return;
        SkinnedMeshRenderer[] smrs = CurrentAvatar.GetComponentsInChildren<SkinnedMeshRenderer>();

        TargetSMR = null;
        foreach (var smr in smrs)
        {
            if (smr.sharedMesh == null || smr.sharedMesh.blendShapeCount == 0)
                continue;

            // 收集所有 blendshape 名称
            var blendShapeNames = new HashSet<string>();
            bool hasDummy = false;
            for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
            {
                string blendShapeName = smr.sharedMesh.GetBlendShapeName(i);
                blendShapeNames.Add(blendShapeName);
                if (blendShapeName.ToLower().Contains("dummy"))
                {
                    hasDummy = true;
                    break;
                }
            }

            if (hasDummy)
                continue;

            // 检查所有 mmd 形态键都存在
            bool allKeywordsPresent = _mmdBlendshapeKeywords.All(keyword =>
                blendShapeNames.Any(name => name.Contains(keyword))
            );

            if (allKeywordsPresent)
            {
                TargetSMR = smr;
                break;
            }
        }

        if (TargetSMR == null)
        {
            Debug.LogWarning($"No valid MMD SMR found in {CurrentAvatar.name}, attaching DummyMesh instead");

            // 先处理旧 Body 防止冲突
            Transform existingBody = CurrentAvatar.transform.Find(BODY_NAME);
            if (existingBody != null)
            {
                existingBody.name = $"{BODY_NAME}_Old_{Random.Range(0, 10000)}";
            }

            // 创建 Dummy Body
            GameObject dummyObj = new GameObject(BODY_NAME);
            dummyObj.transform.SetParent(CurrentAvatar.transform, false);

            var smr = dummyObj.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = DummyBlendshapeMesh;
            smr.updateWhenOffscreen = true;
            var dummySync = CurrentAvatar.AddComponent<DummyToUniversalSync>();
            dummySync.enabled = false;
            dummySync.dummySmr = smr;


        }
        else
        {
            Debug.Log($"Found valid MMD SMR: {TargetSMR.name} in {CurrentAvatar.name}");
            if (TargetSMR.transform.parent != CurrentAvatar.transform)
            {
                TargetSMR.transform.SetParent(CurrentAvatar.transform, false);
            }
            TargetSMR.gameObject.name = BODY_NAME;
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
        TargetSMR = null;
    }

    public bool IsAvatarAvailable()
    {
        return CurrentAvatar != null && CurrentAnimator != null;
    }


}