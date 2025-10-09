using UnityEngine;
using UnityEngine.UI;

namespace CustomDancePlayer
{
    /// <summary>
    /// Utility class for handling SkinnedMeshRenderer components with toggle control
    /// </summary>
    public class SMRHandler
    {
        public static int SetUpdateWhenOffscreen(GameObject model, bool value)
        {
            // Check if model is null
            if (model == null)
            {
                Debug.LogError("Model cannot be null!");
                return 0;
            }

            SkinnedMeshRenderer[] smrComponents = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            foreach (SkinnedMeshRenderer smr in smrComponents)
            {
                smr.updateWhenOffscreen = value;
            }

            // Return the number of processed components
            return smrComponents.Length;
        }

        public static SkinnedMeshRenderer[] GetAllSkinnedMeshRenderers(GameObject model)
        {
            if (model == null)
            {
                Debug.LogError("Model cannot be null!");
                return new SkinnedMeshRenderer[0];
            }

            return model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        }
    }
}
