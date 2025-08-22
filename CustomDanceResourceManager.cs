using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 资源管理器：加载/卸载舞蹈资源（.unity3d、动画控制器、音频）
/// </summary>
public class DanceResourceManager : MonoBehaviour
{
    // 舞蹈资源文件夹（StreamingAssets下的子文件夹）
    private const string DANCE_FOLDER_NAME = "CustomDances";
    // 当前加载的AssetBundle（必须记录，否则无法卸载）
    private AssetBundle _currentAssetBundle;
    // 当前加载的资源（动画控制器+音频）
    public RuntimeAnimatorController CurrentAnimatorCtrl { get; private set; }
    public AudioClip CurrentAudioClip { get; private set; }
    // 所有可用的舞蹈文件列表（播放列表来源）
    public List<string> DanceFileList { get; private set; } = new List<string>();

    // 引用角色工具类（获取角色的AudioSource）
    public AvatarHelper avatarHelper;

    void Start()
    {
        // 初始化：加载舞蹈文件列表
        RefreshDanceFileList();
    }

    /// <summary>
    /// 刷新舞蹈文件列表（从CustomDances文件夹读取）
    /// </summary>
    public void RefreshDanceFileList()
    {
        DanceFileList.Clear();
        string danceFolderPath = GetDanceFolderPath();

        // 检查文件夹是否存在
        if (!Directory.Exists(danceFolderPath))
        {
            Directory.CreateDirectory(danceFolderPath);
            Debug.Log($"创建舞蹈文件夹：{danceFolderPath}");
            return;
        }

        // 读取所有.unity3d文件
        string[] unity3dFiles = Directory.GetFiles(danceFolderPath, "*.unity3d");
        foreach (string filePath in unity3dFiles)
        {
            DanceFileList.Add(Path.GetFileName(filePath));
        }

        Debug.Log($"刷新舞蹈列表：共{DanceFileList.Count}个文件");
    }

    /// <summary>
    /// 加载指定的舞蹈文件（核心：先卸载旧资源，再加载新资源）
    /// </summary>
    /// <param name="fileName">.unity3d文件名（如“舞蹈1.unity3d”）</param>
    /// <returns>加载成功返回true</returns>
    public bool LoadDanceResource(string fileName)
    {
        // 1. 前置检查：角色是否可用、文件名是否有效
        if (!avatarHelper.IsAvatarAvailable())
        {
            Debug.LogError("角色不可用，无法加载资源");
            return false;
        }
        if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".unity3d"))
        {
            Debug.LogError("无效的文件名：" + fileName);
            return false;
        }

        // 2. 卸载旧资源（避免内存泄漏）
        UnloadCurrentResource();

        // 3. 拼接文件路径
        string fullPath = Path.Combine(GetDanceFolderPath(), fileName);
        if (!File.Exists(fullPath))
        {
            Debug.LogError("文件不存在：" + fullPath);
            return false;
        }

        // 4. 加载AssetBundle
        _currentAssetBundle = AssetBundle.LoadFromFile(fullPath);
        if (_currentAssetBundle == null)
        {
            Debug.LogError("加载.unity3d失败（可能是文件损坏或版本不兼容）：" + fullPath);
            return false;
        }

        // 5. 提取资源（核心：按“文件名=资源名”约定加载）
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        bool loadAnimatorSuccess = LoadAnimatorController(baseName);
        bool loadAudioSuccess = LoadAudioClip(baseName);

        if (!loadAnimatorSuccess)
        {
            UnloadCurrentResource();
            return false;
        }

        if (CurrentAudioClip != null)
        {
            Debug.Log($"加载成功：{fileName}（动画+音频）");
        }
        else
        {
            Debug.LogWarning($"加载成功：{fileName}（仅动画，未找到音频）");
        }
        return true;
    }

    /// <summary>
    /// 加载动画控制器（修复：明确指定资源类型）
    /// </summary>
    private bool LoadAnimatorController(string baseName)
    {
        string ctrlPath = $"{baseName}.controller";
        CurrentAnimatorCtrl = _currentAssetBundle.LoadAsset<RuntimeAnimatorController>(ctrlPath);
        if (CurrentAnimatorCtrl == null)
        {
            Debug.LogError($"未找到动画控制器：{ctrlPath}（检查资源名是否与文件名一致）");
            return false;
        }
        return true;
    }

    /// <summary>
    /// 加载音频（修复：挂在角色身上，支持多格式）
    /// </summary>
    private bool LoadAudioClip(string baseName)
    {
        string[] audioExts = { ".wav", ".mp3", ".ogg" };
        AudioSource avatarAudioSource = avatarHelper.CurrentAudioSource;

        foreach (string ext in audioExts)
        {
            string audioPath = $"{baseName}{ext}";
            CurrentAudioClip = _currentAssetBundle.LoadAsset<AudioClip>(audioPath);
            if (CurrentAudioClip != null)
            {
                avatarAudioSource.clip = CurrentAudioClip;
                avatarAudioSource.loop = false;
                return true; // 音频加载成功
            }
        }

        // 音频加载失败，返回false但不阻断
        CurrentAudioClip = null;
        avatarAudioSource.clip = null;
        return false;
    }

    /// <summary>
    /// 卸载当前资源（核心：避免内存泄漏）
    /// </summary>
    public void UnloadCurrentResource()
    {
        // 1. 停止音频播放
        if (avatarHelper.IsAvatarAvailable())
        {
            avatarHelper.CurrentAudioSource.Stop();
            avatarHelper.CurrentAudioSource.clip = null;
        }

        // 2. 卸载AssetBundle（必须调用，否则资源占内存）
        if (_currentAssetBundle != null)
        {
            _currentAssetBundle.Unload(true); // true：卸载所有从该包加载的资源
            _currentAssetBundle = null;
            Debug.Log("已卸载旧资源");
        }

        // 3. 清空资源引用
        CurrentAnimatorCtrl = null;
        CurrentAudioClip = null;
    }

    /// <summary>
    /// 获取舞蹈文件夹路径（封装，避免重复代码）
    /// </summary>
    private string GetDanceFolderPath()
    {
        return Path.Combine(Application.streamingAssetsPath, DANCE_FOLDER_NAME);
    }

    /// <summary>
    /// 检查资源是否加载完成（对外提供判断）
    /// </summary>
    public bool IsResourceLoaded()
    {
        return CurrentAnimatorCtrl != null && CurrentAudioClip != null;
    }
}