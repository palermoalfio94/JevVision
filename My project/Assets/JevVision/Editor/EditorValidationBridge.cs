using System;
using System.IO;
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
        static EditorValidationBridge() => EditorApplication.update += Poll;
        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            string request = Path.GetFullPath("Validation/build.request");
            if (!File.Exists(request)) return;
            // Import changed scripts before using their editor-side types to author a scene.
            // Otherwise BuildPlayer may compile newer fields than the currently loaded editor assembly.
            DateTime newestRuntimeSource = DateTime.MinValue, newestEditorSource = DateTime.MinValue;
            foreach (string source in Directory.GetFiles("Assets/JevVision", "*.cs", SearchOption.AllDirectories))
            {
                DateTime modified = File.GetLastWriteTimeUtc(source);
                if (source.Replace('\\', '/').Contains("/Editor/"))
                    newestEditorSource = modified > newestEditorSource ? modified : newestEditorSource;
                else newestRuntimeSource = modified > newestRuntimeSource ? modified : newestRuntimeSource;
            }
            if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp.dll") < newestRuntimeSource ||
                File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < newestEditorSource)
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
