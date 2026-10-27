How to Use
1. Basic Setup (Quick Start)
Navigate to the Prefab folder in your Project window.

Drag and drop the desired volume prefab into your scene hierarchy.

Use VP Lit or VP Unlit for Point Lights.

Use V Cone variations (e.g., V Cone, V Cone Lit Flicker) for Spotlights.

Move the prefab so its transform origin perfectly aligns with your Unity Light source.

Scale the prefab's transform (X, Y, Z) to match the range, radius, and angle of your actual light.

2. Customizing the Visuals
Select your instantiated volumetric prefab and locate the Material component in the Inspector. All visual properties are exposed here:

Opacity & Color: Dial in the exact density and tint of the fog beam.

Edge & Fade Distance: Adjust the intersection fading. This ensures the fog blends smoothly and prevents harsh clipping when it intersects with walls, floors, or the camera.

Use Wind? (Toggle): Enable this to activate procedural noise mapping.

Noise (A & B) Settings: Fine-tune the Scale, Speed, and Density to simulate scrolling dust particles, underwater scattering, or atmospheric wind.

3. Applying Procedural Scripts
The package includes 5 lightweight, plug-and-play scripts to add dynamic behavior without the need for complex animation timelines.

Setting up Light Scripts (LightFlicker, PartyLightController, PartyLightSwing)

Select your light setup in the hierarchy (or use a pre-built prefab like V Cone Lit Flicker).

Attach the desired script from the MonoLimboStudio namespace.

In the Inspector, assign the Target Light (the standard Unity Light) and the Target Renderer (the volumetric mesh).

Adjust the exposed parameters (Min/Max Intensity, Speed, Color Offsets).

Note on Duplication: Scripts like PartyLightController and PartyLightSwing automatically generate random time and color offsets on Start(). You can duplicate the light dozens of times, and they will naturally desynchronize.

Setting up Camera Scripts (HandheldCameraShake, IdleCameraDolly)

Attach the script directly to your Main Camera object.

For HandheldCameraShake, adjust the slow "breathing" sway and fast "jitter" parameters. The script naturally halves the Z-axis roll for biological realism.

For IdleCameraDolly, adjust the push distance and FOV intensity to dial in the cinematic vertigo effect. Use the isIdle toggle to smoothly blend the camera back to its resting state.