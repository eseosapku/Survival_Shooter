using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ricochet.Audio
{
    /// <summary>
    /// Maps each SoundId to one or more clips plus volume and pitch variation.
    /// Swapping sounds never touches code: edit this asset instead.
    /// </summary>
    [CreateAssetMenu(menuName = "Ricochet/Sound Library", fileName = "SoundLibrary")]
    public class SoundLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public SoundId id;
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 0.8f;
            public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
            [Tooltip("Positional (3D) sounds are played from a pooled source at the event position.")]
            public bool spatial;
        }

        [SerializeField] List<Entry> entries = new List<Entry>();

        Dictionary<SoundId, Entry> _lookup;

        public bool TryGet(SoundId id, out Entry entry)
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<SoundId, Entry>();
                foreach (var e in entries)
                    _lookup[e.id] = e;
            }
            return _lookup.TryGetValue(id, out entry) && entry.clips != null && entry.clips.Length > 0;
        }

        void OnValidate() => _lookup = null;

#if UNITY_EDITOR
        /// <summary>Used by the editor builder to fill the library.</summary>
        public void EditorSetEntries(List<Entry> newEntries)
        {
            entries = newEntries;
            _lookup = null;
        }
#endif
    }
}
