using System.Collections.Generic;
using UnityEngine;

namespace JevVision
{
    // Continuous ribbons eliminate wedge-shaped gaps on convex AND concave bends.
    public static class TrackSurface
    {
        public static void Create(TrackLayout track, Material[] materials, int segments)
        {
            Ribbon("Continuous asphalt", track, segments, 0, track.roadWidth, 0.03f, new[] { materials[1] });
            for (int side = -1; side <= 1; side += 2)
            {
                Ribbon("Continuous white edge", track, segments, side * (track.roadWidth * 0.5f - 0.25f), 0.18f, 0.055f, new[] { materials[2] });
                Ribbon("Red and white curb", track, segments, side * (track.roadWidth * 0.5f + 0.2f), 0.6f, 0.075f, new[] { materials[2], materials[3] });
            }
        }
        private static void Ribbon(string name, TrackLayout track, int count, float offset, float width, float height, Material[] materials)
        {
            var vertices = new Vector3[(count + 1) * 2];
            var normals = new Vector3[vertices.Length];
            var triangles = new List<int>[materials.Length];
            for (int i = 0; i < triangles.Length; i++) triangles[i] = new List<int>();
            for (int i = 0; i <= count; i++)
            {
                float s = i * track.Length / count;
                Vector3 center = track.Point(s), right = Vector3.Cross(Vector3.up, track.Tangent(s));
                vertices[i * 2] = track.transform.InverseTransformPoint(center + right * (offset - width * 0.5f) + Vector3.up * height);
                vertices[i * 2 + 1] = track.transform.InverseTransformPoint(center + right * (offset + width * 0.5f) + Vector3.up * height);
                normals[i * 2] = normals[i * 2 + 1] = Vector3.up;
                if (i == count) continue;
                var t = triangles[(i / 3) % materials.Length]; int a = i * 2;
                t.Add(a); t.Add(a + 2); t.Add(a + 1); t.Add(a + 1); t.Add(a + 2); t.Add(a + 3);
            }
            var mesh = new Mesh { name = name, vertices = vertices, normals = normals, subMeshCount = materials.Length };
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateBounds();
            var go = new GameObject(name); go.transform.SetParent(track.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }
    }
}
