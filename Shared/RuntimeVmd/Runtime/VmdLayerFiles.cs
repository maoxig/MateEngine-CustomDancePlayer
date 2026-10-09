using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Maoxig.RuntimeVmd
{
    /// <summary>Shared file naming rules for optional VMD motion layers.</summary>
    public static class VmdLayerFiles
    {
        private const long MaximumManifestBytes = 1024 * 1024;

        private static readonly string[] OverlaySuffixes =
        {
            ".camera", "_camera", "-camera", ".cam", "_cam", "-cam", "_カメラ", "-カメラ",
            ".face", "_face", "-face", ".facial", "_facial", "-facial", "_表情", "-表情",
            ".lip", "_lip", "-lip", ".morph", "_morph", "-morph", "_リップ", "-リップ"
        };

        public static bool TryGetPrimaryPath(string path, out string primaryPath)
        {
            primaryPath = null;
            if (string.IsNullOrEmpty(path) ||
                !Path.GetExtension(path).Equals(".vmd", StringComparison.OrdinalIgnoreCase)) return false;
            string stem = Path.GetFileNameWithoutExtension(path);
            for (int index = 0; index < OverlaySuffixes.Length; index++)
            {
                string suffix = OverlaySuffixes[index];
                if (!stem.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) || stem.Length <= suffix.Length) continue;
                string candidate = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty,
                    stem.Substring(0, stem.Length - suffix.Length) + ".vmd");
                primaryPath = FindExistingPathIgnoringFileNameCase(candidate) ?? candidate;
                return true;
            }
            return false;
        }

        public static bool IsOverlaySidecar(string path)
        {
            string primaryPath;
            return TryGetPrimaryPath(path, out primaryPath) && File.Exists(primaryPath);
        }

        public static string[] FindAutomaticOverlays(string primaryPath)
        {
            if (string.IsNullOrWhiteSpace(primaryPath)) throw new ArgumentException("A primary VMD path is required.", "primaryPath");
            string fullPrimary = Path.GetFullPath(primaryPath);
            string directory = Path.GetDirectoryName(fullPrimary) ?? string.Empty;
            if (!Directory.Exists(directory)) return new string[0];

            string[] candidates = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
            Array.Sort(candidates, StringComparer.OrdinalIgnoreCase);
            List<string> result = new List<string>();
            for (int index = 0; index < candidates.Length; index++)
            {
                if (!Path.GetExtension(candidates[index]).Equals(".vmd", StringComparison.OrdinalIgnoreCase)) continue;
                string candidatePrimary;
                if (TryGetPrimaryPath(candidates[index], out candidatePrimary) &&
                    string.Equals(Path.GetFullPath(candidatePrimary), fullPrimary, StringComparison.OrdinalIgnoreCase))
                    result.Add(Path.GetFullPath(candidates[index]));
            }
            return result.ToArray();
        }

        /// <summary>
        /// Resolves either an explicit .layers.json/.vmd.json manifest or the
        /// conventional sidecars. The presence of a manifest disables automatic
        /// discovery even when the manifest is empty or malformed.
        /// </summary>
        public static string[] ResolveOverlays(string primaryPath, Action<string> warning = null)
        {
            if (string.IsNullOrWhiteSpace(primaryPath)) throw new ArgumentException("A primary VMD path is required.", "primaryPath");
            string fullPrimary = Path.GetFullPath(primaryPath);
            string manifestPath;
            if (TryGetManifestPath(fullPrimary, out manifestPath))
                return ReadManifestOverlays(fullPrimary, manifestPath, warning);
            try { return FindAutomaticOverlays(fullPrimary); }
            catch (Exception exception)
            {
                Warn(warning, "Could not discover VMD sidecars for '" + fullPrimary + "': " + exception.Message);
                return new string[0];
            }
        }

        /// <summary>
        /// Returns existing VMD files referenced as overlays by manifests in the
        /// supplied catalog. Manifest owners are kept as primary entries even if a
        /// malformed cyclic manifest also references them.
        /// </summary>
        public static string[] FindExplicitManifestOverlays(IEnumerable<string> vmdPaths, Action<string> warning = null)
        {
            if (vmdPaths == null) throw new ArgumentNullException("vmdPaths");
            List<string> primaries = new List<string>();
            HashSet<string> seenPrimaries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> manifestOwners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in vmdPaths)
            {
                if (string.IsNullOrWhiteSpace(path) ||
                    !Path.GetExtension(path).Equals(".vmd", StringComparison.OrdinalIgnoreCase)) continue;
                string fullPath = Path.GetFullPath(path);
                if (!seenPrimaries.Add(fullPath)) continue;
                primaries.Add(fullPath);
                string manifestPath;
                if (TryGetManifestPath(fullPath, out manifestPath)) manifestOwners.Add(fullPath);
            }

            HashSet<string> overlays = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < primaries.Count; index++)
            {
                string primary = primaries[index];
                string manifestPath;
                if (!TryGetManifestPath(primary, out manifestPath)) continue;
                string[] resolved = ReadManifestOverlays(primary, manifestPath, warning);
                for (int overlayIndex = 0; overlayIndex < resolved.Length; overlayIndex++) overlays.Add(resolved[overlayIndex]);
            }
            foreach (string owner in manifestOwners) overlays.Remove(owner);
            string[] result = new string[overlays.Count];
            overlays.CopyTo(result);
            Array.Sort(result, StringComparer.OrdinalIgnoreCase);
            return result;
        }

        public static bool TryGetManifestPath(string primaryPath, out string manifestPath)
        {
            manifestPath = null;
            if (string.IsNullOrWhiteSpace(primaryPath)) return false;
            string fullPrimary = Path.GetFullPath(primaryPath);
            string basePath = Path.Combine(Path.GetDirectoryName(fullPrimary) ?? string.Empty,
                Path.GetFileNameWithoutExtension(fullPrimary));
            string[] candidates = { fullPrimary + ".json", basePath + ".layers.json" };
            for (int index = 0; index < candidates.Length; index++)
            {
                string existing = FindExistingPathIgnoringFileNameCase(candidates[index]);
                if (existing == null) continue;
                manifestPath = existing;
                return true;
            }
            return false;
        }

		private static string FindExistingPathIgnoringFileNameCase(string candidate)
		{
			string fullCandidate = Path.GetFullPath(candidate);
			if (File.Exists(fullCandidate)) return fullCandidate;
			string directory = Path.GetDirectoryName(fullCandidate);
			if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return null;
			string expectedName = Path.GetFileName(fullCandidate);
			string[] siblings = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
			for (int index = 0; index < siblings.Length; index++)
				if (string.Equals(Path.GetFileName(siblings[index]), expectedName, StringComparison.OrdinalIgnoreCase))
					return Path.GetFullPath(siblings[index]);
			return null;
		}

        private static string[] ReadManifestOverlays(string fullPrimary, string manifestPath, Action<string> warning)
        {
            try
            {
                FileInfo info = new FileInfo(manifestPath);
                if (info.Length > MaximumManifestBytes)
                    throw new InvalidDataException("The manifest exceeds the 1 MiB safety limit.");
                Dictionary<string, object> json;
                using (FileStream stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false, true), true))
                {
                    json = RuntimeVmdJson.ParseObject(reader.ReadToEnd());
                }
                VmdLayerManifest manifest = new VmdLayerManifest
                {
                    AdditionalVmdFiles = RuntimeVmdJson.GetStringArray(json, "additionalVmdFiles"),
                    FaceVmd = RuntimeVmdJson.GetString(json, "faceVmd"),
                    LipVmd = RuntimeVmdJson.GetString(json, "lipVmd"),
                    CameraVmd = RuntimeVmdJson.GetString(json, "cameraVmd")
                };

                string directory = Path.GetDirectoryName(fullPrimary) ?? string.Empty;
                List<string> result = new List<string>();
                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { fullPrimary };
                Action<string> add = delegate(string value)
                {
                    if (string.IsNullOrWhiteSpace(value)) return;
                    string candidate = Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(directory, value));
                    if (!Path.GetExtension(candidate).Equals(".vmd", StringComparison.OrdinalIgnoreCase))
                    {
                        Warn(warning, "Ignoring non-VMD layer in '" + manifestPath + "': " + candidate);
                        return;
                    }
                    if (!seen.Add(candidate)) return;
                    if (File.Exists(candidate)) result.Add(candidate);
                    else Warn(warning, "Additional VMD listed for '" + fullPrimary + "' was not found: " + candidate);
                };
                if (manifest.AdditionalVmdFiles != null)
                    for (int index = 0; index < manifest.AdditionalVmdFiles.Length; index++) add(manifest.AdditionalVmdFiles[index]);
                add(manifest.FaceVmd);
                add(manifest.LipVmd);
                add(manifest.CameraVmd);
                return result.ToArray();
            }
            catch (Exception exception)
            {
                Warn(warning, "Could not read VMD layer manifest '" + manifestPath + "': " + exception.Message);
                return new string[0];
            }
        }

        private static void Warn(Action<string> warning, string message)
        {
            if (warning != null) warning(message);
        }

        private sealed class VmdLayerManifest
        {
            public string[] AdditionalVmdFiles = null;

            public string FaceVmd = null;

            public string LipVmd = null;

            public string CameraVmd = null;
        }
    }
}
