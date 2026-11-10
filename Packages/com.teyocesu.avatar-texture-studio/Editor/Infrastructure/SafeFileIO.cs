using System;
using System.IO;

namespace AvatarTextureStudio.Infrastructure
{
    /// <summary>Same-directory writes: failure must not delete the previous painting or texture.</summary>
    internal static class SafeFileIO
    {
        public static void Write(string path, Action<Stream> write, bool overwrite)
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    write(stream);
                    stream.Flush(true);
                }
                if (overwrite && File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
                else File.Move(temporary, fullPath);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public static string ExportAssetPath(string path, string projectRoot)
        {
            string assets = Path.GetFullPath(Path.Combine(projectRoot, "Assets")) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, path));
            var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!fullPath.StartsWith(assets, comparison) || !string.Equals(Path.GetExtension(fullPath), ".png", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Export must be a PNG inside this project's Assets folder.");
            if (!Directory.Exists(Path.GetDirectoryName(fullPath)))
                throw new DirectoryNotFoundException("Choose an existing folder inside Assets for the export.");
            string current = assets.TrimEnd(Path.DirectorySeparatorChar);
            foreach (string segment in fullPath.Substring(assets.Length).Split(Path.DirectorySeparatorChar))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Export through symbolic links or junctions is not supported. Choose an ordinary Assets folder.");
                current = Path.Combine(current, segment);
            }
            if (File.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Export cannot replace a symbolic link. Choose a new PNG path.");
            return "Assets/" + fullPath.Substring(assets.Length).Replace('\\', '/');
        }
    }
}
