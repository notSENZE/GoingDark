# Going Dark

Streets should not glow like a shopping district at midnight.

Going Dark pulls the plug on Streets of Tarkov and Ground Zero.
Street lamps die, interiors lose their electrical glow, advertisements go dark and the power stays out for the entire raid.
The result is not a darker filter. It is a city without a working grid, where night vision and flashlights finally matter.

The blackout is permanent. It is not tied to raid time, weather or a scripted event.

## What goes dark

- Electrical street and interior lighting on Streets of Tarkov and both Ground Zero variants.
- Illuminated advertisements, shop signs and other powered surfaces that use EFT's normal lighting behavior.
- Map switches and triggers are prevented from turning standard electrical lights back on later in the raid.

Going Dark works during day raids too. The electrical grid remains off, but natural daylight still lights the map.

## What the mod is not

Going Dark is not a ReShade preset, darkness filter or night-vision overhaul. It does not change the sun, moon, sky, weather, fog, ambient light or the player's brightness settings.

It does not alter NVGs, flashlights, lasers, IR illuminators, muzzle flashes or other mobile light sources. Fires, candles and gas lamps are left alone. Extraction lights are preserved by default.

The blackout is visual. Bot vision and AI behavior are not changed, and there is no generator, breaker, repair or power-restoration system.

## Supported maps

- Streets of Tarkov
- Ground Zero
- Ground Zero level 21+

Other maps are not changed in this release.

## Installation

Copy the contents of the release archive into the main SPT folder and allow Windows to merge the folders.

The plugin is installed here:

`BepInEx/plugins/GoingDark/GoingDark.dll`

Going Dark 0.1.8 was built and tested for SPT 4.1.3.

## Configuration

Press F12 in game and open the **Going Dark** section.

The blackout, supported maps, additional scene lights, illuminated surfaces and extraction-light protection can be configured separately.

Changing the main enable setting after a blackout has already been applied does not rebuild the original scene state. Start a new raid after disabling the mod.

## Diagnostics

Going Dark includes an optional in-raid diagnostic mode for tracking down bright signs that use an unusual shader.

1. Press F12, open the **Going Dark** section and enable **Diagnostic Mode**.
2. Use the diagnostic reticle in the exact center of the screen to aim at a bright sign, advertisement or light effect, then press F7. The selected renderer blinks for 1.25 seconds before it is added to the current raid report. Its name and material slots remain visible on screen so a wrong target can be recognized before the live test. Lights, particle systems and trails within three metres of the selected point are recorded too.
3. Press F8 to temporarily black out properties whose shader names suggest emission, illumination, glow or neon. If the surface goes dark, the report contains the relevant candidates.
4. Press F8 again to repeat the renderer blink if another visual confirmation is needed.
5. Press F9 to cycle through reversible surface tests for base color, reflections, the main texture and all three combined. Each new stage restores the previous one first; the fifth press restores the original material state.

You can capture as many different targets as needed during the same raid. F7 selects the next target; capturing the same renderer twice does not create a duplicate. The report is updated after every capture and live test here:

`BepInEx/plugins/GoingDark/diagnostics/`

Selecting another target or ending the raid also restores an active F9 surface test. F7, F8 and F9 can be changed in the F12 configuration menu. Diagnostic mode is disabled by default and does not scan continuously while it is off.

## Compatibility

Going Dark leaves Borkel's Realistic Night Vision Goggles and normal tactical devices alone.

Mods that change ambient light, post-processing or map lighting can change the final result. Brighter Interiors should be disabled when evaluating the blackout because it brightens interiors independently of map lamps.

## Known limitations and issues

- A small number of illuminated-looking surfaces can remain bright when they use ordinary reflective or albedo materials instead of emission properties.
- Diagnostic selection follows colliders first and renderer bounds second. A large or overlapping map object can occasionally be selected instead of the visible sign; the automatic F7 blink makes this apparent before the live test.
- Light baked directly into the map can remain visible on walls, floors or signs. Removing it would require editing the map assets themselves.
- Active renderers are inspected in small batches and only emission-capable candidates enter the blackout queue. Inactive renderers below recognized map-light hierarchies and active renderers from scenes loaded later are also discovered incrementally without a recurring global scan. A completely runtime-generated effect can still remain on when it bypasses EFT's normal lighting behavior.
- Chem lights are preserved when their scene hierarchy uses a recognizable chem-light or glow-stick name. Unusually named map props can be identified through the nearby-effect data in a diagnostic report.
- Extraction signals that are not attached to a recognizable extraction object may not always be identified correctly.
- Disabling the mod during a raid does not restore lights that have already been switched off. Start a new raid to return to normal lighting.
- The darker world does not reduce bot vision or detection ranges.
