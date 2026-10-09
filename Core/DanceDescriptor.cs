using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

namespace CustomDancePlayer
{
    public sealed class DanceDescriptor
    {
        public string Id, Path, Format, Title, Author;
    }

    public static class OfficialDancePackage
    {
        public static JObject ReadMetadata(string archivePath)
        {
            using (var stream = File.OpenRead(archivePath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var entry = archive.Entries.FirstOrDefault(e => e.FullName.Equals("dance_meta.json", StringComparison.OrdinalIgnoreCase));
                if (entry == null) return null; // Ordinary .me object mods are not dances.
                if (entry.Length > 1024 * 1024) throw new InvalidDataException("Dance metadata is too large.");
                using (var reader = new StreamReader(entry.Open())) return JObject.Parse(reader.ReadToEnd());
            }
        }

        public static string Extract(string archivePath, string cacheRoot)
        {
            string hash;
            using (var sha = SHA256.Create())
            using (var file = File.OpenRead(archivePath)) hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
            var destination = System.IO.Path.Combine(cacheRoot, hash);
            if (File.Exists(System.IO.Path.Combine(destination, ".complete"))) return destination;
            var staging = destination + ".tmp-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(staging);
            try
            {
                using (var stream = File.OpenRead(archivePath))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    if (archive.Entries.Count > 512 || archive.Entries.Sum(e => e.Length) > 1024L * 1024 * 1024)
                        throw new InvalidDataException("Dance package exceeds extraction limits.");
                    string prefix = System.IO.Path.GetFullPath(staging) + System.IO.Path.DirectorySeparatorChar;
                    foreach (var entry in archive.Entries)
                    {
                        string name = entry.FullName.Replace('/', System.IO.Path.DirectorySeparatorChar);
                        string target = System.IO.Path.GetFullPath(System.IO.Path.Combine(staging, name));
                        if (System.IO.Path.IsPathRooted(name) || !target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                            name.Contains(":"))
                            throw new InvalidDataException("Unsafe package entry: " + entry.FullName);
                        if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\")) Directory.CreateDirectory(target);
                        else
                        {
                            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
                            using (var source = entry.Open())
                            using (var output = new FileStream(target, FileMode.CreateNew)) source.CopyTo(output);
                        }
                    }
                }
                File.WriteAllText(System.IO.Path.Combine(staging, ".complete"), hash);
                if (Directory.Exists(destination)) Directory.Delete(destination, true);
                Directory.Move(staging, destination);
                return destination;
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        }
    }
}
