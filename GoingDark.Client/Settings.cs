using BepInEx.Configuration;

namespace GoingDark.Client
{
    internal enum BlackoutMode
    {
        Permanent,
        RaidStartEvent,
        MidRaidEvent
    }

    internal enum BlackoutSoundMode
    {
        HideoutGenerator,
        CustomFile
    }

    internal static class Settings
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> NightBlackoutsOnly;
        internal static ConfigEntry<BlackoutMode> Mode;
        internal static ConfigEntry<int> EventChance;
        internal static ConfigEntry<float> EarliestEventMinute;
        internal static ConfigEntry<float> LatestEventMinute;
        internal static ConfigEntry<bool> PlayEventSound;
        internal static ConfigEntry<BlackoutSoundMode> EventSound;
        internal static ConfigEntry<float> EventSoundVolume;
        internal static ConfigEntry<bool> StreetsEnabled;
        internal static ConfigEntry<bool> GroundZeroEnabled;
        internal static ConfigEntry<bool> InterchangeEnabled;
        internal static ConfigEntry<bool> ReserveEnabled;
        internal static ConfigEntry<bool> FactoryEnabled;
        internal static ConfigEntry<bool> CustomsEnabled;
        internal static ConfigEntry<bool> LighthouseEnabled;
        internal static ConfigEntry<bool> BotReactionsEnabled;
        internal static ConfigEntry<bool> BotEquipmentResponseEnabled;
        internal static ConfigEntry<int> UnaidedBotVisionPercent;
        internal static ConfigEntry<int> UnaidedBotRecognitionPercent;
        internal static ConfigEntry<bool> DisableUnmanagedSceneLights;
        internal static ConfigEntry<bool> DisableEmissiveMapMaterials;
        internal static ConfigEntry<bool> PreserveExtractionLights;

        internal static void Bind(ConfigFile config)
        {
            Enabled = config.Bind(
                "1. General",
                "Enabled",
                true,
                "Enable Going Dark in supported raids.");

            NightBlackoutsOnly = config.Bind(
                "1. General",
                "Night Blackouts Only",
                true,
                "Only activate new blackouts between 20:00 and 06:00. Active blackouts last until the raid ends.");

            Mode = config.Bind(
                "2. Events",
                "Blackout Mode",
                BlackoutMode.MidRaidEvent,
                "Permanent starts every supported raid dark. RaidStartEvent rolls at raid start. MidRaidEvent rolls for a delayed blackout.");

            EventChance = config.Bind(
                "2. Events",
                "Event Chance Percent",
                30,
                new ConfigDescription(
                    "Chance for a blackout in either event mode. Permanent mode ignores this setting.",
                    new AcceptableValueRange<int>(0, 100)));

            EarliestEventMinute = config.Bind(
                "2. Events",
                "Earliest Mid-Raid Minute",
                5f,
                new ConfigDescription(
                    "Earliest raid minute at which a MidRaidEvent can trigger.",
                    new AcceptableValueRange<float>(0f, 60f)));

            LatestEventMinute = config.Bind(
                "2. Events",
                "Latest Mid-Raid Minute",
                15f,
                new ConfigDescription(
                    "Latest raid minute at which a MidRaidEvent can trigger.",
                    new AcceptableValueRange<float>(0f, 60f)));

            PlayEventSound = config.Bind(
                "2. Events",
                "Play Mid-Raid Sound",
                true,
                "Play a sound when a blackout starts during the raid.");

            EventSound = config.Bind(
                "2. Events",
                "Blackout Sound",
                BlackoutSoundMode.HideoutGenerator,
                "HideoutGenerator uses Tarkov's generator shutdown sound. CustomFile uses blackout.wav from the client mod folder.");

            EventSoundVolume = config.Bind(
                "2. Events",
                "Mid-Raid Sound Volume",
                0.05f,
                new ConfigDescription(
                    "Volume of the blackout sound.",
                    new AcceptableValueRange<float>(0f, 1f)));

            StreetsEnabled = config.Bind(
                "2. Maps",
                "Streets of Tarkov",
                true,
                "Apply the blackout on Streets of Tarkov.");

            GroundZeroEnabled = config.Bind(
                "2. Maps",
                "Ground Zero",
                true,
                "Apply the blackout on both Ground Zero level variants.");

            InterchangeEnabled = config.Bind(
                "2. Maps",
                "Interchange",
                true,
                "Apply the blackout on Interchange.");

            ReserveEnabled = config.Bind(
                "2. Maps",
                "Reserve",
                true,
                "Apply the blackout on Reserve without disabling extraction mechanics.");

            FactoryEnabled = config.Bind(
                "2. Maps",
                "Factory",
                true,
                "Apply the blackout on the night version of Factory.");

            CustomsEnabled = config.Bind(
                "2. Maps",
                "Customs",
                true,
                "Apply the blackout on Customs.");

            LighthouseEnabled = config.Bind(
                "2. Maps",
                "Lighthouse",
                true,
                "Apply the blackout on Lighthouse.");

            BotReactionsEnabled = config.Bind(
                "3. Bot Reactions",
                "Bots React to Blackout",
                true,
                "Reduce unaided bot vision and recognition in dark areas while a blackout is active.");

            BotEquipmentResponseEnabled = config.Bind(
                "3. Bot Reactions",
                "Bots Use Night Vision and Lights",
                true,
                "Let bots use equipped night vision or weapon lights when the blackout leaves them in darkness.");

            UnaidedBotVisionPercent = config.Bind(
                "3. Bot Reactions",
                "Unaided Vision Distance Percent",
                55,
                new ConfigDescription(
                    "Maximum bot vision distance without active night vision during a blackout.",
                    new AcceptableValueRange<int>(10, 100)));

            UnaidedBotRecognitionPercent = config.Bind(
                "3. Bot Reactions",
                "Unaided Recognition Speed Percent",
                55,
                new ConfigDescription(
                    "Bot recognition speed without active night vision during a blackout.",
                    new AcceptableValueRange<int>(10, 100)));

            DisableUnmanagedSceneLights = config.Bind(
                "3. Blackout",
                "Disable Unmanaged Scene Lights",
                true,
                "Also disable scene lights that are not connected to EFT's normal lamp controllers.");

            DisableEmissiveMapMaterials = config.Bind(
                "3. Blackout",
                "Disable Emissive Map Materials",
                true,
                "Turn off powered emission on map signs, advertisements and other illuminated surfaces.");

            PreserveExtractionLights = config.Bind(
                "3. Blackout",
                "Preserve Extraction Lights",
                true,
                "Keep lights that are attached to extraction points or clearly named extraction signals.");
        }
    }
}
