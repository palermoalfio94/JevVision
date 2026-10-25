using UnityEngine;

namespace JevVision
{
    public sealed class LapCheckpoint : MonoBehaviour
    {
        public int index;
        public ExperimentManager experiment;
        private void OnTriggerEnter(Collider other)
        {
            var car = other.attachedRigidbody;
            if (car == null || car.GetComponent<VehicleController>() != experiment.vehicle) return;
            bool forward = Vector3.Dot(car.linearVelocity, transform.forward) > 0.1f;
            experiment.PassCheckpoint(index, forward);
        }
    }
}
