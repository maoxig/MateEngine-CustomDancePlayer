using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using Maoxig.RuntimeVmd;
using Newtonsoft.Json.Linq;

namespace Maoxig.VmdDanceStudio
{
    public static class VmdDancePackageBuilder
    {
        public static List<string> Validate(VmdDanceDraft draft)
        {
            List<string> errors = new List<string>();
            if (draft == null) return new List<string> { "Package draft is null." };
            if (string.IsNullOrWhiteSpace(draft.Id)) errors.Add("Package id is required.");
            RequireFile(errors, draft.MotionVmd, ".vmd", "Body motion");
            OptionalFile(errors, draft.FaceVmd, new[] { ".vmd" }, "Face VMD");
            OptionalFile(errors, draft.LipVmd, new[] { ".vmd" }, "Lip VMD");
            OptionalFile(errors, draft.CameraVmd, new[] { ".vmd" }, "Camera VMD");
            OptionalFile(errors, draft.AudioFile, new[] { ".ogg", ".wav", ".mp3" }, "Audio");
            OptionalFile(errors, draft.ReferencePmx, new[] { ".pmx" }, "Reference PMX");
            if (draft.AdditionalVmdFiles != null)
                foreach (string path in draft.AdditionalVmdFiles) OptionalFile(errors, path, new[] { ".vmd" }, "Additional VMD");
            if (float.IsNaN(draft.AudioOffsetSeconds) || float.IsInfinity(draft.AudioOffsetSeconds)
                || Math.Abs(draft.AudioOffsetSeconds) > 3600f) errors.Add("Audio offset must be finite and within one hour.");
            if (float.IsNaN(draft.PositionScale) || float.IsInfinity(draft.PositionScale)
                || draft.PositionScale <= 0f || draft.PositionScale > 100f) errors.Add("Position scale must be greater than zero and at most 100.");
            if(draft.CameraReferenceEyeHeight.HasValue && (float.IsNaN(draft.CameraReferenceEyeHeight.Value)||float.IsInfinity(draft.CameraReferenceEyeHeight.Value)||draft.CameraReferenceEyeHeight.Value<=0||draft.CameraReferenceEyeHeight.Value>10f))errors.Add("cameraReferenceEyeHeight must be finite and positive within its supported range.");
            if(draft.CameraReferenceBodyHeight.HasValue && (float.IsNaN(draft.CameraReferenceBodyHeight.Value)||float.IsInfinity(draft.CameraReferenceBodyHeight.Value)||draft.CameraReferenceBodyHeight.Value<=0||draft.CameraReferenceBodyHeight.Value>10f))errors.Add("cameraReferenceBodyHeight must be finite and positive within its supported range.");
            if(draft.CameraAuthoringScale.HasValue && (float.IsNaN(draft.CameraAuthoringScale.Value)||float.IsInfinity(draft.CameraAuthoringScale.Value)||draft.CameraAuthoringScale.Value<=0||draft.CameraAuthoringScale.Value>100f))errors.Add("cameraAuthoringScale must be finite and positive within its supported range.");
            InspectTrack(errors, draft.MotionVmd, "Body motion", track => track.BoneKeys > 0);
            InspectTrack(errors, draft.FaceVmd, "Face VMD", track => track.MorphKeys > 0);
            InspectTrack(errors, draft.LipVmd, "Lip VMD", track => track.MorphKeys > 0);
            InspectTrack(errors, draft.CameraVmd, "Camera VMD", track => track.CameraKeys > 0);
            if (draft.AdditionalVmdFiles != null) foreach (var path in draft.AdditionalVmdFiles)
                InspectTrack(errors, path, "Additional VMD", track => track.BoneKeys + track.MorphKeys + track.CameraKeys + track.LightKeys + track.ShadowKeys + track.IkFrames > 0);
            return errors;
        }

        private static void InspectTrack(List<string> errors, string path, string label, Func<VmdTrackInfo, bool> expected)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !Path.GetExtension(path).Equals(".vmd", StringComparison.OrdinalIgnoreCase)) return;
            var track = VmdInspector.Inspect(path);
            if (!track.IsValid) errors.Add(label + ": invalid VMD - " + track.Error);
            else if (!expected(track)) errors.Add(label + ": file has no compatible track.");
        }

        public static VmdDanceBuildResult Build(VmdDanceDraft draft, string outputPath)
        {
            List<string> errors = Validate(draft);
            if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
            string fullOutput = Path.GetFullPath(outputPath);
            if (!string.Equals(Path.GetExtension(fullOutput), ".vmdance", StringComparison.OrdinalIgnoreCase))
                fullOutput += ".vmdance";
            string parent = Path.GetDirectoryName(fullOutput);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            string temporary = fullOutput + ".tmp-" + Guid.NewGuid().ToString("N");
            VmdDanceBuildResult result = new VmdDanceBuildResult { OutputPath = fullOutput };
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                // Keep packages authorable inside older Unity/Mono hosts such as
                // MateEngine, whose ZipArchive only exposes the two-argument ctor.
                using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    Dictionary<string, string> entryBySource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    string motion = AddFile(archive, draft.MotionVmd, "motion.vmd", result, entryBySource);
                    string face = PreserveSlot(draft,draft.FaceVmd,draft.OriginalFaceVmd)?AddOptional(archive,draft.FaceVmd,"face.vmd",result,entryBySource):AddScopedOptional(archive, draft.FaceVmd, "face.vmd", false, result, entryBySource);
                    string lip = PreserveSlot(draft,draft.LipVmd,draft.OriginalLipVmd)?AddOptional(archive,draft.LipVmd,"lip.vmd",result,entryBySource):AddScopedOptional(archive, draft.LipVmd, "lip.vmd", false, result, entryBySource);
                    string camera = PreserveSlot(draft,draft.CameraVmd,draft.OriginalCameraVmd)?AddOptional(archive,draft.CameraVmd,"camera.vmd",result,entryBySource):AddScopedOptional(archive, draft.CameraVmd, "camera.vmd", true, result, entryBySource);
                    List<string> overlays = new List<string>();
                    if (draft.AdditionalVmdFiles != null)
                    {
                        int overlayIndex = 1;
                        foreach (string path in draft.AdditionalVmdFiles)
                        {
                            if (string.IsNullOrWhiteSpace(path)) continue;
                            string source = Path.GetFullPath(path);
                            string existing;
                            if (entryBySource.TryGetValue(source, out existing))
                            {
                                result.Warnings.Add("Skipped duplicate VMD layer: " + source);
                                continue;
                            }
                            overlays.Add(AddFile(archive, source, "overlay-" + overlayIndex.ToString("00") + ".vmd",
                                result, entryBySource));
                            overlayIndex++;
                        }
                    }
                    string audio = AddOptional(archive, draft.AudioFile,
                        string.IsNullOrEmpty(draft.AudioFile) ? null : "audio" + Path.GetExtension(draft.AudioFile).ToLowerInvariant(),
                        result, entryBySource);
                    string pmx = AddOptional(archive, draft.ReferencePmx, "reference.pmx", result, entryBySource);
                    string manifest = BuildManifest(draft, motion, face, lip, camera, overlays, audio, pmx);
                    if(!string.IsNullOrEmpty(draft.OriginalManifestJson))
                    {
                        var original=JObject.Parse(draft.OriginalManifestJson);var updated=JObject.Parse(manifest);
                        foreach(string key in new[]{"formatVersion","id","title","author","credits","motionVmd","additionalVmdFiles","faceVmd","lipVmd","cameraVmd","audioFile","referencePmx","audioOffsetSeconds","positionScale","footIk","loop","cameraReferenceEyeHeight","cameraReferenceBodyHeight","cameraAuthoringScale"})original.Remove(key);
                        foreach(var property in updated.Properties())original[property.Name]=property.Value;
                        manifest=original.ToString();
                    }
                    PreserveExtraFiles(archive,draft,result);
                    ZipArchiveEntry manifestEntry = archive.CreateEntry("dance.json", CompressionLevel.Optimal);
                    using (StreamWriter writer = new StreamWriter(manifestEntry.Open(), new UTF8Encoding(false))) writer.Write(manifest);
                    result.Entries.Insert(0, "dance.json");
                }
                if(File.Exists(fullOutput))File.Replace(temporary,fullOutput,fullOutput+".bak-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));
                else File.Move(temporary, fullOutput);
            }
            catch
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
                throw;
            }
            return result;
        }

        private static string AddScopedOptional(ZipArchive archive,string source,string entryName,bool camera,
            VmdDanceBuildResult result,Dictionary<string,string> entryBySource)
        {
            if(string.IsNullOrWhiteSpace(source))return null;
            string fullSource=Path.GetFullPath(source),existing;
            // The main motion is copied verbatim with ALL its sections. Reusing
            // that file in an optional slot must not add it as an overlay.
            if(entryBySource.TryGetValue(fullSource,out existing))return existing;
            string key=(camera?"camera|":"morph|")+fullSource;
            if(entryBySource.TryGetValue(key,out existing))return existing;
            VmdMotion motion=VmdReader.Read(fullSource);
            motion.BoneFrames.Clear();motion.IkFrames.Clear();
            if(camera)motion.MorphFrames.Clear();
            else { motion.CameraFrames.Clear();motion.LightFrames.Clear();motion.SelfShadowFrames.Clear(); }
            byte[] bytes=VmdWriter.Write(motion);
            ZipArchiveEntry entry=archive.CreateEntry(entryName,CompressionLevel.Optimal);
            using(Stream output=entry.Open())output.Write(bytes,0,bytes.Length);
            entryBySource[key]=entryName;result.Entries.Add(entryName);
            return entryName;
        }

        private static bool PreserveSlot(VmdDanceDraft draft,string value,string original) => draft.PreserveSlotTracks&&string.Equals(value,original,StringComparison.OrdinalIgnoreCase);

        private static void PreserveExtraFiles(ZipArchive archive,VmdDanceDraft draft,VmdDanceBuildResult result)
        {
            if(string.IsNullOrEmpty(draft.OriginalPackageRoot)||!Directory.Exists(draft.OriginalPackageRoot))return;
            string root=Path.GetFullPath(draft.OriginalPackageRoot).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
            var names=new HashSet<string>(result.Entries,StringComparer.OrdinalIgnoreCase){"dance.json"};
            foreach(string file in Directory.GetFiles(root,"*",SearchOption.AllDirectories))
            {
                string full=Path.GetFullPath(file);if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Original package asset escapes its directory.");
                string relative=full.Substring(root.Length).Replace('\\','/');if(!names.Add(relative))continue;
                var entry=archive.CreateEntry(relative,CompressionLevel.Optimal);
                using(var input=File.OpenRead(full))using(var output=entry.Open())input.CopyTo(output);
                result.Entries.Add(relative);
            }
        }

        private static string AddOptional(ZipArchive archive, string source, string entryName,
            VmdDanceBuildResult result, Dictionary<string, string> entryBySource)
        {
            return string.IsNullOrWhiteSpace(source) ? null : AddFile(archive, source, entryName, result, entryBySource);
        }

        private static string AddFile(ZipArchive archive, string source, string entryName,
            VmdDanceBuildResult result, Dictionary<string, string> entryBySource)
        {
            string fullSource = Path.GetFullPath(source);
            string existing;
            if (entryBySource.TryGetValue(fullSource, out existing)) return existing;
            ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using (Stream input = new FileStream(fullSource, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (Stream output = entry.Open()) input.CopyTo(output);
            entryBySource[fullSource] = entryName;
            result.Entries.Add(entryName);
            return entryName;
        }

        private static string BuildManifest(VmdDanceDraft draft, string motion, string face, string lip,
            string camera, List<string> overlays, string audio, string pmx)
        {
            StringBuilder json = new StringBuilder();
            json.AppendLine("{");
            Append(json, "formatVersion", "1", true);
            Append(json, "id", Quote(draft.Id.Trim()), true);
            Append(json, "title", Quote(string.IsNullOrWhiteSpace(draft.Title) ? draft.Id.Trim() : draft.Title.Trim()), true);
            if (!string.IsNullOrWhiteSpace(draft.Author)) Append(json, "author", Quote(draft.Author.Trim()), true);
            if (!string.IsNullOrWhiteSpace(draft.Credits)) Append(json, "credits", Quote(draft.Credits), true);
            Append(json, "motionVmd", Quote(motion), true);
            if (overlays.Count > 0)
            {
                string[] quotedOverlays = new string[overlays.Count];
                for (int index = 0; index < overlays.Count; index++) quotedOverlays[index] = Quote(overlays[index]);
                Append(json, "additionalVmdFiles", "[" + string.Join(", ", quotedOverlays) + "]", true);
            }
            if (!string.IsNullOrEmpty(face)) Append(json, "faceVmd", Quote(face), true);
            if (!string.IsNullOrEmpty(lip)) Append(json, "lipVmd", Quote(lip), true);
            if (!string.IsNullOrEmpty(camera)) Append(json, "cameraVmd", Quote(camera), true);
            if (!string.IsNullOrEmpty(audio)) Append(json, "audioFile", Quote(audio), true);
            if (!string.IsNullOrEmpty(pmx)) Append(json, "referencePmx", Quote(pmx), true);
            Append(json, "audioOffsetSeconds", draft.AudioOffsetSeconds.ToString("G9", CultureInfo.InvariantCulture), true);
            if(draft.CameraReferenceEyeHeight.HasValue)Append(json,"cameraReferenceEyeHeight",draft.CameraReferenceEyeHeight.Value.ToString("G9",CultureInfo.InvariantCulture),true);
            if(draft.CameraReferenceBodyHeight.HasValue)Append(json,"cameraReferenceBodyHeight",draft.CameraReferenceBodyHeight.Value.ToString("G9",CultureInfo.InvariantCulture),true);
            if(draft.CameraAuthoringScale.HasValue)Append(json,"cameraAuthoringScale",draft.CameraAuthoringScale.Value.ToString("G9",CultureInfo.InvariantCulture),true);
            Append(json, "positionScale", draft.PositionScale.ToString("G9", CultureInfo.InvariantCulture), true);
            if (draft.FootIk.HasValue) Append(json, "footIk", draft.FootIk.Value ? "true" : "false", true);
            Append(json, "loop", draft.Loop ? "true" : "false", false);
            json.AppendLine("}");
            return json.ToString();
        }

        private static void Append(StringBuilder json, string name, string value, bool comma)
        {
            json.Append("  ").Append(Quote(name)).Append(": ").Append(value);
            if (comma) json.Append(',');
            json.AppendLine();
        }

        private static string Quote(string value)
        {
            if (value == null) return "null";
            StringBuilder escaped = new StringBuilder(value.Length + 2).Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"': escaped.Append("\\\""); break;
                    case '\\': escaped.Append("\\\\"); break;
                    case '\b': escaped.Append("\\b"); break;
                    case '\f': escaped.Append("\\f"); break;
                    case '\n': escaped.Append("\\n"); break;
                    case '\r': escaped.Append("\\r"); break;
                    case '\t': escaped.Append("\\t"); break;
                    default:
                        if (character < 32) escaped.Append("\\u").Append(((int)character).ToString("x4"));
                        else escaped.Append(character);
                        break;
                }
            }
            return escaped.Append('"').ToString();
        }

        private static void RequireFile(List<string> errors, string path, string extension, string label)
        {
            if (string.IsNullOrWhiteSpace(path)) { errors.Add(label + " is required."); return; }
            OptionalFile(errors, path, new[] { extension }, label);
        }

        private static void OptionalFile(List<string> errors, string path, string[] extensions, string label)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (!File.Exists(path)) { errors.Add(label + " was not found: " + path); return; }
            string extension = Path.GetExtension(path);
            bool supported = false;
            for (int index = 0; index < extensions.Length; index++)
                if (string.Equals(extension, extensions[index], StringComparison.OrdinalIgnoreCase)) supported = true;
            if (!supported) errors.Add(label + " uses an unsupported extension: " + path);
        }
    }
}
