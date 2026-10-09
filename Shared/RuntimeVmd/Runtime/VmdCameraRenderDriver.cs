using UnityEngine;

namespace Maoxig.RuntimeVmd
{
    /// <summary>
    /// Reapplies the sampled VMD pose immediately before a Camera renders.  Some
    /// host games update their first-person camera after ordinary LateUpdate,
    /// which otherwise wins over the dance camera for the visible frame.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(32000)]
    public sealed class VmdCameraRenderDriver : MonoBehaviour
    {
        private VmdNativePmxPlayer owner;

        internal void Bind(VmdNativePmxPlayer value)
        {
            owner = value;
            enabled = value != null;
        }

        private void OnPreCull()
        {
            if (owner != null && owner.isActiveAndEnabled && owner.IsActive)
                owner.ApplyLastCameraPoseForRender();
        }

        private void OnPreRender()
        {
            // A few host camera stacks also write during OnPreCull. Reapply once
            // more after culling so the actual render matrices keep the VMD pose.
            if (owner != null && owner.isActiveAndEnabled && owner.IsActive)
                owner.ApplyLastCameraPoseForRender();
        }
    }
}
