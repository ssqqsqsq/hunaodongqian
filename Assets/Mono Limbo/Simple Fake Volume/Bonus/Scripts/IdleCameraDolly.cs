using UnityEngine;

namespace MonoLimboStudio
{
    [RequireComponent(typeof(Camera))]
    public class IdleCameraDolly : MonoBehaviour
    {
        [Header("Idle State Control")]
        [Tooltip("Toggle this to turn the idle effect on and off.")]
        public bool isIdle = true;

        [Header("Dolly Zoom Settings")]
        [Tooltip("How far the camera physically moves on its local Z axis.")]
        public float movementDistance = 1.5f;
        [Tooltip("How much the FOV changes to counter the movement.")]
        public float fovOffset = 15f;
        [Tooltip("How fast the camera breathes back and forth.")]
        public float loopSpeed = 1.0f;
        [Tooltip("Smoothness of transitioning in and out of the idle state.")]
        public float transitionSpeed = 3.0f;

        private Camera cam;
        private Vector3 initialPosition;
        private float initialFOV;

        // Used to smoothly blend the effect on and off
        private float blendFactor = 0f;

        void Start()
        {
            cam = GetComponent<Camera>();
            
            // Save the exact starting state
            initialPosition = transform.localPosition;
            initialFOV = cam.fieldOfView;
        }

        void Update()
        {
            // Smoothly transition the blend factor between 0 (off) and 1 (on)
            float targetBlend = isIdle ? 1f : 0f;
            blendFactor = Mathf.Lerp(blendFactor, targetBlend, Time.deltaTime * transitionSpeed);

            // If the effect is completely off, snap to initial values and exit early to save performance
            if (blendFactor < 0.001f)
            {
                transform.localPosition = initialPosition;
                cam.fieldOfView = initialFOV;
                return;
            }

            // Generate a smooth looping wave from -1 to 1 based on time
            float wave = Mathf.Sin(Time.time * loopSpeed);

            // Calculate the target position: 
            // When wave is positive, camera pushes forward. When negative, it pulls back.
            Vector3 targetPosition = initialPosition + new Vector3(0, 0, wave * movementDistance);

            // Calculate the target FOV: 
            // Invert the wave (-wave) so when the camera pulls back, it zooms IN (decreases FOV).
            float targetFOV = initialFOV + (-wave * fovOffset);

            // Apply the values, multiplied by the blendFactor so it smoothly toggles on/off
            transform.localPosition = Vector3.Lerp(initialPosition, targetPosition, blendFactor);
            cam.fieldOfView = Mathf.Lerp(initialFOV, targetFOV, blendFactor);
        }
    }
}