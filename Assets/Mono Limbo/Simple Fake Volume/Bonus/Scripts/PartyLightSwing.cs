using UnityEngine;

namespace MonoLimboStudio
{
public class PartyLightSwing : MonoBehaviour
{
    [Header("Swing Settings")]
    [Tooltip("How fast the light swings around.")]
    public float speed = 2.0f;
    
    [Tooltip("The maximum angle (in degrees) the light will tilt from its center.")]
    public float maxAngle = 15.0f;

    [Header("Pattern Complexity")]
    [Tooltip("Offsets the speed on the X and Y axes to make the sweep look like a natural figure-8 rather than a perfect circle.")]
    public float xSpeedMultiplier = 1.0f;
    public float ySpeedMultiplier = 1.3f;

    // Stores the initial facing direction of the light
    private Quaternion startRotation;
    
    // Stores a random time offset to desync multiple lights
    private float randomTimeOffset;

    void Start()
    {
        // Remember the rotation the light was placed at in the editor
        startRotation = transform.localRotation;
        
        // Pick a random starting point in time for this specific light
        randomTimeOffset = Random.Range(0f, 100f);
    }

    void Update()
    {
        // Calculate our moving time value, incorporating the random offset
        float time = (Time.time + randomTimeOffset) * speed;

        // Generate smooth oscillating values between -maxAngle and +maxAngle
        float angleX = Mathf.Sin(time * xSpeedMultiplier) * maxAngle;
        float angleY = Mathf.Cos(time * ySpeedMultiplier) * maxAngle;

        // Create the new rotation based on the angles
        Quaternion offsetRotation = Quaternion.Euler(angleX, angleY, 0f);

        // Apply it relative to the original starting rotation
        transform.localRotation = startRotation * offsetRotation;
    }
}}