using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace JevVision
{
    public sealed class JevClient : MonoBehaviour
    {
        public ExperimentConfig config;
        public sealed class Result
        {
            public bool success;
            public JevDecision decision;
            public JevProtocol.Response response;
            public string raw = "";
            public string requestJson = "";
            public string error = "";
            public long httpStatus;
            public double latencyMs;
            public double retryAfter;
        }
        private UnityWebRequest current;
        public bool Busy => current != null;
        public string GetKey()
        {
            string key = Environment.GetEnvironmentVariable(config.apiKeyEnvironmentVariable);
            // User-scoped lookup allows configuration after launching the editor; no secret is stored in Assets.
            if (string.IsNullOrWhiteSpace(key))
                try { key = Environment.GetEnvironmentVariable(config.apiKeyEnvironmentVariable, EnvironmentVariableTarget.User); }
                catch (PlatformNotSupportedException) { }
            return key;
        }
        public bool HasKey => !string.IsNullOrWhiteSpace(GetKey());
        public string Redact(string value)
        {
            string key = GetKey();
            return !string.IsNullOrEmpty(key) && value != null ? value.Replace(key, "[REDACTED]") : value;
        }
        public IEnumerator Decide(byte[] jpeg, Action<Result> completed)
        {
            var result = new Result();
            string key = GetKey();
            if (string.IsNullOrWhiteSpace(key)) { result.error = "Missing " + config.apiKeyEnvironmentVariable; completed(result); yield break; }
            if (!Uri.TryCreate(config.endpoint, UriKind.Absolute, out Uri uri) || uri.Scheme != "https")
            { result.error = "API endpoint must use HTTPS."; completed(result); yield break; }
            if (Busy) { result.error = "Request already in flight."; completed(result); yield break; }
            result.requestJson = JevProtocol.BuildRequest(jpeg, config.model);
            byte[] body = Encoding.UTF8.GetBytes(result.requestJson);
            double start = Time.realtimeSinceStartupAsDouble;
            using (var request = new UnityWebRequest(config.endpoint, "POST"))
            {
                current = request;
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + key.Trim());
                request.SetRequestHeader("TypeSafe-Danger-Zone-Images", "true");
                request.timeout = config.timeoutSeconds;
                request.redirectLimit = 0;
                yield return request.SendWebRequest();
                result.latencyMs = (Time.realtimeSinceStartupAsDouble - start) * 1000;
                result.httpStatus = request.responseCode;
                result.raw = Redact(request.downloadHandler.text);
                string retry = request.GetResponseHeader("Retry-After");
                if (double.TryParse(retry, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)) result.retryAfter = Math.Max(0, seconds);
                else if (DateTimeOffset.TryParse(retry, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset until))
                    result.retryAfter = Math.Max(0, (until - DateTimeOffset.UtcNow).TotalSeconds);
                if (request.result == UnityWebRequest.Result.Success)
                    result.success = JevProtocol.TryParse(result.raw, out result.decision, out result.response, out result.error);
                else result.error = "HTTP " + result.httpStatus + ": " + request.error;
                current = null;
            }
            completed(result);
        }
        public void Cancel() { if (current != null) current.Abort(); }
        private void OnDestroy() => Cancel();
    }
}
