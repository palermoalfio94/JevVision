using System.Collections.Generic;
using UnityEngine;

namespace JevVision
{
    public static class SceneFactory
    {
        // Palette: grass, asphalt, white, red, rail, body, glass, tyre, yellow.
        public static GameObject Build(ExperimentConfig config, Material[] m)
        {
            var root = new GameObject("JEV VISION — camera-only driving laboratory");
            var track = new GameObject("Circuit — evaluation geometry only").AddComponent<TrackLayout>();
            track.transform.SetParent(root.transform);
            Cube("Ground", root.transform, new Vector3(0, -0.25f, 0), new Vector3(260, 0.5f, 220), Quaternion.identity, m[0], true);
            int segments = Mathf.CeilToInt(track.Length / 0.8f);
            TrackSurface.Create(track, m, segments);
            for (int i = 0; i < segments; i++)
            {
                float s = i * track.Length / segments;
                Vector3 a = track.Point(s), b = track.Point(s + track.Length / segments);
                Vector3 center = (a + b) * 0.5f;
                Vector3 forward = (b - a).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                float turn = Vector3.Angle(track.Tangent(s), track.Tangent(s + track.Length / segments)) * Mathf.Deg2Rad;
                float length = Vector3.Distance(a, b) + 0.05f + turn * (track.roadWidth * 0.5f + 1.3f);
                Quaternion rotation = Quaternion.LookRotation(forward);
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject barrier = Cube("Visible safety barrier", track.transform, center + right * (side * (track.roadWidth * 0.5f + 1.1f)) + Vector3.up * 0.75f,
                        new Vector3(0.22f, 1.5f, length), rotation, m[4], true);
                    barrier.tag = "Respawn";
                }
            }
            // Start stripe and center dashes are visual cues only.
            for (int x = 0; x < 12; x++)
                Cube("Start grid", track.transform, track.Point(0) + Vector3.Cross(Vector3.up, track.Tangent(0)) * (x - 5.5f) + Vector3.up * 0.085f,
                    new Vector3(0.95f, 0.02f, 0.65f), Quaternion.LookRotation(track.Tangent(0)), x % 2 == 0 ? m[2] : m[1], false);
            AddCenterDashes(track, m[8]);

            var car = new GameObject("Vehicle — Rigidbody bicycle controller");
            car.transform.SetParent(root.transform);
            car.transform.position = track.StartPosition;
            car.transform.rotation = Quaternion.LookRotation(track.Tangent(0));
            var body = car.AddComponent<Rigidbody>();
            body.mass = 900; body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.centerOfMass = new Vector3(0, 0.4f, 0);
            var collider = car.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, 0.55f, 0); collider.size = new Vector3(1.8f, 1.1f, 3.6f);
            var friction = new PhysicsMaterial("Car sliding contact") { dynamicFriction = 0, staticFriction = 0, bounciness = 0, frictionCombine = PhysicsMaterialCombine.Minimum };
            collider.sharedMaterial = friction;
            var vehicle = car.AddComponent<VehicleController>(); vehicle.config = config;
            Cube("Blue body", car.transform, new Vector3(0, 0.65f, 0), new Vector3(1.8f, 0.6f, 3.6f), Quaternion.identity, m[5], false, true);
            Cube("Cabin", car.transform, new Vector3(0, 1.16f, -0.25f), new Vector3(1.5f, 0.55f, 1.8f), Quaternion.identity, m[6], false, true);
            Cube("Front camera marker", car.transform, new Vector3(0, config.cameraHeight, 1.7f), new Vector3(0.25f, 0.18f, 0.2f), Quaternion.identity, m[3], false, true);
            vehicle.visualWheels = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = i < 2 ? "Steering wheel" : "Rear wheel";
                wheel.transform.SetParent(car.transform, false);
                wheel.transform.localPosition = new Vector3(i % 2 == 0 ? -0.94f : 0.94f, 0.38f, i < 2 ? 1.25f : -1.25f);
                wheel.transform.localScale = new Vector3(0.76f, 0.15f, 0.76f);
                wheel.transform.localRotation = Quaternion.Euler(0, 0, 90);
                wheel.GetComponent<Renderer>().sharedMaterial = m[7];
                Remove(wheel.GetComponent<Collider>()); wheel.layer = 8;
                vehicle.visualWheels[i] = wheel.transform;
            }
            var front = new GameObject("FrontCamera — ONLY MODEL SENSOR").AddComponent<Camera>();
            front.transform.SetParent(car.transform, false);
            front.transform.localPosition = new Vector3(0, config.cameraHeight, 1.7f);
            front.transform.localRotation = Quaternion.Euler(config.cameraPitch, 0, 0);
            front.fieldOfView = config.fieldOfView; front.nearClipPlane = 0.05f; front.farClipPlane = 180;
            front.clearFlags = CameraClearFlags.SolidColor; front.backgroundColor = new Color(0.49f, 0.69f, 0.86f);
            front.cullingMask = ~(1 << 8); front.depth = -2;
            var capture = car.AddComponent<CameraCapture>(); capture.frontCamera = front; capture.config = config;

            var services = new GameObject("Experiment services"); services.transform.SetParent(root.transform);
            var client = services.AddComponent<JevClient>(); client.config = config;
            var logger = services.AddComponent<TelemetryLogger>(); logger.config = config;
            var control = services.AddComponent<AutonomousController>();
            control.config = config; control.capture = capture; control.vehicle = vehicle; control.client = client; control.logger = logger;
            var experiment = services.AddComponent<ExperimentManager>();
            experiment.config = config; experiment.track = track; experiment.vehicle = vehicle; experiment.controller = control; experiment.logger = logger;
            logger.controller = control; logger.experiment = experiment; logger.vehicle = vehicle;
            var hud = services.AddComponent<TelemetryHud>(); hud.config = config; hud.controller = control; hud.capture = capture; hud.experiment = experiment; hud.vehicle = vehicle; hud.logger = logger;
            var smoke = services.AddComponent<RuntimeValidation>(); smoke.config = config; smoke.experiment = experiment; smoke.controller = control; smoke.capture = capture; smoke.vehicle = vehicle; smoke.logger = logger;

            for (int i = 0; i < config.checkpointCount; i++)
            {
                float s = track.Length * i / config.checkpointCount;
                var gate = new GameObject("Evaluation checkpoint " + i + " (invisible)");
                gate.transform.SetParent(track.transform); gate.transform.position = track.Point(s) + Vector3.up * 1.5f;
                gate.transform.rotation = Quaternion.LookRotation(track.Tangent(s));
                var trigger = gate.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = new Vector3(track.roadWidth, 3, 0.4f);
                var checkpoint = gate.AddComponent<LapCheckpoint>(); checkpoint.index = i; checkpoint.experiment = experiment;
            }

            var overview = new GameObject("Observer camera — never sent to model").AddComponent<Camera>();
            overview.tag = "MainCamera";
            overview.transform.SetParent(root.transform); overview.nearClipPlane = 0.1f; overview.farClipPlane = 300;
            overview.clearFlags = CameraClearFlags.SolidColor; overview.backgroundColor = new Color(0.49f, 0.69f, 0.86f);
            var observer = overview.gameObject.AddComponent<ObserverCamera>(); observer.target = car.transform;
            overview.transform.position = car.transform.position - car.transform.forward * 10 + Vector3.up * 7;
            overview.transform.LookAt(car.transform.position + car.transform.forward * 5 + Vector3.up);
            overview.gameObject.AddComponent<AudioListener>();
            var light = new GameObject("Daylight").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.5f;
            light.transform.SetParent(root.transform); light.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientLight = Color.white;
            track.MarkGeometryGenerated();
            return root;
        }
        public static void AddCenterDashes(TrackLayout track, Material material = null)
        {
            const string groupName = "Yellow center dashes - 4m spacing";
            if (track.transform.Find(groupName) != null) return;
            // Upgrade older saved scenes on Play, reusing their original yellow material.
            var previous = new List<GameObject>();
            foreach (Transform child in track.transform)
            {
                if (child.name != "Center dash") continue;
                previous.Add(child.gameObject);
                if (material == null) material = child.GetComponent<MeshRenderer>().sharedMaterial;
            }
            if (material == null) return;
            foreach (GameObject dash in previous) { dash.SetActive(false); Remove(dash); }
            var group = new GameObject(groupName);
            group.transform.SetParent(track.transform, false);
            for (float s = 5; s < track.Length; s += 4)
                Cube("Center dash", group.transform, track.Point(s) + Vector3.up * 0.055f,
                    new Vector3(0.12f, 0.02f, 1.6f), Quaternion.LookRotation(track.Tangent(s)), material, false);
        }

        // Decorative landmarks only: never consulted by the driver or added to API telemetry.
        // Generated at startup so existing saved scenes also gain the trees without rebuilding the road.
        public static void AddRoadsideTrees(TrackLayout track)
        {
            const string groupName = "Roadside low poly trees";
            if (track.transform.Find(groupName) != null) return;
            var reference = track.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            var bark = TreeMaterial(reference, "Tree bark", new Color(0.34f, 0.18f, 0.085f));
            var leaves = new[] {
                TreeMaterial(reference, "Pine green light", new Color(0.20f, 0.47f, 0.18f)),
                TreeMaterial(reference, "Pine green", new Color(0.12f, 0.35f, 0.13f)),
                TreeMaterial(reference, "Pine green shade", new Color(0.075f, 0.25f, 0.10f)),
                TreeMaterial(reference, "Leaf green light", new Color(0.39f, 0.55f, 0.18f)),
                TreeMaterial(reference, "Leaf green", new Color(0.25f, 0.42f, 0.12f)),
                TreeMaterial(reference, "Leaf green shade", new Color(0.16f, 0.31f, 0.095f))
            };
            Mesh cone = TreeCone();
            Mesh trunkMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var group = new GameObject(groupName);
            group.transform.SetParent(track.transform, false);
            var random = new System.Random(17519);
            var placed = new List<Vector3>();
            float length = track.Length;
            for (int i = 0; i * 9.5f < length; i++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    PlaceTree(i * 9.5f + (float)random.NextDouble() * 4, 12.5f + (float)random.NextDouble() * 3, side);
                    if (i % 2 == 0) PlaceTree(i * 9.5f + 5, 23 + (float)random.NextDouble() * 7, side);
                }
            }
            Debug.Log("JEV_VISION_ROADSIDE_TREES " + placed.Count);

            void PlaceTree(float distance, float offset, int side)
            {
                float scale = 0.85f + (float)random.NextDouble() * 0.4f;
                float radius = 1.8f * scale;
                Vector3 right = Vector3.Cross(Vector3.up, track.Tangent(distance));
                Vector3 position = track.Point(distance) + right * (offset * side);
                position.y = 0;
                // Check the whole circuit, including nearby bends, and keep crowns beyond the barriers.
                if (track.DistanceFromCenterline(position) < track.roadWidth * 0.5f + radius + 4 ||
                    Mathf.Abs(position.x) + radius > 126 || Mathf.Abs(position.z) + radius > 106) return;
                foreach (Vector3 other in placed)
                    if ((other - position).sqrMagnitude < 30.25f) return;
                placed.Add(position);
                var tree = new GameObject("Low poly tree " + placed.Count);
                tree.transform.SetParent(group.transform, false);
                tree.transform.position = position;
                tree.transform.rotation = Quaternion.Euler(0, (float)random.NextDouble() * 360, 0);
                tree.transform.localScale = Vector3.one * scale;
                int palette = random.Next(2) * 3;
                var crownMaterials = new[] { leaves[palette], leaves[palette + 1], leaves[palette + 2] };
                TreePart("Trunk", tree.transform, trunkMesh, new[] { bark }, new Vector3(0, 1.5f, 0), new Vector3(0.48f, 3, 0.48f));
                TreePart("Lower crown", tree.transform, cone, crownMaterials, new Vector3(0, 1.8f, 0), new Vector3(1.8f, 4.2f, 1.8f));
                TreePart("Upper crown", tree.transform, cone, crownMaterials, new Vector3(0, 3.8f, 0), new Vector3(1.3f, 3.6f, 1.3f));
            }
        }

        private static Material TreeMaterial(Material reference, string name, Color color)
        {
            var material = new Material(reference) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color);
            return material;
        }

        private static Mesh TreeCone()
        {
            var vertices = new List<Vector3>();
            var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
            for (int side = 0; side < 6; side++)
            {
                float a = side * Mathf.PI / 3, b = (side + 1) * Mathf.PI / 3;
                Vector3 first = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Vector3 second = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
                AddTriangle(first, Vector3.up, second, side % 3);
                AddTriangle(first, second, Vector3.zero, side % 3);
            }
            var mesh = new Mesh { name = "Six sided low poly tree crown" };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = 3;
            for (int i = 0; i < 3; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;

            void AddTriangle(Vector3 a, Vector3 b, Vector3 c, int material)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                triangles[material].Add(start); triangles[material].Add(start + 1); triangles[material].Add(start + 2);
            }
        }

        private static void TreePart(string name, Transform parent, Mesh mesh, Material[] materials, Vector3 position, Vector3 scale)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale, Quaternion rotation, Material material, bool collide, bool local = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent, false);
            if (local) { go.transform.localPosition = position; go.transform.localRotation = rotation; go.layer = 8; }
            else { go.transform.position = position; go.transform.rotation = rotation; }
            go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collide) Remove(go.GetComponent<Collider>());
            return go;
        }
        private static void Remove(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }
    }
}
