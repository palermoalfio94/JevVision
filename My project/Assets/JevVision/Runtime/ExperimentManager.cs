using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JevVision
{
    // Independent evaluator. Only stop/reset signals may flow back to the controller.
    public sealed class ExperimentManager : MonoBehaviour
    {
        public ExperimentConfig config;
        public TrackLayout track;
        public VehicleController vehicle;
        public AutonomousController controller;
        public TelemetryLogger logger;
        public int Resets { get; private set; }
        public int Laps { get; private set; }
        public int JevLaps { get; private set; }
        public int ManualLaps { get; private set; }
        public int NextCheckpoint { get; private set; } = 1;
        public int Attempt { get; private set; } = 1;
        public float AttemptSeconds { get; private set; }
        public float BestLapSeconds { get; private set; }
        public float CenterlineError { get; private set; }
        public double SumSquaredCenterlineError { get; private set; }
        public long EvaluationSamples { get; private set; }
        public float MaximumCenterlineError { get; private set; }
        public string LastResetReason { get; private set; } = "none";
        public string SceneError { get; private set; } = "";
        public double Elapsed { get; private set; }
        private float offRoadTime, stuckTime;
        private Vector3 lastProgressPosition;
        private DrivingMode lapMode;
        private bool mixedModeLap, lapPaused;
        private int lapStartResets;
        private void Start()
        {
            if (!track.GeometryMatchesSettings)
            {
                SceneError = "Scena da rigenerare: JEV Vision > Create or rebuild circuit scene";
                controller.SetPaused(true);
                logger.RecordEvent("scene_geometry_mismatch", SceneError);
                Debug.LogError(SceneError);
            }
            lapMode = config.startMode;
            lastProgressPosition = vehicle.transform.position;
        }
        public bool StartDriving(DrivingMode mode, float callsPerSecond, float maximumSpeed)
        {
            if (controller.HasStarted || !track.GeometryMatchesSettings) return false;
            config.startMode = mode;
            config.decisionsPerSecond = callsPerSecond;
            config.maximumSpeed = maximumSpeed;
            config.Validate();
            // Slow schedules must allow a command to survive until the next scheduled frame.
            config.maximumDecisionAgeSeconds = Mathf.Max(config.maximumDecisionAgeSeconds, 1f / config.decisionsPerSecond + 0.5f);
            lapMode = config.startMode;
            mixedModeLap = lapPaused = false;
            lastProgressPosition = vehicle.transform.position;
            logger.SaveConfiguration();
            controller.BeginDriving(config.startMode);
            logger.RecordEvent("driving_start", "mode=" + config.startMode + ";hz=" + config.decisionsPerSecond.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";maximumSpeed=" + config.maximumSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture));
            logger.RecordEvent("attempt_start", "1");
            return true;
        }
        private void Update()
        {
            if (!controller.HasStarted) return;
            if (!controller.Paused) Elapsed += Time.unscaledDeltaTime;
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ResetExperiment("user_reset");
        }
        private void FixedUpdate()
        {
            if (!controller.HasStarted) return;
            if (!string.IsNullOrEmpty(SceneError)) { if (!controller.Paused) controller.SetPaused(true); return; }
            if (controller.Paused) { lapPaused = true; return; }
            if (controller.Mode != lapMode) mixedModeLap = true;
            AttemptSeconds += Time.fixedDeltaTime;
            CenterlineError = track.DistanceFromCenterline(vehicle.transform.position);
            MaximumCenterlineError = Mathf.Max(MaximumCenterlineError, CenterlineError);
            SumSquaredCenterlineError += CenterlineError * CenterlineError;
            EvaluationSamples++;
            offRoadTime = CenterlineError > track.roadWidth * 0.5f + 0.5f ? offRoadTime + Time.fixedDeltaTime : 0;
            if ((vehicle.transform.position - lastProgressPosition).sqrMagnitude > 0.25f)
            { stuckTime = 0; lastProgressPosition = vehicle.transform.position; }
            else stuckTime += Time.fixedDeltaTime;
            bool canDrive = controller.Mode == DrivingMode.JevVision && controller.SuccessfulCalls > 0 ||
                controller.Mode == DrivingMode.Manual && vehicle.Command.throttle > 0.05f;
            string failure = offRoadTime > config.offTrackGraceSeconds ? "off_track" :
                Vector3.Dot(vehicle.transform.up, Vector3.up) < 0.35f ? "rollover" :
                vehicle.transform.position.y < -2 || vehicle.transform.position.y > 5 ? "invalid_height" :
                vehicle.LastCollisionImpulse > config.severeCollisionImpulse ? "severe_collision" :
                canDrive && stuckTime > config.stuckSeconds ? "stuck" :
                canDrive && AttemptSeconds > config.attemptTimeoutSeconds ? "attempt_timeout" : null;
            if (failure != null) ResetExperiment(failure);
        }
        public void PassCheckpoint(int index, bool forward)
        {
            if (!controller.HasStarted || !forward || index != NextCheckpoint || controller.Paused) return;
            logger.RecordEvent("checkpoint", index.ToString());
            if (index == 0)
            {
                Laps++;
                if (!mixedModeLap && !lapPaused && Resets == lapStartResets)
                {
                    if (lapMode == DrivingMode.JevVision) JevLaps++;
                    if (lapMode == DrivingMode.Manual) ManualLaps++;
                }
                if (BestLapSeconds == 0 || AttemptSeconds < BestLapSeconds) BestLapSeconds = AttemptSeconds;
                logger.RecordEvent("lap_completed", lapMode + (mixedModeLap ? " mixed" : "") + ";seconds=" + AttemptSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                Debug.Log("JEV_VISION_LAP mode=" + lapMode + " mixed=" + mixedModeLap + " seconds=" + AttemptSeconds);
                AttemptSeconds = 0;
                lapMode = controller.Mode;
                mixedModeLap = false; lapPaused = false; lapStartResets = Resets;
                NextCheckpoint = 1;
            }
            else NextCheckpoint = (index + 1) % config.checkpointCount;
        }
        public void ResetExperiment(string reason)
        {
            if (!controller.HasStarted) return;
            logger.RecordEvent("attempt_end", reason + ";checkpoint=" + NextCheckpoint);
            Resets++;
            LastResetReason = reason;
            Attempt++;
            NextCheckpoint = 1;
            AttemptSeconds = offRoadTime = stuckTime = 0;
            lapMode = controller.Mode; mixedModeLap = false; lapPaused = false; lapStartResets = Resets;
            controller.Invalidate();
            vehicle.ResetPose(track.StartPosition, Quaternion.LookRotation(track.Tangent(0)));
            lastProgressPosition = vehicle.transform.position;
            logger.RecordEvent("reset", reason);
            if (Resets >= config.maximumAutomaticResets)
            { controller.SetPaused(true); logger.RecordEvent("reset_budget_reached", Resets.ToString()); }
            else logger.RecordEvent("attempt_start", Attempt.ToString());
        }
    }
}
