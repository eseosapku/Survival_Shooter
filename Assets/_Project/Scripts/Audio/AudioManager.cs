using UnityEngine;

namespace Ricochet.Audio
{
    /// <summary>
    /// Single place that plays every sound (Singleton).
    /// - 1 looping 2D source for music
    /// - 1 2D source for UI / non-positional one-shots (PlayOneShot lets them overlap)
    /// - a small pool of 3D sources that are moved to the event position
    /// Enemies and bullets have NO AudioSource of their own, so components are never duplicated.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] SoundLibrary library;
        [SerializeField, Range(0f, 1f)] float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.25f;
        [SerializeField, Min(1)] int spatialSourceCount = 10;

        AudioSource _music;
        AudioSource _oneShot;
        AudioSource[] _spatial;
        int _nextSpatial;

        void Awake()
        {
            // Safe singleton: if a second one appears, it removes itself.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _music = CreateSource("Music", false);
            _music.loop = true;
            _oneShot = CreateSource("OneShot2D", false);

            _spatial = new AudioSource[spatialSourceCount];
            for (int i = 0; i < spatialSourceCount; i++)
                _spatial[i] = CreateSource($"Spatial3D_{i:00}", true);
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

        /// <summary>Plays a non-positional sound (UI, player weapon, countdown...).</summary>
        public void Play(SoundId id)
        {
            if (!library || !library.TryGet(id, out var e)) return;
            _oneShot.pitch = Random.Range(e.pitchRange.x, e.pitchRange.y);
            _oneShot.PlayOneShot(PickClip(e), e.volume * masterVolume);
        }

        /// <summary>Plays a sound at a world position using the next pooled 3D source (round-robin).</summary>
        public void PlayAt(SoundId id, Vector3 position)
        {
            if (!library || !library.TryGet(id, out var e)) return;
            if (!e.spatial)
            {
                Play(id);
                return;
            }

            var src = _spatial[_nextSpatial];
            _nextSpatial = (_nextSpatial + 1) % _spatial.Length;

            src.transform.position = position;
            src.pitch = Random.Range(e.pitchRange.x, e.pitchRange.y);
            src.clip = PickClip(e);
            src.volume = e.volume * masterVolume;
            src.Play();
        }

        public void PlayMusic()
        {
            if (!library || !library.TryGet(SoundId.Music, out var e)) return;
            if (_music.isPlaying) return;
            _music.clip = PickClip(e);
            _music.volume = musicVolume * masterVolume;
            _music.Play();
        }

        public void StopMusic() => _music.Stop();

        /// <summary>Music ignores Time.timeScale, so pause it explicitly.</summary>
        public void SetMusicPaused(bool paused)
        {
            if (paused) _music.Pause();
            else _music.UnPause();
        }

        static AudioClip PickClip(SoundLibrary.Entry e) => e.clips[Random.Range(0, e.clips.Length)];
    }
}
