using System.Collections.Generic;
using UnityEngine;

namespace Ricochet
{
    /// <summary>
    /// Single place that plays every sound (Singleton). Clips, volumes and pitch ranges come from GameConfig.
    /// - 1 looping 2D source for music
    /// - 1 2D source for UI / player one-shots (PlayOneShot lets them overlap)
    /// - a pool of 10 3D sources that are moved to the event position (round-robin)
    /// Enemies and projectiles have NO AudioSource of their own, so components are never duplicated.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] GameConfig config;
        [SerializeField, Range(0f, 1f)] float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.25f;
        [SerializeField, Min(1)] int spatialSourceCount = 10;

        readonly Dictionary<SoundId, SoundEntry> _lookup = new Dictionary<SoundId, SoundEntry>();
        AudioSource _music, _oneShot;
        AudioSource[] _spatial;
        int _next;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            foreach (var e in config.Sounds)
                if (e.clips != null && e.clips.Length > 0) _lookup[e.id] = e;

            _music = CreateSource("Music", false);
            _music.loop = true;
            _oneShot = CreateSource("OneShot2D", false);
            _spatial = new AudioSource[spatialSourceCount];
            for (int i = 0; i < spatialSourceCount; i++) _spatial[i] = CreateSource($"Spatial3D_{i:00}", true);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        AudioSource CreateSource(string sourceName, bool spatial)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = spatial ? 1f : 0f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 0.5f;
            src.maxDistance = 12f;
            src.dopplerLevel = 0f;
            return src;
        }

        /// <summary>Non-positional sound (UI, player weapon, countdown...).</summary>
        public void Play(SoundId id)
        {
            if (!_lookup.TryGetValue(id, out var e)) return;
            _oneShot.pitch = Random.Range(e.pitchRange.x, e.pitchRange.y);
            _oneShot.PlayOneShot(Pick(e), e.volume * masterVolume);
        }

        /// <summary>Positional sound from the next pooled 3D source.</summary>
        public void PlayAt(SoundId id, Vector3 position)
        {
            if (!_lookup.TryGetValue(id, out var e)) return;
            if (!e.spatial)
            {
                Play(id);
                return;
            }
            var src = _spatial[_next];
            _next = (_next + 1) % _spatial.Length;
            src.transform.position = position;
            src.pitch = Random.Range(e.pitchRange.x, e.pitchRange.y);
            src.clip = Pick(e);
            src.volume = e.volume * masterVolume;
            src.Play();
        }

        public void PlayMusic()
        {
            if (_music.isPlaying || !_lookup.TryGetValue(SoundId.Music, out var e)) return;
            _music.clip = Pick(e);
            _music.volume = musicVolume * masterVolume;
            _music.Play();
        }

        static AudioClip Pick(SoundEntry e) => e.clips[Random.Range(0, e.clips.Length)];
    }
}
