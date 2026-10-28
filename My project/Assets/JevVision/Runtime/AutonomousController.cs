using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JevVision
{
    // The autonomous path has access to a camera and a command sink, never TrackLayout/ExperimentManager.
    public sealed class AutonomousController : MonoBehaviour
    {
        public ExperimentConfig config;
        public CameraCapture capture;
        public VehicleController vehicle;
        public JevClient client;
        public TelemetryLogger logger;
        public DrivingMode Mode { get; private set; }
        public string ApiStatus { get; private set; } = "offline";
        public string LastResponse { get; private set; } = "";
        public string LastError { get; private set; } = "";
        public JevProtocol.Answers LastJevAnswers { get; private set; }
        public double LastJevDecisionAge => lastValidCapture < 0 ? -1 : Time.realtimeSinceStartupAsDouble - lastValidCapture;
        public double LastLatencyMs { get; private set; }
        public double TotalLatencyMs { get; private set; }
        public int ApiCalls { get; private set; }
        public int CompletedCalls { get; private set; }
        public int SuccessfulCalls { get; private set; }
        public int Failures { get; private set; }
        public int DiscardedResponses { get; private set; }
        public long InputTokens { get; private set; }
        public long OutputTokens { get; private set; }
        public int UnmeteredCalls { get; private set; }
        public double EstimatedCost => (InputTokens * config.inputUsdPerMillionTokens + OutputTokens * config.outputUsdPerMillionTokens) / 1000000.0;
        public double AverageLatencyMs => CompletedCalls == 0 ? 0 : TotalLatencyMs / CompletedCalls;
        public bool HasStarted { get; private set; }
        public bool Paused { get; private set; } = true;
        private int generation;
        private double nextDecision, lastValidCapture = -1;
        private int consecutiveErrors;
        private bool terminalApiError;
        private JevDecision lastValid = JevDecision.Coast();

        private void Start()
        {
            if (!HasStarted) Mode = config.startMode;
            if (!HasStarted) ApiStatus = "in attesa di Play / nessuna chiamata";
            logger.RecordEvent("mode", Mode.ToString());
            StartCoroutine(DecisionLoop());
        }
        public void BeginDriving(DrivingMode mode)
        {
            SetMode(mode);
            HasStarted = true;
            vehicle.SetHeld(false);
            SetPaused(false);
        }
        public void SetMode(DrivingMode mode)
        {
            if (mode != DrivingMode.Manual && mode != DrivingMode.JevVision) throw new ArgumentOutOfRangeException(nameof(mode));
            Invalidate();
            Mode = mode;
            terminalApiError = false;
            consecutiveErrors = 0;
            LastError = "";
            logger.RecordEvent("mode", mode.ToString());
        }
        public void SetPaused(bool paused)
        {
            Paused = !HasStarted || paused;
            vehicle.SetHeld(Paused);
            Invalidate();
            logger.RecordEvent(Paused ? "pause" : "resume", Mode.ToString());
        }
        public void Invalidate()
        {
            generation++;
            client.Cancel();
            lastValidCapture = -1;
            LastJevAnswers = null;
            lastValid = JevDecision.Coast();
            vehicle.SetCommand(lastValid);
            nextDecision = Time.realtimeSinceStartupAsDouble + 0.25;
        }
        private void Update()
        {
            if (!HasStarted) { vehicle.SetCommand(JevDecision.Coast("waiting_for_play")); return; }
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SetMode(DrivingMode.Manual);
                if (keyboard.digit2Key.wasPressedThisFrame || keyboard.digit3Key.wasPressedThisFrame) SetMode(DrivingMode.JevVision);
                if (keyboard.pKey.wasPressedThisFrame) SetPaused(!Paused);
            }
            if (Paused) { vehicle.SetCommand(JevDecision.Coast("paused")); return; }
            if (Mode == DrivingMode.Manual)
            {
                float steering = keyboard == null ? 0 : (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                float throttle = keyboard != null && (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) ? config.throttleScale : 0;
                vehicle.SetCommand(new JevDecision { steering = JevDecision.Label(steering), steeringValue = steering, throttle = throttle, source = "manual" });
            }
            else if (lastValidCapture < 0 || Time.realtimeSinceStartupAsDouble - lastValidCapture > config.maximumDecisionAgeSeconds)
            {
                vehicle.SetCommand(JevDecision.Coast("stale_or_missing_decision"));
                if (Mode == DrivingMode.JevVision && ApiStatus == "ready") ApiStatus = "decision expired: coasting";
            }
            else vehicle.SetCommand(lastValid);
        }
        private IEnumerator DecisionLoop()
        {
            yield return null;
            yield return null; // allow the front RenderTexture to render before first readback
            while (true)
            {
                yield return null;
                double now = Time.realtimeSinceStartupAsDouble;
                if (!HasStarted || Paused || now < nextDecision) continue;
                if (Mode == DrivingMode.Manual)
                {
                    capture.Capture();
                    nextDecision = now + 0.5;
                    ApiStatus = "manual / no API";
                    continue;
                }
                if (terminalApiError) continue;
                if (!client.HasKey)
                {
                    capture.Capture();
                    ApiStatus = "missing " + config.apiKeyEnvironmentVariable;
                    nextDecision = now + 1;
                    continue;
                }
                if (ApiCalls >= config.maximumApiCallsPerSession || EstimatedCost >= config.maximumEstimatedCostUsd)
                {
                    ApiStatus = "session API budget reached";
                    terminalApiError = true;
                    continue;
                }
                byte[] jpeg = capture.Capture();
                double capturedAt = capture.CapturedAt;
                int requestedGeneration = generation;
                ApiCalls++;
                ApiStatus = "waiting";
                JevClient.Result result = null;
                yield return client.Decide(jpeg, r => result = r);
                if (result == null) continue;
                CompletedCalls++;
                LastLatencyMs = result.latencyMs;
                TotalLatencyMs += result.latencyMs;
                LastResponse = result.raw;
                LastError = result.error;
                // Count billed usage even when a reply is stale or belongs to a reset attempt.
                if (result.response?.usage != null && result.response.usage.input_tokens >= 0 && result.response.usage.output_tokens >= 0)
                { InputTokens += result.response.usage.input_tokens; OutputTokens += result.response.usage.output_tokens; }
                else if (result.httpStatus >= 200 && result.httpStatus < 300) UnmeteredCalls++;
                string outcome;
                if (requestedGeneration != generation || Mode != DrivingMode.JevVision || Paused)
                { DiscardedResponses++; outcome = "discarded_generation"; }
                else if (result.success && Time.realtimeSinceStartupAsDouble - capturedAt <= config.maximumDecisionAgeSeconds)
                {
                    lastValid = result.decision;
                    LastJevAnswers = result.response.answers;
                    lastValid.throttle *= config.throttleScale;
                    lastValidCapture = capturedAt;
                    SuccessfulCalls++;
                    consecutiveErrors = 0;
                    ApiStatus = "ready";
                    outcome = "applied";
                }
                else
                {
                    Failures++;
                    consecutiveErrors++;
                    outcome = result.success ? "discarded_stale" : "error";
                    if (result.success) DiscardedResponses++;
                    ApiStatus = result.success ? "response too old: coasting" : result.error;
                    if (result.httpStatus == 400 || result.httpStatus == 401 || result.httpStatus == 402 || result.httpStatus == 403 || result.httpStatus == 404 || result.httpStatus == 413 || result.httpStatus == 422)
                        terminalApiError = true;
                }
                logger.RecordDecision(result.decision ?? JevDecision.Coast("api_error"), jpeg, result, outcome, capturedAt);
                double backoff = consecutiveErrors == 0 ? 0 : Math.Min(config.maximumBackoffSeconds, Math.Pow(2, Math.Min(consecutiveErrors, 8)));
                nextDecision = Math.Max(capturedAt + 1.0 / config.decisionsPerSecond, Time.realtimeSinceStartupAsDouble + Math.Max(backoff, result.retryAfter));
            }
        }
        private void OnDisable() { if (client != null) client.Cancel(); }
    }
}
