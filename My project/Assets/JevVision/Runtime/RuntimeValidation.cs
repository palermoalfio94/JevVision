using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace JevVision
{
    [DefaultExecutionOrder(-200)]
    public sealed class RuntimeValidation : MonoBehaviour
    {
        public ExperimentConfig config;
        public ExperimentManager experiment;
        public AutonomousController controller;
        public CameraCapture capture;
        public VehicleController vehicle;
        public TelemetryLogger logger;
        private string validationMode;
        private float duration = 160;
        private double start;
        private bool finishing;
        private int stage;
        private double phaseStarted;
        private int observedResets;
        private float accelerationSpeed, turnedDegrees, brakeSpeed;
        private Quaternion initialRotation;
        private Keyboard keyboard;
        private readonly List<string> resetReasons = new List<string>();
        [Serializable] public sealed class Report
        {
            public string utc, validation, mode, result, logDirectory;
            public double seconds, rmsCenterlineError, maximumCenterlineError, bestLapSeconds;
            public int laps, localLaps, jevLaps, manualLaps, resets, nextCheckpoint, apiCalls, localDecisions;
            public float accelerationSpeed, turnedDegrees, brakeSpeed;
            public string[] resetReasons;
            public bool frameValid, loggingValid;
            public string antiCheating = "Local driver: Texture2D + fixed camera calibration only. JEV: JPEG + static questions only. Checkpoints are evaluation-only.";
        }
        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            validationMode = GetArg(args, "-jev-validation");
            string output = GetArg(args, "-jev-output");
            if (!string.IsNullOrEmpty(output)) config.outputDirectory = Path.GetFullPath(output);
            string mode = GetArg(args, "-jev-mode");
            if (mode == "manual") config.startMode = DrivingMode.Manual;
            if (mode == "local") config.startMode = DrivingMode.LocalVision;
            if (mode == "jev") config.startMode = DrivingMode.JevVision;
            if (validationMode == "manual" || validationMode == "resets") config.startMode = DrivingMode.Manual;
            if (float.TryParse(GetArg(args, "-jev-seconds"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float seconds)) duration = seconds;
            config.Validate();
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60; Application.runInBackground = true;
        }
        private void Start()
        {
            start = phaseStarted = Time.realtimeSinceStartupAsDouble;
            initialRotation = vehicle.transform.rotation;
            if (validationMode == "manual")
            {
                controller.SetMode(DrivingMode.Manual);
                keyboard = InputSystem.AddDevice<Keyboard>();
            }
            if (validationMode == "resets")
            {
                controller.SetMode(DrivingMode.Manual);
                config.stuckSeconds = 2;
            }
        }
        private void Update()
        {
            if (string.IsNullOrEmpty(validationMode) || finishing) return;
            double elapsed = Time.realtimeSinceStartupAsDouble - start;
            double phase = Time.realtimeSinceStartupAsDouble - phaseStarted;
            if (validationMode == "manual")
            {
                if (elapsed < 2) InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                else if (elapsed < 4)
                {
                    if (stage == 0) { accelerationSpeed = vehicle.Speed; stage = 1; }
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D));
                }
                else
                {
                    if (stage == 1) { turnedDegrees = Quaternion.Angle(initialRotation, vehicle.transform.rotation); stage = 2; }
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                    brakeSpeed = vehicle.Speed;
                }
            }
            if (validationMode == "resets")
            {
                if (experiment.Resets > observedResets)
                {
                    resetReasons.Add(experiment.LastResetReason); observedResets = experiment.Resets;
                    stage++; phaseStarted = Time.realtimeSinceStartupAsDouble;
                }
                if (phase > 0.6)
                {
                    if (stage == 0) vehicle.Body.position = new Vector3(0, 0.1f, 0);
                    if (stage == 1) vehicle.Body.rotation = Quaternion.Euler(0, 0, 90);
                    if (stage == 2) { controller.SetMode(DrivingMode.LocalVision); controller.vehicle.SetCommand(JevDecision.Stop("validation_stuck")); }
                    if (stage >= 3) Finish();
                }
            }
            if (elapsed > duration || validationMode == "lap" && experiment.LocalLaps > 0 || validationMode == "jev" && experiment.JevLaps > 0) Finish();
        }
        private void LateUpdate()
        {
            if (validationMode == "resets" && stage == 2) vehicle.SetCommand(JevDecision.Stop("validation_stuck"));
        }
        private void Finish()
        {
            if (finishing) return; finishing = true;
            if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            bool frameValid = capture.LastJpeg != null && capture.LastJpeg.Length > 500 && capture.LastFrame.width >= 280 && HasSceneContent(capture.LastFrame);
            bool logsValid = string.IsNullOrEmpty(logger.LoggingError) && Directory.Exists(logger.SessionDirectory);
            bool passed = validationMode == "lap" ? experiment.LocalLaps > 0 && experiment.Resets == 0 && controller.ApiCalls == 0 :
                validationMode == "manual" ? accelerationSpeed > 1 && turnedDegrees > 5 && brakeSpeed < 0.2f :
                validationMode == "resets" ? resetReasons.Contains("off_track") && resetReasons.Contains("rollover") && resetReasons.Contains("stuck") :
                validationMode == "frame" ? controller.LocalDecisions > 0 && vehicle.Speed > 0.5f && experiment.Resets == 0 && controller.ApiCalls == 0 :
                validationMode == "jev" ? experiment.JevLaps > 0 : validationMode == "no-key" ? controller.ApiCalls == 0 && vehicle.Speed < 0.1f : false;
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), validation = validationMode, mode = controller.Mode.ToString(),
                result = passed && frameValid && logsValid ? "PASS" : "FAIL", logDirectory = logger.SessionDirectory,
                seconds = experiment.Elapsed, laps = experiment.Laps, localLaps = experiment.LocalLaps, jevLaps = experiment.JevLaps, manualLaps = experiment.ManualLaps,
                resets = experiment.Resets, nextCheckpoint = experiment.NextCheckpoint, apiCalls = controller.ApiCalls, localDecisions = controller.LocalDecisions,
                rmsCenterlineError = Math.Sqrt(experiment.SumSquaredCenterlineError / Math.Max(1, experiment.EvaluationSamples)), maximumCenterlineError = experiment.MaximumCenterlineError,
                bestLapSeconds = experiment.BestLapSeconds, accelerationSpeed = accelerationSpeed, turnedDegrees = turnedDegrees, brakeSpeed = brakeSpeed,
                resetReasons = resetReasons.ToArray(), frameValid = frameValid, loggingValid = logsValid };
            logger.RecordEvent("validation_complete", report.result);
            logger.Flush();
            File.WriteAllText(Path.Combine(logger.SessionDirectory, "report.json"), JsonUtility.ToJson(report, true));
            if (capture.LastJpeg != null) File.WriteAllBytes(Path.Combine(logger.SessionDirectory, "front-final.jpg"), capture.LastJpeg);
            SaveOverview(Path.Combine(logger.SessionDirectory, "overview-final.png"));
            Debug.Log("JEV_VISION_VALIDATION " + JsonUtility.ToJson(report));
            StartCoroutine(QuitAfterScreenshot(report.result == "PASS" ? 0 : 2));
        }
        private static bool HasSceneContent(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            int low = 255, high = 0;
            foreach (Color32 pixel in pixels)
            {
                int brightness = (pixel.r + pixel.g + pixel.b) / 3;
                low = Math.Min(low, brightness); high = Math.Max(high, brightness);
            }
            return high - low > 24;
        }
        private static void SaveOverview(string path)
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            var target = new RenderTexture(1440, 900, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            target.Create();
            try
            {
                CameraCapture.RenderInto(camera, target);
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; target.Release(); Destroy(target); Destroy(texture);
            }
        }
        private System.Collections.IEnumerator QuitAfterScreenshot(int exitCode)
        {
            controller.SetPaused(true);
            yield return null; yield return null; yield return null;
            Application.Quit(exitCode);
        }
        private static string GetArg(string[] args, string key)
        {
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
        }
    }
}
