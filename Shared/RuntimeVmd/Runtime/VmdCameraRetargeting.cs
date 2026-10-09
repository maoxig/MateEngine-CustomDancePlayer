using UnityEngine;

namespace Maoxig.RuntimeVmd
{
    /// <summary>
    /// Builds a uniform camera scale plus an origin offset from corresponding
    /// source/target humanoid landmarks.  Head-to-feet height preserves the
    /// authored framing; anchoring the feet keeps the MMD ground plane aligned.
    /// </summary>
    public static class VmdCameraRetargeting
    {
        public static bool TryCalculate(
            Vector3 sourceHead,
            Vector3 sourceLeftFoot,
            Vector3 sourceRightFoot,
            Vector3 targetHead,
            Vector3 targetLeftFoot,
            Vector3 targetRightFoot,
            out float scale,
            out Vector3 offset)
        {
            scale = 1f;
            offset = Vector3.zero;
            if (!IsFinite(sourceHead) || !IsFinite(sourceLeftFoot) || !IsFinite(sourceRightFoot) ||
                !IsFinite(targetHead) || !IsFinite(targetLeftFoot) || !IsFinite(targetRightFoot))
                return false;

            Vector3 sourceFeet = (sourceLeftFoot + sourceRightFoot) * 0.5f;
            Vector3 targetFeet = (targetLeftFoot + targetRightFoot) * 0.5f;
            float sourceHeight = sourceHead.y - sourceFeet.y;
            float targetHeight = targetHead.y - targetFeet.y;
            if (sourceHeight <= 0.0001f || targetHeight <= 0.0001f) return false;

            scale = targetHeight / sourceHeight;
            if (!IsFinite(scale) || scale <= 0.0001f) return false;
            scale = Mathf.Clamp(scale, 0.01f, 100f);
            offset = targetFeet - sourceFeet * scale;
            return IsFinite(offset);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
