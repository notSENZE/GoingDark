# ⚠️ IMPORTANT NOTE ⚠️
My mods will no longer be available on The Forge.
Under The Forge's rules, mods that aren't released there aren't supported.
This means that if you run into an issue that might be related to one of my mods, you may be denied support or advised to remove the mod.
If you experience any issues with my mods, please join my [Discord](https://discord.gg/PD5TPQFKpa) for support.


# ABOUT GOING DARK
**Going Dark** causes power outages on `Streets of Tarkov`, `Reserve`, `Ground Zero`, `Interchange`, `Factory (night)`, `Customs` and `Lighthouse`  
This makes NVGs and tactical lights even more useful.

**SPT version:** `~4.1.6`  
**Dependencies:** None  
**FIKA compatibility:** Unknown  
**Recommended mod:** [Bodycam](https://github.com/notSENZE/Bodycam)  
**Known incompatibilities:** None that I know of


# DETAILS
Press `F12`, open **Going Dark** and select a **Blackout Mode**.

- **Permanent** applies the blackout in every supported raid. Event chance, trigger minutes and event sound are ignored.
- **RaidStartEvent** rolls the configured chance once at raid start. A successful roll starts the map dark; a failed roll leaves normal lighting active for the whole raid. Trigger minutes and sound are ignored.
- **MidRaidEvent** rolls the same chance, then selects a shutdown time between the earliest and latest configured raid minute. A successful event plays the optional sound and shuts down the prepared grid over a few frames. On Interchange, it also trips and locks the power station. A failed roll leaves the map unchanged.
- **Night Blackouts Only** ensures that blackouts can begin only between 20:00 and 06:00. If a raid reaches 20:00 while you are still in it, a waiting Permanent or Mid-Raid blackout may activate, depending on your selected mode and event chance. Raid Start Events occur only when the raid begins at night. Once activated, the blackout lasts until the raid ends.

The minute settings use elapsed raid time, not the map's displayed time of day. Reversed earliest and latest values are handled automatically. Event decisions are fixed when the raid begins, so changing event settings during a raid affects the next raid.

Defaults are `Permanent`, a `25%` event chance and a mid-raid window from minute `5` to `15`.

Only **MidRaidEvent** disables Interchange's power station and related objectives.  
**Permanent** and **RaidStartEvent** keep that gameplay available while holding supported lighting dark.

## CUSTOM EVENT SOUND 
`blackout.wav` plays only when a **MidRaidEvent** shuts down the grid. You can replace it with another WAV file:

1. Rename your file to `blackout.wav`.
2. Replace the existing file beside `GoingDark.dll`.
3. Restart the game before testing it.

Going Dark scales the volume but does not normalize replacement files, so very quiet audio should be normalized beforehand.

A missing or unreadable sound does not prevent the blackout. Make sure you have redistribution rights before including a replacement sound in a public release.


# COMPATIBILITY AND LIMITATIONS
- Going Dark leaves Borkel's Realistic Night Vision Goggles and normal tactical devices alone.
- Mods that alter ambient light, post-processing or map lighting can change the final result. Disable Brighter Interiors when evaluating the blackout.
- FIKA players should use the same Going Dark version and event configuration on every human client. FIKA event behavior has not yet been verified in multiplayer.
- Ordinary albedo, reflections and baked lighting can make a few surfaces remain bright even after their runtime glow is removed.
- Unusually named chem lights, extraction signals or runtime effects can bypass the current recognition rules.
- A late join or reconnect after a scheduled mid-raid shutdown applies the blackout immediately without replaying the sound.
- Disabling the mod during a raid does not restore lights that are already off.


# BUG REPORTS
If you encounter a bug or experience any issues with my mods, please include the following information in your report:

- **Description:** What happened, and what were you doing when the issue occurred?
- **BepInEx logs**
- **Server logs**
- **Screenshots or video recordings**, if available

Please submit your bug reports in the dedicated channel [on my Discord server](https://discord.com/channels/1551226098354753576/1552432219102904441).
