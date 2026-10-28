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
        private float accelerationSpeed, turnedDegrees, coastSpeed;
        private Quaternion initialRotation;
        private Keyboard keyboard;
        private RigidbodyConstraints initialConstraints;
        private Vector3 initialPosition;
        private float maximumTravel, maximumSpeed;
        private float maximumHorizontalSpeed;
        private bool setupBlocked, settingsApplied;
        private bool startupScreenshotSaved;
        private readonly List<string> resetReasons = new List<string>();
        [Serializable] public sealed class Report
        {
            public string utc, validation, mode, result, logDirectory;
            public double seconds, rmsCenterlineError, maximumCenterlineError, bestLapSeconds;
            public float trackLengthMeters;
            public int laps, jevLaps, manualLaps, resets, nextCheckpoint, apiCalls, successfulCalls, failedCalls, discardedResponses;
            public long inputTokens, outputTokens;
            public double meanLatencyMs, estimatedCostUsd;
            public float maximumTravelMeters, maximumSpeedMetersPerSecond;
            public float maximumHorizontalSpeedMetersPerSecond;
            public float accelerationSpeed, turnedDegrees, coastSpeed;
            public string[] resetReasons;
            public bool frameValid, loggingValid;
            public bool startupBlocked, selectedSettingsApplied;
            public string antiCheating = "Autonomous commands come exclusively from JEV responses to the front-camera JPEG and static questions. Checkpoints are evaluation-only.";
        }
        private void Awake()
        {
            // All components share a session copy; UI changes never overwrite the project asset.
            config = Instantiate(config);
            experiment.config = controller.config = capture.config = vehicle.config = logger.config = config;
            controller.client.config = config;
            GetComponent<TelemetryHud>().config = config;
            string[] args = Environment.GetCommandLineArgs();
            validationMode = GetArg(args, "-jev-validation");
            string output = GetArg(args, "-jev-output");
            if (!string.IsNullOrEmpty(output)) config.outputDirectory = Path.GetFullPath(output);
            string mode = GetArg(args, "-jev-mode");
            if (mode == "manual") config.startMode = DrivingMode.Manual;
            if (mode == "jev") config.startMode = DrivingMode.JevVision;
            if (validationMode == "manual" || validationMode == "resets" || validationMode == "frame") config.startMode = DrivingMode.Manual;
            if (validationMode == "no-key" || validationMode == "setup-jev")
            {
                config.startMode = DrivingMode.JevVision;
                config.apiKeyEnvironmentVariable = "JEV_TEST_MISSING_" + Guid.NewGuid().ToString("N");
            }
            if (validationMode == "jev-frame" || validationMode == "jev-smoke" || validationMode == "jev")
            {
                config.startMode = DrivingMode.JevVision;
                if (validationMode == "jev-frame") config.maximumApiCallsPerSession = 1;
                if (validationMode == "jev-smoke") config.maximumApiCallsPerSession = 12;
                config.maximumSpeed = Mathf.Min(config.maximumSpeed, 1f);
            }
            if (float.TryParse(GetArg(args, "-jev-seconds"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float seconds)) duration = seconds;
            config.Validate();
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60; Application.runInBackground = true;
        }
        private void Start()
        {
            SceneFactory.AddCenterDashes(experiment.track);
            SceneFactory.AddRoadsideTrees(experiment.track);
            start = phaseStarted = Time.realtimeSinceStartupAsDouble;
            initialRotation = vehicle.transform.rotation;
            initialPosition = vehicle.transform.position;
            initialConstraints = vehicle.Body.constraints;
            if (!string.IsNullOrEmpty(validationMode) && validationMode != "setup" && validationMode != "setup-jev" && validationMode != "setup-screen")
            {
                experiment.StartDriving(config.startMode, config.decisionsPerSecond, config.maximumSpeed);
                initialConstraints = vehicle.Body.constraints;
            }
            if (validationMode == "setup" || validationMode == "setup-jev") keyboard = InputSystem.AddDevice<Keyboard>();
            if (validationMode == "manual" || validationMode == "resets")
            {
                controller.SetMode(DrivingMode.Manual);
                keyboard = InputSystem.AddDevice<Keyboard>();
            }
            if (validationMode == "resets")
            {
                controller.SetMode(DrivingMode.Manual);
                config.stuckSeconds = 2;
            }
            if (validationMode == "jev-frame") vehicle.Body.constraints = RigidbodyConstraints.FreezeAll;
        }
        private void Update()
        {
            if (string.IsNullOrEmpty(validationMode) || finishing) return;
            double elapsed = Time.realtimeSinceStartupAsDouble - start;
            double phase = Time.realtimeSinceStartupAsDouble - phaseStarted;
            maximumTravel = Mathf.Max(maximumTravel, Vector3.Distance(initialPosition, vehicle.transform.position));
            maximumSpeed = Mathf.Max(maximumSpeed, vehicle.Speed);
            maximumHorizontalSpeed = Mathf.Max(maximumHorizontalSpeed, Vector3.ProjectOnPlane(vehicle.Body.linearVelocity, Vector3.up).magnitude);
            if (validationMode == "setup-screen")
            {
                if (elapsed > 1 && !startupScreenshotSaved)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(logger.SessionDirectory, "startup-settings.png"));
                    startupScreenshotSaved = true;
                }
                if (elapsed > 3)
                {
                    setupBlocked = !controller.HasStarted && controller.Paused && vehicle.Held && controller.ApiCalls == 0 && maximumTravel < 0.001f;
                    capture.Capture();
                    Finish();
                }
            }
            if (validationMode == "setup" || validationMode == "setup-jev")
            {
                if (stage == 0)
                {
                    if (elapsed > 0.7 && !startupScreenshotSaved)
                    {
                        ScreenCapture.CaptureScreenshot(Path.Combine(logger.SessionDirectory, "startup-settings.png"));
                        startupScreenshotSaved = true;
                    }
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.P, Key.Digit2));
                    controller.SetPaused(false); // Pause shortcuts and direct resume must not bypass Play.
                    if (elapsed > 2)
                    {
                        setupBlocked = !controller.HasStarted && controller.Paused && vehicle.Held && controller.ApiCalls == 0 && maximumTravel < 0.001f && experiment.Elapsed == 0 && experiment.EvaluationSamples == 0;
                        DrivingMode selectedMode = validationMode == "setup-jev" ? DrivingMode.JevVision : DrivingMode.Manual;
                        settingsApplied = experiment.StartDriving(selectedMode, 0.5f, 0.8f) && controller.Mode == selectedMode && !controller.Paused && !vehicle.Held && Mathf.Approximately(config.decisionsPerSecond, 0.5f) && Mathf.Approximately(config.maximumSpeed, 0.8f) && ReferenceEquals(config, controller.config) && ReferenceEquals(config, vehicle.config);
                        initialConstraints = vehicle.Body.constraints;
                        phaseStarted = Time.realtimeSinceStartupAsDouble;
                        stage = 1;
                    }
                }
                else if (phase < 2) InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                else
                {
                    if (stage == 1) { accelerationSpeed = vehicle.Speed; stage = 2; }
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    if (phase > 3) { coastSpeed = vehicle.Speed; capture.Capture(); Finish(); }
                }
            }
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
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    coastSpeed = vehicle.Speed;
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
                    if (stage == 2)
                    {
                        vehicle.Body.constraints = RigidbodyConstraints.FreezeAll;
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    }
                    if (stage >= 3) Finish();
                }
            }
            bool requestBudgetFinished = (validationMode == "jev-frame" || validationMode == "jev-smoke") &&
                controller.ApiCalls >= config.maximumApiCallsPerSession && controller.CompletedCalls == controller.ApiCalls;
            if (elapsed > duration || requestBudgetFinished || validationMode == "jev" && experiment.JevLaps > 0) Finish();
        }
        private void Finish()
        {
            if (finishing) return; finishing = true;
            if (keyboard != null) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            vehicle.Body.constraints = initialConstraints;
            bool frameValid = capture.LastJpeg != null && capture.LastJpeg.Length > 500 && capture.LastFrame.width >= 280 && HasSceneContent(capture.LastFrame);
            bool logsValid = string.IsNullOrEmpty(logger.LoggingError) && Directory.Exists(logger.SessionDirectory);
            bool passed = validationMode == "setup-screen" ? setupBlocked && File.Exists(Path.Combine(logger.SessionDirectory, "startup-settings.png")) :
                validationMode == "setup-jev" ? setupBlocked && settingsApplied && maximumTravel < 0.1f && vehicle.Speed < 0.1f && controller.ApiCalls == 0 && controller.ApiStatus.StartsWith("missing ") :
                validationMode == "setup" ? setupBlocked && settingsApplied && maximumHorizontalSpeed <= 0.81f && accelerationSpeed > 0.7f && coastSpeed < accelerationSpeed - 0.05f && controller.ApiCalls == 0 :
                validationMode == "manual" ? accelerationSpeed > 0.8f && turnedDegrees > 5 && coastSpeed < accelerationSpeed - 0.2f :
                validationMode == "resets" ? resetReasons.Contains("off_track") && resetReasons.Contains("rollover") && resetReasons.Contains("stuck") :
                validationMode == "frame" ? experiment.Resets == 0 && controller.ApiCalls == 0 :
                validationMode == "jev-frame" ? controller.ApiCalls == 1 && controller.SuccessfulCalls == 1 && maximumTravel < 0.1f :
                validationMode == "jev-smoke" ? controller.SuccessfulCalls >= 3 && maximumTravel > 0.5f && experiment.Resets == 0 :
                validationMode == "jev" ? experiment.JevLaps > 0 : validationMode == "no-key" ? controller.ApiCalls == 0 && vehicle.Speed < 0.1f : false;
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), validation = validationMode, mode = controller.Mode.ToString(),
                result = passed && frameValid && logsValid ? "PASS" : "FAIL", logDirectory = logger.SessionDirectory,
                seconds = experiment.Elapsed, laps = experiment.Laps, jevLaps = experiment.JevLaps, manualLaps = experiment.ManualLaps,
                trackLengthMeters = experiment.track.Length,
                resets = experiment.Resets, nextCheckpoint = experiment.NextCheckpoint, apiCalls = controller.ApiCalls,
                successfulCalls = controller.SuccessfulCalls, failedCalls = controller.Failures, discardedResponses = controller.DiscardedResponses,
                inputTokens = controller.InputTokens, outputTokens = controller.OutputTokens, meanLatencyMs = controller.AverageLatencyMs,
                estimatedCostUsd = controller.EstimatedCost, maximumTravelMeters = maximumTravel, maximumSpeedMetersPerSecond = maximumSpeed,
                maximumHorizontalSpeedMetersPerSecond = maximumHorizontalSpeed,
                rmsCenterlineError = Math.Sqrt(experiment.SumSquaredCenterlineError / Math.Max(1, experiment.EvaluationSamples)), maximumCenterlineError = experiment.MaximumCenterlineError,
                bestLapSeconds = experiment.BestLapSeconds, accelerationSpeed = accelerationSpeed, turnedDegrees = turnedDegrees, coastSpeed = coastSpeed,
                resetReasons = resetReasons.ToArray(), frameValid = frameValid, loggingValid = logsValid,
                startupBlocked = setupBlocked, selectedSettingsApplied = settingsApplied };
            logger.RecordEvent("validation_complete", report.result);
            logger.Flush();
            File.WriteAllText(Path.Combine(logger.SessionDirectory, "report.json"), JsonUtility.ToJson(report, true));
            if (capture.LastJpeg != null) File.WriteAllBytes(Path.Combine(logger.SessionDirectory, "front-final.jpg"), capture.LastJpeg);
            SaveOverview(Path.Combine(logger.SessionDirectory, "overview-final.png"));
            SaveOverview(Path.Combine(logger.SessionDirectory, "circuit-plan.png"), true);
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
        private static void SaveOverview(string path, bool plan = false)
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            Vector3 oldPosition = camera.transform.position;
            Quaternion oldRotation = camera.transform.rotation;
            bool oldOrthographic = camera.orthographic;
            float oldSize = camera.orthographicSize;
            if (plan)
            {
                camera.transform.SetPositionAndRotation(new Vector3(0, 150, 0), Quaternion.Euler(90, 0, 0));
                camera.orthographic = true; camera.orthographicSize = 85;
            }
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
                camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
                camera.orthographic = oldOrthographic; camera.orthographicSize = oldSize;
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
