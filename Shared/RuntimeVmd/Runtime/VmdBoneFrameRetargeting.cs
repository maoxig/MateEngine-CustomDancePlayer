using UnityEngine;

namespace Maoxig.RuntimeVmd
{
    // A segment direction leaves axial roll undetermined. A projected second
    // anatomical direction supplies the missing degree of freedom. These frames
    // live in world space; arbitrary bone-local axes do not enter calibration.
    public static class VmdBoneFrameRetargeting
    {
        public static bool TryFrame(Vector3 segment, Vector3 secondary, out Quaternion frame)
        {
            frame = Quaternion.identity;
            if (segment.sqrMagnitude < 0.00000001f) return false;
            Vector3 forward = segment.normalized;
            Vector3 up = secondary - forward * Vector3.Dot(secondary, forward);
            if (up.sqrMagnitude < 0.00000001f) return false;
            frame = Quaternion.LookRotation(forward, up.normalized);
            return true;
        }

        public static bool TryAlignRest(Vector3 targetSegment, Vector3 targetSecondary,
            Vector3 sourceSegment, Vector3 sourceSecondary, Quaternion targetRest,
            out Quaternion alignedRest)
        {
            alignedRest = targetRest;
            Quaternion targetFrame, sourceFrame;
            if (!TryFrame(targetSegment, targetSecondary, out targetFrame) ||
                !TryFrame(sourceSegment, sourceSecondary, out sourceFrame)) return false;
            alignedRest = sourceFrame * Quaternion.Inverse(targetFrame) * targetRest;
            return true;
        }
    }
}
