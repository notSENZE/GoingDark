# IMPORTANT NOTE
My mods will no longer be available on The Forge. 
According to the rules their, you won’t receive support if you’ve installed mods that weren’t published there. You’ll be advised to uninstall these (my) mods or be denied support altogether. 
If you have any issues with my mods, please contact me directly on Discord.
**Discord Name:** *@obviously.senze*
**My Discord server:** https://discord.gg/PD5TPQFKpa

### GOING DARK

Streets should not glow like a shopping district at midnight.

I built Going Dark to pull the plug on Streets of Tarkov, Ground Zero and Interchange. Street lamps die, interiors lose their electrical glow, advertisements go dark and map switches cannot simply turn the grid back on. This is not a darkness filter. It is a city without working power, where night vision and flashlights finally matter.

The permanent blackout remains the default. Optional event modes can instead decide per raid whether the map starts without power or the grid fails while you are already outside.

Going Dark 0.3.0 is made for SPT 4.1.5.

#### WHAT IT DOES

- Switches off electrical street and interior lighting on Streets, both Ground Zero variants and Interchange.
- Darkens supported advertisements, shop signs, terminals and other powered surfaces.
- Prevents normal map switches and triggers from turning standard electrical lights back on.
- On Interchange, a mid-raid failure also shuts down the power station, stops active shop alarms and cuts Safe Room power for the rest of the raid.
- Works during day raids too. Natural sunlight remains unchanged.

Going Dark does not alter the sun, moon, sky, weather, fog, ambient light, post-processing, NVGs, tactical devices or mobile light sources. Fires, candles, gas lamps, recognizable chem lights and extraction lights are preserved.

Bot vision and AI behavior are not changed, and there is no generator, repair or power-restoration system.

#### BLACKOUT MODES

Press `F12`, open **Going Dark** and select a **Blackout Mode**.

- **Permanent** applies the blackout in every supported raid. Event chance, trigger minutes and event sound are ignored.
- **RaidStartEvent** rolls the configured chance once at raid start. A successful roll starts the map dark; a failed roll leaves normal lighting active for the whole raid. Trigger minutes and sound are ignored.
- **MidRaidEvent** rolls the same chance, then selects a shutdown time between the earliest and latest configured raid minute. A successful event plays the optional sound and shuts down the prepared grid over a few frames. On Interchange, it also trips and locks the power station. A failed roll leaves the map unchanged.

The minute settings use elapsed raid time, not the map's displayed time of day. Reversed earliest and latest values are handled automatically. Event decisions are fixed when the raid begins, so changing event settings during a raid affects the next raid.

Defaults are `Permanent`, a `25%` event chance and a mid-raid window from minute `5` to `15`.

Only **MidRaidEvent** disables Interchange's power station and related objectives. **Permanent** and **RaidStartEvent** keep that gameplay available while holding supported lighting dark.

#### SUPPORTED MAPS

- Streets of Tarkov
- Ground Zero
- Ground Zero level 21+
- Interchange

Other maps are not changed in this release.

### INSTALLATION AND SETTINGS

Copy the contents of the release archive into your main SPT folder and allow Windows to merge the folders.

```text
BepInEx/plugins/GoingDark/GoingDark.dll
BepInEx/plugins/GoingDark/blackout.wav
```

The `F12` menu also lets you enable each supported map separately, toggle unmanaged scene lights and emissive surfaces, preserve extraction lights, disable the event sound or adjust its volume.

Disabling Going Dark after a blackout does not reconstruct the map's original runtime state. Start a new raid to restore normal lighting.

### CUSTOM EVENT SOUND

`blackout.wav` plays only when a **MidRaidEvent** shuts down the grid. You can replace it with another WAV file:

1. Rename your file to `blackout.wav`.
2. Replace the existing file beside `GoingDark.dll`.
3. Restart the game before testing it.

Uncompressed 16-bit PCM at 44.1 or 48 kHz, in mono or stereo, is the recommended and tested format. Different durations are supported. Going Dark scales the volume but does not normalize replacement files, so very quiet audio should be normalized beforehand.

A missing or unreadable sound does not prevent the blackout. Make sure you have redistribution rights before including a replacement sound in a public release.

### DIAGNOSTIC MODE

Diagnostic mode helps identify bright map surfaces that use unusual shaders. Enable it in `F12`, aim with the center-screen indicator and use:

- `F7` to select and record a renderer.
- `F8` to test emission-like properties.
- `F9` to cycle through reversible color, reflection and texture tests.

You can test multiple targets in one raid. Reports are updated here:

```text
BepInEx/plugins/GoingDark/diagnostics/
```

The keys can be changed in `F12`. Diagnostic mode is disabled by default.

### COMPATIBILITY AND LIMITATIONS

- Going Dark leaves Borkel's Realistic Night Vision Goggles and normal tactical devices alone.
- Mods that alter ambient light, post-processing or map lighting can change the final result. Disable Brighter Interiors when evaluating the blackout.
- FIKA players should use the same Going Dark version and event configuration on every human client. FIKA event behavior has not yet been verified in multiplayer.
- Ordinary albedo, reflections and baked lighting can make a few surfaces remain bright even after their runtime glow is removed.
- Unusually named chem lights, extraction signals or runtime effects can bypass the current recognition rules.
- A late join or reconnect after a scheduled mid-raid shutdown applies the blackout immediately without replaying the sound.
- Disabling the mod during a raid does not restore lights that are already off.
- Darkness does not reduce bot vision or detection ranges.

### AI TRANSPARENCY

Just to be completely open about it: I’m not a trained software developer, and I don’t understand every single part of the code yet. I’m learning as I go.

A large part of the code was implemented with the help of Codex. It also helps me find the right classes and APIs, read telemetry logs, track down bugs and work on the documentation.

The idea behind the mod, its features, balancing, design decisions and the final say on what gets released all come from me. I don’t simply take generated code and throw it into a release.

New features are tested in-game, checked through logs and, whenever something is unclear, compared against the public SPT source code. If something cannot be properly verified or tested, it gets put aside for later.

That doesn’t mean mistakes can’t happen, especially while I’m still learning. At the end of the day, though, I’m responsible for the mod I publish and for the decisions made along the way, not the tool I used to help build it.

I know that AI-assisted mods are a touchy subject in parts of the community, and I understand why. That’s exactly why I want to be honest about how AI was used here.

The source code is publicly available, and constructive code reviews, specific suggestions and reproducible bug reports are always welcome.

### SUPPORT ME

If you enjoy my work and want to support it, you can find me on [Ko-fi](https://ko-fi.com/not_senze).
