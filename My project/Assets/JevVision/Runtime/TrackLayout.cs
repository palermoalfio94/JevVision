using UnityEngine;

namespace JevVision
{
    // Evaluation and scene construction only. Neither autonomous driver references this class.
    public sealed class TrackLayout : MonoBehaviour
    {
        public float roadWidth = 12f;
        [Tooltip("Closed cubic B-spline control polygon, approximating the supplied outline. Evaluation/scene construction only.")]
        public Vector3[] controlPoints = DefaultControlPoints();
        [Range(16, 96)] public int samplesPerSpan = 48;
        [SerializeField, HideInInspector] private int generatedLayoutSignature;
        private Vector3[] points;
        private float[] distances;
        public float Length { get { EnsureSamples(); return distances[distances.Length - 1]; } }
        public Vector3 StartPosition => Point(0) + Vector3.up * 0.08f;
        public bool GeometryMatchesSettings => generatedLayoutSignature != 0 && generatedLayoutSignature == ShapeSignature();
        public void MarkGeometryGenerated() => generatedLayoutSignature = ShapeSignature();
        private int ShapeSignature()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Mathf.RoundToInt(roadWidth * 1000);
                hash = hash * 31 + samplesPerSpan;
                if (controlPoints == null) return 0;
                foreach (Vector3 point in controlPoints)
                {
                    hash = hash * 31 + Mathf.RoundToInt(point.x * 1000);
                    hash = hash * 31 + Mathf.RoundToInt(point.z * 1000);
                }
                return hash == 0 ? 1 : hash;
            }
        }

        private static Vector3[] DefaultControlPoints()
        {
            // Drawing coordinates are used only to author the shape, never by a driving controller.
            Vector2[] outline = {
                new Vector2(300,408), new Vector2(239,410), new Vector2(175,385), new Vector2(124,356),
                new Vector2(99,309), new Vector2(79,248), new Vector2(106,216), new Vector2(160,176),
                new Vector2(148,103), new Vector2(247,75), new Vector2(304,75), new Vector2(351,98),
                new Vector2(396,144), new Vector2(455,236), new Vector2(500,256), new Vector2(560,265),
                new Vector2(655,255), new Vector2(696,272), new Vector2(723,309), new Vector2(718,375),
                new Vector2(705,461), new Vector2(687,510), new Vector2(620,542), new Vector2(540,558),
                new Vector2(474,558), new Vector2(437,503), new Vector2(410,435), new Vector2(381,411)
            };
            var result = new Vector3[outline.Length];
            for (int i = 0; i < result.Length; i++) result[i] = new Vector3((outline[i].x - 400) / 3.7f, 0, (320 - outline[i].y) / 3.7f);
            return result;
        }
        private void OnValidate() { points = null; distances = null; }
        private void EnsureSamples()
        {
            if (points != null) return;
            if (controlPoints == null || controlPoints.Length < 4) controlPoints = DefaultControlPoints();
            int steps = controlPoints.Length * Mathf.Clamp(samplesPerSpan, 16, 96);
            points = new Vector3[steps + 1]; distances = new float[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps * controlPoints.Length;
                int span = Mathf.FloorToInt(t);
                float u = t - span;
                int n = controlPoints.Length;
                Vector3 a = controlPoints[(span + n - 1) % n], b = controlPoints[span % n];
                Vector3 c = controlPoints[(span + 1) % n], d = controlPoints[(span + 2) % n];
                float u2 = u * u, u3 = u2 * u;
                points[i] = (a * Mathf.Pow(1 - u, 3) + b * (3 * u3 - 6 * u2 + 4) +
                    c * (-3 * u3 + 3 * u2 + 3 * u + 1) + d * u3) / 6;
                if (i > 0) distances[i] = distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            }
        }
        public Vector3 Point(float distance)
        {
            EnsureSamples();
            float s = Mathf.Repeat(distance, Length);
            int lo = 0, hi = distances.Length - 1;
            while (lo + 1 < hi) { int mid = (lo + hi) / 2; if (distances[mid] <= s) lo = mid; else hi = mid; }
            return transform.TransformPoint(Vector3.Lerp(points[lo], points[hi], Mathf.InverseLerp(distances[lo], distances[hi], s)));
        }
        public Vector3 Tangent(float s) => (Point(s + 0.05f) - Point(s - 0.05f)).normalized;
        public float DistanceFromCenterline(Vector3 worldPosition)
        {
            Vector3 p = transform.InverseTransformPoint(worldPosition);
            p.y = 0;
            EnsureSamples();
            float closest = float.PositiveInfinity;
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 ab = points[i + 1] - points[i];
                float t = Mathf.Clamp01(Vector3.Dot(p - points[i], ab) / Mathf.Max(ab.sqrMagnitude, 0.000001f));
                closest = Mathf.Min(closest, (p - points[i] - ab * t).sqrMagnitude);
            }
            return Mathf.Sqrt(closest);
        }
    }
}
