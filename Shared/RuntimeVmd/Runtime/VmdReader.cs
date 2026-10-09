using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Maoxig.RuntimeVmd
{
    public static class VmdReader
    {
        private const uint MaxReasonableFrameCount = 10000000;
        public const long MaximumInputBytes = 512L * 1024L * 1024L;
        private const int BoneFrameBytes = 111;
        private const int MorphFrameBytes = 23;
        private const int CameraFrameBytes = 61;
        private const int LightFrameBytes = 28;
        private const int SelfShadowFrameBytes = 9;
        private const int IkFrameMinimumBytes = 9;
        private const int IkToggleBytes = 21;
        private static readonly Encoding ShiftJis = CreateShiftJisEncoding();
        private const uint WindowsShiftJisCodePage = 932;

        public static string LastDecodingBackend { get; private set; }

        public static VmdMotion Read(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("A VMD path is required.", "path");
            }

            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Read(stream, false);
            }
        }

        public static VmdMotion Read(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException("bytes");
            }

            using (MemoryStream stream = new MemoryStream(bytes, false))
            {
                return Read(stream, false);
            }
        }

        public static VmdMotion Read(Stream stream, bool leaveOpen)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            if (!stream.CanRead)
            {
                throw new ArgumentException("The VMD stream must be readable.", "stream");
            }
            ValidateRemainingInputSize(stream);

            BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen);
            try
            {
                VmdMotion motion = new VmdMotion();
                motion.Header = ReadFixedString(reader, 30);
                bool version2 = motion.Header.IndexOf("Vocaloid Motion Data 0002", StringComparison.Ordinal) >= 0;
                bool version1 = motion.Header.IndexOf("Vocaloid Motion Data file", StringComparison.Ordinal) >= 0;
                if (!version2 && !version1)
                {
                    throw new InvalidDataException("The file is not a supported Vocaloid Motion Data (VMD) file.");
                }

                motion.ModelName = ReadFixedString(reader, version2 ? 20 : 10);
                ReadBoneFrames(reader, motion);
                ReadMorphFrames(reader, motion);

                if (!HasMoreData(stream)) return motion;
                ReadCameraFrames(reader, motion);
                if (!HasMoreData(stream)) return motion;
                ReadLightFrames(reader, motion);
                if (!HasMoreData(stream)) return motion;
                ReadSelfShadowFrames(reader, motion);
                if (!HasMoreData(stream)) return motion;
                ReadIkFrames(reader, motion);

                return motion;
            }
            catch (EndOfStreamException exception)
            {
                throw new InvalidDataException("The VMD file is truncated or contains an invalid section count.", exception);
            }
            finally
            {
                reader.Dispose();
            }
        }

        private static void ReadBoneFrames(BinaryReader reader, VmdMotion motion)
        {
            uint count = ReadCount(reader, "bone", BoneFrameBytes);
            for (uint index = 0; index < count; index++)
            {
                VmdBoneKeyframe frame = new VmdBoneKeyframe();
                frame.BoneName = ReadFixedString(reader, 15);
                frame.FrameNumber = reader.ReadUInt32();
                frame.Position = ReadVector3(reader);
                frame.Rotation = NormalizeSafe(ReadQuaternion(reader));
                byte[] interpolation = ReadExactly(reader, 64);
                frame.Interpolation = ParseBoneInterpolation(interpolation);
                motion.BoneFrames.Add(frame);
                IncludeFrame(motion, frame.FrameNumber);
            }
        }

        private static void ReadMorphFrames(BinaryReader reader, VmdMotion motion)
        {
            uint count = ReadCount(reader, "morph", MorphFrameBytes);
            for (uint index = 0; index < count; index++)
            {
                VmdMorphKeyframe frame = new VmdMorphKeyframe();
                frame.MorphName = ReadFixedString(reader, 15);
                frame.FrameNumber = reader.ReadUInt32();
                frame.Weight = reader.ReadSingle();
                motion.MorphFrames.Add(frame);
                IncludeFrame(motion, frame.FrameNumber);
            }
        }

        private static void ReadCameraFrames(BinaryReader reader, VmdMotion motion)
        {
            uint count = ReadCount(reader, "camera", CameraFrameBytes);
            for (uint index = 0; index < count; index++)
            {
                VmdCameraKeyframe frame = new VmdCameraKeyframe();
                frame.FrameNumber = reader.ReadUInt32();
                frame.Distance = reader.ReadSingle();
                frame.Position = ReadVector3(reader);
                frame.RotationRadians = ReadVector3(reader);
                frame.Interpolation = ParseCameraInterpolation(ReadExactly(reader, 24));
                frame.FieldOfView = reader.ReadUInt32();
                frame.Perspective = reader.ReadByte() == 0;
                motion.CameraFrames.Add(frame);
                IncludeFrame(motion, frame.FrameNumber);
            }
        }

        private static void ReadLightFrames(BinaryReader reader, VmdMotion motion)
        {
            uint count = ReadCount(reader, "light", LightFrameBytes);
            for (uint index = 0; index < count; index++)
            {
                VmdLightKeyframe frame = new VmdLightKeyframe();
                frame.FrameNumber = reader.ReadUInt32();
                Vector3 color = ReadVector3(reader);
                frame.Color = new Color(color.x, color.y, color.z, 1f);
                frame.Position = ReadVector3(reader);
                motion.LightFrames.Add(frame);
                IncludeFrame(motion, frame.FrameNumber);
            }
        }

        private static void ReadSelfShadowFrames(BinaryReader reader, VmdMotion motion)
        {
            uint count = ReadCount(reader, "self-shadow", SelfShadowFrameBytes);
            for (uint index = 0; index < count; index++)
            {
                VmdSelfShadowKeyframe frame = new VmdSelfShadowKeyframe();
                frame.FrameNumber = reader.ReadUInt32();
                frame.Mode = reader.ReadByte();
                frame.Distance = reader.ReadSingle();
                motion.SelfShadowFrames.Add(frame);
                IncludeFrame(motion, frame.FrameNumber);
            }
        }

        private static void ReadIkFrames(BinaryReader reader, VmdMotion motion)
        {
            uint count = ReadCount(reader, "IK", IkFrameMinimumBytes);
            for (uint index = 0; index < count; index++)
            {
                VmdIkKeyframe frame = new VmdIkKeyframe();
                frame.FrameNumber = reader.ReadUInt32();
                frame.Visible = reader.ReadByte() != 0;
                uint toggleCount = ReadCount(reader, "IK toggle", IkToggleBytes);
                for (uint toggleIndex = 0; toggleIndex < toggleCount; toggleIndex++)
                {
                    VmdIkToggle toggle = new VmdIkToggle();
                    toggle.Name = ReadFixedString(reader, 20);
                    toggle.Enabled = reader.ReadByte() != 0;
                    frame.Toggles.Add(toggle);
                }
                motion.IkFrames.Add(frame);
                IncludeFrame(motion, frame.FrameNumber);
            }
        }

        private static VmdBoneInterpolation ParseBoneInterpolation(byte[] bytes)
        {
            return new VmdBoneInterpolation(
                ParseInterleavedCurve(bytes, 0),
                ParseInterleavedCurve(bytes, 1),
                ParseInterleavedCurve(bytes, 2),
                ParseInterleavedCurve(bytes, 3));
        }

        private static VmdBezierCurve ParseInterleavedCurve(byte[] bytes, int channel)
        {
            return new VmdBezierCurve(
                bytes[channel],
                bytes[4 + channel],
                bytes[8 + channel],
                bytes[12 + channel]);
        }

        private static VmdCameraInterpolation ParseCameraInterpolation(byte[] bytes)
        {
            return new VmdCameraInterpolation(
                ParseSequentialCurve(bytes, 0),
                ParseSequentialCurve(bytes, 1),
                ParseSequentialCurve(bytes, 2),
                ParseSequentialCurve(bytes, 3),
                ParseSequentialCurve(bytes, 4),
                ParseSequentialCurve(bytes, 5));
        }

        private static VmdBezierCurve ParseSequentialCurve(byte[] bytes, int channel)
        {
            int offset = channel * 4;
            return new VmdBezierCurve(bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3]);
        }

        private static uint ReadCount(BinaryReader reader, string section, int minimumItemBytes)
        {
            uint count = reader.ReadUInt32();
            if (count > MaxReasonableFrameCount)
            {
                throw new InvalidDataException("The VMD " + section + " section declares an unreasonable item count: " + count + ".");
            }
            Stream stream = reader.BaseStream;
            if (count > 0 && minimumItemBytes > 0 && stream.CanSeek)
            {
                long remaining = stream.Length - stream.Position;
                ulong minimumRequired = (ulong)count * (uint)minimumItemBytes;
                if (remaining < 0 || minimumRequired > (ulong)remaining)
                {
                    throw new InvalidDataException("The VMD " + section + " section declares " + count
                        + " item(s), requiring at least " + minimumRequired + " bytes, but only "
                        + Math.Max(0L, remaining) + " bytes remain.");
                }
            }
            return count;
        }

        private static void ValidateRemainingInputSize(Stream stream)
        {
            if (!stream.CanSeek) return;
            long remaining = stream.Length - stream.Position;
            if (remaining < 0)
                throw new InvalidDataException("The VMD stream position is beyond the end of the stream.");
            if (remaining > MaximumInputBytes)
                throw new InvalidDataException("The VMD input exceeds the 512 MiB safety limit.");
        }

        public static string DecodeName(byte[] bytes)
        {
            if(bytes==null)throw new ArgumentNullException("bytes");
            int length=Array.IndexOf(bytes,(byte)0);
            if(length<0)length=bytes.Length;
            return DecodeShiftJis(bytes,length).Trim();
        }

        private static string ReadFixedString(BinaryReader reader, int byteCount)
        {
            byte[] bytes = ReadExactly(reader, byteCount);
            int length = Array.IndexOf(bytes, (byte)0);
            if (length < 0) length = bytes.Length;
            return DecodeShiftJis(bytes, length).TrimEnd();
        }

        private static byte[] ReadExactly(BinaryReader reader, int count)
        {
            byte[] bytes = reader.ReadBytes(count);
            if (bytes.Length != count)
            {
                throw new EndOfStreamException();
            }
            return bytes;
        }

        private static Vector3 ReadVector3(BinaryReader reader)
        {
            return new Vector3(ReadFinite(reader), ReadFinite(reader), ReadFinite(reader));
        }

        private static Quaternion ReadQuaternion(BinaryReader reader)
        {
            return new Quaternion(ReadFinite(reader), ReadFinite(reader), ReadFinite(reader), ReadFinite(reader));
        }

        private static float ReadFinite(BinaryReader reader)
        {
            float value = reader.ReadSingle();
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        private static Quaternion NormalizeSafe(Quaternion value)
        {
            float magnitudeSquared = value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w;
            if (magnitudeSquared < 0.00000001f)
            {
                return Quaternion.identity;
            }
            float inverseMagnitude = 1f / Mathf.Sqrt(magnitudeSquared);
            return new Quaternion(
                value.x * inverseMagnitude,
                value.y * inverseMagnitude,
                value.z * inverseMagnitude,
                value.w * inverseMagnitude);
        }

        private static void IncludeFrame(VmdMotion motion, uint frameNumber)
        {
            if (frameNumber > motion.MaxFrameNumber)
            {
                motion.MaxFrameNumber = frameNumber;
            }
        }

        private static bool HasMoreData(Stream stream)
        {
            if (!stream.CanSeek)
            {
                return true;
            }
            return stream.Position < stream.Length;
        }

        private static Encoding CreateShiftJisEncoding()
        {
            try
            {
                return Encoding.GetEncoding(932);
            }
            catch
            {
                try
                {
                    return Encoding.GetEncoding("shift_jis");
                }
                catch
                {
                    // Some stripped Unity Mono players omit I18N.CJK entirely. Keep the
                    // type initializer alive and use the Windows cp932 API when available.
                    return null;
                }
            }
        }

        private static string DecodeShiftJis(byte[] bytes, int length)
        {
            if (ShiftJis != null)
            {
                LastDecodingBackend = "managed-cp932";
                return ShiftJis.GetString(bytes, 0, length);
            }
            if (length == 0) return string.Empty;
            try
            {
                StringBuilder characters = new StringBuilder(length);
                int characterCount = MultiByteToWideChar(WindowsShiftJisCodePage, 0, bytes, length, characters, characters.Capacity);
                if (characterCount > 0)
                {
                    LastDecodingBackend = "windows-cp932";
                    return characters.ToString(0, characterCount);
                }
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }

            // Last-resort portable degradation: preserve ASCII and half-width katakana,
            // replace unsupported double-byte characters instead of rejecting the VMD.
            LastDecodingBackend = "portable-lossy";
            StringBuilder fallback = new StringBuilder(length);
            for (int index = 0; index < length; index++)
            {
                byte value = bytes[index];
                if (value <= 0x7f) fallback.Append((char)value);
                else if (value >= 0xa1 && value <= 0xdf) fallback.Append((char)(0xff61 + value - 0xa1));
                else
                {
                    fallback.Append('\ufffd');
                    if (index + 1 < length) index++;
                }
            }
            return fallback.ToString();
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern int MultiByteToWideChar(
            uint codePage,
            uint flags,
            byte[] multiByteText,
            int multiByteCount,
            [Out] StringBuilder wideText,
            int wideCharacterCapacity);
    }
}
