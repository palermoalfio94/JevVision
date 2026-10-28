using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace JevVision.Editor
{
    // A local build request lets the already-open editor validate without taking its project lock.
    [InitializeOnLoad]
    public static class EditorValidationBridge
    {
        private static double nextPoll;
        private static string compiledSourceHash;
        static EditorValidationBridge()
        {
            compiledSourceHash = SourceHash();
            CompilationPipeline.compilationFinished += _ => compiledSourceHash = SourceHash();
            EditorApplication.update += Poll;
        }
        private static string SourceHash()
        {
            string[] files = Directory.GetFiles("Assets/JevVision", "*.cs", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            var source = new StringBuilder();
            foreach (string file in files) source.Append(file).Append('\0').Append(File.ReadAllText(file)).Append('\0');
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(source.ToString())));
        }
        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            string request = Path.GetFullPath("Validation/build.request");
            if (!File.Exists(request)) return;
            // Import changed scripts before using their editor-side types to author a scene.
            // Otherwise BuildPlayer may compile newer fields than the currently loaded editor assembly.
            if (SourceHash() != compiledSourceHash)
            {
                AssetDatabase.ImportAsset("Assets/JevVision", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
                CompilationPipeline.RequestScriptCompilation();
                return;
            }
            File.Delete(request);
            try
            {
                File.WriteAllText("Validation/editor-build-status.txt", "RUNNING");
                bool success = ProjectTools.BuildInOpenEditor();
                File.WriteAllText("Validation/editor-build-status.txt", success ? "PASS" : "FAIL");
            }
            catch (Exception e)
            {
                File.WriteAllText("Validation/editor-build-status.txt", "FAIL\n" + e);
                Debug.LogException(e);
            }
        }
    }
}
