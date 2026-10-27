using UnityEngine;

namespace MonoLimboStudio
{
    public class HandheldCameraShake : MonoBehaviour
    {
        [Header("Sway Settings (Breathing)")]
        [Tooltip("How far the camera drifts smoothly.")]
        public float swayAmplitude = 0.5f;
        [Tooltip("How fast the camera drifts.")]
        public float swayFrequency = 0.8f;

        [Header("Jitter Settings (Scared Shaking)")]
        [Tooltip("How intense the micro-shakes are. Keep this low for realism.")]
        public float jitterAmplitude = 0.15f;
        [Tooltip("How fast the adrenaline shakes happen.")]
        public float jitterFrequency = 1.0f;

        private Quaternion initialRotation;
        
        // Random offsets to ensure axes don't move in a predictable diagonal line
        private float seedX;
        private float seedY;
        private float seedZ;

        void Start()
        {
            // Store the starting rotation so we don't permanently drift away
            initialRotation = transform.localRotation;

            // Generate random starting points for the noise
            seedX = Random.Range(0f, 100f);
            seedY = Random.Range(0f, 100f);
            seedZ = Random.Range(0f, 100f);
        }

        void Update()
        {
            // Calculate slow sway (Returns -1 to 1)
            float swayX = (Mathf.PerlinNoise(Time.time * swayFrequency + seedX, 0f) - 0.5f) * 2f;
            float swayY = (Mathf.PerlinNoise(0f, Time.time * swayFrequency + seedY) - 0.5f) * 2f;
            float swayZ = (Mathf.PerlinNoise(Time.time * swayFrequency, Time.time * swayFrequency + seedZ) - 0.5f) * 2f;

            // Calculate fast jitter (Returns -1 to 1)
            float jitterX = (Mathf.PerlinNoise(Time.time * jitterFrequency + seedX + 100f, 0f) - 0.5f) * 2f;
            float jitterY = (Mathf.PerlinNoise(0f, Time.time * jitterFrequency + seedY + 100f) - 0.5f) * 2f;
            float jitterZ = (Mathf.PerlinNoise(Time.time * jitterFrequency + 100f, Time.time * jitterFrequency + seedZ + 100f) - 0.5f) * 2f;

            // Combine both noises into final rotation angles
            Vector3 rotationOffset = new Vector3(
                (swayX * swayAmplitude) + (jitterX * jitterAmplitude),
                (swayY * swayAmplitude) + (jitterY * jitterAmplitude),
                // We multiply the Z (roll) by 0.5f because real human hands don't tilt side-to-side as much as they pan
                ((swayZ * swayAmplitude) + (jitterZ * jitterAmplitude)) * 0.5f 
            );

            // Apply the offset on top of the camera's original rotation
            transform.localRotation = initialRotation * Quaternion.Euler(rotationOffset);
        }
    }
}