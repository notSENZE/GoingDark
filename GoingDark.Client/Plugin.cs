using System;
using System.Collections;
using System.IO;
using BepInEx;
using BepInEx.Logging;
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
        public const string PluginName = "Going Dark";
        public const string PluginVersion = "0.3.0";

        internal static ManualLogSource Log { get; private set; }

        private BlackoutController _blackoutController;
        private DiagnosticController _diagnosticController;
        private AudioClip _blackoutSound;
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

            _blackoutController = new BlackoutController(Logger, PlayBlackoutSound);
            BlackoutController.Instance = _blackoutController;
            _diagnosticController = new DiagnosticController(Logger);

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            SceneManager.sceneLoaded += OnSceneLoaded;

            Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
        }

        private void Update()
        {
            _blackoutController.Tick();
            _diagnosticController.Tick();
        }

        private void OnGUI()
        {
            _diagnosticController.DrawOverlay();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _blackoutController.Reset();
            _diagnosticController.Reset();
            ReleaseBlackoutSound();
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
            _diagnosticController.SceneLoaded();
        }

        private void PlayBlackoutSound()
        {
            if (!Settings.PlayEventSound.Value)
            {
                return;
            }

            StartCoroutine(LoadAndPlayBlackoutSound());
        }

        private IEnumerator LoadAndPlayBlackoutSound()
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

            if (_blackoutAudioSource == null)
            {
                CreateBlackoutAudioSource();
            }

            if (_blackoutSound.samples == 0)
            {
                Logger.LogWarning(
                    "Mid-raid blackout sound was not played because the loaded clip has no samples.");
                ReleaseBlackoutSound();
                yield break;
            }

            _blackoutAudioSource.Stop();
            _blackoutAudioSource.clip = _blackoutSound;
            _blackoutAudioSource.volume = Settings.EventSoundVolume.Value;
            _blackoutAudioSource.Play();

            var soundLength = _blackoutSound.length;
            Logger.LogInfo(
                $"Started mid-raid blackout sound at volume " +
                $"{Settings.EventSoundVolume.Value:0.00}; " +
                $"samples={_blackoutSound.samples}, " +
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
                    $"Clip state={_blackoutSound.loadState}, samples={_blackoutSound.samples}.");
                ReleaseBlackoutSound();
                yield break;
            }

            Logger.LogInfo("Blackout AudioSource is playing.");
            yield return new WaitForSecondsRealtime(soundLength + 0.25f);
            ReleaseBlackoutSound();
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
            _blackoutAudioSource.spatialBlend = 0f;
            _blackoutAudioSource.ignoreListenerPause = true;
            _blackoutAudioSource.ignoreListenerVolume = true;
            _blackoutAudioSource.bypassEffects = true;
            _blackoutAudioSource.bypassListenerEffects = true;
            _blackoutAudioSource.bypassReverbZones = true;
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
