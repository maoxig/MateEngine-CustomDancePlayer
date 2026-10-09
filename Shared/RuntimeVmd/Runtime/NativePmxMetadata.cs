using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Maoxig.RuntimeVmd
{
    /// <summary>
    /// The small managed-side PMX view needed to build a Humanoid bridge. All MMD
    /// deformation semantics remain in mmd_runtime_ffi; this parser only reads bone
    /// hierarchy/rest positions and morph names.
    /// </summary>
    public sealed class NativePmxMetadata
    {
        private const int MaximumElementCount = 10000000;

        public sealed class Bone
        {
            public int Index;
            public string NameJapanese;
            public string NameEnglish;
            public Vector3 Position;
            public int ParentIndex;
            public ushort Flags;
        }

        public sealed class Morph
        {
            public int Index;
            public string NameJapanese;
            public string NameEnglish;
            public byte Type;
        }

        public float Version { get; private set; }
        public string NameJapanese { get; private set; }
        public string NameEnglish { get; private set; }
        public List<Bone> Bones { get; private set; }
        public List<Morph> Morphs { get; private set; }

        private NativePmxMetadata()
        {
            NameJapanese = string.Empty;
            NameEnglish = string.Empty;
            Bones = new List<Bone>();
            Morphs = new List<Morph>();
        }

        public static NativePmxMetadata Read(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A PMX path is required.", "path");
            return Read(File.ReadAllBytes(path));
        }

        public static NativePmxMetadata Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) throw new ArgumentException("PMX data is required.", "bytes");
            using (MemoryStream stream = new MemoryStream(bytes, false))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                byte[] signature = ReadBytes(reader, 4, "signature");
                if (signature[0] != (byte)'P' || signature[1] != (byte)'M' || signature[2] != (byte)'X' || signature[3] != (byte)' ')
                    throw new InvalidDataException("The file is not a PMX model.");

                NativePmxMetadata result = new NativePmxMetadata();
                result.Version = reader.ReadSingle();
                if (result.Version < 2.0f || result.Version > 2.1f)
                    throw new InvalidDataException("Unsupported PMX version " + result.Version + ".");

                int headerSize = reader.ReadByte();
                if (headerSize < 8) throw new InvalidDataException("The PMX global header is incomplete.");
                byte[] globals = ReadBytes(reader, headerSize, "global header");
                Context context = new Context(
                    reader,
                    globals[0] == 0 ? Encoding.Unicode : Encoding.UTF8,
                    globals[1], globals[2], globals[3], globals[4], globals[5], globals[6], globals[7]);
                context.Validate();

                result.NameJapanese = context.ReadString("model name");
                result.NameEnglish = context.ReadString("model English name");
                context.ReadString("model comment");
                context.ReadString("model English comment");

                SkipVertices(context);
                SkipFaces(context);
                SkipTextures(context);
                SkipMaterials(context);
                ReadBones(context, result);
                ReadMorphs(context, result);
                ValidateHierarchy(result.Bones);
                return result;
            }
        }

        private static void SkipVertices(Context context)
        {
            int count = context.ReadCount("vertex");
            for (int i = 0; i < count; i++)
            {
                context.SkipFloats(3 + 3 + 2 + context.AdditionalUvCount * 4);
                byte deform = context.Reader.ReadByte();
                switch (deform)
                {
                    case 0:
                        context.ReadBoneIndex();
                        break;
                    case 1:
                        context.ReadBoneIndex(); context.ReadBoneIndex(); context.Reader.ReadSingle();
                        break;
                    case 2:
                    case 4:
                        for (int n = 0; n < 4; n++) context.ReadBoneIndex();
                        context.SkipFloats(4);
                        break;
                    case 3:
                        context.ReadBoneIndex(); context.ReadBoneIndex(); context.Reader.ReadSingle();
                        context.SkipFloats(9);
                        break;
                    default:
                        throw new InvalidDataException("Unsupported PMX vertex deform type " + deform + ".");
                }
                context.Reader.ReadSingle();
            }
        }

        private static void SkipFaces(Context context)
        {
            int count = context.ReadCount("face index");
            for (int i = 0; i < count; i++) context.ReadVertexIndex();
        }

        private static void SkipTextures(Context context)
        {
            int count = context.ReadCount("texture");
            for (int i = 0; i < count; i++) context.ReadString("texture path");
        }

        private static void SkipMaterials(Context context)
        {
            int count = context.ReadCount("material");
            for (int i = 0; i < count; i++)
            {
                context.ReadString("material name");
                context.ReadString("material English name");
                context.SkipFloats(4 + 3 + 1 + 3);
                context.Reader.ReadByte();
                context.SkipFloats(4 + 1);
                context.ReadTextureIndex();
                context.ReadTextureIndex();
                context.Reader.ReadByte();
                byte sharedToon = context.Reader.ReadByte();
                if (sharedToon == 0) context.ReadTextureIndex();
                else if (sharedToon == 1) context.Reader.ReadByte();
                else throw new InvalidDataException("The PMX shared toon flag is invalid.");
                context.ReadString("material memo");
                context.Reader.ReadInt32();
            }
        }

        private static void ReadBones(Context context, NativePmxMetadata result)
        {
            int count = context.ReadCount("bone");
            result.Bones.Capacity = count;
            for (int i = 0; i < count; i++)
            {
                Bone bone = new Bone();
                bone.Index = i;
                bone.NameJapanese = context.ReadString("bone name");
                bone.NameEnglish = context.ReadString("bone English name");
                bone.Position = context.ReadVector3();
                bone.ParentIndex = context.ReadBoneIndex();
                context.Reader.ReadInt32();
                bone.Flags = context.Reader.ReadUInt16();

                if ((bone.Flags & 0x0001) != 0) context.ReadBoneIndex(); else context.ReadVector3();
                if ((bone.Flags & 0x0300) != 0) { context.ReadBoneIndex(); context.Reader.ReadSingle(); }
                if ((bone.Flags & 0x0400) != 0) context.ReadVector3();
                if ((bone.Flags & 0x0800) != 0) { context.ReadVector3(); context.ReadVector3(); }
                if ((bone.Flags & 0x2000) != 0) context.Reader.ReadInt32();
                if ((bone.Flags & 0x0020) != 0)
                {
                    context.ReadBoneIndex();
                    context.Reader.ReadInt32();
                    context.Reader.ReadSingle();
                    int links = context.ReadCount("IK link");
                    for (int link = 0; link < links; link++)
                    {
                        context.ReadBoneIndex();
                        byte limited = context.Reader.ReadByte();
                        if (limited != 0) { context.ReadVector3(); context.ReadVector3(); }
                    }
                }
                result.Bones.Add(bone);
            }
        }

        private static void ReadMorphs(Context context, NativePmxMetadata result)
        {
            int count = context.ReadCount("morph");
            result.Morphs.Capacity = count;
            for (int i = 0; i < count; i++)
            {
                Morph morph = new Morph();
                morph.Index = i;
                morph.NameJapanese = context.ReadString("morph name");
                morph.NameEnglish = context.ReadString("morph English name");
                context.Reader.ReadByte();
                morph.Type = context.Reader.ReadByte();
                int offsets = context.ReadCount("morph offset");
                for (int offset = 0; offset < offsets; offset++) SkipMorphOffset(context, morph.Type);
                result.Morphs.Add(morph);
            }
        }

        private static void SkipMorphOffset(Context context, byte type)
        {
            switch (type)
            {
                case 0:
                case 9:
                    context.ReadMorphIndex(); context.Reader.ReadSingle();
                    break;
                case 1:
                    context.ReadVertexIndex(); context.SkipFloats(3);
                    break;
                case 2:
                    context.ReadBoneIndex(); context.SkipFloats(3 + 4);
                    break;
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                    context.ReadVertexIndex(); context.SkipFloats(4);
                    break;
                case 8:
                    context.ReadMaterialIndex(); context.Reader.ReadByte();
                    context.SkipFloats(4 + 3 + 1 + 3 + 4 + 1 + 4 + 4 + 4);
                    break;
                case 10:
                    context.ReadRigidBodyIndex(); context.Reader.ReadByte(); context.SkipFloats(3 + 3);
                    break;
                default:
                    throw new InvalidDataException("Unsupported PMX morph type " + type + ".");
            }
        }

        private static void ValidateHierarchy(List<Bone> bones)
        {
            for (int i = 0; i < bones.Count; i++)
            {
                int parent = bones[i].ParentIndex;
                if (parent < -1 || parent >= bones.Count) throw new InvalidDataException("PMX bone " + i + " has an invalid parent.");
                int current = parent;
                int hops = 0;
                while (current >= 0)
                {
                    if (current == i || ++hops > bones.Count) throw new InvalidDataException("The PMX bone hierarchy contains a cycle.");
                    current = bones[current].ParentIndex;
                }
            }
        }

        private static byte[] ReadBytes(BinaryReader reader, int count, string label)
        {
            byte[] data = reader.ReadBytes(count);
            if (data.Length != count) throw new EndOfStreamException("The PMX " + label + " is incomplete.");
            return data;
        }

        private sealed class Context
        {
            public readonly BinaryReader Reader;
            public readonly Encoding Encoding;
            public readonly int AdditionalUvCount;
            private readonly int vertexIndexSize;
            private readonly int textureIndexSize;
            private readonly int materialIndexSize;
            private readonly int boneIndexSize;
            private readonly int morphIndexSize;
            private readonly int rigidBodyIndexSize;

            public Context(BinaryReader reader, Encoding encoding, int additionalUvCount, int vertexIndexSize,
                int textureIndexSize, int materialIndexSize, int boneIndexSize, int morphIndexSize, int rigidBodyIndexSize)
            {
                Reader = reader;
                Encoding = encoding;
                AdditionalUvCount = additionalUvCount;
                this.vertexIndexSize = vertexIndexSize;
                this.textureIndexSize = textureIndexSize;
                this.materialIndexSize = materialIndexSize;
                this.boneIndexSize = boneIndexSize;
                this.morphIndexSize = morphIndexSize;
                this.rigidBodyIndexSize = rigidBodyIndexSize;
            }

            public void Validate()
            {
                if (AdditionalUvCount < 0 || AdditionalUvCount > 4) throw new InvalidDataException("The PMX additional UV count is invalid.");
                ValidateIndexSize(vertexIndexSize); ValidateIndexSize(textureIndexSize); ValidateIndexSize(materialIndexSize);
                ValidateIndexSize(boneIndexSize); ValidateIndexSize(morphIndexSize); ValidateIndexSize(rigidBodyIndexSize);
            }

            public int ReadCount(string label)
            {
                int value = Reader.ReadInt32();
                if (value < 0 || value > MaximumElementCount) throw new InvalidDataException("The PMX " + label + " count is invalid: " + value + ".");
                return value;
            }

            public string ReadString(string label)
            {
                int length = ReadCount(label + " byte");
                return Encoding.GetString(ReadBytes(Reader, length, label));
            }

            public Vector3 ReadVector3()
            {
                return new Vector3(Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle());
            }

            public void SkipFloats(int count)
            {
                for (int i = 0; i < count; i++) Reader.ReadSingle();
            }

            public int ReadVertexIndex() { return ReadIndex(vertexIndexSize, false); }
            public int ReadTextureIndex() { return ReadIndex(textureIndexSize, true); }
            public int ReadMaterialIndex() { return ReadIndex(materialIndexSize, true); }
            public int ReadBoneIndex() { return ReadIndex(boneIndexSize, true); }
            public int ReadMorphIndex() { return ReadIndex(morphIndexSize, true); }
            public int ReadRigidBodyIndex() { return ReadIndex(rigidBodyIndexSize, true); }

            private int ReadIndex(int size, bool signed)
            {
                if (size == 1) return signed ? (int)Reader.ReadSByte() : (int)Reader.ReadByte();
                if (size == 2) return signed ? (int)Reader.ReadInt16() : (int)Reader.ReadUInt16();
                if (size == 4) return Reader.ReadInt32();
                throw new InvalidDataException("Unsupported PMX index size " + size + ".");
            }

            private static void ValidateIndexSize(int size)
            {
                if (size != 1 && size != 2 && size != 4) throw new InvalidDataException("The PMX index size is invalid: " + size + ".");
            }
        }
    }
}
