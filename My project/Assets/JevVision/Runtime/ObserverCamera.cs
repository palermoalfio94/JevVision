using UnityEngine;
using UnityEngine.InputSystem;

namespace JevVision
{
    public sealed class ObserverCamera : MonoBehaviour
    {
        public Transform target;
        private bool overhead;
        private void LateUpdate()
        {
            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) overhead = !overhead;
            if (target == null) return;
            if (overhead)
            {
                GetComponent<Camera>().orthographic = true;
                GetComponent<Camera>().orthographicSize = 85;
                transform.position = new Vector3(0, 150, 0);
                transform.rotation = Quaternion.Euler(90, 0, 0);
            }
            else
            {
                GetComponent<Camera>().orthographic = false;
                transform.position = target.position - target.forward * 10 + Vector3.up * 7;
                transform.LookAt(target.position + target.forward * 5 + Vector3.up);
            }
        }
    }
}
