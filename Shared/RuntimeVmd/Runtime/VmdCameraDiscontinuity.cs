using UnityEngine;

namespace Maoxig.RuntimeVmd
{
    /// <summary>Detects camera changes for which temporal render history is invalid.</summary>
    public static class VmdCameraDiscontinuity
    {
        public const float MaximumContinuousFrameAdvance = 2.5f;
        public const float PositionCutDistance = 0.5f;
        public const float RotationCutDegrees = 15f;
        public const float FieldOfViewCutDegrees = 8f;

        public static bool TryGetReason(
            bool hasPreviousPose,
            VmdUnityCameraPose previousPose,
            float previousFrame,
            VmdUnityCameraPose currentPose,
            float currentFrame,
            bool authoredHardCut,
            out string reason)
        {
            if (!hasPreviousPose)
            {
                reason = "initial camera pose";
                return true;
            }
            if (currentFrame + 0.001f < previousFrame)
            {
                reason = "camera seek or loop";
                return true;
            }
            if (currentFrame - previousFrame > MaximumContinuousFrameAdvance)
            {
                reason = "camera frame skip";
                return true;
            }
            if (authoredHardCut)
            {
                reason = "authored one-frame camera cut";
                return true;
            }
            if (previousPose.Perspective != currentPose.Perspective)
            {
                reason = "camera projection switch";
                return true;
            }
            if (Vector3.Distance(previousPose.Position, currentPose.Position) > PositionCutDistance)
            {
                reason = "camera position jump";
                return true;
            }
            if (QuaternionAngle(previousPose.Rotation, currentPose.Rotation) > RotationCutDegrees)
            {
                reason = "camera rotation jump";
                return true;
            }
            if (Mathf.Abs(previousPose.FieldOfView - currentPose.FieldOfView) > FieldOfViewCutDegrees)
            {
                reason = "camera field-of-view jump";
                return true;
            }

            reason = null;
            return false;
        }

        // Quaternion.Angle is absent from some of the older Unity reference
        // assemblies used by MateEngine. Keep the same shortest-arc result
        // without binding RuntimeVmd to that API member.
        private static float QuaternionAngle(Quaternion left, Quaternion right)
        {
            float dot = left.x * right.x + left.y * right.y + left.z * right.z + left.w * right.w;
            dot = Mathf.Clamp(Mathf.Abs(dot), 0f, 1f);
            return (float)(System.Math.Acos(dot) * (360.0 / System.Math.PI));
        }
    }
}
