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

    private Transform originalBodyTransform = null;
    private Transform dummyBodyTransform = null;
    private string oldBodyName = null;

    private int lastLoadedInstanceID = 0;

    public float danceVolume = 0.5f;

    void Awake()
    {
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
        CurrentAudioSource.volume = danceVolume;
    }

    void Update()
    {
        CheckAndUpdateCurrentAvatar();
    }

    private void OnDestroy()
    {
        //RestoreOriginalBody();
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

        bool hasChanged = (newAvatar != CurrentAvatar);
        if (!hasChanged && newAvatar != null)
        {


            int currentID = newAvatar.GetInstanceID();
#if UNITY_EDITOR
             Debug.Log($"Current avatar instance ID: {currentID}, Last loaded ID: {lastLoadedInstanceID}");
#endif
            if (currentID != lastLoadedInstanceID)
            {
                hasChanged = true;
            }
        }

        if (hasChanged)
        {
            UpdateAvatarComponents(newAvatar);
        }
    }

    private void UpdateAvatarComponents(GameObject newAvatar)
    {


        ClearCurrentAvatar();

        if (newAvatar == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning("No active avatar found");
#endif
            return;
        }

        CurrentAvatar = newAvatar;
        CurrentAnimator = newAvatar.GetComponentInChildren<Animator>();
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


        lastLoadedInstanceID = CurrentAvatar.GetInstanceID();
        if (CurrentAvatar != null)
        {
            DancePlayerCore playerCore = Object.FindFirstObjectByType<DancePlayerCore>();
            if (playerCore != null)
            {
                playerCore.StopPlay();
                playerCore.ResetDanceEndFlag();
            }
        }

#if UNITY_EDITOR
        Debug.Log($"Avatar updated: {newAvatar.name}, InstanceID: {lastLoadedInstanceID}");
        Debug.Log($"Connected to avatar: {newAvatar.name}");
#endif
    }

    private void SetupMMDBlendshapeSMR()
    {
        if (CurrentAvatar == null)
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

        }
        else
        {
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
        //RestoreOriginalBody();

        CurrentAvatar = null;
        CurrentAnimator = null;
        CurrentAudioSource = null;
        lastLoadedInstanceID = 0;

    }

    public bool IsAvatarAvailable()
    {
        return CurrentAvatar != null && CurrentAnimator != null;
    }

    public void SetupDummyForDance()
    {
        if (TargetSMR != null) return;  // 有原MMD SMR，无需dummy

        // 查找原始Body
        Transform existingBody = CurrentAvatar.transform.Find(BODY_NAME);
        if (existingBody == null)
        {
            return;
        }

        // 检查Body下的SkinnedMeshRenderer是否已包含dummy形态键
        var smr = existingBody.GetComponent<SkinnedMeshRenderer>();
        if (smr != null && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
        {
            for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
            {
                string blendShapeName = smr.sharedMesh.GetBlendShapeName(i);
                if (blendShapeName.ToLower().Contains("dummy"))
                {
                    return;
                }
            }
        }

        // 存储原始状态
        originalBodyTransform = existingBody;

        // 重命名原始Body
        oldBodyName = BODY_NAME + $"_Old_{Random.Range(0, 10000)}";
        existingBody.name = oldBodyName;

        // 创建Dummy Body
        GameObject dummyObj = new GameObject(BODY_NAME);
        dummyObj.transform.SetParent(CurrentAvatar.transform, false);

        var dummySmr = dummyObj.AddComponent<SkinnedMeshRenderer>();
        dummySmr.sharedMesh = DummyBlendshapeMesh;
        dummySmr.updateWhenOffscreen = true;

        dummyBodyTransform = dummyObj.transform;

        // 添加/启用sync
        var dummySync = CurrentAvatar.GetComponent<DummyToUniversalSync>();
        if (dummySync == null)
        {
            dummySync = CurrentAvatar.AddComponent<DummyToUniversalSync>();
        }
        dummySync.dummySmr = dummySmr;
        dummySync.enabled = true;
#if UNITY_EDITOR
        Debug.Log($"Setup dummy: Renamed original to {existingBody.name}, created new Body.");
#endif
    }

    public void RestoreOriginalBody()
    {
        if (TargetSMR != null) return;  // 无需恢复

        // 销毁dummy
        if (dummyBodyTransform != null)
        {
            Destroy(dummyBodyTransform.gameObject);
            dummyBodyTransform = null;
        }

        // 恢复原始Body
        if (originalBodyTransform != null)
        {
            originalBodyTransform.name = BODY_NAME;
            originalBodyTransform = null;
            oldBodyName = null;
        }

        // 禁用/移除sync
        var sync = CurrentAvatar?.GetComponent<DummyToUniversalSync>();
        if (sync != null)
        {
            sync.enabled = false;
        }
    }

}