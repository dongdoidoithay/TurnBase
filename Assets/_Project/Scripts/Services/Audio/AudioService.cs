using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Services.Audio
{
    public interface IAudioService
    {
        void PlaySfx(string key, float volumeScale = 1f, float pitch = 1f);
        void PlayMusic(string key, bool loop = true, float fadeSeconds = 0.8f);
        void StopMusic(float fadeSeconds = 0.5f);
        /// <summary>Hạ BGM tạm thời (Ultimate, cutscene) — plan.md §12.</summary>
        void DuckMusic(float duration, float amount = 0.5f);
        void SetVolumes(float bgm, float sfx);
    }

    /// <summary>
    /// Phát BGM/SFX bằng pool AudioSource. Không cấp phát trong lúc phát —
    /// ngân sách 0 GC/frame khi chiến đấu (plan.md §11.11).
    /// </summary>
    public sealed class AudioService : MonoBehaviour, IAudioService
    {
        private const int SFX_VOICES = 16;
        private const string SFX_RESOURCE_ROOT = "Audio/SFX/";
        private const string BGM_RESOURCE_ROOT = "Audio/BGM/";

        private readonly Dictionary<string, AudioClip> _clipCache = new();
        private AudioSource[] _sfxVoices;
        private int _nextVoice;

        private AudioSource _musicA, _musicB;
        private AudioSource _activeMusic;
        private string _currentMusicKey;

        private float _bgmVolume = 0.7f;
        private float _sfxVolume = 0.9f;

        private float _duckUntil;
        private float _duckAmount = 1f;
        private float _fadeSpeed;
        private float _targetMusicVolume;

        public static AudioService Create(Transform parent)
        {
            var go = new GameObject("AudioService");
            go.transform.SetParent(parent, false);
            return go.AddComponent<AudioService>();
        }

        private void Awake()
        {
            _sfxVoices = new AudioSource[SFX_VOICES];
            for (int i = 0; i < SFX_VOICES; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                _sfxVoices[i] = s;
            }

            _musicA = gameObject.AddComponent<AudioSource>();
            _musicB = gameObject.AddComponent<AudioSource>();
            foreach (var m in new[] { _musicA, _musicB })
            {
                m.playOnAwake = false;
                m.loop = true;
                m.spatialBlend = 0f;
                m.volume = 0f;
            }
            _activeMusic = _musicA;
        }

        private void Update()
        {
            // Ducking
            if (_duckUntil > 0f && Time.unscaledTime > _duckUntil)
            {
                _duckUntil = 0f;
                _duckAmount = 1f;
            }

            // Crossfade
            if (_fadeSpeed > 0f)
            {
                float target = _targetMusicVolume * _bgmVolume * _duckAmount;
                _activeMusic.volume = Mathf.MoveTowards(_activeMusic.volume, target,
                                                        _fadeSpeed * Time.unscaledDeltaTime);
                var other = _activeMusic == _musicA ? _musicB : _musicA;
                if (other.isPlaying)
                {
                    other.volume = Mathf.MoveTowards(other.volume, 0f,
                                                     _fadeSpeed * Time.unscaledDeltaTime);
                    if (other.volume <= 0.001f) other.Stop();
                }
            }
            else
            {
                _activeMusic.volume = _targetMusicVolume * _bgmVolume * _duckAmount;
            }
        }

        // =====================================================================

        public void PlaySfx(string key, float volumeScale = 1f, float pitch = 1f)
        {
            if (string.IsNullOrEmpty(key)) return;
            var clip = LoadClip(SFX_RESOURCE_ROOT + key);
            if (clip == null) return;

            var voice = _sfxVoices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % SFX_VOICES;

            voice.Stop();
            voice.clip = clip;
            voice.volume = Mathf.Clamp01(_sfxVolume * volumeScale);
            voice.pitch = pitch;
            voice.Play();
        }

        public void PlayMusic(string key, bool loop = true, float fadeSeconds = 0.8f)
        {
            if (string.IsNullOrEmpty(key) || key == _currentMusicKey) return;
            var clip = LoadClip(BGM_RESOURCE_ROOT + key);
            if (clip == null) return;

            _currentMusicKey = key;
            var next = _activeMusic == _musicA ? _musicB : _musicA;

            next.clip = clip;
            next.loop = loop;
            next.volume = 0f;
            next.Play();

            _activeMusic = next;
            _targetMusicVolume = 1f;
            _fadeSpeed = fadeSeconds > 0.01f ? 1f / fadeSeconds : 0f;
            if (_fadeSpeed <= 0f) _activeMusic.volume = _bgmVolume;
        }

        public void StopMusic(float fadeSeconds = 0.5f)
        {
            _currentMusicKey = null;
            _targetMusicVolume = 0f;
            _fadeSpeed = fadeSeconds > 0.01f ? 1f / fadeSeconds : 0f;
            if (_fadeSpeed <= 0f) { _musicA.Stop(); _musicB.Stop(); }
        }

        public void DuckMusic(float duration, float amount = 0.5f)
        {
            _duckAmount = Mathf.Clamp01(amount);
            _duckUntil = Time.unscaledTime + duration;
        }

        public void SetVolumes(float bgm, float sfx)
        {
            _bgmVolume = Mathf.Clamp01(bgm);
            _sfxVolume = Mathf.Clamp01(sfx);
        }

        // =====================================================================

        private AudioClip LoadClip(string path)
        {
            if (_clipCache.TryGetValue(path, out var cached)) return cached;

            var clip = Resources.Load<AudioClip>(path);
            if (clip == null)
            {
                // Không log spam mỗi frame — chỉ ghi 1 lần rồi cache null
                Debug.LogWarning($"[Audio] Không tìm thấy clip '{path}'");
            }
            _clipCache[path] = clip;
            return clip;
        }
    }
}
