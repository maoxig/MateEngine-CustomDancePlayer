using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Maoxig.RuntimeVmd
{
    /// <summary>A validated, materialized VMD dance package.</summary>
    public sealed class VmdDancePackageDescriptor
    {
        public int FormatVersion { get; internal set; }
        public string Id { get; internal set; }
        public string Title { get; internal set; }
        public string Author { get; internal set; }
        public string Credits { get; internal set; }
        public string SourcePath { get; internal set; }
        public string PackageRoot { get; internal set; }
        public string ManifestPath { get; internal set; }
        public string PrimaryVmdPath { get; internal set; }
        public string[] OverlayVmdPaths { get; internal set; }
        public string[] AdditionalVmdPaths { get; internal set; }
        public string FaceVmdPath { get; internal set; }
        public string LipVmdPath { get; internal set; }
        public string CameraVmdPath { get; internal set; }
        public string AudioPath { get; internal set; }
        public string ReferencePmxPath { get; internal set; }
        public float AudioOffsetSeconds { get; internal set; }
        public float? CameraReferenceEyeHeight { get; internal set; }
        public float? CameraReferenceBodyHeight { get; internal set; }
        public float? CameraAuthoringScale { get; internal set; }
        public float? PositionScale { get; internal set; }
        public bool? Loop { get; internal set; }
        public bool? FootIk { get; internal set; }
        public bool IsArchive { get; internal set; }
        public string ContentHash { get; internal set; }
    }

    /// <summary>
    /// Opens editable directory packages and safely materializes portable
    /// .vmdance ZIP packages. Package-relative paths are always contained by
    /// the directory that owns dance.json.
    /// </summary>
    public static class VmdDancePackage
    {
        public const string ManifestFileName = "dance.json";
        public const string ArchiveExtension = ".vmdance";
        public const int CurrentFormatVersion = 1;
        public const int MaximumArchiveEntries = 256;
        public const long MaximumSingleFileBytes = 512L * 1024L * 1024L;
        public const long MaximumExpandedBytes = 1024L * 1024L * 1024L;
        private const long MaximumManifestBytes = 1024L * 1024L;

        public static bool TryOpenDirectory(string directory, out VmdDancePackageDescriptor package, out string error)
        {
            package = null;
            error = null;
            try
            {
                if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A package directory is required.");
                string root = Path.GetFullPath(directory);
                if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Package directory was not found: " + root);
                string manifestPath = FindManifestInDirectory(root);
                if (manifestPath == null)
                    throw new FileNotFoundException("Package manifest was not found.", Path.Combine(root, ManifestFileName));
                package = ReadManifest(manifestPath, root, root, false, null);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static bool TryOpenArchive(string archivePath, string cacheRoot,
            out VmdDancePackageDescriptor package, out string error)
        {
            package = null;
            error = null;
            string temporaryDirectory = null;
            try
            {
                if (string.IsNullOrWhiteSpace(archivePath)) throw new ArgumentException("A .vmdance archive path is required.");
                string source = Path.GetFullPath(archivePath);
                if (!File.Exists(source)) throw new FileNotFoundException("Dance archive was not found.", source);
                if (!Path.GetExtension(source).Equals(ArchiveExtension, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Dance archives must use the .vmdance extension.");
                if (string.IsNullOrWhiteSpace(cacheRoot)) throw new ArgumentException("A package cache directory is required.");

                string cache = Path.GetFullPath(cacheRoot);
                Directory.CreateDirectory(cache);
                string hash;
                if (!TryReadSourceHash(cache, source, out hash))
                {
                    hash = ComputeSha256(source);
                    TryWriteSourceHash(cache, source, hash);
                }
                string cacheName = MakeSafeFileName(Path.GetFileNameWithoutExtension(source)) + "-" + hash.Substring(0, 16);
                string destination = EnsureContainedPath(cache, Path.Combine(cache, cacheName));
                string marker = Path.Combine(destination, ".vmdance.sha256");

                if (Directory.Exists(destination) && File.Exists(marker) &&
                    string.Equals(File.ReadAllText(marker).Trim(), hash, StringComparison.OrdinalIgnoreCase))
                {
                    string cachedManifest = FindSingleManifest(destination);
                    package = ReadManifest(cachedManifest, Path.GetDirectoryName(cachedManifest), source, true, hash);
                    return true;
                }

                temporaryDirectory = EnsureContainedPath(cache,
                    destination + ".tmp-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temporaryDirectory);
                ExtractSafely(source, temporaryDirectory);
                string temporaryManifest = FindSingleManifest(temporaryDirectory);
                // Validate all manifest references before publishing the cache.
                ReadManifest(temporaryManifest, Path.GetDirectoryName(temporaryManifest), source, true, hash);
                File.WriteAllText(Path.Combine(temporaryDirectory, ".vmdance.sha256"), hash);

                if (Directory.Exists(destination)) Directory.Delete(destination, true);
                Directory.Move(temporaryDirectory, destination);
                temporaryDirectory = null;
                string manifest = FindSingleManifest(destination);
                package = ReadManifest(manifest, Path.GetDirectoryName(manifest), source, true, hash);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                if (!string.IsNullOrEmpty(temporaryDirectory) && Directory.Exists(temporaryDirectory))
                {
                    try { Directory.Delete(temporaryDirectory, true); }
                    catch { }
                }
            }
        }

        public static VmdDancePackageDescriptor[] Discover(string rootDirectory, string cacheRoot, Action<string> warning = null)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("A discovery root is required.", "rootDirectory");
            string root = Path.GetFullPath(rootDirectory);
            if (!Directory.Exists(root)) return new VmdDancePackageDescriptor[0];
            List<VmdDancePackageDescriptor> result = new List<VmdDancePackageDescriptor>();
            HashSet<string> seenSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string[] manifests = FindManifestFiles(root);
            Array.Sort(manifests, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < manifests.Length; index++)
            {
                VmdDancePackageDescriptor package;
                string error;
                string directory = Path.GetDirectoryName(manifests[index]);
                if (TryOpenDirectory(directory, out package, out error))
                {
                    if (seenSources.Add(package.SourcePath)) result.Add(package);
                }
                else Warn(warning, "Could not open VMD dance package directory '" + directory + "': " + error);
            }

            // Filter the actual extension instead of relying on filesystem glob
            // semantics. This recognizes .vmdance case-insensitively on Windows
            // and Unix and cannot accidentally accept names such as
            // "dance.vmdance.backup".
            string[] allFiles = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            List<string> archiveFiles = new List<string>();
            for (int index = 0; index < allFiles.Length; index++)
            {
                if (string.Equals(Path.GetExtension(allFiles[index]), ArchiveExtension,
                    StringComparison.OrdinalIgnoreCase)) archiveFiles.Add(allFiles[index]);
            }
            string[] archives = archiveFiles.ToArray();
            Array.Sort(archives, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < archives.Length; index++)
            {
                VmdDancePackageDescriptor package;
                string error;
                if (TryOpenArchive(archives[index], cacheRoot, out package, out error))
                {
                    if (seenSources.Add(package.SourcePath)) result.Add(package);
                }
                else Warn(warning, "Could not open VMD dance archive '" + archives[index] + "': " + error);
            }
            return result.ToArray();
        }

        public static bool IsPackageManifest(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                string.Equals(Path.GetFileName(path), ManifestFileName, StringComparison.OrdinalIgnoreCase);
        }

        private static VmdDancePackageDescriptor ReadManifest(string manifestPath, string rootDirectory,
            string sourcePath, bool isArchive, string contentHash)
        {
            string manifest = Path.GetFullPath(manifestPath);
            string root = Path.GetFullPath(rootDirectory);
            FileInfo manifestInfo = new FileInfo(manifest);
            if (!manifestInfo.Exists) throw new FileNotFoundException("Package manifest was not found.", manifest);
            if (manifestInfo.Length > MaximumManifestBytes)
                throw new InvalidDataException("The package manifest exceeds the 1 MiB safety limit.");

            Dictionary<string, object> json;
            using (FileStream stream = new FileStream(manifest, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false, true), true))
            {
                json = RuntimeVmdJson.ParseObject(reader.ReadToEnd());
            }
            DanceManifest data = new DanceManifest
            {
                FormatVersion = RuntimeVmdJson.GetInt32(json, "formatVersion", 0),
                Id = RuntimeVmdJson.GetString(json, "id"),
                Title = RuntimeVmdJson.GetString(json, "title"),
                Author = RuntimeVmdJson.GetString(json, "author"),
                Credits = RuntimeVmdJson.GetString(json, "credits"),
                MotionVmd = RuntimeVmdJson.GetString(json, "motionVmd"),
                AdditionalVmdFiles = RuntimeVmdJson.GetStringArray(json, "additionalVmdFiles"),
                FaceVmd = RuntimeVmdJson.GetString(json, "faceVmd"),
                LipVmd = RuntimeVmdJson.GetString(json, "lipVmd"),
                CameraVmd = RuntimeVmdJson.GetString(json, "cameraVmd"),
                AudioFile = RuntimeVmdJson.GetString(json, "audioFile"),
                ReferencePmx = RuntimeVmdJson.GetString(json, "referencePmx"),
                CameraReferenceEyeHeight = RuntimeVmdJson.Contains(json,"cameraReferenceEyeHeight") ? (float?)RuntimeVmdJson.GetSingle(json,"cameraReferenceEyeHeight",0f) : null,
                CameraReferenceBodyHeight = RuntimeVmdJson.Contains(json,"cameraReferenceBodyHeight") ? (float?)RuntimeVmdJson.GetSingle(json,"cameraReferenceBodyHeight",0f) : null,
                CameraAuthoringScale = RuntimeVmdJson.Contains(json,"cameraAuthoringScale") ? (float?)RuntimeVmdJson.GetSingle(json,"cameraAuthoringScale",0f) : null,
                AudioOffsetSeconds = RuntimeVmdJson.GetSingle(json, "audioOffsetSeconds", 0f),
                PositionScale = RuntimeVmdJson.Contains(json, "positionScale")
                    ? (float?)RuntimeVmdJson.GetSingle(json, "positionScale", 0f)
                    : null,
                Loop = RuntimeVmdJson.Contains(json, "loop")
                    ? (bool?)RuntimeVmdJson.GetBoolean(json, "loop", false)
                    : null,
                FootIk = RuntimeVmdJson.Contains(json, "footIk")
                    ? (bool?)RuntimeVmdJson.GetBoolean(json, "footIk", true)
                    : null
            };
            int version = data.FormatVersion == 0 ? CurrentFormatVersion : data.FormatVersion;
            if (version != CurrentFormatVersion)
                throw new InvalidDataException("Unsupported VMD dance package formatVersion " + version + ".");

            string primary = ResolveRequiredFile(root, data.MotionVmd, ".vmd", "motionVmd");
            List<string> overlays = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { primary };
            Action<string, string> addOverlay = delegate(string value, string field)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                string resolved = ResolveRequiredFile(root, value, ".vmd", field);
                if (seen.Add(resolved)) overlays.Add(resolved);
            };
            if (data.AdditionalVmdFiles != null)
                for (int index = 0; index < data.AdditionalVmdFiles.Length; index++)
                    addOverlay(data.AdditionalVmdFiles[index], "additionalVmdFiles");
            addOverlay(data.FaceVmd, "faceVmd");
            addOverlay(data.LipVmd, "lipVmd");
            addOverlay(data.CameraVmd, "cameraVmd");
            var additional=new List<string>();
            if(data.AdditionalVmdFiles!=null)foreach(var value in data.AdditionalVmdFiles)
                additional.Add(ResolveRequiredFile(root,value,".vmd","additionalVmdFiles"));

            string audio = ResolveOptionalFile(root, data.AudioFile,
                new[] { ".ogg", ".wav", ".mp3" }, "audioFile");
            string pmx = ResolveOptionalFile(root, data.ReferencePmx,
                new[] { ".pmx" }, "referencePmx");
            if (float.IsNaN(data.AudioOffsetSeconds) || float.IsInfinity(data.AudioOffsetSeconds) ||
                Math.Abs(data.AudioOffsetSeconds) > 3600f)
                throw new InvalidDataException("audioOffsetSeconds must be finite and within one hour.");
            if (data.PositionScale.HasValue &&
                (float.IsNaN(data.PositionScale.Value) || float.IsInfinity(data.PositionScale.Value) ||
                 data.PositionScale.Value <= 0f || data.PositionScale.Value > 100f))
                throw new InvalidDataException("positionScale must be finite, greater than zero, and at most 100.");

            ValidateCameraValue(data.CameraReferenceEyeHeight,"cameraReferenceEyeHeight",10f);
            ValidateCameraValue(data.CameraReferenceBodyHeight,"cameraReferenceBodyHeight",10f);
            ValidateCameraValue(data.CameraAuthoringScale,"cameraAuthoringScale",100f);
            string fallbackId = isArchive
                ? Path.GetFileNameWithoutExtension(sourcePath)
                : new DirectoryInfo(root).Name;
            string id = string.IsNullOrWhiteSpace(data.Id) ? fallbackId : data.Id.Trim();
            if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("The dance package id is empty.");
            return new VmdDancePackageDescriptor
            {
                FormatVersion = version,
                Id = id,
                Title = string.IsNullOrWhiteSpace(data.Title) ? id : data.Title.Trim(),
                Author = string.IsNullOrWhiteSpace(data.Author) ? null : data.Author.Trim(),
                Credits = data.Credits,
                SourcePath = Path.GetFullPath(sourcePath),
                PackageRoot = root,
                ManifestPath = manifest,
                PrimaryVmdPath = primary,
                OverlayVmdPaths = overlays.ToArray(),
                AdditionalVmdPaths = additional.ToArray(),
                FaceVmdPath = ResolveOptionalFile(root,data.FaceVmd,new[]{".vmd"},"faceVmd"),
                LipVmdPath = ResolveOptionalFile(root,data.LipVmd,new[]{".vmd"},"lipVmd"),
                CameraVmdPath = ResolveOptionalFile(root,data.CameraVmd,new[]{".vmd"},"cameraVmd"),
                AudioPath = audio,
                ReferencePmxPath = pmx,
                AudioOffsetSeconds = data.AudioOffsetSeconds,
                CameraReferenceEyeHeight = data.CameraReferenceEyeHeight,
                CameraReferenceBodyHeight = data.CameraReferenceBodyHeight,
                CameraAuthoringScale = data.CameraAuthoringScale,
                PositionScale = data.PositionScale,
                Loop = data.Loop,
                FootIk = data.FootIk,
                IsArchive = isArchive,
                ContentHash = contentHash
            };
        }

        private static void ValidateCameraValue(float? value,string name,float maximum)
        {
            if(value.HasValue && (float.IsNaN(value.Value)||float.IsInfinity(value.Value)||value.Value<=0||value.Value>maximum))
                throw new InvalidDataException(name+" must be finite, positive and at most "+maximum+".");
        }

        private static string ResolveRequiredFile(string root, string value, string extension, string field)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException(field + " is required.");
            string result = ResolveContainedReference(root, value, field);
            if (!Path.GetExtension(result).Equals(extension, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(field + " must reference a " + extension + " file.");
            if (!File.Exists(result)) throw new FileNotFoundException(field + " was not found.", result);
            return result;
        }

        private static string ResolveOptionalFile(string root, string value, string[] extensions, string field)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string result = ResolveContainedReference(root, value, field);
            bool valid = false;
            for (int index = 0; index < extensions.Length; index++)
                if (Path.GetExtension(result).Equals(extensions[index], StringComparison.OrdinalIgnoreCase)) valid = true;
            if (!valid) throw new InvalidDataException(field + " uses an unsupported extension.");
            if (!File.Exists(result)) throw new FileNotFoundException(field + " was not found.", result);
            return result;
        }

        private static string ResolveContainedReference(string root, string value, string field)
        {
            if (Path.IsPathRooted(value)) throw new InvalidDataException(field + " must be package-relative.");
            if (value.IndexOf(':') >= 0) throw new InvalidDataException(field + " contains an invalid path.");
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.Combine(fullRoot, value));
            string prefix = fullRoot + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(field + " escapes the dance package directory.");
            return candidate;
        }

        private static void ExtractSafely(string archivePath, string destination)
        {
            string fullDestination = Path.GetFullPath(destination)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string prefix = fullDestination + Path.DirectorySeparatorChar;
            long totalBytes = 0L;
            int entryCount = 0;
            using (FileStream stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            // MateEngine ships an older System.IO.Compression implementation
            // without the leaveOpen/entryEncoding constructor overloads.
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    entryCount++;
                    if (entryCount > MaximumArchiveEntries)
                        throw new InvalidDataException("The dance archive contains too many entries.");
                    string name = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (Path.IsPathRooted(name) || name.IndexOf(':') >= 0)
                        throw new InvalidDataException("The dance archive contains an unsafe path: " + entry.FullName);
                    string target = Path.GetFullPath(Path.Combine(fullDestination, name));
                    if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The dance archive contains a path traversal entry: " + entry.FullName);

                    bool directoryEntry = entry.FullName.EndsWith("/", StringComparison.Ordinal) ||
                        entry.FullName.EndsWith("\\", StringComparison.Ordinal);
                    if (directoryEntry)
                    {
                        Directory.CreateDirectory(target);
                        continue;
                    }
                    if (entry.Length < 0L || entry.Length > MaximumSingleFileBytes)
                        throw new InvalidDataException("A dance archive entry exceeds the 512 MiB safety limit: " + entry.FullName);
                    checked { totalBytes += entry.Length; }
                    if (totalBytes > MaximumExpandedBytes)
                        throw new InvalidDataException("The expanded dance archive exceeds the 1 GiB safety limit.");
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (Stream input = entry.Open())
                    using (FileStream output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        input.CopyTo(output);
                }
            }
        }

        private static string FindSingleManifest(string root)
        {
            string[] manifests = FindManifestFiles(root);
            if (manifests.Length == 0) throw new InvalidDataException("The dance archive does not contain dance.json.");
            if (manifests.Length != 1) throw new InvalidDataException("The dance archive must contain exactly one dance.json.");
            return Path.GetFullPath(manifests[0]);
        }

        private static string FindManifestInDirectory(string directory)
        {
            string[] files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
            for (int index = 0; index < files.Length; index++)
                if (IsPackageManifest(files[index])) return Path.GetFullPath(files[index]);
            return null;
        }

        private static string[] FindManifestFiles(string root)
        {
            string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            List<string> manifests = new List<string>();
            for (int index = 0; index < files.Length; index++)
                if (IsPackageManifest(files[index])) manifests.Add(Path.GetFullPath(files[index]));
            return manifests.ToArray();
        }

        private static string EnsureContainedPath(string root, string path)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidate = Path.GetFullPath(path);
            if (!candidate.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The package cache path escapes its root.");
            return candidate;
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        // Hashing large .vmdance archives on every startup stalls library discovery.
        // Keep the verified content hash while the source identity is unchanged.
        // A replaced or edited archive changes its length or UTC write timestamp and
        // is hashed again before its extracted cache can be reused.
        private static bool TryReadSourceHash(string cacheRoot, string sourcePath, out string contentHash)
        {
            contentHash = null;
            try
            {
                FileInfo source = new FileInfo(sourcePath);
                string indexPath = GetSourceHashIndexPath(cacheRoot, source.FullName);
                if (!File.Exists(indexPath)) return false;
                string[] lines = File.ReadAllLines(indexPath);
                if (lines.Length != 5 || lines[0] != "vmdance-source-hash-v1") return false;
                string indexedPath = Encoding.UTF8.GetString(Convert.FromBase64String(lines[1]));
                long indexedLength;
                long indexedWriteTicks;
                if (!long.TryParse(lines[2], out indexedLength) ||
                    !long.TryParse(lines[3], out indexedWriteTicks)) return false;
                string indexedHash = lines[4].Trim().ToLowerInvariant();
                if (!string.Equals(indexedPath, source.FullName, StringComparison.OrdinalIgnoreCase) ||
                    indexedLength != source.Length ||
                    indexedWriteTicks != source.LastWriteTimeUtc.Ticks ||
                    !IsSha256(indexedHash)) return false;
                contentHash = indexedHash;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void TryWriteSourceHash(string cacheRoot, string sourcePath, string contentHash)
        {
            try
            {
                FileInfo source = new FileInfo(sourcePath);
                string indexPath = GetSourceHashIndexPath(cacheRoot, source.FullName);
                File.WriteAllLines(indexPath, new[]
                {
                    "vmdance-source-hash-v1",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(source.FullName)),
                    source.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    source.LastWriteTimeUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    contentHash
                });
            }
            catch
            {
                // The extracted package cache remains valid even when the small
                // acceleration index cannot be written.
            }
        }

        private static string GetSourceHashIndexPath(string cacheRoot, string sourcePath)
        {
            byte[] pathBytes = Encoding.UTF8.GetBytes(Path.GetFullPath(sourcePath).ToUpperInvariant());
            string pathHash;
            using (SHA256 sha = SHA256.Create())
                pathHash = BitConverter.ToString(sha.ComputeHash(pathBytes)).Replace("-", string.Empty).ToLowerInvariant();
            return EnsureContainedPath(cacheRoot, Path.Combine(cacheRoot, ".source-" + pathHash + ".sha256"));
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!((character >= '0' && character <= '9') ||
                    (character >= 'a' && character <= 'f'))) return false;
            }
            return true;
        }

        private static string MakeSafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "dance";
            HashSet<char> invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
            char[] chars = value.ToCharArray();
            for (int index = 0; index < chars.Length; index++) if (invalid.Contains(chars[index])) chars[index] = '_';
            string result = new string(chars).Trim().TrimEnd('.');
            return string.IsNullOrWhiteSpace(result) ? "dance" : result;
        }

        private static void Warn(Action<string> warning, string message)
        {
            if (warning != null) warning(message);
        }

        private sealed class DanceManifest
        {
            public int FormatVersion;
            public string Id;
            public string Title;
            public string Author;
            public string Credits;
            public string MotionVmd;
            public string[] AdditionalVmdFiles;
            public string FaceVmd;
            public string LipVmd;
            public string CameraVmd;
            public string AudioFile;
            public string ReferencePmx;
            public float AudioOffsetSeconds;
            public float? CameraReferenceEyeHeight;
            public float? CameraReferenceBodyHeight;
            public float? CameraAuthoringScale;
            public float? PositionScale;
            public bool? Loop;
            public bool? FootIk;
        }
    }
}
