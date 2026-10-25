using UnityEngine;

namespace JevVision
{
    // Explicit offline baseline: pixels + fixed camera calibration, no simulation state.
    public static class LocalVisionDriver
    {
        public static JevDecision Decide(Texture2D frame, ExperimentConfig config)
        {
            Color32[] pixels = frame.GetPixels32();
            int width = frame.width, height = frame.height;
            int y = Mathf.Clamp(Mathf.RoundToInt(height * config.baselineScanRow), 0, height - 1);
            int bestLeft = -1, bestRight = -1, runLeft = -1, lastRoad = -1;
            float bestScore = -1;
            int gap = Mathf.Max(2, width / 30);
            for (int x = 0; x <= width + gap; x++)
            {
                bool road = x < width && IsRoad(pixels[y * width + x]);
                if (road)
                {
                    if (runLeft < 0) runLeft = x;
                    lastRoad = x;
                }
                if (runLeft >= 0 && x - lastRoad > gap)
                {
                    float center = (runLeft + lastRoad) * 0.5f;
                    float score = lastRoad - runLeft - Mathf.Abs(center - width * 0.5f) * 0.35f;
                    if (score > bestScore) { bestScore = score; bestLeft = runLeft; bestRight = lastRoad; }
                    runLeft = -1;
                }
            }
            if (bestLeft < 0 || bestRight - bestLeft < width * 0.07f) return JevDecision.Stop("local_pixels_road_lost");
            float nx = ((bestLeft + bestRight) * 0.5f / (width - 1) - 0.5f) * 2;
            float ny = (y / (float)(height - 1) - 0.5f) * 2;
            float tangent = Mathf.Tan(config.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Vector3 ray = Quaternion.Euler(config.cameraPitch, 0, 0) * new Vector3(nx * tangent * width / height, ny * tangent, 1);
            if (ray.y >= -0.01f) return JevDecision.Stop("local_pixels_horizon");
            Vector3 target = ray * (-config.cameraHeight / ray.y);
            // Camera is 1.7m in front of the body; pure pursuit in car-local ground space.
            target.z += 1.7f;
            float angle = Mathf.Atan2(2f * config.wheelbase * target.x, target.x * target.x + target.z * target.z);
            float steering = Mathf.Clamp(angle * Mathf.Rad2Deg / config.maximumSteeringDegrees * config.baselineSteeringGain, -1, 1);
            return new JevDecision { steering = JevDecision.Label(steering), steeringValue = steering, throttle = 0.65f, brake = 0, source = "local_pixels", confidence = -1 };
        }
        private static bool IsRoad(Color32 c)
        {
            int maximum = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            int minimum = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            return maximum - minimum < 20 && maximum > 20 && maximum < 145;
        }
    }
}
