using System;
using UnityEngine;

namespace JevVision
{
    // Wire contract verified against https://api.typesafe.ai/openapi.json (2026-10-25).
    public static class JevProtocol
    {
        [Serializable] private sealed class State
        {
            public string camera;
            public string task = "Drive from the front-camera image only. The dashed yellow line marks the track center and desired driving path. Stay on the road, anticipate bends, avoid collisions, prefer safety.";
        }
        [Serializable] private sealed class SteeringCriteria
        {
            public string strong_left = "Steer sharply left.";
            public string left = "Steer left.";
            public string slight_left = "Steer gently left.";
            public string straight = "Keep the wheels straight.";
            public string slight_right = "Steer gently right.";
            public string right = "Steer right.";
            public string strong_right = "Steer sharply right.";
        }
        [Serializable] private sealed class ThrottleCriteria
        {
            public string coast = "No acceleration: road is unsafe or unclear.";
            public string slow = "Accelerate gently for a bend or caution.";
            public string cruise = "Accelerate moderately: road ahead is clear.";
        }
        [Serializable] private sealed class SteeringQuestion
        {
            public string type = "choice";
            public string instructions = "Choose steering to follow the dashed yellow centerline and its upcoming curvature in `camera`. Use several consecutive dashes and steer smoothly. If markings are unclear, use the road edges.";
            public SteeringCriteria criteria = new SteeringCriteria();
        }
        [Serializable] private sealed class ThrottleQuestion
        {
            public string type = "choice";
            public string instructions = "Choose safe acceleration from `camera`.";
            public ThrottleCriteria criteria = new ThrottleCriteria();
        }
        [Serializable] private sealed class Questions
        {
            public SteeringQuestion steering = new SteeringQuestion();
            public ThrottleQuestion throttle = new ThrottleQuestion();
        }
        [Serializable] private sealed class Request
        {
            public string model;
            public State state;
            public Questions questions = new Questions();
        }
        [Serializable] private sealed class Asset
        {
            public string type = "image";
            public string format = "jpg";
            public string data_base64;
        }
        [Serializable] public sealed class ChoiceAnswer
        {
            public string type;
            public string choice;
            public float confidence = -1;
        }
        [Serializable] public sealed class Answers
        {
            public ChoiceAnswer steering;
            public ChoiceAnswer throttle;
        }
        [Serializable] public sealed class Usage
        {
            public long input_tokens = -1;
            public long output_tokens = -1;
        }
        [Serializable] public sealed class Response
        {
            public string model;
            public Answers answers;
            public Usage usage;
        }
        public static string BuildRequest(byte[] jpeg, string model)
        {
            if (jpeg == null || jpeg.Length == 0) throw new ArgumentException("Camera JPEG is empty.");
            string reference = Guid.NewGuid().ToString();
            var request = new Request { model = model, state = new State { camera = reference } };
            string json = JsonUtility.ToJson(request);
            return json.Substring(0, json.Length - 1) + ",\"assets\":{\"" + reference + "\":" +
                JsonUtility.ToJson(new Asset { data_base64 = Convert.ToBase64String(jpeg) }) + "}}";
        }
        public static bool TryParse(string json, out JevDecision decision, out Response response, out string error)
        {
            decision = null; response = null; error = "Invalid structured response.";
            try
            {
                response = JsonUtility.FromJson<Response>(json);
                Answers a = response?.answers;
                if (a == null || !ValidChoice(a.steering) || !ValidChoice(a.throttle)) return false;
                if (!JevDecision.TrySteering(a.steering.choice, out float steer)) { error = "Unknown steering label."; return false; }
                float throttle;
                switch (a.throttle.choice)
                {
                    case "coast": throttle = 0; break;
                    case "slow": throttle = 0.35f; break;
                    case "cruise": throttle = 0.7f; break;
                    default: error = "Unknown throttle label."; return false;
                }
                float confidence = a.steering.confidence >= 0 && a.throttle.confidence >= 0
                    ? Mathf.Min(a.steering.confidence, a.throttle.confidence) : -1;
                decision = new JevDecision { steering = a.steering.choice, steeringValue = steer,
                    throttle = throttle, confidence = confidence, source = "jev" };
                error = "";
                return true;
            }
            catch (Exception e) { error = "Response JSON: " + e.GetType().Name; return false; }
        }
        private static bool ValidChoice(ChoiceAnswer a) => a != null && a.type == "choice" && !string.IsNullOrEmpty(a.choice)
            && !float.IsNaN(a.confidence) && !float.IsInfinity(a.confidence) && (a.confidence == -1 || a.confidence >= 0 && a.confidence <= 1);
    }
}
