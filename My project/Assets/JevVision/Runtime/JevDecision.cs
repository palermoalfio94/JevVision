using System;
using UnityEngine;

namespace JevVision
{
    [Serializable]
    public sealed class JevDecision
    {
        public string steering = "straight";
        public float steeringValue;
        public float throttle;
        public float brake = 1f;
        public float confidence = -1f;
        public string source = "stopped";
        public static JevDecision Stop(string source = "safety") => new JevDecision { source = source };
        public static bool TrySteering(string label, out float value)
        {
            switch (label)
            {
                case "strong_left": value = -1; return true;
                case "left": value = -0.5f; return true;
                case "slight_left": value = -0.2f; return true;
                case "straight": value = 0; return true;
                case "slight_right": value = 0.2f; return true;
                case "right": value = 0.5f; return true;
                case "strong_right": value = 1; return true;
                default: value = 0; return false;
            }
        }
        public static string Label(float value)
        {
            float magnitude = Mathf.Abs(value);
            return magnitude < 0.06f ? "straight" :
                (magnitude < 0.32f ? "slight_" : magnitude < 0.72f ? "" : "strong_") + (value < 0 ? "left" : "right");
        }
    }
}
