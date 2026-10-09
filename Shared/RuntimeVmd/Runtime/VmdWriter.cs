using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Maoxig.RuntimeVmd
{
    /// <summary>Writes the parsed subset used by RuntimeVmd back to a standard VMD 0002 stream.</summary>
    public static class VmdWriter
    {
        private const uint WindowsShiftJisCodePage = 932;
        private const uint DefaultCharacterFlag = 0x00000000;
        private static readonly Encoding ShiftJis = CreateShiftJisEncoding();

        public static byte[] Write(VmdMotion motion)
        {
            if (motion == null) throw new ArgumentNullException("motion");
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII, true))
            {
                WriteFixedAscii(writer, "Vocaloid Motion Data 0002", 30);
                WriteFixedShiftJis(writer, motion.ModelName, 20);
                writer.Write(CheckedCount(motion.BoneFrames.Count, "bone"));
                for (int index = 0; index < motion.BoneFrames.Count; index++) WriteBone(writer, motion.BoneFrames[index]);
                writer.Write(CheckedCount(motion.MorphFrames.Count, "morph"));
                for (int index = 0; index < motion.MorphFrames.Count; index++) WriteMorph(writer, motion.MorphFrames[index]);
                writer.Write(CheckedCount(motion.CameraFrames.Count, "camera"));
                for (int index = 0; index < motion.CameraFrames.Count; index++) WriteCamera(writer, motion.CameraFrames[index]);
                writer.Write(CheckedCount(motion.LightFrames.Count, "light"));
                for (int index = 0; index < motion.LightFrames.Count; index++) WriteLight(writer, motion.LightFrames[index]);
                writer.Write(CheckedCount(motion.SelfShadowFrames.Count, "self-shadow"));
                for (int index = 0; index < motion.SelfShadowFrames.Count; index++) WriteSelfShadow(writer, motion.SelfShadowFrames[index]);
                writer.Write(CheckedCount(motion.IkFrames.Count, "IK"));
                for (int index = 0; index < motion.IkFrames.Count; index++) WriteIk(writer, motion.IkFrames[index]);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static void WriteBone(BinaryWriter writer, VmdBoneKeyframe frame)
        {
            WriteFixedShiftJis(writer, frame.BoneName, 15);
            writer.Write(frame.FrameNumber);
            WriteVector(writer, frame.Position);
            writer.Write(Finite(frame.Rotation.x));
            writer.Write(Finite(frame.Rotation.y));
            writer.Write(Finite(frame.Rotation.z));
            writer.Write(Finite(frame.Rotation.w, 1f));
            writer.Write(EncodeBoneInterpolation(frame.Interpolation));
        }

        private static void WriteMorph(BinaryWriter writer, VmdMorphKeyframe frame)
        {
            WriteFixedShiftJis(writer, frame.MorphName, 15);
            writer.Write(frame.FrameNumber);
            writer.Write(Finite(frame.Weight));
        }

        private static void WriteCamera(BinaryWriter writer, VmdCameraKeyframe frame)
        {
            writer.Write(frame.FrameNumber);
            writer.Write(Finite(frame.Distance));
            WriteVector(writer, frame.Position);
            WriteVector(writer, frame.RotationRadians);
            writer.Write(EncodeCameraInterpolation(frame.Interpolation));
            writer.Write(frame.FieldOfView);
            writer.Write((byte)(frame.Perspective ? 0 : 1));
        }

        private static void WriteLight(BinaryWriter writer, VmdLightKeyframe frame)
        {
            writer.Write(frame.FrameNumber);
            writer.Write(Finite(frame.Color.r));
            writer.Write(Finite(frame.Color.g));
            writer.Write(Finite(frame.Color.b));
            WriteVector(writer, frame.Position);
        }

        private static void WriteSelfShadow(BinaryWriter writer, VmdSelfShadowKeyframe frame)
        {
            writer.Write(frame.FrameNumber);
            writer.Write(frame.Mode);
            writer.Write(Finite(frame.Distance));
        }

        private static void WriteIk(BinaryWriter writer, VmdIkKeyframe frame)
        {
            writer.Write(frame.FrameNumber);
            writer.Write((byte)(frame.Visible ? 1 : 0));
            writer.Write(CheckedCount(frame.Toggles.Count, "IK toggle"));
            for (int index = 0; index < frame.Toggles.Count; index++)
            {
                WriteFixedShiftJis(writer, frame.Toggles[index].Name, 20);
                writer.Write((byte)(frame.Toggles[index].Enabled ? 1 : 0));
            }
        }

        private static byte[] EncodeBoneInterpolation(VmdBoneInterpolation interpolation)
        {
            byte[] output = new byte[64];
            VmdBezierCurve[] curves = { interpolation.X, interpolation.Y, interpolation.Z, interpolation.Rotation };
            for (int channel = 0; channel < curves.Length; channel++)
            {
                VmdBezierCurve curve = curves[channel];
                output[channel] = curve.X1;
                output[4 + channel] = curve.Y1;
                output[8 + channel] = curve.X2;
                output[12 + channel] = curve.Y2;
            }
            WriteRegisteredCurve(output, 16, interpolation.Y);
            WriteRegisteredCurve(output, 32, interpolation.Z);
            WriteRegisteredCurve(output, 48, interpolation.Rotation);
            return output;
        }

        private static void WriteRegisteredCurve(byte[] output, int offset, VmdBezierCurve curve)
        {
            output[offset] = curve.X1;
            output[offset + 4] = curve.Y1;
            output[offset + 8] = curve.X2;
            output[offset + 12] = curve.Y2;
        }

        private static byte[] EncodeCameraInterpolation(VmdCameraInterpolation interpolation)
        {
            byte[] output = new byte[24];
            WriteSequentialCurve(output, 0, interpolation.X);
            WriteSequentialCurve(output, 1, interpolation.Y);
            WriteSequentialCurve(output, 2, interpolation.Z);
            WriteSequentialCurve(output, 3, interpolation.Rotation);
            WriteSequentialCurve(output, 4, interpolation.Distance);
            WriteSequentialCurve(output, 5, interpolation.FieldOfView);
            return output;
        }

        private static void WriteSequentialCurve(byte[] output, int channel, VmdBezierCurve curve)
        {
            int offset = channel * 4;
            output[offset] = curve.X1;
            output[offset + 1] = curve.Y1;
            output[offset + 2] = curve.X2;
            output[offset + 3] = curve.Y2;
        }

        private static void WriteVector(BinaryWriter writer, UnityEngine.Vector3 value)
        {
            writer.Write(Finite(value.x));
            writer.Write(Finite(value.y));
            writer.Write(Finite(value.z));
        }

        private static float Finite(float value, float fallback = 0f)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }

        private static uint CheckedCount(int value, string label)
        {
            if (value < 0) throw new InvalidDataException("Negative VMD " + label + " count.");
            return checked((uint)value);
        }

        private static void WriteFixedAscii(BinaryWriter writer, string value, int byteCount)
        {
            WriteFixedBytes(writer, Encoding.ASCII.GetBytes(value ?? string.Empty), byteCount);
        }

        private static void WriteFixedShiftJis(BinaryWriter writer, string value, int byteCount)
        {
            WriteFixedBytes(writer, EncodeShiftJis(value ?? string.Empty), byteCount);
        }

        private static void WriteFixedBytes(BinaryWriter writer, byte[] value, int byteCount)
        {
            byte[] output = new byte[byteCount];
            Buffer.BlockCopy(value, 0, output, 0, Math.Min(value.Length, output.Length));
            writer.Write(output);
        }

        private static Encoding CreateShiftJisEncoding()
        {
            try { return Encoding.GetEncoding(932); }
            catch
            {
                try { return Encoding.GetEncoding("shift_jis"); }
                catch { return null; }
            }
        }

        private static byte[] EncodeShiftJis(string value)
        {
            if (ShiftJis != null) return ShiftJis.GetBytes(value);
            if (string.IsNullOrEmpty(value)) return new byte[0];
            try
            {
                int length = WideCharToMultiByte(WindowsShiftJisCodePage, DefaultCharacterFlag, value, value.Length,
                    null, 0, IntPtr.Zero, IntPtr.Zero);
                if (length > 0)
                {
                    byte[] output = new byte[length];
                    if (WideCharToMultiByte(WindowsShiftJisCodePage, DefaultCharacterFlag, value, value.Length,
                        output, output.Length, IntPtr.Zero, IntPtr.Zero) > 0) return output;
                }
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            return Encoding.ASCII.GetBytes(value);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern int WideCharToMultiByte(uint codePage, uint flags, string wideText, int wideCharacterCount,
            byte[] multiByteText, int multiByteCapacity, IntPtr defaultCharacter, IntPtr usedDefaultCharacter);
    }
}
