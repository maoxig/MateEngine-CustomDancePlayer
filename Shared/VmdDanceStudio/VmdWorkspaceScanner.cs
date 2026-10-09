using System;
using System.Collections.Generic;
using System.IO;

namespace Maoxig.VmdDanceStudio
{
    public static class VmdWorkspaceScanner
    {
        private static readonly HashSet<string> AudioExtensions = new HashSet<string>(
            new[] { ".ogg", ".wav", ".mp3" }, StringComparer.OrdinalIgnoreCase);

        public static List<VmdWorkspaceGroup> Scan(string root, Action<int, int, string> progress = null)
        {
            string fullRoot = Path.GetFullPath(root);
            if (!Directory.Exists(fullRoot)) throw new DirectoryNotFoundException(fullRoot);
            string[] files = Directory.GetFiles(fullRoot, "*", SearchOption.AllDirectories);
            List<string> vmdFiles = new List<string>();
            foreach (string path in files)
                if (string.Equals(Path.GetExtension(path), ".vmd", StringComparison.OrdinalIgnoreCase)) vmdFiles.Add(path);
            vmdFiles.Sort(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, VmdWorkspaceGroup> groups = new Dictionary<string, VmdWorkspaceGroup>(
                StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < vmdFiles.Count; index++)
            {
                string path = vmdFiles[index];
                string directory = Path.GetDirectoryName(path) ?? fullRoot;
                VmdWorkspaceGroup group = GetOrCreate(groups, directory);
                group.Tracks.Add(VmdInspector.Inspect(path));
                if (progress != null) progress(index + 1, vmdFiles.Count, path);
            }
            foreach (string path in files)
            {
                string extension = Path.GetExtension(path);
                string directory = Path.GetDirectoryName(path) ?? fullRoot;
                if(!AudioExtensions.Contains(extension) && !string.Equals(extension,".pmx",StringComparison.OrdinalIgnoreCase))continue;
                VmdWorkspaceGroup group=GetOrCreate(groups,directory);
                if (AudioExtensions.Contains(extension)) group.AudioFiles.Add(path);
                else if (string.Equals(extension, ".pmx", StringComparison.OrdinalIgnoreCase)) group.PmxFiles.Add(path);
            }
            foreach (VmdWorkspaceGroup group in groups.Values)
            {
                group.Tracks.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase));
                group.AudioFiles.Sort(StringComparer.OrdinalIgnoreCase);
                group.PmxFiles.Sort(StringComparer.OrdinalIgnoreCase);
            }
            List<VmdWorkspaceGroup> result = new List<VmdWorkspaceGroup>(groups.Values);
            result.Sort((left, right) => string.Compare(left.DirectoryPath, right.DirectoryPath,
                StringComparison.OrdinalIgnoreCase));
            return result;
        }

        public static VmdDanceDraft SuggestFolder(string root)
        {
            var groups=Scan(root);
            var bodyGroups=groups.FindAll(g=>g.Tracks.Exists(t=>t.IsValid && t.BoneKeys>0));
            if(bodyGroups.Count==0)throw new InvalidDataException("No body motion was found in this folder.");
            bodyGroups.Sort((a,b)=>a.DirectoryPath.Length.CompareTo(b.DirectoryPath.Length));
            var primary=bodyGroups[0];
            var merged=new VmdWorkspaceGroup{DirectoryPath=primary.DirectoryPath};
            merged.Tracks.AddRange(primary.Tracks);merged.AudioFiles.AddRange(primary.AudioFiles);merged.PmxFiles.AddRange(primary.PmxFiles);
            foreach(var group in groups) {
                if(group==primary || group.HasMotion)continue;
                // Associate auxiliary-only folders with their nearest body
                // group, so a multi-dance collection never blends body tracks.
                VmdWorkspaceGroup owner=null;
                foreach(var candidate in bodyGroups)if(IsWithin(group.DirectoryPath,candidate.DirectoryPath) && (owner==null || candidate.DirectoryPath.Length>owner.DirectoryPath.Length))owner=candidate;
                if(owner==primary || bodyGroups.Count==1) {
                    merged.Tracks.AddRange(group.Tracks);merged.AudioFiles.AddRange(group.AudioFiles);merged.PmxFiles.AddRange(group.PmxFiles);
                }
            }
            return Suggest(merged);
        }
        private static bool IsWithin(string path,string root) {
            return string.Equals(path,root,StringComparison.OrdinalIgnoreCase)||path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
        }

        public static VmdDanceDraft Suggest(VmdWorkspaceGroup group)
        {
            if (group == null) throw new ArgumentNullException("group");
            List<VmdTrackInfo> valid = new List<VmdTrackInfo>();
            VmdTrackInfo motion = null;
            VmdTrackInfo camera = null;
            VmdTrackInfo lip = null;
            foreach (VmdTrackInfo track in group.Tracks)
            {
                if (!track.IsValid) continue;
                valid.Add(track);
                if (track.BoneKeys > 0 && (motion == null || track.FkCoverageScore > motion.FkCoverageScore ||
                    (track.FkCoverageScore == motion.FkCoverageScore && (track.BoneKeys > motion.BoneKeys ||
                    (track.BoneKeys == motion.BoneKeys && track.MaximumFrame > motion.MaximumFrame))))) motion = track;
                if (track.CameraKeys > 0 && (camera == null || track.CameraKeys > camera.CameraKeys)) camera = track;
                if (track.MorphKeys > 0 && track.BoneKeys == 0 && track.CameraKeys == 0 &&
                    LooksLike(track.Path, "lip", "リップ", "口型", "嘴型") &&
                    (lip == null || track.MorphKeys > lip.MorphKeys)) lip = track;
            }
            if (camera == motion) camera = null;
            if (motion != null && motion.CameraKeys > 0) camera = null;
            VmdTrackInfo face = null;
            foreach (VmdTrackInfo track in valid)
                if (track != lip && track.MorphKeys > 0 && track.BoneKeys == 0 && track.CameraKeys == 0 &&
                    (face == null || track.MorphKeys > face.MorphKeys)) face = track;
            if (motion != null && motion.MorphKeys > 0) { face = null; lip = null; }
            List<string> used = new List<string>();
            AddIfNotEmpty(used, motion == null ? null : motion.Path);
            AddIfNotEmpty(used, camera == null ? null : camera.Path);
            AddIfNotEmpty(used, lip == null ? null : lip.Path);
            AddIfNotEmpty(used, face == null ? null : face.Path);
            List<string> additional = new List<string>();
            foreach (VmdTrackInfo track in valid)
                if (!ContainsPath(used, track.Path) && track.BoneKeys == 0 && track.MorphKeys == 0 && track.CameraKeys == 0 && (track.LightKeys > 0 || track.ShadowKeys > 0))
                    additional.Add(track.Path);

            return new VmdDanceDraft
            {
                Id = Slugify(group.Name),
                Title = group.Name,
                MotionVmd = motion == null ? null : motion.Path,
                CameraVmd = camera == null ? null : camera.Path,
                FaceVmd = face == null ? null : face.Path,
                LipVmd = lip == null ? null : lip.Path,
                AdditionalVmdFiles = additional,
                AudioFile = SelectAudio(group.AudioFiles, group.Name),
                ReferencePmx = group.PmxFiles.Count == 1 ? group.PmxFiles[0] : null,
                PositionScale = 0.08f
            };
        }

        public static string Slugify(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "dance";
            char[] invalid = Path.GetInvalidFileNameChars();
            char[] characters = value.Trim().ToCharArray();
            for (int index = 0; index < characters.Length; index++)
                if (Array.IndexOf(invalid, characters[index]) >= 0 || char.IsControl(characters[index])) characters[index] = '-';
            string cleaned = new string(characters);
            while (cleaned.Contains("--")) cleaned = cleaned.Replace("--", "-");
            return cleaned.Trim(' ', '.', '-');
        }

        private static string SelectAudio(List<string> files, string groupName)
        {
            string selected = null;
            foreach (string path in files)
                if (selected == null || CompareAudio(path, selected, groupName) < 0) selected = path;
            return selected;
        }

        private static int CompareAudio(string left, string right, string groupName)
        {
            int leftEdited = LooksLike(left, "已编辑", "edited", "trimmed", "compressed") ? 0 : 1;
            int rightEdited = LooksLike(right, "已编辑", "edited", "trimmed", "compressed") ? 0 : 1;
            int comparison = leftEdited.CompareTo(rightEdited);
            if (comparison != 0) return comparison;
            int leftNamed = Path.GetFileNameWithoutExtension(left).IndexOf(groupName,
                StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1;
            int rightNamed = Path.GetFileNameWithoutExtension(right).IndexOf(groupName,
                StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1;
            comparison = leftNamed.CompareTo(rightNamed);
            if (comparison != 0) return comparison;
            comparison = new FileInfo(left).Length.CompareTo(new FileInfo(right).Length);
            return comparison != 0 ? comparison : string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLike(string value, params string[] tokens)
        {
            for (int index = 0; index < tokens.Length; index++)
                if (value.IndexOf(tokens[index], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static VmdWorkspaceGroup GetOrCreate(Dictionary<string, VmdWorkspaceGroup> groups, string directory)
        {
            VmdWorkspaceGroup result;
            if (!groups.TryGetValue(directory, out result))
            {
                result = new VmdWorkspaceGroup { DirectoryPath = directory };
                groups[directory] = result;
            }
            return result;
        }

        private static void AddIfNotEmpty(List<string> target, string value)
        {
            if (!string.IsNullOrEmpty(value)) target.Add(value);
        }

        private static bool ContainsPath(List<string> paths, string candidate)
        {
            for (int index = 0; index < paths.Count; index++)
                if (string.Equals(paths[index], candidate, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
