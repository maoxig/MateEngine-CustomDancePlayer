using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Maoxig.RuntimeVmd
{
    public enum VmdNativePhysicsMode
    {
        Off = 0,
        Trace = 1,
        Live = 2
    }

    /// <summary>Thin Unity-2022-compatible binding for the high fidelity MMD runtime ABI.</summary>
    public sealed class NativeMmdSession : IDisposable
    {
        public const uint ExpectedAbiVersion = 3;
        public const uint FeatureSplitPhysicsEvaluation = 1u << 0;
        public const uint FeatureBulletPhysics = 1u << 1;
        private static readonly object LoadLock = new object();
        private static IntPtr loadedModule;
        private static string loadedPath;

        private IntPtr model;
        private IntPtr clip;
        private IntPtr instance;
        private IntPtr physicsWorld;
        private bool disposed;

        public uint AbiVersion { get; private set; }
        public uint FeatureFlags { get; private set; }
        public int BoneCount { get; private set; }
        public int MorphCount { get; private set; }
        public int IkCount { get; private set; }
        public VmdNativePhysicsMode PhysicsMode { get; private set; }
        public int PhysicsRigidbodyCount { get; private set; }
        public int PhysicsDrivenBoneCount { get; private set; }
        public int LastPhysicsSeededRigidbodyCount { get; private set; }
        public int LastPhysicsSubstepCount { get; private set; }
        public int LastPhysicsKinematicRigidbodyCount { get; private set; }
        public int LastPhysicsBonesWritten { get; private set; }
        public float[] WorldMatrices { get; private set; }
        public float[] MorphWeights { get; private set; }
        public byte[] IkEnabled { get; private set; }
        public bool IsPhysicsActive
        {
            get { return physicsWorld != IntPtr.Zero && PhysicsMode != VmdNativePhysicsMode.Off && PhysicsRigidbodyCount > 0; }
        }
        public static string LoadedLibraryPath { get { return loadedPath; } }

        private NativeMmdSession()
        {
        }

        public static string FindDefaultLibraryPath()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(NativeMmdSession).Assembly.Location) ?? string.Empty;
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory ?? string.Empty;
            string[] candidates =
            {
                Path.Combine(assemblyDirectory, "Native", "x86_64", "mmd_runtime_ffi.dll"),
                Path.Combine(assemblyDirectory, "Native", "mmd_runtime_ffi.dll"),
                Path.Combine(assemblyDirectory, "mmd_runtime_ffi.dll"),
                Path.Combine(baseDirectory, "Plugins", "x86_64", "mmd_runtime_ffi.dll"),
                Path.Combine(baseDirectory, "Plugins", "mmd_runtime_ffi.dll"),
                Path.Combine(baseDirectory, "mmd_runtime_ffi.dll"),
                Path.Combine(Environment.CurrentDirectory, "mmd_runtime_ffi.dll")
            };
            for (int i = 0; i < candidates.Length; i++)
                if (File.Exists(candidates[i])) return candidates[i];
            return null;
        }

        public static uint LoadRuntime(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                string fullPath = Path.GetFullPath(path);
                if (!File.Exists(fullPath)) throw new FileNotFoundException("The native MMD runtime was not found.", fullPath);
                lock (LoadLock)
                {
                    if (loadedModule == IntPtr.Zero)
                    {
                        loadedModule = LoadLibraryW(fullPath);
                        if (loadedModule == IntPtr.Zero)
                            throw new InvalidOperationException("LoadLibrary failed for '" + fullPath + "' (Win32 " + Marshal.GetLastWin32Error() + ").");
                        loadedPath = fullPath;
                    }
                }
            }
            uint abi;
            try
            {
                // Unity can resolve an imported native plug-in by DllImport name
                // even when its PackageCache source path is intentionally opaque.
                abi = NativeMethods.AbiVersion();
            }
            catch (DllNotFoundException exception)
            {
                throw new FileNotFoundException(
                    "mmd_runtime_ffi.dll was neither found at a runtime path nor resolved as a Unity native plug-in. " +
                    "Inner error: " + exception.Message,
                    "mmd_runtime_ffi.dll");
            }
            if (abi != ExpectedAbiVersion)
                throw new InvalidOperationException("mmd-runtime ABI " + abi + " is unsupported; expected " + ExpectedAbiVersion + ".");
            return abi;
        }

        public static NativeMmdSession Create(byte[] pmxBytes, byte[] vmdBytes, string nativeLibraryPath)
        {
            return Create(pmxBytes, vmdBytes, nativeLibraryPath, VmdNativePhysicsMode.Off);
        }

        public static NativeMmdSession Create(byte[] pmxBytes, byte[] vmdBytes, string nativeLibraryPath,
            VmdNativePhysicsMode physicsMode)
        {
            if (pmxBytes == null || pmxBytes.Length == 0) throw new ArgumentException("PMX data is required.", "pmxBytes");
            if (vmdBytes == null || vmdBytes.Length == 0) throw new ArgumentException("VMD data is required.", "vmdBytes");
            if (physicsMode < VmdNativePhysicsMode.Off || physicsMode > VmdNativePhysicsMode.Live)
                throw new ArgumentOutOfRangeException("physicsMode");
            NativeMmdSession session = new NativeMmdSession();
            try
            {
                session.AbiVersion = LoadRuntime(nativeLibraryPath);
                session.FeatureFlags = NativeMethods.FeatureFlags();
                session.model = NativeMethods.ModelCreateFromPmxBytes(pmxBytes, new IntPtr(pmxBytes.Length));
                if (session.model == IntPtr.Zero) throw NativeFailure("PMX import returned a null model");
                session.clip = NativeMethods.ClipCreateFromVmdBytesForModel(session.model, vmdBytes, new IntPtr(vmdBytes.Length));
                if (session.clip == IntPtr.Zero) throw NativeFailure("VMD import returned a null clip");
                session.instance = NativeMethods.InstanceCreateForModel(session.model);
                if (session.instance == IntPtr.Zero) throw NativeFailure("MMD instance creation returned null");

                session.BoneCount = CheckedCount(NativeMethods.ModelBoneCount(session.model), "bone");
                session.MorphCount = CheckedCount(NativeMethods.ModelMorphCount(session.model), "morph");
                session.IkCount = CheckedCount(NativeMethods.ModelIkCount(session.model), "IK");
                int worldLength = CheckedCount(NativeMethods.InstanceWorldMatrixF32Len(session.instance), "world matrix float");
                int morphLength = CheckedCount(NativeMethods.InstanceMorphWeightLen(session.instance), "morph weight");
                int ikLength = CheckedCount(NativeMethods.InstanceIkEnabledLen(session.instance), "IK enabled");
                if (worldLength != session.BoneCount * 16)
                    throw new InvalidOperationException("Native world matrix count does not match the PMX bone count.");
                session.WorldMatrices = new float[worldLength];
                session.MorphWeights = new float[morphLength];
                session.IkEnabled = new byte[ikLength];
                session.PhysicsMode = physicsMode;
                if (physicsMode != VmdNativePhysicsMode.Off)
                    session.CreatePhysicsWorld(pmxBytes, physicsMode);
                return session;
            }
            catch
            {
                session.Dispose();
                throw;
            }
        }

        public void Evaluate(float frame)
        {
            ThrowIfDisposed();
            if (float.IsNaN(frame) || float.IsInfinity(frame)) throw new ArgumentOutOfRangeException("frame");
            if (NativeMethods.InstanceEvaluateClipFrame(instance, clip, frame) == 0)
                throw NativeFailure("MMD frame evaluation returned false");
            ClearPhysicsStepDiagnostics();
            CopyOutputs();
        }

        public void EvaluateWithoutIk(float frame)
        {
            ThrowIfDisposed();
            if (float.IsNaN(frame) || float.IsInfinity(frame)) throw new ArgumentOutOfRangeException("frame");
            if (NativeMethods.InstanceEvaluateClipFrameWithoutIk(instance, clip, frame) == 0)
                throw NativeFailure("MMD frame evaluation without IK returned false");
            ClearPhysicsStepDiagnostics();
            CopyOutputs();
        }

        /// <summary>
        /// Evaluates a sequential animation frame through the native PMX Bullet world.
        /// Reset must be true for the first frame, seeks, backwards playback and loop wraps.
        /// </summary>
        public void EvaluateWithPhysics(float frame, float deltaSeconds, bool reset)
        {
            ThrowIfDisposed();
            if (float.IsNaN(frame) || float.IsInfinity(frame)) throw new ArgumentOutOfRangeException("frame");
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0f)
                throw new ArgumentOutOfRangeException("deltaSeconds");
            if (!IsPhysicsActive)
            {
                Evaluate(frame);
                return;
            }

            RequireOk(NativeMethods.InstanceEvaluateClipFrameBeforePhysics(instance, clip, frame),
                "MMD before-physics frame evaluation failed");
            if (reset)
            {
                IntPtr seeded;
                RequireOk(NativeMethods.PhysicsWorldReset(physicsWorld, instance, out seeded),
                    "MMD physics reset failed");
                LastPhysicsSeededRigidbodyCount = CheckedCount(seeded, "seeded rigidbody");
                LastPhysicsSubstepCount = 0;
                LastPhysicsKinematicRigidbodyCount = 0;
                LastPhysicsBonesWritten = 0;
            }
            else
            {
                NativePhysicsWorldStepReport report;
                RequireOk(NativeMethods.PhysicsWorldStepRuntime(physicsWorld, instance, deltaSeconds, out report),
                    "MMD physics step failed");
                LastPhysicsSeededRigidbodyCount = 0;
                LastPhysicsSubstepCount = CheckedUInt(report.Tick.Substeps, "physics substep");
                LastPhysicsKinematicRigidbodyCount = CheckedCount(report.KinematicRigidbodiesFed, "kinematic rigidbody");
                LastPhysicsBonesWritten = CheckedCount(report.BonesWrittenBack, "physics-written bone");
            }
            CopyOutputs();
        }

        private void CreatePhysicsWorld(byte[] pmxBytes, VmdNativePhysicsMode physicsMode)
        {
            uint required = FeatureSplitPhysicsEvaluation | FeatureBulletPhysics;
            if ((FeatureFlags & required) != required)
                throw new NotSupportedException("This mmd-runtime build does not provide native PMX Bullet physics.");
            RequireOk(NativeMethods.InstanceSetPhysicsMode(instance, (uint)physicsMode),
                "MMD physics mode setup failed");
            RequireOk(NativeMethods.PhysicsWorldCreateFromPmxBytes(pmxBytes, new IntPtr(pmxBytes.Length), out physicsWorld),
                "PMX physics world creation failed");
            if (physicsWorld == IntPtr.Zero) throw NativeFailure("PMX physics world creation returned null");

            IntPtr rigidbodyCount;
            RequireOk(NativeMethods.PhysicsWorldRigidbodyCount(physicsWorld, out rigidbodyCount),
                "PMX rigidbody count query failed");
            PhysicsRigidbodyCount = CheckedCount(rigidbodyCount, "rigidbody");
            if (BoneCount > 0)
            {
                byte[] mask = new byte[BoneCount];
                RequireOk(NativeMethods.PhysicsWorldPhysicsDrivenBoneMask(physicsWorld, mask, new IntPtr(mask.Length)),
                    "PMX physics-driven bone mask query failed");
                for (int index = 0; index < mask.Length; index++)
                    if (mask[index] != 0) PhysicsDrivenBoneCount++;
            }
        }

        private void CopyOutputs()
        {
            if (WorldMatrices.Length > 0 && NativeMethods.InstanceCopyWorldMatrices(instance, WorldMatrices, new IntPtr(WorldMatrices.Length)) == 0)
                throw NativeFailure("MMD world matrix copy returned false");
            if (MorphWeights.Length > 0 && NativeMethods.InstanceCopyMorphWeights(instance, MorphWeights, new IntPtr(MorphWeights.Length)) == 0)
                throw NativeFailure("MMD morph weight copy returned false");
            if (IkEnabled.Length > 0 && NativeMethods.InstanceCopyIkEnabled(instance, IkEnabled, new IntPtr(IkEnabled.Length)) == 0)
                throw NativeFailure("MMD IK state copy returned false");
        }

        private void ClearPhysicsStepDiagnostics()
        {
            LastPhysicsSeededRigidbodyCount = 0;
            LastPhysicsSubstepCount = 0;
            LastPhysicsKinematicRigidbodyCount = 0;
            LastPhysicsBonesWritten = 0;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (physicsWorld != IntPtr.Zero) { NativeMethods.PhysicsWorldFree(physicsWorld); physicsWorld = IntPtr.Zero; }
            if (instance != IntPtr.Zero) { NativeMethods.InstanceFree(instance); instance = IntPtr.Zero; }
            if (clip != IntPtr.Zero) { NativeMethods.ClipFree(clip); clip = IntPtr.Zero; }
            if (model != IntPtr.Zero) { NativeMethods.ModelFree(model); model = IntPtr.Zero; }
            WorldMatrices = null;
            MorphWeights = null;
            IkEnabled = null;
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException("NativeMmdSession");
        }

        private static int CheckedCount(IntPtr value, string label)
        {
            long count = value.ToInt64();
            if (count < 0 || count > int.MaxValue) throw new InvalidOperationException("Invalid native " + label + " count " + count + ".");
            return (int)count;
        }

        private static int CheckedUInt(uint value, string label)
        {
            if (value > int.MaxValue) throw new InvalidOperationException("Invalid native " + label + " count " + value + ".");
            return (int)value;
        }

        private static void RequireOk(int status, string message)
        {
            if (status != 0) throw NativeFailure(message + " (status " + status + ")");
        }

        private static InvalidOperationException NativeFailure(string message)
        {
            IntPtr pointer = NativeMethods.LastErrorMessage();
            string detail = pointer == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(pointer);
            return new InvalidOperationException(message + (string.IsNullOrEmpty(detail) ? "." : ": " + detail));
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryW(string path);

        private static class NativeMethods
        {
            private const string Library = "mmd_runtime_ffi.dll";

            [DllImport(Library, EntryPoint = "mmd_runtime_abi_version", CallingConvention = CallingConvention.Cdecl)] public static extern uint AbiVersion();
            [DllImport(Library, EntryPoint = "mmd_runtime_feature_flags", CallingConvention = CallingConvention.Cdecl)] public static extern uint FeatureFlags();
            [DllImport(Library, EntryPoint = "mmd_runtime_last_error_message", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr LastErrorMessage();
            [DllImport(Library, EntryPoint = "mmd_runtime_model_create_from_pmx_bytes", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr ModelCreateFromPmxBytes(byte[] data, IntPtr length);
            [DllImport(Library, EntryPoint = "mmd_runtime_model_bone_count", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr ModelBoneCount(IntPtr model);
            [DllImport(Library, EntryPoint = "mmd_runtime_model_morph_count", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr ModelMorphCount(IntPtr model);
            [DllImport(Library, EntryPoint = "mmd_runtime_model_ik_count", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr ModelIkCount(IntPtr model);
            [DllImport(Library, EntryPoint = "mmd_runtime_model_free", CallingConvention = CallingConvention.Cdecl)] public static extern void ModelFree(IntPtr model);
            [DllImport(Library, EntryPoint = "mmd_runtime_clip_create_from_vmd_bytes_for_model", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr ClipCreateFromVmdBytesForModel(IntPtr model, byte[] data, IntPtr length);
            [DllImport(Library, EntryPoint = "mmd_runtime_clip_free", CallingConvention = CallingConvention.Cdecl)] public static extern void ClipFree(IntPtr clip);
            [DllImport(Library, EntryPoint = "mmd_runtime_instance_create_for_model", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr InstanceCreateForModel(IntPtr model);
            [DllImport(Library, EntryPoint = "mmd_runtime_instance_free", CallingConvention = CallingConvention.Cdecl)] public static extern void InstanceFree(IntPtr instance);
            [DllImport(Library, EntryPoint = "mmd_runtime_instance_evaluate_clip_frame", CallingConvention = CallingConvention.Cdecl)] public static extern byte InstanceEvaluateClipFrame(IntPtr instance, IntPtr clip, float frame);
            [DllImport(Library, EntryPoint = "mmd_runtime_instance_evaluate_clip_frame_without_ik", CallingConvention = CallingConvention.Cdecl)] public static extern byte InstanceEvaluateClipFrameWithoutIk(IntPtr instance, IntPtr clip, float frame);
            [DllImport(Library, EntryPoint = "mmd_runtime_instance_set_physics_mode", CallingConvention = CallingConvention.Cdecl)] public static extern int InstanceSetPhysicsMode(IntPtr instance, uint mode);
            [DllImport(Library, EntryPoint = "mmd_runtime_instance_evaluate_clip_frame_before_physics", CallingConvention = CallingConvention.Cdecl)] public static extern int InstanceEvaluateClipFrameBeforePhysics(IntPtr instance, IntPtr clip, float frame);
            [DllImport(Library, EntryPoint = "mmd_runtime_physics_world_create_from_pmx_bytes", CallingConvention = CallingConvention.Cdecl)] public static extern int PhysicsWorldCreateFromPmxBytes(byte[] data, IntPtr length, out IntPtr world);
            [DllImport(Library, EntryPoint = "mmd_runtime_physics_world_free", CallingConvention = CallingConvention.Cdecl)] public static extern void PhysicsWorldFree(IntPtr world);
            [DllImport(Library, EntryPoint = "mmd_runtime_physics_world_reset", CallingConvention = CallingConvention.Cdecl)] public static extern int PhysicsWorldReset(IntPtr world, IntPtr instance, out IntPtr seededRigidbodyCount);
            [DllImport(Library, EntryPoint = "mmd_runtime_physics_world_step_runtime", CallingConvention = CallingConvention.Cdecl)] public static extern int PhysicsWorldStepRuntime(IntPtr world, IntPtr instance, float deltaSeconds, out NativePhysicsWorldStepReport report);
            [DllImport(Library, EntryPoint = "mmd_runtime_physics_world_rigidbody_count", CallingConvention = CallingConvention.Cdecl)] public static extern int PhysicsWorldRigidbodyCount(IntPtr world, out IntPtr rigidbodyCount);
            [DllImport(Library, EntryPoint = "mmd_runtime_physics_world_physics_driven_bone_mask", CallingConvention = CallingConvention.Cdecl)] public static extern int PhysicsWorldPhysicsDrivenBoneMask(IntPtr world, [Out] byte[] mask, IntPtr boneCount);
            [DllImport(Library, EntryPoint = "mmd_runtime_instance_world_matrix_f32_len", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr InstanceWorldMatrixF32Len(IntPtr instance);
            [DllImport(Library, EntryPoint = "mmd_runtime_instance_copy_world_matrices", CallingConvention = CallingConvention.Cdecl)] public static extern byte InstanceCopyWorldMatrices(IntPtr instance, [Out] float[] output, IntPtr length);
            [DllImport(Library, EntryPoint = "mmd_runtime_instance_morph_weight_len", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr InstanceMorphWeightLen(IntPtr instance);
            [DllImport(Library, EntryPoint = "mmd_runtime_instance_copy_morph_weights", CallingConvention = CallingConvention.Cdecl)] public static extern byte InstanceCopyMorphWeights(IntPtr instance, [Out] float[] output, IntPtr length);
            [DllImport(Library, EntryPoint = "mmd_runtime_instance_ik_enabled_len", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr InstanceIkEnabledLen(IntPtr instance);
            [DllImport(Library, EntryPoint = "mmd_runtime_instance_copy_ik_enabled", CallingConvention = CallingConvention.Cdecl)] public static extern byte InstanceCopyIkEnabled(IntPtr instance, [Out] byte[] output, IntPtr length);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePhysicsStepStats
        {
            public float InputDeltaSeconds;
            public float ClampedDeltaSeconds;
            public uint Substeps;
            public float AccumulatorSeconds;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePhysicsWorldStepReport
        {
            public NativePhysicsStepStats Tick;
            public IntPtr KinematicRigidbodiesFed;
            public IntPtr BonesWrittenBack;
        }
    }
}
