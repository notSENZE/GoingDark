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

Going Dark 0.1.2 was built and tested for SPT 4.1.3.

## Configuration

Press F12 in game and open the **Going Dark** section.

The blackout, supported maps, additional scene lights, illuminated surfaces and extraction-light protection can be configured separately.

Changing the main enable setting after a blackout has already been applied does not rebuild the original scene state. Start a new raid after disabling the mod.

## Compatibility

Going Dark leaves Borkel's Realistic Night Vision Goggles and normal tactical devices alone.

Mods that change ambient light, post-processing or map lighting can change the final result. Brighter Interiors should be disabled when evaluating the blackout because it brightens interiors independently of map lamps.

## Known limitations and issues

- A small number of advertisements, shop signs and hotel signs can remain bright when they use a custom map shader.
- Light baked directly into the map can remain visible on walls, floors or signs. Removing it would require editing the map assets themselves.
- An unusual light or illuminated surface loaded later in the raid may not use EFT's normal lighting behavior and can remain on.
- Extraction signals that are not attached to a recognizable extraction object may not always be identified correctly.
- Disabling the mod during a raid does not restore lights that have already been switched off. Start a new raid to return to normal lighting.
- The darker world does not reduce bot vision or detection ranges.
