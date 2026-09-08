## {.tabset}

### GOING DARK

Streets should not glow like a shopping district at midnight.

I built Going Dark to pull the plug on Streets of Tarkov and Ground Zero. Street lamps die, interiors lose their electrical glow, advertisements go dark and map switches cannot simply turn the grid back on. The result is not a darker filter. It is a city without working power, where night vision and flashlights finally matter.

The original permanent blackout remains the default. You can also turn Going Dark into a raid event that either starts selected raids without power or shuts the grid down while you are already outside.

I made **Going Dark 0.2.0** for **SPT 4.1.5**. Later `4.1.x` versions may work, but I only claim support for versions I have tested.

#### 0.2.0

- I added optional raid-start and mid-raid blackout events with a configurable chance.
- I added a configurable mid-raid trigger window based on elapsed raid time.
- I added an optional, replaceable `blackout.wav` event sound.
- I moved the expensive map preparation away from the trigger and spread the final light shutdown across a few frames to avoid the previous event freeze.
- I kept the permanent blackout as the default behavior.

### WHAT GOES DARK

- Electrical street and interior lighting on Streets of Tarkov and both Ground Zero variants.
- Standard EFT lamp controllers, lamp groups and scene lights.
- Illuminated advertisements, shop signs, terminals and other powered surfaces covered by Going Dark's material rules.
- Map switches and triggers that try to turn standard electrical lights back on after the blackout.

Going Dark also works in daytime raids. It switches off the electrical grid regardless of raid time, while natural daylight from the sun and sky remains untouched.

### THE THREE BLACKOUT MODES

Open the **Going Dark** section in the `F12` configuration menu and use **Blackout Mode** to choose one of the three modes.

#### Permanent

This is the original Going Dark behavior and remains the default.

- Every supported raid receives a blackout.
- The map starts dark as soon as the raid is ready.
- **Event Chance Percent**, the mid-raid time window and the event sound are ignored.
- Use this when you always want Streets and Ground Zero to have no working electrical grid.

#### RaidStartEvent

This turns the blackout into a chance-based raid condition.

- Going Dark rolls **Event Chance Percent** once when the raid starts.
- If the roll succeeds, the supported map starts without power.
- If the roll fails, the map keeps its normal lighting for the entire raid.
- The earliest and latest mid-raid minute settings are ignored.
- No event sound is played because there is no live shutdown during the raid.

Use `0` percent to prevent the event and `100` percent to make every supported raid start dark.

#### MidRaidEvent

This lets the electrical grid fail while the raid is already running.

- Going Dark rolls **Event Chance Percent** once when the raid starts.
- If the roll fails, the map keeps its normal lighting for the entire raid.
- If the roll succeeds, Going Dark selects a trigger point between **Earliest Mid-Raid Minute** and **Latest Mid-Raid Minute**.
- The minute values refer to elapsed raid time, not the displayed time of day.
- If the two minute settings are entered in reverse order, Going Dark automatically treats the lower value as the beginning and the higher value as the end of the window.
- At the trigger, the optional sound plays and the prepared electrical lights shut down over a few frames instead of freezing the game in one large operation.

The event decision and trigger time are fixed when the raid begins. Changing the mode, chance or time window during a raid affects the next raid.

### INSTALLATION AND SETTINGS

Copy the contents of the release archive into your main SPT folder and allow Windows to merge the folders.

The client files are installed here:

```text
BepInEx/plugins/GoingDark/GoingDark.dll
BepInEx/plugins/GoingDark/blackout.wav
```

Press `F12` in game and open **Going Dark**. The menu is divided into these groups:

- **General** enables or disables Going Dark.
- **Events** selects the blackout mode, event chance, mid-raid trigger window, sound and sound volume.
- **Maps** enables Streets of Tarkov and Ground Zero separately.
- **Blackout** controls unmanaged scene lights, emissive map materials and extraction-light protection.
- **Diagnostics** contains the optional tools for identifying bright map surfaces that need a dedicated material rule.

The default event chance is `25` percent and the default mid-raid window is between raid minutes `5` and `15`. The permanent mode ignores all three values.

Disabling Going Dark after a blackout has already happened does not reconstruct the map's original runtime state. Start a new raid to restore normal lighting.

### CUSTOM EVENT SOUND

Going Dark includes `blackout.wav` beside the plugin. It is played as a non-positional sound only when a **MidRaidEvent** actually shuts down the grid.

You can replace it with your own sound:

1. Prepare a valid WAV file. Uncompressed 16-bit PCM at 44.1 or 48 kHz, in mono or stereo, is the recommended and tested format.
2. Rename it to exactly `blackout.wav`.
3. Replace the existing file in `BepInEx/plugins/GoingDark/`.
4. Restart the game before testing it.

The replacement can have a different duration. Going Dark keeps the file loaded for the complete playback time. The **Mid-Raid Sound Volume** setting scales playback from `0.00` to `1.00`, but the mod does not normalize replacement files automatically. If a new sound is extremely quiet, normalize it in an audio editor before using it.

Set **Play Mid-Raid Sound** to `false` if you want a silent event. A missing, invalid or unreadable audio file does not prevent the blackout itself.

If you distribute a replacement sound with a public build, make sure you have permission to redistribute it.

### WHAT THE MOD DOES NOT CHANGE

Going Dark is not a ReShade preset, darkness filter or night-vision overhaul. It does not change the sun, moon, sky, weather, fog, ambient light, post-processing or the player's brightness settings.

It does not alter NVGs, flashlights, lasers, IR illuminators, muzzle flashes or other mobile light sources. Fires, candles, gas lamps and recognizable chem lights are preserved. Extraction lights are preserved by default.

The blackout is currently visual. Bot vision, hearing, detection range and AI behavior are not changed. There is also no generator, breaker, repair or power-restoration system.

### SUPPORTED MAPS

- Streets of Tarkov
- Ground Zero
- Ground Zero level 21+

Other maps are not changed in this release.

### DIAGNOSTIC MODE

Going Dark includes optional in-raid tools for identifying signs or surfaces that remain bright because they use unusual shaders. Diagnostic mode is disabled by default and does not continuously scan while it is off.

1. Press `F12`, open **Going Dark** and enable **Diagnostic Mode**.
2. Aim with the permanent center-screen indicator and press `F7`. Going Dark selects the renderer under the crosshair, blinks it for visual confirmation and adds it to the current raid report.
3. Press `F8` to temporarily disable emission-like shader properties on the selected renderer. If the visible surface goes dark, the report records the relevant candidates.
4. Press `F8` again if you want to repeat the confirmation blink.
5. Press `F9` to cycle through reversible tests for base color, reflections, the main texture and all three together. The fifth press restores the original material state.

You can select and test multiple renderers during one raid. Capturing the same renderer again does not create a duplicate. Going Dark also records nearby lights, particle systems and trails that may explain a visible glow.

Reports are written here:

```text
BepInEx/plugins/GoingDark/diagnostics/
```

Selecting another target or ending the raid restores an active `F9` surface test. The `F7`, `F8` and `F9` bindings can be changed in the `F12` menu.

### COMPATIBILITY

Going Dark leaves Borkel's Realistic Night Vision Goggles and normal tactical devices alone.

Mods that change ambient light, post-processing or map lighting can alter the final result. Disable Brighter Interiors when evaluating Going Dark because it brightens interiors independently of the map's electrical lamps.

FIKA players should install the same Going Dark version and use the same event settings on every human client. Event selection and timing use shared raid information so matching clients should choose the same result. FIKA event behavior has not yet been verified in a multiplayer raid.

### KNOWN LIMITATIONS

- A small number of illuminated-looking surfaces can remain bright when their appearance comes from ordinary albedo, reflection data or baked map assets instead of runtime emission.
- Light baked directly into walls, floors or signs cannot be removed by this client plugin without editing the original map assets.
- Runtime effects that bypass EFT's normal lamp, light and material systems can remain active.
- Diagnostic selection follows colliders first and renderer bounds second. Large or overlapping objects can occasionally be selected instead of the visible sign; the confirmation blink makes this apparent before a live test.
- Chem lights are preserved when their hierarchy uses a recognizable chem-light or glow-stick name. Unusually named props may need an additional exception.
- Extraction signals that are not attached to a recognizable extraction object or name may not always be identified correctly.
- Disabling the mod during a raid does not restore lights that have already been switched off.
- A player joining or reconnecting after the scheduled mid-raid trigger receives the blackout immediately without replaying the event sound.
- Going Dark does not reduce bot vision or detection ranges in darkness.

### SOURCE AND BUILDING

I publish the exact source used for this release on [GitHub](https://github.com/notSENZE/GoingDark).

To build it yourself, point `SptPath` at a clean SPT 4.1.5 installation:

```powershell
dotnet build .\GoingDark.Client\GoingDark.Client.csproj -c Release -p:SptPath="C:\Path\To\SPT"
```

### LICENSE

I release Going Dark under the [MIT License](LICENSE). You may use, modify and redistribute it as long as you keep the applicable copyright and license notice.

The bundled `blackout.wav` is part of this Going Dark release. Do not assume that an unrelated replacement sound uses the same license.

### AI TRANSPARENCY

Just to be completely open about it: I’m not a trained software developer, and I don’t understand every single part of the code yet. I’m learning as I go.

A large part of the code was implemented with the help of Codex. It also helps me find the right classes and APIs, read telemetry logs, track down bugs and work on the documentation.

The idea behind the mod, its features, design decisions and the final say on what gets released all come from me. I don’t simply take generated code and throw it into a release.

New features are tested in game, checked through logs and, whenever something is unclear, compared against the public SPT source code. If something cannot be properly verified or tested, it gets put aside for later.

That doesn’t mean mistakes can’t happen, especially while I’m still learning. At the end of the day, though, I’m responsible for the mod I publish and for the decisions made along the way, not the tool I used to help build it.

I know that AI-assisted mods are a touchy subject in parts of the community, and I understand why. That’s exactly why I want to be honest about how AI was used here.

The source code is publicly available, and constructive code reviews, specific suggestions and reproducible bug reports are always welcome.

### SUPPORT ME

If you enjoy my work and want to support it, you can find me on [Ko-fi](https://ko-fi.com/not_senze).
