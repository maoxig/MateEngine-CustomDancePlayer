using UnityEngine;

namespace Maoxig.RuntimeVmd
{
    /// <summary>
    /// Converts the MMD target + signed-distance camera rig into a Unity camera pose.
    /// The rotation convention matches MMD's Y-X-Z camera Euler order after the
    /// (-X, +Y, -Z) coordinate-space conversion used by the runtime skeleton.
    /// </summary>
    public static class VmdCameraConverter
    {
        public const float MinimumFieldOfView = 1f;
        public const float MaximumFieldOfView = 179f;

        public static bool TryConvert(
            VmdCameraPose source,
            float importScale,
            bool convertMmdCoordinates,
            out VmdUnityCameraPose result)
        {
            result = default(VmdUnityCameraPose);
            if (!IsFinite(source.Position) || !IsFinite(source.RotationRadians) ||
                !IsFinite(source.Distance) || !IsFinite(source.FieldOfView) ||
                !IsFinite(importScale) || importScale <= 0f)
                return false;

            Vector3 pivot = convertMmdCoordinates
                ? new Vector3(-source.Position.x, source.Position.y, -source.Position.z)
                : source.Position;
            pivot *= importScale;

            Quaternion rotation = Multiply(
                Multiply(
                    Multiply(
                        AngleAxis(-source.RotationRadians.y, 0f, 1f, 0f),
                        AngleAxis(source.RotationRadians.x, 1f, 0f, 0f)),
                    AngleAxis(source.RotationRadians.z, 0f, 0f, 1f)),
                AngleAxis((float)System.Math.PI, 0f, 1f, 0f));
            Vector3 position = pivot + Rotate(rotation, new Vector3(0f, 0f, source.Distance * importScale));
            if(!source.Perspective&&source.Distance>0.00001f)rotation=Multiply(rotation,AngleAxis((float)System.Math.PI,0f,1f,0f));
            float fieldOfView = source.FieldOfView < MinimumFieldOfView
                ? MinimumFieldOfView
                : source.FieldOfView > MaximumFieldOfView ? MaximumFieldOfView : source.FieldOfView;
            if (!IsFinite(position) || !IsFinite(rotation) || !IsFinite(fieldOfView)) return false;

            result = new VmdUnityCameraPose(position, rotation, fieldOfView, source.Perspective);
            return true;
        }

        private static Quaternion AngleAxis(float radians, float axisX, float axisY, float axisZ)
        {
            float half = radians * 0.5f;
            float sine = (float)System.Math.Sin(half);
            float cosine = (float)System.Math.Cos(half);
            return new Quaternion(axisX * sine, axisY * sine, axisZ * sine, cosine);
        }

        private static Quaternion Multiply(Quaternion left, Quaternion right)
        {
            return new Quaternion(
                left.w * right.x + left.x * right.w + left.y * right.z - left.z * right.y,
                left.w * right.y - left.x * right.z + left.y * right.w + left.z * right.x,
                left.w * right.z + left.x * right.y - left.y * right.x + left.z * right.w,
                left.w * right.w - left.x * right.x - left.y * right.y - left.z * right.z);
        }

        private static Vector3 Rotate(Quaternion rotation, Vector3 value)
        {
            Vector3 axis = new Vector3(rotation.x, rotation.y, rotation.z);
            float dotAxisValue = axis.x * value.x + axis.y * value.y + axis.z * value.z;
            float dotAxisAxis = axis.x * axis.x + axis.y * axis.y + axis.z * axis.z;
            Vector3 cross = new Vector3(
                axis.y * value.z - axis.z * value.y,
                axis.z * value.x - axis.x * value.z,
                axis.x * value.y - axis.y * value.x);
            return axis * (2f * dotAxisValue)
                + value * (rotation.w * rotation.w - dotAxisAxis)
                + cross * (2f * rotation.w);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public struct VmdUnityCameraPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float FieldOfView;
        public bool Perspective;

        public VmdUnityCameraPose(Vector3 position, Quaternion rotation, float fieldOfView, bool perspective)
        {
            Position = position;
            Rotation = rotation;
            FieldOfView = fieldOfView;
            Perspective = perspective;
        }
    }
}
