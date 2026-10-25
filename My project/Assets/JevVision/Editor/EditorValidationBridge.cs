using System;
using System.IO;
using UnityEditor;
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
