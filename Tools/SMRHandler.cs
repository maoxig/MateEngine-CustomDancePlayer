using UnityEngine;
using UnityEngine.UI;

namespace CustomDancePlayer
{
    /// <summary>
    /// Utility class for handling SkinnedMeshRenderer components with toggle control
    /// </summary>
    public class SMRHandler
    {
        /// <summary>
        /// Sets the updateWhenOffscreen property for all SkinnedMeshRenderer components in the model and its children
        /// </summary>
        /// <param name="model">Target model</param>
        /// <param name="value">Value to set (true or false)</param>
        /// <returns>Number of components successfully updated</returns>
        public static int SetUpdateWhenOffscreen(GameObject model, bool value)
        {
            // Check if model is null
            if (model == null)
            {
                Debug.LogError("Model cannot be null!");
                return 0;
            }

            // Get all SkinnedMeshRenderer components in the model and its children, including inactive ones
            SkinnedMeshRenderer[] smrComponents = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            // Iterate through all components and set the property
            foreach (SkinnedMeshRenderer smr in smrComponents)
            {
                smr.updateWhenOffscreen = value;
            }

            // Return the number of processed components
            return smrComponents.Length;
        }

        /// <summary>
        /// Gets all SkinnedMeshRenderer components in the model and its children
        /// </summary>
        /// <param name="model">Target model</param>
        /// <returns>Array of SkinnedMeshRenderer components, empty array if model is null</returns>
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
