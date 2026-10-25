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
                    GameObject barrier = Cube("Visible safety barrier", track.transform, center + right * (side * (track.roadWidth * 0.5f + 1.1f)) + Vector3.up * 0.45f,
                        new Vector3(0.22f, 0.9f, length), rotation, m[4], true);
                    barrier.tag = "Respawn";
                }
            }
            // A start stripe and sparse center dashes are visual cues only.
            for (int x = 0; x < 12; x++)
                Cube("Start grid", track.transform, track.Point(0) + Vector3.Cross(Vector3.up, track.Tangent(0)) * (x - 5.5f) + Vector3.up * 0.085f,
                    new Vector3(0.95f, 0.02f, 0.65f), Quaternion.LookRotation(track.Tangent(0)), x % 2 == 0 ? m[2] : m[1], false);
            for (float s = 5; s < track.Length; s += 8)
                Cube("Center dash", track.transform, track.Point(s) + Vector3.up * 0.055f, new Vector3(0.12f, 0.02f, 1.6f), Quaternion.LookRotation(track.Tangent(s)), m[8], false);

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
