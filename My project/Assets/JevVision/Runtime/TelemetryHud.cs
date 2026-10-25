using System;
using UnityEngine;

namespace JevVision
{
    public sealed class TelemetryHud : MonoBehaviour
    {
        public ExperimentConfig config;
        public AutonomousController controller;
        public ExperimentManager experiment;
        public VehicleController vehicle;
        public CameraCapture capture;
        public TelemetryLogger logger;
        private GUIStyle title, label, small, warning;
        private bool showHelp;
        private void OnGUI()
        {
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.3f, 0.88f, 1) } };
                label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, normal = { textColor = Color.white } };
                small = new GUIStyle(label) { fontSize = 12, normal = { textColor = new Color(0.73f, 0.83f, 0.88f) } };
                warning = new GUIStyle(label) { normal = { textColor = new Color(1f, 0.76f, 0.27f) } };
            }
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 800f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float h = Screen.height / scale;
            GUI.color = new Color(0.04f, 0.07f, 0.12f, 0.94f);
            GUI.DrawTexture(new Rect(14, 14, 360, h - 28), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.BeginArea(new Rect(30, 28, 328, h - 54));
            GUILayout.Label("JEV / VISION LAB", title);
            GUILayout.Label("PIXELS → DECISION → VEHICLE", small);
            GUILayout.Space(12);
            GUILayout.Label(controller.Mode == DrivingMode.LocalVision ? "LOCAL VISION · OFFLINE" : controller.Mode == DrivingMode.JevVision ? "JEV VISION · API" : "MANUAL · KEYBOARD", title);
            if (controller.Mode == DrivingMode.LocalVision) GUILayout.Label("Baseline dai pixel. Nessuna chiamata JEV.", warning);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1 Manuale")) controller.SetMode(DrivingMode.Manual);
            if (GUILayout.Button("2 Locale")) controller.SetMode(DrivingMode.LocalVision);
            if (GUILayout.Button("3 JEV")) controller.SetMode(DrivingMode.JevVision);
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            var command = vehicle.Command;
            GUILayout.Label($"{vehicle.Speed * 3.6f:F1} km/h   |   {vehicle.Speed:F2} m/s", title);
            GUILayout.Label($"Sterzo  {command.steering}  ({vehicle.AppliedSteering:+0.00;-0.00;0.00})\nGas  {command.throttle:F2}     Freno  {command.brake:F2}\nConfidence  {(command.confidence < 0 ? "n/d" : command.confidence.ToString("F2"))}", label);
            GUILayout.Space(7);
            GUILayout.Label($"Giri  {experiment.Laps}  (locale {experiment.LocalLaps} / JEV {experiment.JevLaps})\nCheckpoint  {experiment.NextCheckpoint}/{config.checkpointCount}\nTempo  {experiment.Elapsed:F1}s   Tentativo  {experiment.AttemptSeconds:F1}s\nReset  {experiment.Resets}  ·  {experiment.LastResetReason}", label);
            GUILayout.Space(7);
            GUILayout.Label("API  " + controller.ApiStatus, warning);
            GUILayout.Label($"Chiamate  {controller.ApiCalls}   Errori  {controller.Failures}\nLatenza  {controller.LastLatencyMs:F0} ms   Media  {controller.AverageLatencyMs:F0} ms\nToken in/out  {controller.InputTokens}/{controller.OutputTokens}", small);
            double minutes = Math.Max(experiment.Elapsed / 60, 0.001);
            double perMinute = controller.EstimatedCost / minutes;
            GUILayout.Label($"{controller.ApiCalls / minutes:F1} call/min  ·  {(controller.InputTokens + controller.OutputTokens) / minutes:F0} token/min\nStima $ {perMinute:F6}/min · $ {perMinute * 60:F5}/h\nTotale $ {controller.EstimatedCost:F6}  ·  $/giro JEV {(experiment.JevLaps == 0 ? "n/d" : (controller.EstimatedCost / experiment.JevLaps).ToString("F6"))}", small);
            if (controller.UnmeteredCalls > 0) GUILayout.Label("Utilizzo parziale: risposte senza token.", warning);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(controller.Paused ? "Riprendi [P]" : "Pausa [P]")) controller.SetPaused(!controller.Paused);
            if (GUILayout.Button("Reset [R]")) experiment.ResetExperiment("user_reset");
            if (GUILayout.Button("Aiuto")) showHelp = !showHelp;
            GUILayout.EndHorizontal();
            if (showHelp) GUILayout.Label("W/↑ gas · S/↓/Spazio freno\nA/D o ←/→ sterzo · C vista\n1 manuale · 2 locale · 3 JEV\nLa camera non contiene HUD o checkpoint.", small);
            if (!string.IsNullOrEmpty(logger.LoggingError)) GUILayout.Label("LOG ERROR: " + logger.LoggingError, warning);
            GUILayout.EndArea();

            if (config.debugOverlay && capture.LastFrame != null)
            {
                float x = Screen.width / scale - 302;
                GUI.color = new Color(0.04f, 0.07f, 0.12f, 0.94f); GUI.DrawTexture(new Rect(x - 10, 14, 298, 393), Texture2D.whiteTexture); GUI.color = Color.white;
                GUI.Label(new Rect(x, 22, 280, 24), "FRAME EFFETTIVO / " + capture.CapturedAt.ToString("F2") + "s", small);
                GUI.DrawTexture(new Rect(x, 48, 280, 280), capture.LastFrame, ScaleMode.ScaleToFit);
                string detail = !string.IsNullOrEmpty(controller.LastError) ? controller.LastError : controller.LastResponse;
                if (detail.Length > 270) detail = detail.Substring(0, 270) + "…";
                GUI.Label(new Rect(x, 333, 280, 68), detail, small);
            }
        }
    }
}
