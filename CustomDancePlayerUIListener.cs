using UnityEngine;

/// <summary>
/// 独立的K键监听组件（挂在Canvas父对象上，不受Canvas激活状态影响）
/// </summary>
public class DanceUIKeyListener : MonoBehaviour
{
    [Header("配置")]
    
    public GameObject targetCanvasObject; // 目标Canvas所在的GameObject
    public DancePlayerUIManager uiManager;// 原UI管理器

    private MenuActions _gameMenuActions; // 游戏MenuActions实例
    private KeyCode toggleKey;

    void Awake()
    {
        // 1. 获取游戏MenuActions（确保联动逻辑正常）
        _gameMenuActions = Object.FindFirstObjectByType<MenuActions>();
        if (_gameMenuActions == null)
        {
            Debug.LogWarning("未找到MenuActions，UI联动功能可能失效！");
        }

        // 2. 确保父对象始终激活
        gameObject.SetActive(true);
        if (uiManager != null)
        {
            uiManager.AddMyUIToGameMenuList();
            toggleKey = uiManager.toggleKey; // 从UI管理器获取切换按键
        }
    }

    void Update()
    {
        // 仅在引用齐全时处理K键（避免空引用报错）
        if (targetCanvasObject == null || uiManager == null) return;

        // 监听K键切换
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleUICanvas();
        }
    }

    /// <summary>
    /// 切换Canvas显隐，并同步更新MenuEntries
    /// </summary>
    private void ToggleUICanvas()
    {
        // 1. 切换Canvas状态（直接操作Canvas.enabled，不依赖缓存变量）
        bool newVisibleState = !targetCanvasObject.activeSelf;
        targetCanvasObject.SetActive(newVisibleState);

        if (newVisibleState)
        {
            uiManager.AddMyUIToGameMenuList(); // 显示时加入菜单列表
        }
        else
        {
            uiManager.RemoveMyUIFromGameMenuList(); // 隐藏时移除菜单列表
        }

        // 3. 更新按键提示文本（通知UI管理器同步显示）
        uiManager.UpdateToggleKeyText(newVisibleState);

    }

}