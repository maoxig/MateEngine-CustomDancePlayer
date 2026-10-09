using UnityEngine;

namespace Maoxig.RuntimeVmd
{
    /// <summary>
    /// Transfers the root portion of a Unity HumanPose between Humanoid Avatars.
    /// HumanPose.bodyPosition is already normalized by Avatar humanScale, so its
    /// delta must not be multiplied by a second source/target height ratio.
    /// </summary>
    public static class VmdHumanPoseRetargeting
    {
        public static Vector3 RetargetBodyPosition(
            Vector3 sourceRest,
            Vector3 sourcePose,
            Vector3 targetRest)
        {
            return targetRest + (sourcePose - sourceRest);
        }

        public static Quaternion RetargetBodyRotation(
            Quaternion sourceRest,
            Quaternion sourcePose,
            Quaternion targetRest)
        {
            return targetRest * Quaternion.Inverse(sourceRest) * sourcePose;
        }
    }
}
