using System;
using System.Collections;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using EFT;
using EFT.Hideout;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace GoingDark.Client
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.SPT.core", "4.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.senze.goingdark";
        public const string PluginName = "Senze - Going Dark";
        public const string PluginVersion = "1.0.1";

        internal static ManualLogSource Log { get; private set; }

        private BlackoutController _blackoutController;
        private BotBlackoutController _botBlackoutController;
        private AudioClip _blackoutSound;
        private AudioClip _hideoutGeneratorSound;
        private AudioSource _blackoutAudioSource;
        private UnityWebRequest _blackoutSoundRequest;
        private string _blackoutSoundPath;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Settings.Bind(Config);

            _blackoutSoundPath = Path.Combine(
                Path.GetDirectoryName(typeof(Plugin).Assembly.Location),
                "blackout.wav");
            StartCoroutine(LoadHideoutGeneratorSound());

            _blackoutController = new BlackoutController(Logger, PlayBlackoutSound);
            BlackoutController.Instance = _blackoutController;
            _botBlackoutController = new BotBlackoutController(Logger);

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            SceneManager.sceneLoaded += OnSceneLoaded;

            Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
        }

        private void Update()
        {
            _blackoutController.Tick();
            _botBlackoutController.Tick();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _blackoutController.Reset();
            _botBlackoutController.Reset();
            ReleaseBlackoutSound();
            _hideoutGeneratorSound = null;
            if (_blackoutAudioSource != null)
            {
                Destroy(_blackoutAudioSource.gameObject);
            }

            BlackoutController.Instance = null;
            _harmony?.UnpatchSelf();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _blackoutController.SceneLoaded(scene);
        }

        private void PlayBlackoutSound()
        {
            if (!Settings.PlayEventSound.Value)
            {
                return;
            }

            if (Settings.EventSound.Value == BlackoutSoundMode.HideoutGenerator)
            {
                if (_hideoutGeneratorSound != null)
                {
                    StartCoroutine(PlayBlackoutClip(
                        _hideoutGeneratorSound,
                        "Hideout generator"));
                    return;
                }

                Logger.LogWarning(
                    "Tarkov's generator shutdown sound is unavailable. Falling back to blackout.wav.");
            }

            StartCoroutine(LoadAndPlayCustomBlackoutSound());
        }

        private IEnumerator LoadHideoutGeneratorSound()
        {
            var request = Resources.LoadAsync<HideoutAreasInfo>("Hideout/HideoutAreasInfo");
            yield return request;

            var areasInfo = request.asset as HideoutAreasInfo;
            if (areasInfo == null)
            {
                Logger.LogWarning("Could not load Tarkov's hideout area audio data.");
                yield break;
            }

            foreach (var areaTemplate in areasInfo.Templates)
            {
                if (areaTemplate == null || areaTemplate.Type != EAreaType.Generator)
                {
                    continue;
                }

                if (areaTemplate.DefaultSounds != null)
                {
                    areaTemplate.DefaultSounds.TryGetValue(
                        EAreaActivityType.TurnOff,
                        out _hideoutGeneratorSound);
                }

                if (_hideoutGeneratorSound == null && areaTemplate.Stages != null)
                {
                    foreach (var stage in areaTemplate.Stages.Values)
                    {
                        if (stage?.Sounds?.Data == null)
                        {
                            continue;
                        }

                        if (stage.Sounds.Data.TryGetValue(
                            EAreaActivityType.TurnOff,
                            out _hideoutGeneratorSound))
                        {
                            break;
                        }
                    }
                }

                break;
            }

            if (_hideoutGeneratorSound == null)
            {
                Logger.LogWarning("Tarkov's generator shutdown sound was not found.");
                yield break;
            }

            Logger.LogInfo(
                $"Loaded Tarkov generator shutdown sound '{_hideoutGeneratorSound.name}'.");
        }

        private IEnumerator LoadAndPlayCustomBlackoutSound()
        {
            if (!File.Exists(_blackoutSoundPath))
            {
                Logger.LogWarning(
                    $"Mid-raid blackout sound was not played. File not found: {_blackoutSoundPath}");
                yield break;
            }

            ReleaseBlackoutSound();
            var soundUri = new Uri(_blackoutSoundPath).AbsoluteUri;
            _blackoutSoundRequest = UnityWebRequestMultimedia.GetAudioClip(
                soundUri,
                AudioType.WAV);
            var request = _blackoutSoundRequest;
            var downloadHandler = request.downloadHandler as DownloadHandlerAudioClip;
            if (downloadHandler != null)
            {
                downloadHandler.streamAudio = false;
                downloadHandler.compressed = false;
            }

            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Logger.LogWarning($"Could not load blackout.wav: {request.error}");
                ReleaseBlackoutSound();
                yield break;
            }

            _blackoutSound = DownloadHandlerAudioClip.GetContent(request);
            if (_blackoutSound == null)
            {
                Logger.LogWarning("Could not load blackout.wav as an audio clip.");
                ReleaseBlackoutSound();
                yield break;
            }

            if (_blackoutSound.samples == 0)
            {
                Logger.LogWarning(
                    "Mid-raid blackout sound was not played because the loaded clip has no samples.");
                ReleaseBlackoutSound();
                yield break;
            }

            yield return PlayBlackoutClip(_blackoutSound, "blackout.wav");
            ReleaseBlackoutSound();
        }

        private IEnumerator PlayBlackoutClip(AudioClip clip, string soundName)
        {
            if (clip == null || clip.samples == 0)
            {
                Logger.LogWarning($"Blackout sound '{soundName}' has no audio samples.");
                yield break;
            }

            if (_blackoutAudioSource == null)
            {
                CreateBlackoutAudioSource();
            }

            PositionBlackoutAudioSource();
            _blackoutAudioSource.Stop();
            _blackoutAudioSource.clip = clip;
            _blackoutAudioSource.volume = Settings.EventSoundVolume.Value;
            _blackoutAudioSource.Play();

            var soundLength = clip.length;
            Logger.LogInfo(
                $"Started blackout sound '{soundName}' at volume " +
                $"{Settings.EventSoundVolume.Value:0.00}; " +
                $"samples={clip.samples}, spatial blend={_blackoutAudioSource.spatialBlend:0.00}, " +
                $"listener volume={AudioListener.volume:0.00}, paused={AudioListener.pause}.");

            yield return null;
            if (_blackoutAudioSource == null)
            {
                ReleaseBlackoutSound();
                yield break;
            }

            if (!_blackoutAudioSource.isPlaying)
            {
                Logger.LogWarning(
                    $"Blackout AudioSource did not start. " +
                    $"Clip state={clip.loadState}, samples={clip.samples}.");
                yield break;
            }

            Logger.LogInfo("Blackout AudioSource is playing.");
            yield return new WaitForSecondsRealtime(soundLength + 0.25f);
            if (_blackoutAudioSource != null && _blackoutAudioSource.clip == clip)
            {
                _blackoutAudioSource.Stop();
                _blackoutAudioSource.clip = null;
            }
        }

        private void CreateBlackoutAudioSource()
        {
            var soundObject = new GameObject("Going Dark blackout sound");
            soundObject.transform.SetParent(transform, false);

            _blackoutAudioSource = soundObject.AddComponent<AudioSource>();
            _blackoutAudioSource.playOnAwake = false;
            _blackoutAudioSource.loop = false;
            _blackoutAudioSource.mute = false;
            _blackoutAudioSource.priority = 0;
            _blackoutAudioSource.volume = Settings.EventSoundVolume.Value;
            _blackoutAudioSource.spatialBlend = 0.6f;
            _blackoutAudioSource.spread = 140f;
            _blackoutAudioSource.dopplerLevel = 0f;
            _blackoutAudioSource.rolloffMode = AudioRolloffMode.Linear;
            _blackoutAudioSource.minDistance = 8f;
            _blackoutAudioSource.maxDistance = 80f;
            _blackoutAudioSource.reverbZoneMix = 0.35f;
            _blackoutAudioSource.ignoreListenerPause = true;
            _blackoutAudioSource.ignoreListenerVolume = true;
            _blackoutAudioSource.bypassEffects = true;
            _blackoutAudioSource.bypassListenerEffects = true;
            _blackoutAudioSource.bypassReverbZones = false;
        }

        private void PositionBlackoutAudioSource()
        {
            var listener = FindObjectOfType<AudioListener>();
            if (listener == null)
            {
                _blackoutAudioSource.spatialBlend = 0f;
                return;
            }

            _blackoutAudioSource.spatialBlend = 0.6f;
            var direction = listener.transform.forward * 0.65f
                + listener.transform.right * 0.75f
                + Vector3.up * 0.15f;
            _blackoutAudioSource.transform.position = listener.transform.position
                + direction.normalized * 30f;
        }

        private void ReleaseBlackoutSound()
        {
            if (_blackoutAudioSource != null)
            {
                _blackoutAudioSource.Stop();
                _blackoutAudioSource.clip = null;
            }

            _blackoutSound = null;
            if (_blackoutSoundRequest != null)
            {
                _blackoutSoundRequest.Dispose();
                _blackoutSoundRequest = null;
            }
        }
    }
}
