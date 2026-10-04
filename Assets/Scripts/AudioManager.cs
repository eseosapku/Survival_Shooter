using System.Collections.Generic;
using UnityEngine;

namespace Ricochet
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] GameConfig config;
        [SerializeField, Range(0f, 1f)] float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.25f;
        [SerializeField, Min(1)] int spatialSourceCount = 10;

        readonly Dictionary<SoundId, SoundEntry> sounds = new Dictionary<SoundId, SoundEntry>();
        AudioSource music;
        AudioSource effects;
        AudioSource[] spatial;
        int next;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            foreach (var s in config.Sounds)
                if (s.clips != null && s.clips.Length > 0) sounds[s.id] = s;

            music = CreateSource("Music", false);
            music.loop = true;
            effects = CreateSource("Effects", false);
            spatial = new AudioSource[spatialSourceCount];
            for (int i = 0; i < spatialSourceCount; i++)
                spatial[i] = CreateSource("Spatial " + i, true);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        AudioSource CreateSource(string sourceName, bool is3D)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = is3D ? 1f : 0f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 0.5f;
            src.maxDistance = 12f;
            src.dopplerLevel = 0f;
            return src;
        }

        public void Play(SoundId id)
        {
            if (!sounds.TryGetValue(id, out var s)) return;
            effects.pitch = Random.Range(s.pitchRange.x, s.pitchRange.y);
            effects.PlayOneShot(RandomClip(s), s.volume * masterVolume);
        }

        public void PlayAt(SoundId id, Vector3 position)
        {
            if (!sounds.TryGetValue(id, out var s)) return;
            if (!s.spatial)
            {
                Play(id);
                return;
            }

            var src = spatial[next];
            next = (next + 1) % spatial.Length;
            src.transform.position = position;
            src.pitch = Random.Range(s.pitchRange.x, s.pitchRange.y);
            src.clip = RandomClip(s);
            src.volume = s.volume * masterVolume;
            src.Play();
        }

        public void PlayMusic()
        {
            if (music.isPlaying || !sounds.TryGetValue(SoundId.Music, out var s)) return;
            music.clip = RandomClip(s);
            music.volume = musicVolume * masterVolume;
            music.Play();
        }

        static AudioClip RandomClip(SoundEntry s) => s.clips[Random.Range(0, s.clips.Length)];
    }
}
