using UnityEngine;

namespace JevVision
{
    // Keep the serialized value of JevVision; value 1 belonged to a removed mode.
    public enum DrivingMode { Manual = 0, JevVision = 2 }

    [CreateAssetMenu(menuName = "JEV Vision/Experiment settings")]
    public sealed class ExperimentConfig : ScriptableObject
    {
        [Header("Start and control")]
        public DrivingMode startMode = DrivingMode.JevVision;
        [Range(0.2f, 10f)] public float decisionsPerSecond = 2f;
        [Range(0.5f, 8f)] public float maximumSpeed = 1.2f;
        [Range(5f, 40f)] public float maximumSteeringDegrees = 24f;
        public float acceleration = 2.5f;
        public float wheelbase = 2.6f;
        public float steeringSlewPerSecond = 3f;
        [Range(0f, 1f)] public float throttleScale = 0.7f;

        [Header("Front camera / actual API frame")]
        [Range(280, 1568)] public int imageWidth = 280;
        [Range(280, 1568)] public int imageHeight = 280;
        [Range(20, 100)] public int jpegQuality = 80;
        [Range(40, 100)] public float fieldOfView = 65f;
        public float cameraHeight = 1.5f;
        public float cameraPitch = 8f;

        [Header("TypeSafe official API (experimental images)")]
        public string endpoint = "https://api.typesafe.ai/v1/systemone";
        public string model = "jev-1.14.0";
        public string apiKeyEnvironmentVariable = "TYPESAFE_API_KEY";
        [Range(1, 60)] public int timeoutSeconds = 8;
        public float maximumDecisionAgeSeconds = 2f;
        public float maximumBackoffSeconds = 30f;
        public int maximumApiCallsPerSession = 600;
        public float maximumEstimatedCostUsd = 0.05f;

        [Header("Central price card — TypeSafe models, checked 2026-10-28")]
        public double inputUsdPerMillionTokens = 0.042;
        public double outputUsdPerMillionTokens = 0;
        public string pricingSource = "https://docs.typesafe.ai/models";
        public string pricingChecked = "2026-10-28";

        [Header("Evaluation only — never included in the model request")]
        public float offTrackGraceSeconds = 1.5f;
        public float stuckSeconds = 12f;
        public float attemptTimeoutSeconds = 600f;
        public float severeCollisionImpulse = 1200f;
        public int checkpointCount = 16;
        public int maximumAutomaticResets = 20;

        [Header("Logging")]
        public bool saveDecisionFrames = true;
        public bool debugOverlay = true;
        public string outputDirectory = "";

        public void Validate()
        {
            if (startMode != DrivingMode.Manual && startMode != DrivingMode.JevVision) startMode = DrivingMode.Manual;
            imageWidth = Mathf.Clamp(imageWidth, 280, 1568);
            imageHeight = Mathf.Clamp(imageHeight, 280, 1568);
            decisionsPerSecond = Mathf.Clamp(decisionsPerSecond, 0.2f, 10f);
            timeoutSeconds = Mathf.Clamp(timeoutSeconds, 1, 60);
            maximumSpeed = Mathf.Clamp(maximumSpeed, 0.5f, 8f);
            maximumDecisionAgeSeconds = Mathf.Max(0.25f, maximumDecisionAgeSeconds);
            checkpointCount = Mathf.Max(8, checkpointCount);
        }
    }
}
