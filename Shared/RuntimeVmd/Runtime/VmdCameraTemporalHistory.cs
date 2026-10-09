using System;
using System.Reflection;
using UnityEngine;

namespace Maoxig.RuntimeVmd
{
    /// <summary>
    /// Clears temporal post-processing history without forcing projects to take a
    /// compile-time dependency on a particular render pipeline package.
    /// </summary>
    public static class VmdCameraTemporalHistory
    {
        private const string PostProcessLayerTypeName = "UnityEngine.Rendering.PostProcessing.PostProcessLayer";

        public static int Reset(Camera camera)
        {
            if (camera == null) return 0;
            int resetCount = 0;
            Component[] components = camera.GetComponents<Component>();
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null || component.GetType().FullName != PostProcessLayerTypeName) continue;
                try
                {
                    MethodInfo method = component.GetType().GetMethod(
                        "ResetHistory",
                        BindingFlags.Instance | BindingFlags.Public,
                        null,
                        Type.EmptyTypes,
                        null);
                    if (method == null) continue;
                    method.Invoke(component, null);
                    resetCount++;
                }
                catch (Exception exception)
                {
                    // Some stripped Unity Mono profiles (including the installed
                    // MateEngine build) omit Exception.GetBaseException(). The
                    // temporal reset is optional, so reporting the immediate
                    // message must never abort camera playback.
                    Debug.LogWarning("[RuntimeVmd.Camera] Could not reset temporal history on '" +
                        camera.name + "': " + exception.Message, camera);
                }
            }
            return resetCount;
        }
    }
}
