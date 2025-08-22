using System.Reflection;
using UnityEngine;

/// <summary>
/// 角色工具类：查找当前激活的角色，获取必要组件
/// </summary>
public class AvatarHelper : MonoBehaviour
{
    // 约定：所有角色都在名为"Model"的父对象下（游戏原有结构）
    private const string MODEL_PARENT_NAME = "Model";


    // 当前角色的核心组件（对外提供访问）
    public GameObject CurrentAvatar { get; private set; }
    public Animator CurrentAnimator { get; private set; }
    public AudioSource CurrentAudioSource { get; private set; }
    // 保存角色默认的AnimatorController（停止播放时恢复）
    public RuntimeAnimatorController DefaultAnimatorController { get; private set; }
    private PropertyInfo _animNormalizedTimeProp;
    void Update()
    {
        // 每帧检查角色是否变化（比如用户切换了角色）
        CheckAndUpdateCurrentAvatar();
    }
    private void Awake()
    {
        // 缓存反射属性，避免每帧获取
        _animNormalizedTimeProp = typeof(AnimatorStateInfo).GetProperty(
            "normalizedTime", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    }

    /// <summary>
    /// 检查并更新当前角色（核心：兼容多角色）
    /// </summary>
    private void CheckAndUpdateCurrentAvatar()
    {
        // 1. 先找Model父对象（游戏角色都在这个节点下）
        GameObject modelParent = GameObject.Find(MODEL_PARENT_NAME);
        if (modelParent == null)
        {
            Debug.LogWarning("Model parent object not found, please check the game scene structure");

            ClearCurrentAvatar();
            return;
        }

        // 2. 找Model下激活的角色（可能是VRMModel或CustomVRM(Clone)）
        GameObject newAvatar = null;


        // 如果没加标签，遍历Model下所有子对象，找有Animator的激活对象
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

        // 3. 如果角色变化了，更新组件引用
        if (newAvatar != CurrentAvatar)
        {
            UpdateAvatarComponents(newAvatar);
        }
    }

    /// <summary>
    /// 更新角色的Animator和AudioSource（确保音频挂在角色身上）
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
        // 清空旧引用
        ClearCurrentAvatar();

        if (newAvatar == null)
        {
            Debug.LogWarning("No active avatar found");
            return;
        }

        // 保存新角色引用
        CurrentAvatar = newAvatar;

        // 获取Animator（角色必须有，否则无法播放动画）
        CurrentAnimator = newAvatar.GetComponent<Animator>();
        if (CurrentAnimator == null)
        {
            Debug.LogError($"Avatar {newAvatar.name} does not have an Animator component, cannot play dance");
            CurrentAvatar = null;
            return;
        }

        GameObject soundFX = GameObject.Find("SoundFX");
        if (soundFX != null)
        {
            CurrentAudioSource = soundFX.GetComponentInChildren<AudioSource>();
            if (CurrentAudioSource == null)
            {
                GameObject danceAudioObj = new GameObject("DanceAudio");
                danceAudioObj.transform.parent = soundFX.transform;
                CurrentAudioSource = danceAudioObj.AddComponent<AudioSource>();
            }
        }

        // 保存角色默认的AnimatorController（停止播放时恢复）
        if (DefaultAnimatorController == null)
        {
            DefaultAnimatorController = CurrentAnimator.runtimeAnimatorController;
        }

        // 更新UI状态（后续由UIManager调用）
        Debug.Log($"Connected to avatar: {newAvatar.name}");
    }

    /// <summary>
    /// 清空当前角色引用（角色切换时调用）
    /// </summary>
    private void ClearCurrentAvatar()
    {
        if (CurrentAnimator != null && DefaultAnimatorController != null)
        {

            CurrentAnimator.runtimeAnimatorController = DefaultAnimatorController;
        }
        CurrentAvatar = null;
        CurrentAnimator = null;
        CurrentAudioSource = null;
    }

    /// <summary>
    /// 检查角色是否可用（对外提供判断）
    /// </summary>
    public bool IsAvatarAvailable()
    {
        return CurrentAvatar != null && CurrentAnimator != null && CurrentAudioSource != null;
    }
    void Start()
    {
        // 初始化时获取当前角色的默认AnimatorController
        CheckAndUpdateCurrentAvatar();
        if (CurrentAnimator != null)
        {
            DefaultAnimatorController = CurrentAnimator.runtimeAnimatorController;
        }
    }
    // 对外提供获取动画进度的方法
    public float GetAnimatorNormalizedTime()
    {
        if (CurrentAnimator == null || _animNormalizedTimeProp == null) return 0f;
        try
        {
            var stateInfo = CurrentAnimator.GetCurrentAnimatorStateInfo(0);
            return (float)_animNormalizedTimeProp.GetValue(stateInfo);
        }
        catch { return 0f; }
    }
}