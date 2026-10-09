using System;
using System.IO;
using System.Text;

namespace Maoxig.VmdDanceStudio
{
    public static class VmdInspector
    {
        private const uint MaximumReasonableFrameCount = 100000000;
        private static readonly object cacheGate=new object();
        private static readonly System.Collections.Generic.Dictionary<string,VmdTrackInfo> cache=new System.Collections.Generic.Dictionary<string,VmdTrackInfo>(StringComparer.OrdinalIgnoreCase);

        public static VmdTrackInfo Inspect(string path)
        {
            try
            {
                var file=new FileInfo(System.IO.Path.GetFullPath(path));
                if(!file.Exists)return InspectUncached(path);
                string key=file.FullName+"|"+file.Length+"|"+file.LastWriteTimeUtc.Ticks;
                lock(cacheGate){VmdTrackInfo cached;if(cache.TryGetValue(key,out cached))return cached.Copy();}
                var info=InspectUncached(file.FullName);
                lock(cacheGate){if(cache.Count>=64)cache.Clear();cache[key]=info.Copy();}
                return info;
            }
            catch(Exception error){return new VmdTrackInfo{Path=path,Error=error.Message};}
        }

        private static VmdTrackInfo InspectUncached(string path)
        {
            VmdTrackInfo info = new VmdTrackInfo { Path = System.IO.Path.GetFullPath(path) };
            try
            {
                FileInfo file = new FileInfo(info.Path);
                info.FileSize = file.Length;
                using (FileStream stream = new FileStream(info.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    byte[] headerBytes = ReadExactly(reader, 30);
                    string header = Encoding.ASCII.GetString(headerBytes).TrimEnd('\0', ' ');
                    if (!header.StartsWith("Vocaloid Motion Data", StringComparison.Ordinal))
                        throw new InvalidDataException("Not a VMD motion file.");
                    bool version2 = header.IndexOf("0002", StringComparison.Ordinal) >= 0;
                    info.ModelName = ReadFixedString(reader, version2 ? 20 : 10);
                    info.BoneKeys = ReadNamedRecords(reader, "bone", 92, info, info.BoneNames);
                    info.MorphKeys = ReadNamedRecords(reader, "morph", 4, info, info.MorphNames);
                    if (!HasMoreData(stream)) { Classify(info); return info; }
                    info.CameraKeys = ReadFixedRecords(reader, "camera", 0, 57, info);
                    if (!HasMoreData(stream)) { Classify(info); return info; }
                    info.LightKeys = ReadFixedRecords(reader, "light", 0, 24, info);
                    if (!HasMoreData(stream)) { Classify(info); return info; }
                    info.ShadowKeys = ReadFixedRecords(reader, "self-shadow", 0, 5, info);
                    if (!HasMoreData(stream)) { Classify(info); return info; }
                    ReadIkFrames(reader, info);
                }
                Classify(info);
            }
            catch (Exception exception)
            {
                info.Error = exception.Message;
            }
            return info;
        }

        private static uint ReadFixedRecords(BinaryReader reader, string section, int prefixBytes,
            int suffixBytes, VmdTrackInfo info)
        {
            uint count = ReadCount(reader, section);
            long recordSize = prefixBytes + 4L + suffixBytes;
            EnsureRemaining(reader.BaseStream, checked((long)count * recordSize), section);
            for (uint index = 0; index < count; index++)
            {
                Skip(reader, prefixBytes);
                uint frame = reader.ReadUInt32();
                if (frame > info.MaximumFrame) info.MaximumFrame = frame;
                Skip(reader, suffixBytes);
            }
            return count;
        }

        private static uint ReadNamedRecords(BinaryReader reader, string section, int suffixBytes,
            VmdTrackInfo info, System.Collections.Generic.List<string> names)
        {
            uint count = ReadCount(reader, section);
            long recordSize = 15L + 4L + suffixBytes;
            EnsureRemaining(reader.BaseStream, checked((long)count * recordSize), section);
            var decodedNames=new System.Collections.Generic.Dictionary<string,string>(StringComparer.Ordinal);
            for (uint index = 0; index < count; index++)
            {
                byte[] rawName=ReadExactly(reader,15);string key=Convert.ToBase64String(rawName),name;
                if(!decodedNames.TryGetValue(key,out name)){name=Maoxig.RuntimeVmd.VmdReader.DecodeName(rawName);decodedNames[key]=name;}
                AddUnique(names, name);
                uint frame = reader.ReadUInt32();
                if (frame > info.MaximumFrame) info.MaximumFrame = frame;
                Skip(reader, suffixBytes);
            }
            return count;
        }

        private static void ReadIkFrames(BinaryReader reader, VmdTrackInfo info)
        {
            uint count = ReadCount(reader, "IK");
            info.IkFrames = count;
            for (uint index = 0; index < count; index++)
            {
                EnsureRemaining(reader.BaseStream, 9, "IK frame");
                uint frame = reader.ReadUInt32();
                if (frame > info.MaximumFrame) info.MaximumFrame = frame;
                reader.ReadByte();
                uint toggleCount = ReadCount(reader, "IK toggle");
                EnsureRemaining(reader.BaseStream, checked((long)toggleCount * 21L), "IK toggle");
                info.IkToggles = checked(info.IkToggles + toggleCount);
                for (uint toggleIndex = 0; toggleIndex < toggleCount; toggleIndex++)
                {
                    string name = ReadFixedString(reader, 20);
                    AddUnique(info.IkNames, name);
                    if (reader.ReadByte() == 0) info.IkDisableToggles++;
                }
            }
        }

        private static void Classify(VmdTrackInfo info)
        {
            for (int index = 0; index < info.BoneNames.Count; index++)
            {
                string normalized = NormalizeName(info.BoneNames[index]);
                bool toeIk = ContainsAny(normalized, "つま先IK", "爪先IK", "TOEIK");
                bool legIk = !toeIk && ContainsAny(normalized, "左足IK", "右足IK", "LEGIK", "FOOTIK");
                if (toeIk) info.HasToeIk = true;
                if (legIk) info.HasLegIk = true;
                if (normalized.IndexOf("IK", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    info.HasFk = true;
                    info.FkBoneNameCount++;
                    if (IsRootBone(normalized)) info.HasRootFk = true;
                    if (IsTorsoBone(normalized)) info.HasTorsoFk = true;
                    if (IsLeftArmBone(normalized)) info.HasLeftArmFk = true;
                    if (IsRightArmBone(normalized)) info.HasRightArmFk = true;
                    if (IsLeftLegBone(normalized)) info.HasLeftLegFk = true;
                    if (IsRightLegBone(normalized)) info.HasRightLegFk = true;
                    if (IsFingerBone(normalized)) info.HasFingerFk = true;
                    if (IsEyeBone(normalized)) info.HasEyeFk = true;
                }
            }
            for (int index = 0; index < info.MorphNames.Count; index++)
            {
                string normalized = NormalizeName(info.MorphNames[index]);
                if (IsLipMorph(normalized)) info.HasLipMorph = true;
                if (IsExpressionMorph(normalized)) info.HasExpressionMorph = true;
            }
        }

        private static bool IsRootBone(string name)
        {
            return ContainsAny(name, "全ての親", "センター", "グルーブ", "ROOT", "CENTER", "GROOVE", "HIPS");
        }

        private static bool IsTorsoBone(string name)
        {
            return ContainsAny(name, "下半身", "上半身", "腰", "首", "頭", "SPINE", "CHEST", "BODY", "NECK", "HEAD");
        }

        private static bool IsLeftArmBone(string name)
        {
            return HasSide(name, true) && ContainsAny(name, "肩", "腕", "ひじ", "肘", "手首", "SHOULDER", "ARM", "ELBOW", "WRIST", "HAND");
        }

        private static bool IsRightArmBone(string name)
        {
            return HasSide(name, false) && ContainsAny(name, "肩", "腕", "ひじ", "肘", "手首", "SHOULDER", "ARM", "ELBOW", "WRIST", "HAND");
        }

        private static bool IsLeftLegBone(string name)
        {
            return HasSide(name, true) && ContainsAny(name, "足", "ひざ", "膝", "足首", "LEG", "KNEE", "ANKLE", "FOOT");
        }

        private static bool IsRightLegBone(string name)
        {
            return HasSide(name, false) && ContainsAny(name, "足", "ひざ", "膝", "足首", "LEG", "KNEE", "ANKLE", "FOOT");
        }

        private static bool IsFingerBone(string name)
        {
            return ContainsAny(name, "指", "親指", "人指", "中指", "薬指", "小指", "THUMB", "INDEX", "MIDDLE", "RING", "LITTLE", "PINKY");
        }

        private static bool IsEyeBone(string name)
        {
            return ContainsAny(name, "両目", "左目", "右目", "目線", "EYE", "GAZE");
        }

        private static bool HasSide(string name, bool left)
        {
            string japanese = left ? "左" : "右";
            string english = left ? "LEFT" : "RIGHT";
            string shortPrefix = left ? "L_" : "R_";
            string shortSuffix = left ? "_L" : "_R";
            return name.IndexOf(japanese, StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf(english, StringComparison.OrdinalIgnoreCase) >= 0
                || name.StartsWith(shortPrefix, StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(shortSuffix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLipMorph(string name)
        {
            if (name == "あ" || name == "い" || name == "う" || name == "え" || name == "お" || name == "ん" ||
                name == "ア" || name == "イ" || name == "ウ" || name == "エ" || name == "オ") return true;
            return ContainsAny(name, "リップ", "口", "MOUTH", "LIP", "VISEME", "MTH_");
        }

        private static bool IsExpressionMorph(string name)
        {
            return ContainsAny(name, "まばたき", "笑い", "ウィンク", "困る", "にこり", "怒り", "びっくり",
                "BLINK", "WINK", "EYE_", "BRW_", "BROW", "JOY", "ANGRY", "SORROW", "SURPRISE");
        }

        private static string NormalizeName(string value)
        {
            return (value ?? string.Empty).Trim()
                .Replace(" ", string.Empty).Replace("　", string.Empty)
                .Replace('Ｉ', 'I').Replace('ｉ', 'I').Replace('Ｋ', 'K').Replace('ｋ', 'K');
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            for (int index = 0; index < tokens.Length; index++)
                if (value.IndexOf(tokens[index], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static void AddUnique(System.Collections.Generic.List<string> values, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            for (int index = 0; index < values.Count; index++)
                if (string.Equals(values[index], value, StringComparison.OrdinalIgnoreCase)) return;
            values.Add(value);
        }

        private static uint ReadCount(BinaryReader reader, string section)
        {
            EnsureRemaining(reader.BaseStream, 4, section + " count");
            uint count = reader.ReadUInt32();
            if (count > MaximumReasonableFrameCount)
                throw new InvalidDataException("The VMD " + section + " section declares an unreasonable count: " + count + ".");
            return count;
        }

        private static string ReadFixedString(BinaryReader reader, int length)
        {
            byte[] bytes = ReadExactly(reader, length);
            return Maoxig.RuntimeVmd.VmdReader.DecodeName(bytes);
        }

        private static byte[] ReadExactly(BinaryReader reader, int count)
        {
            byte[] value = reader.ReadBytes(count);
            if (value.Length != count) throw new EndOfStreamException("Unexpected end of VMD file.");
            return value;
        }

        private static void Skip(BinaryReader reader, int count)
        {
            if (count <= 0) return;
            EnsureRemaining(reader.BaseStream, count, "record");
            reader.BaseStream.Seek(count, SeekOrigin.Current);
        }

        private static void EnsureRemaining(Stream stream, long required, string section)
        {
            if (required < 0 || stream.Length - stream.Position < required)
                throw new EndOfStreamException("The VMD " + section + " section is truncated.");
        }

        private static bool HasMoreData(Stream stream)
        {
            return stream.Position < stream.Length;
        }
    }
}
