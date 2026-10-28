using System;
using System.Globalization;
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
        private bool setupInitialized;
        private DrivingMode selectedMode;
        private string speedText, frequencyText;
        private GUIStyle setupTitle, setupButton, numberField;
        private GUIStyle decisionValue, decisionHeading;
        private void OnGUI()
        {
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.3f, 0.88f, 1) } };
                label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, normal = { textColor = Color.white } };
                small = new GUIStyle(label) { fontSize = 12, normal = { textColor = new Color(0.73f, 0.83f, 0.88f) } };
                warning = new GUIStyle(label) { normal = { textColor = new Color(1f, 0.76f, 0.27f) } };
                setupTitle = new GUIStyle(title) { fontSize = 30 };
                setupButton = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
                numberField = new GUIStyle(GUI.skin.textField) { fontSize = 18, alignment = TextAnchor.MiddleLeft };
                decisionValue = new GUIStyle(label) { fontSize = 28, fontStyle = FontStyle.Bold, wordWrap = false };
                decisionHeading = new GUIStyle(title) { fontSize = 20 };
            }
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 800f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float h = Screen.height / scale;
            if (!controller.HasStarted)
            {
                DrawStartup(Screen.width / scale, h);
                return;
            }
            GUI.color = new Color(0.04f, 0.07f, 0.12f, 0.94f);
            GUI.DrawTexture(new Rect(14, 14, 360, h - 28), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.BeginArea(new Rect(30, 28, 328, h - 54));
            GUILayout.Label("JEV / VISION LAB", title);
            GUILayout.Label("CAMERA → JEV → AUTO", small);
            GUILayout.Space(12);
            if (!string.IsNullOrEmpty(experiment.SceneError)) GUILayout.Label(experiment.SceneError, warning);
            GUILayout.Label(controller.Mode == DrivingMode.JevVision ? "JEV VISION · API" : "MANUAL · KEYBOARD", title);
            GUILayout.Label($"Limite {config.maximumSpeed:F2} m/s · {config.maximumSpeed * 3.6f:F1} km/h" + (controller.Mode == DrivingMode.JevVision ? $"\nFrequenza richiesta {config.decisionsPerSecond:F2} call/s" : ""), small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1 Manuale")) controller.SetMode(DrivingMode.Manual);
            if (GUILayout.Button("2 JEV Vision")) controller.SetMode(DrivingMode.JevVision);
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            var command = vehicle.Command;
            GUILayout.Label($"{vehicle.Speed * 3.6f:F1} km/h   |   {vehicle.Speed:F2} m/s", title);
            GUILayout.Label($"Sterzo  {command.steering}  ({vehicle.AppliedSteering:+0.00;-0.00;0.00})\nGas  {command.throttle:F2}     Freno  {command.brake:F2}\nConfidence  {(command.confidence < 0 ? "n/d" : command.confidence.ToString("F2"))}", label);
            GUILayout.Space(7);
            GUILayout.Label($"Giri  {experiment.Laps}  (JEV {experiment.JevLaps} / manuali {experiment.ManualLaps})\nCheckpoint  {experiment.NextCheckpoint}/{config.checkpointCount}\nTempo  {experiment.Elapsed:F1}s   Tentativo  {experiment.AttemptSeconds:F1}s\nReset  {experiment.Resets}  ·  {experiment.LastResetReason}", label);
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
            if (showHelp) GUILayout.Label("W/↑ gas · S/↓/Spazio freno\nA/D o ←/→ sterzo · C vista\n1 manuale · 2 JEV (anche 3)\nLa camera non contiene HUD o checkpoint.", small);
            if (!string.IsNullOrEmpty(logger.LoggingError)) GUILayout.Label("LOG ERROR: " + logger.LoggingError, warning);
            GUILayout.EndArea();

            DrawJevDecisions(Screen.width / scale);
            if (config.debugOverlay && capture.LastFrame != null)
            {
                float x = Screen.width / scale - 302;
                const float top = 270;
                GUI.color = new Color(0.04f, 0.07f, 0.12f, 0.94f); GUI.DrawTexture(new Rect(x - 10, top, 298, 393), Texture2D.whiteTexture); GUI.color = Color.white;
                GUI.Label(new Rect(x, top + 8, 280, 24), "FRAME EFFETTIVO / " + capture.CapturedAt.ToString("F2") + "s", small);
                GUI.DrawTexture(new Rect(x, top + 34, 280, 280), capture.LastFrame, ScaleMode.ScaleToFit);
                string detail = !string.IsNullOrEmpty(controller.LastError) ? controller.LastError : controller.LastResponse;
                if (detail.Length > 270) detail = detail.Substring(0, 270) + "…";
                GUI.Label(new Rect(x, top + 319, 280, 68), detail, small);
            }
        }

        private void DrawJevDecisions(float screenWidth)
        {
            float x = screenWidth - 464;
            const float y = 14;
            bool manual = controller.Mode == DrivingMode.Manual;
            var answers = manual ? null : controller.LastJevAnswers;
            bool applied = !manual && !controller.Paused && answers != null &&
                controller.LastJevDecisionAge <= config.maximumDecisionAgeSeconds && vehicle.Command.source == "jev";
            GUI.color = new Color(0.025f, 0.045f, 0.075f, 0.97f);
            GUI.DrawTexture(new Rect(x, y, 450, 242), Texture2D.whiteTexture);
            GUI.color = applied ? new Color(0.3f, 0.88f, 1) : new Color(1, 0.76f, 0.27f);
            GUI.DrawTexture(new Rect(x, y, 450, 4), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 18, y + 16, 414, 28), "DECISIONI JEV", decisionHeading);
            DrawDecisionRow(x, y + 54, "STERZO", SteeringText(answers?.steering?.choice), applied ? Color.white : Color.gray);
            DrawDecisionRow(x, y + 98, "GAS", ThrottleText(answers?.throttle?.choice), applied ? Color.white : Color.gray);
            string brake = answers == null ? "—" : answers.brake.choice == "apply" ? "SÌ · FRENA" : "NO";
            DrawDecisionRow(x, y + 142, "FRENO", brake, applied && answers.brake.choice == "apply" ? new Color(1, 0.35f, 0.25f) : applied ? Color.white : Color.gray);
            string status = manual ? "MANUALE · nessuna decisione JEV" : controller.Paused ? "IN PAUSA · auto frenata" :
                answers == null ? "IN ATTESA · " + controller.ApiStatus :
                applied ? $"APPLICATA · età frame {controller.LastJevDecisionAge:F1} s" : "NON APPLICATA · frenata di sicurezza";
            GUI.Label(new Rect(x + 18, y + 198, 414, 36), status, applied ? small : warning);
        }

        private void DrawDecisionRow(float x, float y, string name, string value, Color color)
        {
            GUI.Label(new Rect(x + 18, y + 8, 88, 28), name, small);
            GUI.color = color;
            GUI.Label(new Rect(x + 110, y, 324, 40), value, decisionValue);
            GUI.color = Color.white;
        }

        private static string SteeringText(string choice)
        {
            switch (choice)
            {
                case "strong_left": return "FORTE A SINISTRA";
                case "left": return "SINISTRA";
                case "slight_left": return "POCO A SINISTRA";
                case "straight": return "DRITTO";
                case "slight_right": return "POCO A DESTRA";
                case "right": return "DESTRA";
                case "strong_right": return "FORTE A DESTRA";
                default: return "—";
            }
        }

        private static string ThrottleText(string choice)
        {
            switch (choice)
            {
                case "coast": return "RILASCIA";
                case "slow": return "LEGGERO";
                case "cruise": return "MODERATO";
                default: return "—";
            }
        }

        private void DrawStartup(float width, float height)
        {
            if (!setupInitialized)
            {
                selectedMode = config.startMode;
                speedText = config.maximumSpeed.ToString("0.##", CultureInfo.InvariantCulture);
                frequencyText = config.decisionsPerSecond.ToString("0.##", CultureInfo.InvariantCulture);
                setupInitialized = true;
            }
            GUI.color = new Color(0.015f, 0.025f, 0.04f, 0.72f);
            GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
            float x = (width - 640) / 2, y = (height - 660) / 2;
            GUI.color = new Color(0.04f, 0.07f, 0.12f, 0.98f);
            GUI.DrawTexture(new Rect(x, y, 640, 660), Texture2D.whiteTexture);
            GUI.color = new Color(0.3f, 0.88f, 1);
            GUI.DrawTexture(new Rect(x, y, 640, 4), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.BeginArea(new Rect(x + 32, y + 26, 576, 610));
            GUILayout.Label("PREPARA LA GUIDA", setupTitle);
            GUILayout.Label("Scegli le impostazioni, poi premi PLAY per partire.", label);
            GUILayout.Space(18);
            GUILayout.Label("MODALITÀ", small);
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = selectedMode == DrivingMode.Manual ? new Color(0.3f, 0.88f, 1) : Color.white;
            if (GUILayout.Button(selectedMode == DrivingMode.Manual ? "✓ Manuale" : "Manuale", setupButton, GUILayout.Height(44))) selectedMode = DrivingMode.Manual;
            GUI.backgroundColor = selectedMode == DrivingMode.JevVision ? new Color(0.3f, 0.88f, 1) : Color.white;
            if (GUILayout.Button(selectedMode == DrivingMode.JevVision ? "✓ JEV Vision" : "JEV Vision", setupButton, GUILayout.Height(44))) selectedMode = DrivingMode.JevVision;
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.Space(16);
            bool speedValid = ReadNumber(speedText, 0.5f, 8f, out float speed);
            GUILayout.Label("VELOCITÀ MASSIMA", small);
            GUILayout.BeginHorizontal();
            float speedSlider = GUILayout.HorizontalSlider(speedValid ? speed : config.maximumSpeed, 0.5f, 8f, GUILayout.Width(360));
            if (Mathf.Abs(speedSlider - (speedValid ? speed : config.maximumSpeed)) > 0.005f) speedText = speedSlider.ToString("0.##", CultureInfo.InvariantCulture);
            speedText = GUILayout.TextField(speedText, 8, numberField, GUILayout.Width(100), GUILayout.Height(30));
            GUILayout.Label("m/s", label, GUILayout.Width(45));
            GUILayout.EndHorizontal();
            speedValid = ReadNumber(speedText, 0.5f, 8f, out speed);
            GUILayout.Label(speedValid ? $"{speed * 3.6f:F1} km/h · intervallo 0,5–8 m/s" : "Inserisci un valore tra 0,5 e 8 m/s.", speedValid ? small : warning);
            GUILayout.Space(16);
            GUILayout.Label("FREQUENZA CHIAMATE JEV", small);
            GUI.enabled = selectedMode == DrivingMode.JevVision;
            bool frequencyValid = ReadNumber(frequencyText, 0.2f, 10f, out float frequency);
            GUILayout.BeginHorizontal();
            float frequencySlider = GUILayout.HorizontalSlider(frequencyValid ? frequency : config.decisionsPerSecond, 0.2f, 10f, GUILayout.Width(360));
            if (Mathf.Abs(frequencySlider - (frequencyValid ? frequency : config.decisionsPerSecond)) > 0.005f) frequencyText = frequencySlider.ToString("0.##", CultureInfo.InvariantCulture);
            frequencyText = GUILayout.TextField(frequencyText, 8, numberField, GUILayout.Width(100), GUILayout.Height(30));
            GUILayout.Label("call/s", label, GUILayout.Width(55));
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            frequencyValid = ReadNumber(frequencyText, 0.2f, 10f, out frequency);
            if (selectedMode == DrivingMode.Manual) GUILayout.Label("In manuale: nessuna chiamata API. W/A/S/D o frecce per guidare.", small);
            else if (!frequencyValid) GUILayout.Label("Inserisci un valore tra 0,2 e 10 chiamate al secondo.", warning);
            else GUILayout.Label($"Un'immagine ogni {1f / frequency:F2} s. Una richiesta alla volta: la latenza può ridurre la frequenza effettiva.", small);
            GUILayout.Space(20);
            GUILayout.Label("AUTO FERMA · IN ATTESA DI PLAY", title);
            GUILayout.Label("Prima di Play non parte alcuna chiamata API.", label);
            if (selectedMode == DrivingMode.JevVision)
                GUILayout.Label(controller.client.HasKey ? "JEV usa la chiave configurata e può consumare credito." : "Chiave JEV non configurata: il veicolo resterà frenato.", warning);
            if (!string.IsNullOrEmpty(experiment.SceneError)) GUILayout.Label(experiment.SceneError, warning);
            GUILayout.Space(16);
            GUI.enabled = speedValid && (selectedMode == DrivingMode.Manual || frequencyValid) && experiment.track.GeometryMatchesSettings;
            GUI.backgroundColor = new Color(0.3f, 0.88f, 1);
            if (GUILayout.Button("PLAY  ·  AVVIA GUIDA", setupButton, GUILayout.Height(54)))
            {
                GUI.FocusControl(null);
                experiment.StartDriving(selectedMode, frequencyValid ? frequency : config.decisionsPerSecond, speed);
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private static bool ReadNumber(string text, float minimum, float maximum, out float value)
        {
            return float.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;
        }
    }
}
