using UnityEngine;

namespace JevVision
{
    // Evaluation and scene construction only. Neither autonomous driver references this class.
    public sealed class TrackLayout : MonoBehaviour
    {
        public float radius = 24f;
        public float straightHalfLength = 18f;
        public float roadWidth = 12f;
        public float Length => 4 * straightHalfLength + 2 * Mathf.PI * radius;
        public Vector3 StartPosition => new Vector3(-radius, 0.08f, 0);
        public Vector3 Point(float distance)
        {
            float s = Mathf.Repeat(distance, Length);
            if (s < straightHalfLength) return new Vector3(-radius, 0, s);
            s -= straightHalfLength;
            if (s < Mathf.PI * radius)
            {
                float a = Mathf.PI - s / radius;
                return new Vector3(radius * Mathf.Cos(a), 0, straightHalfLength + radius * Mathf.Sin(a));
            }
            s -= Mathf.PI * radius;
            if (s < 2 * straightHalfLength) return new Vector3(radius, 0, straightHalfLength - s);
            s -= 2 * straightHalfLength;
            if (s < Mathf.PI * radius)
            {
                float a = -s / radius;
                return new Vector3(radius * Mathf.Cos(a), 0, -straightHalfLength + radius * Mathf.Sin(a));
            }
            s -= Mathf.PI * radius;
            return new Vector3(-radius, 0, -straightHalfLength + s);
        }
        public Vector3 Tangent(float s) => (Point(s + 0.05f) - Point(s - 0.05f)).normalized;
        public float DistanceFromCenterline(Vector3 worldPosition)
        {
            Vector3 p = transform.InverseTransformPoint(worldPosition);
            float centerZ = Mathf.Clamp(p.z, -straightHalfLength, straightHalfLength);
            return Mathf.Abs(new Vector2(p.x, p.z - centerZ).magnitude - radius);
        }
    }
}
