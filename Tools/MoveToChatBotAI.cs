using UnityEngine;
using System.Linq;

namespace CustomDancePlayer
{
    /// <summary>
    /// 直接查找所有名称为"ChatBot AI"的物体，将当前GameObject移动到其下（保持世界变换）
    /// </summary>
    public class MoveToChatBotAI : MonoBehaviour
    {
        [Tooltip("当找到多个ChatBot AI时，选择第几个作为父级（从0开始）")]
        public int targetIndex = 0;

        /// <summary>
        /// 执行移动逻辑
        /// </summary>
        /// <returns>是否移动成功</returns>
        public bool ExecuteMove()
        {
            // 记录原始世界变换（用于验证）
            Vector3 originalPos = transform.position;
            Quaternion originalRot = transform.rotation;
            Vector3 originalScale = transform.lossyScale;

            // 查找所有名称为"ChatBot AI"的物体（包括未激活的）
            GameObject[] chatBotAIs = FindAllChatBotAIGameObjects();
            if (chatBotAIs.Length == 0)
            {
                //Debug.LogError($"[{gameObject.name}] 未找到任何名称为'ChatBot AI'的物体", this);
                return false;
            }

            // 显示找到的所有结果（方便调试）
            Debug.Log($"=== 找到{chatBotAIs.Length}个名称为'ChatBot AI'的物体 ===");
            for (int i = 0; i < chatBotAIs.Length; i++)
            {
                GameObject obj = chatBotAIs[i];
                string path = GetGameObjectFullPath(obj);
                //Debug.Log($"索引 {i}：{path} | 激活状态：{obj.activeInHierarchy} ");
            }

            // 检查目标索引是否有效
            if (targetIndex < 0 || targetIndex >= chatBotAIs.Length)
            {
                //Debug.LogError($"目标索引{targetIndex}无效，找到{chatBotAIs.Length}个物体，索引范围应为0~{chatBotAIs.Length - 1}", this);
                return false;
            }

            // 选择目标父物体
            Transform targetParent = chatBotAIs[targetIndex].transform;

            // 移动层级并保持世界变换
            transform.SetParent(targetParent, worldPositionStays: false);

            return true;
        }

        /// <summary>
        /// 查找所有名称为"ChatBot AI"的场景物体（排除预制体资源）
        /// </summary>
        private GameObject[] FindAllChatBotAIGameObjects()
        {
            return Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(obj =>
                    obj.name == "ChatBot AI"  // 名称完全匹配
                    && obj.scene.isLoaded    // 确保是场景中加载的物体（不是预制体资源）
                )
                .OrderBy(obj => GetGameObjectFullPath(obj)) // 按路径排序，结果更稳定
                .ToArray();
        }

        /// <summary>
        /// 获取物体的完整层级路径（如：SceneRoot/Settings/ChatBot AI）
        /// </summary>
        private string GetGameObjectFullPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            while (parent != null)
            {
                path = $"{parent.name}/{path}";
                parent = parent.parent;
            }
            return path;
        }

        // 自动执行（可删除，改为外部调用）
        private void Start()
        {

           
            Invoke(nameof(ExecuteMove), 0.1f);
            DancePlayerUIManager dancePlayerUIManager = FindFirstObjectByType<DancePlayerUIManager>();
            dancePlayerUIManager.gameObject.GetComponent<RectTransform>().localScale = new Vector3(1, 1, 1);

        }
    }
}
