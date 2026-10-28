using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JevVision.Editor
{
    public static class ProjectTools
    {
        private const string Base = "Assets/JevVision/Resources";
        private const string ScenePath = "Assets/Scenes/JevVisionCircuit.unity";
        [MenuItem("JEV Vision/Create or rebuild circuit scene")]
        public static void CreateScene()
        {
            Directory.CreateDirectory(Base + "/Materials"); AssetDatabase.Refresh();
            ExperimentConfig config = AssetDatabase.LoadAssetAtPath<ExperimentConfig>(Base + "/ExperimentSettings.asset");
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ExperimentConfig>();
                AssetDatabase.CreateAsset(config, Base + "/ExperimentSettings.asset");
            }
            config.Validate();
            var colors = new[] { new Color(0.13f, 0.31f, 0.22f), new Color(0.17f, 0.18f, 0.19f), new Color(0.92f, 0.94f, 0.91f),
                new Color(0.88f, 0.18f, 0.12f), new Color(0.02f, 0.22f, 0.95f), new Color(0.03f, 0.48f, 0.86f), new Color(0.04f, 0.12f, 0.19f),
                new Color(0.035f, 0.04f, 0.045f), new Color(0.95f, 0.73f, 0.14f) };
            string[] names = { "Grass", "Asphalt", "White", "Red", "Rail", "Body", "Glass", "Tyre", "Yellow" };
            var materials = new Material[colors.Length];
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new Exception("URP Unlit shader missing.");
            for (int i = 0; i < colors.Length; i++)
            {
                string path = Base + "/Materials/" + names[i] + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                material.SetColor("_BaseColor", colors[i]); EditorUtility.SetDirty(material); materials[i] = material;
            }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = SceneFactory.Build(config, materials);
            Directory.CreateDirectory("Assets/JevVision/Generated"); AssetDatabase.Refresh();
            int meshIndex = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(filter.sharedMesh))) continue;
                string meshPath = "Assets/JevVision/Generated/TrackSurface" + meshIndex++ + ".asset";
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing == null) AssetDatabase.CreateAsset(filter.sharedMesh, meshPath);
                else { EditorUtility.CopySerialized(filter.sharedMesh, existing); filter.sharedMesh = existing; EditorUtility.SetDirty(existing); }
            }
            var collider = root.GetComponentInChildren<VehicleController>().GetComponent<BoxCollider>();
            string physicsPath = Base + "/CarContact.physicsMaterial";
            PhysicsMaterial friction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(physicsPath);
            if (friction == null) { friction = collider.sharedMaterial; AssetDatabase.CreateAsset(friction, physicsPath); }
            collider.sharedMaterial = friction;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "JevVisionLab"; PlayerSettings.productName = "JEV Vision Lab";
            PlayerSettings.defaultScreenWidth = 1440; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
            Debug.Log("JEV_VISION_SCENE_READY " + ScenePath);
        }
        [MenuItem("JEV Vision/Run protocol and isolation checks")]
        public static void ValidateProtocol()
        {
            var checks = new List<string>();
            void Assert(bool condition, string name)
            {
                if (!condition) throw new Exception("Validation failed: " + name);
                checks.Add(name);
            }
            string[] labels = { "strong_left", "left", "slight_left", "straight", "slight_right", "right", "strong_right" };
            foreach (string label in labels)
            {
                string json = Reply(label);
                Assert(JevProtocol.TryParse(json, out JevDecision command, out JevProtocol.Response response, out _), "parse " + label);
                Assert(command.source == "jev" && command.throttle == 0.7f && command.confidence == 0.8f && response.usage.input_tokens == 285, "fields " + label);
            }
            Assert(!JevProtocol.TryParse(Reply("teleport"), out _, out _, out _), "reject unknown steering");
            Assert(!JevProtocol.TryParse("{}", out _, out _, out _), "reject missing answers");
            Assert(!JevProtocol.TryParse("not json", out _, out _, out _), "reject malformed JSON");
            Assert(!JevProtocol.TryParse(Reply("straight").Replace("\"confidence\":0.8", "\"confidence\":2"), out _, out _, out _), "reject invalid confidence");
            Assert(!JevProtocol.TryParse(Reply("straight").Replace("\"cruise\"", "\"full\""), out _, out _, out _), "reject unknown throttle");
            Assert(JevProtocol.TryParse(Reply("straight").Replace("\"cruise\"", "\"coast\""), out JevDecision coasting, out _, out _) && coasting.throttle == 0, "coast releases accelerator");
            string payload = JevProtocol.BuildRequest(new byte[] { 255, 216, 255, 217 }, "jev-1.14.0");
            Assert(payload.Contains("\"data_base64\":\"/9j/2Q==\"") && payload.Contains("\"assets\"") && payload.Contains("\"format\":\"jpg\""), "official image asset envelope");
            foreach (string forbidden in new[] { "position", "coordinate", "waypoint", "velocity", "checkpoint", "raycast", "speed", "vehicle", "yaw", "api_key" })
                Assert(!payload.ToLowerInvariant().Contains(forbidden), "payload excludes " + forbidden);
            // The visual centerline may be named in instructions, but no evaluated centerline data is sent.
            Assert(!payload.Contains("\"centerline\":"), "payload excludes centerline");
            var config = ScriptableObject.CreateInstance<ExperimentConfig>();
            Assert(Enum.GetValues(typeof(DrivingMode)).Length == 2, "only manual and JEV modes exist");
            config.startMode = (DrivingMode)1;
            config.Validate();
            Assert(config.startMode == DrivingMode.Manual, "retired serialized mode safely migrates to manual");
            UnityEngine.Object.DestroyImmediate(config);
            var track = UnityEngine.Object.FindFirstObjectByType<TrackLayout>();
            Assert(track != null && Vector3.Distance(track.Point(0), track.Point(track.Length - 0.001f)) < 0.01f, "closed circuit");
            Assert(track.Length > 400 && track.controlPoints.Length > 12, "reference-shaped circuit replaces oval");
            bool leftBend = false, rightBend = false;
            for (int i = 0; i < 128; i++)
            {
                float distance = i * track.Length / 128;
                Assert(track.DistanceFromCenterline(track.Point(distance)) < 0.05f, "spline evaluation distance " + i);
                float turn = Vector3.SignedAngle(track.Tangent(distance), track.Tangent(distance + 2), Vector3.up);
                leftBend |= turn < -1; rightBend |= turn > 1;
            }
            Assert(leftBend && rightBend, "circuit has both left and right bends");
            Assert(RoadSurfacesAreSaved(track), "continuous road meshes persist in saved scene");
            Assert(track.GeometryMatchesSettings, "generated geometry matches evaluation spline");
            float originalWidth = track.roadWidth;
            track.roadWidth += 0.5f;
            Assert(!track.GeometryMatchesSettings, "reject changed width before scene regeneration");
            track.roadWidth = originalWidth;
            Vector3 originalPoint = track.controlPoints[0];
            track.controlPoints[0] += Vector3.right * 0.5f;
            Assert(!track.GeometryMatchesSettings, "reject changed spline before scene regeneration");
            track.controlPoints[0] = originalPoint;
            Assert(track.GeometryMatchesSettings, "restored scene remains consistent");
            LapCheckpoint[] gates = UnityEngine.Object.FindObjectsByType<LapCheckpoint>(FindObjectsSortMode.None);
            Assert(gates.Length == 16, "16 ordered checkpoints");
            foreach (var gate in gates) Assert(gate.GetComponent<Renderer>() == null && gate.GetComponent<BoxCollider>().isTrigger, "invisible evaluation gate " + gate.index);
            var vehicle = UnityEngine.Object.FindFirstObjectByType<VehicleController>();
            Assert(vehicle.GetComponent<Rigidbody>() != null && vehicle.config.maximumSpeed <= 3, "low-speed Rigidbody vehicle");
            var sensor = UnityEngine.Object.FindFirstObjectByType<CameraCapture>();
            Assert(sensor.frontCamera.transform.IsChildOf(vehicle.transform), "front camera mounted on vehicle");
            Directory.CreateDirectory("Validation");
            File.WriteAllText("Validation/protocol-checks.json", "{\n  \"result\":\"PASS\",\n  \"checks\":" + checks.Count + ",\n  \"items\":[" + string.Join(",", checks.ConvertAll(x => "\"" + x + "\"")) + "]\n}");
            Debug.Log("JEV_VISION_PROTOCOL_PASS checks=" + checks.Count);
        }
        private static string Reply(string steering) => "{\"model\":\"jev-1.14.0\",\"answers\":{\"steering\":{\"type\":\"choice\",\"choice\":\"" + steering + "\",\"confidence\":0.8},\"throttle\":{\"type\":\"choice\",\"choice\":\"cruise\",\"confidence\":0.8}},\"usage\":{\"input_tokens\":285,\"output_tokens\":30}}";
        private static bool RoadSurfacesAreSaved(TrackLayout track)
        {
            int count = 0;
            foreach (var filter in track.GetComponentsInChildren<MeshFilter>())
                if (filter.sharedMesh != null && (filter.gameObject.name.StartsWith("Continuous") || filter.gameObject.name == "Red and white curb"))
                { if (!AssetDatabase.Contains(filter.sharedMesh)) return false; count++; }
            return count == 5;
        }

        public static void BuildAndValidate()
        {
            try
            {
                bool success = BuildInOpenEditor();
                EditorApplication.Exit(success ? 0 : 2);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(2); }
        }
        [MenuItem("JEV Vision/Build Windows player and validate")]
        public static void BuildFromMenu() => BuildInOpenEditor();
        public static bool BuildInOpenEditor()
        {
            CreateScene(); ValidateProtocol();
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = "Builds/Windows/JevVisionLab.exe", target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            Directory.CreateDirectory("Validation");
            File.WriteAllText("Validation/build-result.txt", report.summary.result + "\nErrors=" + report.summary.totalErrors + "\nWarnings=" + report.summary.totalWarnings + "\nBytes=" + report.summary.totalSize);
            return report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
        }
    }
}
