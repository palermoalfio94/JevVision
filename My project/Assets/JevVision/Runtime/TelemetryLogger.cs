using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace JevVision
{
    public sealed class TelemetryLogger : MonoBehaviour
    {
        public ExperimentConfig config;
        public AutonomousController controller;
        public ExperimentManager experiment;
        public VehicleController vehicle;
        public string SessionDirectory { get; private set; }
        public string LoggingError { get; private set; } = "";
        private StreamWriter samples, decisions, events;
        private double nextSample;
        private bool closed;
        [Serializable] private sealed class DecisionRecord
        {
            public string utc, mode, outcome, frameFile, requestFile;
            public double captureSeconds, receivedSeconds, latencyMs;
            public long httpStatus;
            public int attempt;
            public JevDecision decision;
            public JevProtocol.Response response;
            public string rawResponse, error;
        }
        private void Awake()
        {
            try
            {
                string destination = string.IsNullOrWhiteSpace(config.outputDirectory) ? Path.Combine(Application.persistentDataPath, "Runs") : config.outputDirectory;
                SessionDirectory = Path.Combine(destination, DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6));
                Directory.CreateDirectory(SessionDirectory);
                Directory.CreateDirectory(Path.Combine(SessionDirectory, "frames"));
                Directory.CreateDirectory(Path.Combine(SessionDirectory, "requests"));
                File.WriteAllText(Path.Combine(SessionDirectory, "config.json"), JsonUtility.ToJson(config, true));
                samples = Open("telemetry.csv"); decisions = Open("decisions.jsonl"); events = Open("events.csv");
                samples.WriteLine("utc,elapsed_s,attempt,mode,speed_m_s,steering,throttle,brake,confidence,api_status,api_calls,successful_calls,failures,discarded_responses,last_latency_ms,mean_latency_ms,input_tokens,output_tokens,unmetered_calls,estimated_usd,calls_per_min,tokens_per_min,usd_per_min,usd_per_hour,usd_per_jev_lap,resets,laps,jev_laps,manual_laps,next_checkpoint,centerline_error_m,source");
                events.WriteLine("utc,elapsed_s,event,detail");
                Debug.Log("JEV_VISION_LOGS " + SessionDirectory);
            }
            catch (Exception e) { LoggingError = e.GetType().Name + ": " + e.Message; Debug.LogError("Telemetry initialization: " + LoggingError); }
        }
        private StreamWriter Open(string name) => new StreamWriter(Path.Combine(SessionDirectory, name), false, new UTF8Encoding(false)) { AutoFlush = true };
        public void SaveConfiguration()
        {
            WriteSafely(() =>
            {
                if (!string.IsNullOrEmpty(SessionDirectory))
                    File.WriteAllText(Path.Combine(SessionDirectory, "config.json"), JsonUtility.ToJson(config, true));
            });
        }
        private void Update()
        {
            if (Time.realtimeSinceStartupAsDouble < nextSample || closed) return;
            nextSample = Time.realtimeSinceStartupAsDouble + 0.2;
            WriteSafely(() =>
            {
                if (samples == null) return;
                var c = vehicle.Command;
                double minutes = Math.Max(experiment.Elapsed / 60, 1.0 / 60000);
                double costMinute = controller.EstimatedCost / minutes;
                samples.WriteLine(string.Join(",", new[] {
                    Q(DateTime.UtcNow.ToString("O")), F(experiment.Elapsed), F(experiment.Attempt), Q(controller.Mode.ToString()), F(vehicle.Speed), F(vehicle.AppliedSteering), F(c.throttle), F(c.brake), F(c.confidence), Q(controller.ApiStatus),
                    F(controller.ApiCalls), F(controller.SuccessfulCalls), F(controller.Failures), F(controller.DiscardedResponses), F(controller.LastLatencyMs), F(controller.AverageLatencyMs),
                    F(controller.InputTokens), F(controller.OutputTokens), F(controller.UnmeteredCalls), F(controller.EstimatedCost), F(controller.ApiCalls / minutes), F((controller.InputTokens + controller.OutputTokens) / minutes),
                    F(costMinute), F(costMinute * 60), experiment.JevLaps > 0 ? F(controller.EstimatedCost / experiment.JevLaps) : "", F(experiment.Resets), F(experiment.Laps), F(experiment.JevLaps), F(experiment.ManualLaps), F(experiment.NextCheckpoint), F(experiment.CenterlineError), Q(c.source)
                }));
            });
        }
        public void RecordEvent(string kind, string detail)
        {
            if (closed) return;
            WriteSafely(() => events?.WriteLine(Q(DateTime.UtcNow.ToString("O")) + "," + F(experiment == null ? 0 : experiment.Elapsed) + "," + Q(kind) + "," + Q(detail)));
        }
        public void RecordDecision(JevDecision decision, byte[] jpeg, JevClient.Result result, string outcome, double capturedAt)
        {
            if (closed || decisions == null) return;
            WriteSafely(() =>
            {
                string frame = "";
                string requestFile = "";
                if (config.saveDecisionFrames)
                {
                    frame = "frames/" + controller.Mode + "_" + capturedAt.ToString("F6", CultureInfo.InvariantCulture) + ".jpg";
                    File.WriteAllBytes(Path.Combine(SessionDirectory, frame), jpeg);
                }
                if (!string.IsNullOrEmpty(result?.requestJson))
                {
                    requestFile = "requests/" + capturedAt.ToString("F6", CultureInfo.InvariantCulture) + ".json";
                    File.WriteAllText(Path.Combine(SessionDirectory, requestFile), result.requestJson);
                }
                var record = new DecisionRecord { utc = DateTime.UtcNow.ToString("O"), mode = controller.Mode.ToString(), outcome = outcome,
                    frameFile = frame, requestFile = requestFile, captureSeconds = capturedAt, receivedSeconds = Time.realtimeSinceStartupAsDouble,
                    attempt = experiment.Attempt, decision = decision, latencyMs = result?.latencyMs ?? 0,
                    httpStatus = result?.httpStatus ?? 0, response = result?.response, rawResponse = result?.raw ?? "", error = result?.error ?? "" };
                decisions.WriteLine(JsonUtility.ToJson(record));
            });
        }
        private void WriteSafely(Action write)
        {
            try { write(); }
            catch (Exception e) { LoggingError = e.GetType().Name + ": " + e.Message; }
        }
        public void Flush() { samples?.Flush(); decisions?.Flush(); events?.Flush(); }
        private static string F(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
        private static string Q(string text) => "\"" + (text ?? "").Replace("\"", "\"\"") + "\"";
        private void OnApplicationQuit() => Close();
        private void OnDestroy() => Close();
        private void Close()
        {
            if (closed) return; closed = true;
            samples?.Dispose(); decisions?.Dispose(); events?.Dispose();
        }
    }
}
