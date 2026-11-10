using System;
using System.IO;
using System.Linq;
using AvatarTextureStudio.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class Program
{
    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("FILETEST PASS " + message);
    }

    static void Reject<T>(Action action, string message) where T : Exception
    {
        try { action(); }
        catch (T) { Console.WriteLine("FILETEST PASS " + message); return; }
        throw new Exception("Expected " + typeof(T).Name + ": " + message);
    }

    static int Main(string[] args)
    {
        string scratch = Path.Combine(Path.GetTempPath(), "ats-file-safety-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(scratch);
            string original = Path.Combine(scratch, "painting.mtpaint");
            File.WriteAllText(original, "original");
            Reject<IOException>(() => SafeFileIO.Write(original, s => s.WriteByte(1), false), "unapproved replacement is rejected");
            Require(File.ReadAllText(original) == "original", "refused write preserves previous bytes");
            Reject<InvalidOperationException>(() => SafeFileIO.Write(original, s => {
                s.WriteByte(2); throw new InvalidOperationException("injected encoder/write failure");
            }, true), "partial writer failure propagates");
            Require(File.ReadAllText(original) == "original", "failed writer preserves previous painting");
            Require(!Directory.EnumerateFiles(scratch, "*.tmp").Any(), "failed writes leave no temporary output");
            SafeFileIO.Write(original, s => s.WriteByte(42), true);
            Require(File.ReadAllBytes(original).SequenceEqual(new byte[] { 42 }), "approved atomic replacement writes new bytes");
            string fresh = Path.Combine(scratch, "new.png");
            SafeFileIO.Write(fresh, s => s.WriteByte(7), false);
            Require(File.ReadAllBytes(fresh).SequenceEqual(new byte[] { 7 }), "new destination is created without overwrite permission");

            Directory.CreateDirectory(Path.Combine(scratch, "Assets", "Textures"));
            Require(SafeFileIO.ExportAssetPath("Assets/Textures/new.PNG", scratch) == "Assets/Textures/new.PNG", "PNG export normalizes inside Assets");
            Reject<InvalidOperationException>(() => SafeFileIO.ExportAssetPath("Assets/../../original.png", scratch), "parent traversal is rejected");
            Reject<InvalidOperationException>(() => SafeFileIO.ExportAssetPath("AssetsSibling/original.png", scratch), "sibling-prefix escape is rejected");
            Reject<InvalidOperationException>(() => SafeFileIO.ExportAssetPath("Assets/Textures/original.mat", scratch), "non-PNG destination is rejected");
            Reject<DirectoryNotFoundException>(() => SafeFileIO.ExportAssetPath("Assets/missing/new.png", scratch), "missing export folder is rejected");
            string outside = Path.Combine(scratch, "Outside");
            Directory.CreateDirectory(outside);
            Directory.CreateSymbolicLink(Path.Combine(scratch, "Assets", "Linked"), outside);
            Reject<InvalidOperationException>(() => SafeFileIO.ExportAssetPath("Assets/Linked/original.png", scratch), "symbolic-link directory escape is rejected");
            File.WriteAllText(Path.Combine(outside, "source.png"), "source");
            File.CreateSymbolicLink(Path.Combine(scratch, "Assets", "alias.png"), Path.Combine(outside, "source.png"));
            Reject<InvalidOperationException>(() => SafeFileIO.ExportAssetPath("Assets/alias.png", scratch), "symbolic-link destination is rejected");
            Require(File.ReadAllText(Path.Combine(outside, "source.png")) == "source", "linked source remains unchanged");

            string root = args.Length == 0 ? Directory.GetCurrentDirectory() : args[0];
            var sources = Directory.EnumerateFiles(Path.Combine(root, "Packages"), "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(Path.Combine(root, "Tests", "UnityProject", "Assets"), "*.cs", SearchOption.AllDirectories)).ToArray();
            var errors = sources.SelectMany(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path),
                new CSharpParseOptions(LanguageVersion.CSharp9), path).GetDiagnostics())
                .Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            foreach (var error in errors) Console.Error.WriteLine(error);
            Require(errors.Length == 0, "C# 9 syntax parsed for " + sources.Length + " package/harness sources (not Unity compilation)");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("FILETEST FAIL " + e); return 1; }
        finally { if (Directory.Exists(scratch)) Directory.Delete(scratch, true); }
    }
}
