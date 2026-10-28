using UnityEngine;

namespace JevVision
{
    // Low-speed dynamic bicycle. No track, waypoint, checkpoint or raycast references.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class VehicleController : MonoBehaviour
    {
        public ExperimentConfig config;
        public Transform[] visualWheels;
        public JevDecision Command { get; private set; } = JevDecision.Stop();
        public float AppliedSteering { get; private set; }
        public float Speed => Body == null ? 0 : Body.linearVelocity.magnitude;
        public Rigidbody Body { get; private set; }
        public float LastCollisionImpulse { get; private set; }
        public int CollisionCount { get; private set; }
        public bool Held { get; private set; }
        private RigidbodyConstraints drivingConstraints;
        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            SetHeld(true);
        }
        public void SetHeld(bool held)
        {
            if (Held == held) return;
            if (Body == null) Body = GetComponent<Rigidbody>();
            if (held) drivingConstraints = Body.constraints;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.constraints = held ? RigidbodyConstraints.FreezeAll : drivingConstraints;
            Held = held;
            SetCommand(JevDecision.Stop("waiting_for_play"));
        }
        public void SetCommand(JevDecision decision) => Command = decision ?? JevDecision.Stop();
        private void FixedUpdate()
        {
            if (config == null || Held) return;
            AppliedSteering = Mathf.MoveTowards(AppliedSteering, Mathf.Clamp(Command.steeringValue, -1, 1), config.steeringSlewPerSecond * Time.fixedDeltaTime);
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            float forwardSpeed = Vector3.Dot(Body.linearVelocity, forward);
            float sideSpeed = Vector3.Dot(Body.linearVelocity, right);
            float drive = Mathf.Clamp01(Command.throttle) * config.acceleration;
            float stop = Mathf.Clamp01(Command.brake) * config.braking + 0.18f;
            float targetSpeed = Mathf.Clamp(forwardSpeed + (drive - stop) * Time.fixedDeltaTime, 0, config.maximumSpeed);
            Body.AddForce(forward * ((targetSpeed - forwardSpeed) / Time.fixedDeltaTime) - right * (sideSpeed / Time.fixedDeltaTime), ForceMode.Acceleration);
            float yaw = forwardSpeed * Mathf.Tan(AppliedSteering * config.maximumSteeringDegrees * Mathf.Deg2Rad) / config.wheelbase;
            Body.angularVelocity = new Vector3(0, yaw, 0);
            if (visualWheels != null)
                for (int i = 0; i < visualWheels.Length; i++)
                    visualWheels[i].localRotation = Quaternion.Euler(0, i < 2 ? AppliedSteering * config.maximumSteeringDegrees : 0, 90);
        }
        public void ResetPose(Vector3 position, Quaternion rotation)
        {
            if (Body == null) Body = GetComponent<Rigidbody>();
            Body.position = position;
            Body.rotation = rotation;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            AppliedSteering = 0;
            LastCollisionImpulse = 0;
            SetCommand(JevDecision.Stop());
            Physics.SyncTransforms();
        }
        private void OnCollisionEnter(Collision collision)
        {
            if (!collision.collider.CompareTag("Respawn")) return;
            LastCollisionImpulse = collision.impulse.magnitude;
            CollisionCount++;
        }
        public void ClearCollision() => LastCollisionImpulse = 0;
    }
}
