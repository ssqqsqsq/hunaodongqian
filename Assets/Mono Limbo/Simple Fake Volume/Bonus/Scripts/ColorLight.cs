using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MonoLimboStudio
{
    public class PartyLightController : MonoBehaviour
    {
        [Header("Light Settings")]
        public Light targetLight;
        public float minIntensity = 0.5f;
        public float maxIntensity = 1.5f;
        public float flickerSpeed = 5f;

        [Header("Color Party Settings")]
        [Tooltip("How fast the colors cycle through the rainbow.")]
        public float colorCycleSpeed = 0.5f;
        [Tooltip("Saturation of the party light (0 = white, 1 = full color)")]
        [Range(0f, 1f)] public float colorSaturation = 1f;

        [Header("Instance Variations")]
        [Tooltip("If true, each light will start at a different color in the rainbow.")]
        public bool randomizeColorOffset = true;
        [Tooltip("If true, each light will flicker independently instead of all at once.")]
        public bool randomizeFlickerOffset = true;

        [Header("Material Settings")]
        public Renderer targetRenderer;
        public string opacityProperty = "_Opacity"; 
        [Range(0f, 1f)] public float minOpacity = 0.2f;
        [Range(0f, 1f)] public float maxOpacity = 1.0f;
        
        [Tooltip("The color property name in your fake volume shader.")]
        public string colorProperty = "_Color";

        private Material materialInstance;
        
        // Hidden offsets to desynchronize the instances
        private float colorOffset = 0f;
        private float flickerOffset = 0f;

        void Start()
        {
            // Generate random starting points so instances don't match
            if (randomizeColorOffset)
            {
                colorOffset = Random.Range(0f, 1f); // Hue is a 0 to 1 scale
            }

            if (randomizeFlickerOffset)
            {
                flickerOffset = Random.Range(0f, 100f); // Push Perlin noise sampling far away
            }

            if (targetRenderer != null)
            {
                // Instantiate material so each has its own independent color/opacity
                materialInstance = targetRenderer.material;

                if (!materialInstance.HasProperty(opacityProperty))
                {
                    Debug.LogWarning($"Material does not have a float property named '{opacityProperty}'.");
                }
                
                if (!materialInstance.HasProperty(colorProperty))
                {
                    Debug.LogWarning($"Material does not have a color property named '{colorProperty}'.");
                }
            }
        }

        void Update()
        {
            // 1. Calculate desynchronized party color
            // Add the random colorOffset to Time.time so instances start at different hues
            float hue = Mathf.Repeat((Time.time * colorCycleSpeed) + colorOffset, 1f);
            Color partyColor = Color.HSVToRGB(hue, colorSaturation, 1f);

            // 2. Calculate desynchronized flicker noise
            // Add flickerOffset so instances sample different parts of the noise map
            float flickerValue = Mathf.PerlinNoise((Time.time * flickerSpeed) + flickerOffset, 0.0f);

            // 3. Apply to Unity Light
            if (targetLight != null)
            {
                targetLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, flickerValue);
                targetLight.color = partyColor;
            }

            // 4. Apply to Fake Volume Material
            if (materialInstance != null)
            {
                if (materialInstance.HasProperty(opacityProperty))
                {
                    float opacity = Mathf.Lerp(minOpacity, maxOpacity, flickerValue);
                    materialInstance.SetFloat(opacityProperty, opacity);
                }

                if (materialInstance.HasProperty(colorProperty))
                {
                    materialInstance.SetColor(colorProperty, partyColor);
                }
            }
        }
    }
}