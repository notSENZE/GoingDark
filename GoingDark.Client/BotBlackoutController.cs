using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace GoingDark.Client
{
    internal sealed class BotBlackoutController
    {
        private const float RefreshSeconds = 2f;

        private readonly ManualLogSource _log;
        private readonly HashSet<int> _knownBots = new HashSet<int>();
        private readonly HashSet<int> _equipmentFailures = new HashSet<int>();

        private int _worldInstanceId;
        private float _nextRefreshAt;
        private bool _blackoutWasActive;
        private bool _activationLogged;

        internal BotBlackoutController(ManualLogSource log)
        {
            _log = log;
        }

        internal void Tick()
        {
            if (!Singleton<GameWorld>.Instantiated)
            {
                Reset();
                return;
            }

            var world = Singleton<GameWorld>.Instance;
            if (world == null || world.MainPlayer == null)
            {
                return;
            }

            var worldInstanceId = world.GetInstanceID();
            if (_worldInstanceId != worldInstanceId)
            {
                Reset();
                _worldInstanceId = worldInstanceId;
            }

            var blackoutActive = BotBlackoutEffects.Active;
            if (!blackoutActive)
            {
                _blackoutWasActive = false;
                return;
            }

            if (!_blackoutWasActive)
            {
                _blackoutWasActive = true;
                _nextRefreshAt = 0f;
            }

            var now = Time.unscaledTime;
            if (now < _nextRefreshAt)
            {
                return;
            }

            _nextRefreshAt = now + RefreshSeconds;
            var affectedBots = 0;
            var nightVisionActivated = 0;
            var weaponLightsActivated = 0;

            foreach (var player in world.AllAlivePlayersList)
            {
                if (player == null || !player.IsAI)
                {
                    continue;
                }

                var bot = player.AIData?.BotOwner;
                if (bot == null || bot.IsDead || !BotBlackoutEffects.IsDarkFor(bot, null))
                {
                    continue;
                }

                affectedBots++;
                var botInstanceId = bot.GetInstanceID();
                if (Settings.BotEquipmentResponseEnabled.Value)
                {
                    try
                    {
                        if (TryActivateNightVision(bot))
                        {
                            nightVisionActivated++;
                        }
                        else if (TryActivateWeaponLight(bot))
                        {
                            weaponLightsActivated++;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_equipmentFailures.Add(botInstanceId))
                        {
                            _log.LogWarning(
                                $"Bot equipment response failed safely for {bot.ProfileId}: {ex.Message}");
                        }
                    }
                }

                if (_knownBots.Add(botInstanceId) && bot.LookSensor != null)
                {
                    bot.LookSensor.VisibleDist = bot.LookSensor.VisibleDist;
                }
            }

            if (!_activationLogged)
            {
                _activationLogged = true;
                _log.LogInfo(
                    $"Bot reactions active: affected={affectedBots}, "
                    + $"NVGs activated={nightVisionActivated}, "
                    + $"weapon lights activated={weaponLightsActivated}.");
            }
        }

        internal void Reset()
        {
            _worldInstanceId = 0;
            _nextRefreshAt = 0f;
            _blackoutWasActive = false;
            _activationLogged = false;
            _knownBots.Clear();
            _equipmentFailures.Clear();
        }

        private static bool TryActivateNightVision(BotOwner bot)
        {
            var nightVision = bot.NightVision;
            if (nightVision == null || !nightVision.HaveNightVision || nightVision.UsingNow)
            {
                return nightVision?.UsingNow == true;
            }

            nightVision.MoveToHeadAndToggleOn();
            return nightVision.UsingNow;
        }

        private static bool TryActivateWeaponLight(BotOwner bot)
        {
            var botLight = bot.BotLight;
            if (botLight == null || !botLight.CanUseBySettings)
            {
                return false;
            }

            var wasEnabled = botLight.IsEnable;
            botLight.IsInDarkPlace = true;
            botLight.FindLight();
            botLight.TurnOn(true);
            return !wasEnabled && botLight.IsEnable;
        }
    }

    internal static class BotBlackoutEffects
    {
        private const float BotLightVisionMultiplier = 0.80f;
        private const float PlayerLightRecognitionMultiplier = 0.90f;

        internal static bool Active => Settings.BotReactionsEnabled.Value
            && BlackoutController.Instance?.IsBlackoutActive == true;

        internal static bool IsDarkFor(BotOwner bot, IPlayer target)
        {
            if (!Active || bot == null || bot.IsDead)
            {
                return false;
            }

            return bot.AIData?.IsInside == true
                || target?.AIData?.IsInside == true
                || IsNight(bot.GameDateTime);
        }

        internal static float VisionDistanceMultiplier(BotOwner bot)
        {
            if (!IsDarkFor(bot, null) || bot.NightVision?.UsingNow == true)
            {
                return 1f;
            }

            var multiplier = Settings.UnaidedBotVisionPercent.Value / 100f;
            if (bot.BotLight?.IsEnable == true)
            {
                multiplier = Mathf.Max(multiplier, BotLightVisionMultiplier);
            }

            return multiplier;
        }

        internal static float RecognitionMultiplier(EnemyInfo enemy)
        {
            var bot = enemy?.Owner;
            var target = enemy?.Person;
            if (!IsDarkFor(bot, target) || bot.NightVision?.UsingNow == true)
            {
                return 1f;
            }

            var multiplier = Settings.UnaidedBotRecognitionPercent.Value / 100f;
            if (bot.BotLight?.IsEnable == true)
            {
                multiplier = Mathf.Max(multiplier, BotLightVisionMultiplier);
            }

            if (target?.AIData?.UsingLight == true)
            {
                multiplier = Mathf.Max(multiplier, PlayerLightRecognitionMultiplier);
            }

            return multiplier;
        }

        private static bool IsNight(GameDateTime gameDateTime)
        {
            if (gameDateTime == null)
            {
                return false;
            }

            var hour = gameDateTime.Calculate().Hour;
            return hour >= 20 || hour < 6;
        }
    }
}
