using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MonoLimboStudio
{
    public class LightFlicker : MonoBehaviour
    {
        [Header("Light Settings")]
        public Light targetLight;
        public float minIntensity = 0.5f;
        public float maxIntensity = 1.5f;
        public float flickerSpeed = 5f;

        [Header("Material Settings")]
        public Renderer targetRenderer;
        public string opacityProperty = "_Opacity"; // Must be a float property in your shader
        [Range(0f, 1f)] public float minOpacity = 0.2f;
        [Range(0f, 1f)] public float maxOpacity = 1.0f;

        private Material materialInstance;

        void Start()
        {
            if (targetRenderer != null)
            {
                materialInstance = targetRenderer.material;

                // Validate that the property exists
                if (!materialInstance.HasProperty(opacityProperty))
                {
                    Debug.LogWarning($"Material does not have a float property named '{opacityProperty}'.");
                }
            }
        }

        void Update()
        {
            float flickerValue = Mathf.PerlinNoise(Time.time * flickerSpeed, 0.0f);

            // Flicker the light
            if (targetLight != null)
            {
                float intensity = Mathf.Lerp(minIntensity, maxIntensity, flickerValue);
                targetLight.intensity = intensity;
            }

            // Adjust material opacity float
            if (materialInstance != null && materialInstance.HasProperty(opacityProperty))
            {
                float opacity = Mathf.Lerp(minOpacity, maxOpacity, flickerValue);
                materialInstance.SetFloat(opacityProperty, opacity);
            }
        }
    }
}