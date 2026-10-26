# IMPORTANT NOTE
My mods will no longer be available on The Forge.
According to their rules, you won’t receive support if you’ve installed mods that weren’t published there. You’ll be advised to uninstall these (my) mods or be denied support altogether.
If you have any issues with my mods, please contact me directly on Discord.

**Discord Name:** *@obviously.senze*  
**My Discord server:** https://discord.gg/PD5TPQFKpa


# ABOUT GOING DARK
“Going Dark” causes nearly all the lights on `Streets of Tarkov`, `Interchange`, `Ground Zero`, and `Reserve` to go out. 
NVG and tactical lights are finally actually useful.

**SPT version:** ~4.1.6  
**Dependencies:** none  
**FIKA compatible:** unknown  
**Recommendations:**  
**Incompatibilities:** None that I know of


# DETAILS
Press `F12`, open **Going Dark** and select a **Blackout Mode**.

- **Permanent** applies the blackout in every supported raid. Event chance, trigger minutes and event sound are ignored.
- **RaidStartEvent** rolls the configured chance once at raid start. A successful roll starts the map dark; a failed roll leaves normal lighting active for the whole raid. Trigger minutes and sound are ignored.
- **MidRaidEvent** rolls the same chance, then selects a shutdown time between the earliest and latest configured raid minute. A successful event plays the optional sound and shuts down the prepared grid over a few frames. On Interchange, it also trips and locks the power station. A failed roll leaves the map unchanged.

The minute settings use elapsed raid time, not the map's displayed time of day. Reversed earliest and latest values are handled automatically. Event decisions are fixed when the raid begins, so changing event settings during a raid affects the next raid.

Defaults are `Permanent`, a `25%` event chance and a mid-raid window from minute `5` to `15`.

Only **MidRaidEvent** disables Interchange's power station and related objectives. **Permanent** and **RaidStartEvent** keep that gameplay available while holding supported lighting dark.

## Custom Event Sound 
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
It's very important to me that you document bugs visually and send me video or image footage.
Please use https://discord.com/channels/1551226098354753576/1552432219102904441 for this.
