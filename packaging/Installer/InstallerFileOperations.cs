using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace YiZhiMao.Dc.Installer
{
    internal sealed class ResourceMergeResult
    {
        public int AddedCount { get; set; }

        public int ModifiedRestoredCount { get; set; }

        public int ConflictCount { get; set; }

        public int PreservedCount => AddedCount + ModifiedRestoredCount;
    }

    internal sealed class ResourceExportResult
    {
        public int PreservedCount { get; set; }
    }

    internal static class InstallerFileOperations
    {
        private const string ModulesRelativePath = "Contents/Modules";

        public static ResourceMergeResult MergeUserResources(
            string oldBundlePath,
            string newBundlePath)
        {
            var result = new ResourceMergeResult();
            Dictionary<string, string> oldHashes = LoadPackagedHashes(oldBundlePath);
            string oldModules = GetSafePath(oldBundlePath, ModulesRelativePath);
            if (!Directory.Exists(oldModules) || IsReparsePoint(oldModules))
            {
                return result;
            }

            foreach (string sourcePath in EnumerateFilesWithoutReparsePoints(oldModules))
            {
                string relativePath = GetRelativePath(oldBundlePath, sourcePath);
                bool wasPackaged;
                bool wasModified;
                if (!IsUserResource(sourcePath, relativePath, oldHashes, out wasPackaged, out wasModified))
                {
                    continue;
                }

                string targetPath = GetSafePath(newBundlePath, relativePath);
                if (File.Exists(targetPath))
                {
                    if (!HashesMatch(sourcePath, targetPath))
                    {
                        result.ConflictCount++;
                    }

                    continue;
                }

                CopyFile(sourcePath, targetPath);
                if (wasPackaged && wasModified)
                {
                    result.ModifiedRestoredCount++;
                }
                else
                {
                    result.AddedCount++;
                }
            }

            return result;
        }

        public static ResourceExportResult ExportUserResources(
            string bundlePath,
            string destinationRoot)
        {
            var result = new ResourceExportResult();
            Dictionary<string, string> hashes = LoadPackagedHashes(bundlePath);
            string modulesPath = GetSafePath(bundlePath, ModulesRelativePath);
            if (!Directory.Exists(modulesPath) || IsReparsePoint(modulesPath))
            {
                return result;
            }

            foreach (string sourcePath in EnumerateFilesWithoutReparsePoints(modulesPath))
            {
                string relativePath = GetRelativePath(bundlePath, sourcePath);
                bool wasPackaged;
                bool wasModified;
                if (!IsUserResource(sourcePath, relativePath, hashes, out wasPackaged, out wasModified))
                {
                    continue;
                }

                string targetPath = GetSafePath(destinationRoot, relativePath);
                CopyFile(sourcePath, targetPath);
                result.PreservedCount++;
            }

            return result;
        }

        private static bool IsUserResource(
            string filePath,
            string relativePath,
            Dictionary<string, string> packagedHashes,
            out bool wasPackaged,
            out bool wasModified)
        {
            wasPackaged = packagedHashes.ContainsKey(relativePath);
            string expectedHash = wasPackaged ? packagedHashes[relativePath] : string.Empty;
            wasModified = wasPackaged &&
                          !string.Equals(
                              ComputeSha256(filePath),
                              expectedHash,
                              StringComparison.OrdinalIgnoreCase);
            return !wasPackaged || wasModified;
        }

        private static Dictionary<string, string> LoadPackagedHashes(string bundlePath)
        {
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string hashPath = Path.Combine(bundlePath, "bundle-hashes.sha256");
            if (!File.Exists(hashPath))
            {
                return hashes;
            }

            foreach (string line in File.ReadAllLines(hashPath))
            {
                int separator = line.IndexOf(" *", StringComparison.Ordinal);
                if (separator != 64)
                {
                    hashes.Clear();
                    return hashes;
                }

                string relativePath = NormalizeRelativePath(line.Substring(separator + 2));
                if (string.IsNullOrEmpty(relativePath) ||
                    relativePath.StartsWith("../", StringComparison.Ordinal) ||
                    Path.IsPathRooted(relativePath))
                {
                    hashes.Clear();
                    return hashes;
                }

                hashes[relativePath] = line.Substring(0, 64);
            }

            return hashes;
        }

        private static IEnumerable<string> EnumerateFilesWithoutReparsePoints(string rootPath)
        {
            var pending = new Stack<string>();
            pending.Push(Path.GetFullPath(rootPath));
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                foreach (string filePath in Directory.GetFiles(current))
                {
                    if ((File.GetAttributes(filePath) & FileAttributes.ReparsePoint) == 0)
                    {
                        yield return filePath;
                    }
                }

                foreach (string directoryPath in Directory.GetDirectories(current))
                {
                    if ((File.GetAttributes(directoryPath) & FileAttributes.ReparsePoint) == 0)
                    {
                        pending.Push(directoryPath);
                    }
                }
            }
        }

        private static bool IsReparsePoint(string path)
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }

        private static string GetRelativePath(string rootPath, string filePath)
        {
            string rootPrefix = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar) +
                                Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(filePath);
            if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("资源文件不在Bundle目录内：" + filePath);
            }

            return NormalizeRelativePath(fullPath.Substring(rootPrefix.Length));
        }

        private static string GetSafePath(string rootPath, string relativePath)
        {
            string fullRoot = Path.GetFullPath(rootPath);
            string rootPrefix = fullRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string normalizedRelativePath = NormalizeRelativePath(relativePath)
                .Replace('/', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(fullRoot, normalizedRelativePath));
            if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("资源路径超出目标目录：" + relativePath);
            }

            return fullPath;
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            return relativePath.Replace('\\', '/').TrimStart('/');
        }

        private static void CopyFile(string sourcePath, string targetPath)
        {
            var parent = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.Copy(sourcePath, targetPath, false);
        }

        private static bool HashesMatch(string firstPath, string secondPath)
        {
            return string.Equals(
                ComputeSha256(firstPath),
                ComputeSha256(secondPath),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
