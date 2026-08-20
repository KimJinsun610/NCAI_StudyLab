using System;
using System.Collections.Generic;
using UnityEngine;

namespace VARCO_Workshop
{
    [CreateAssetMenu(fileName = "SoundEventRegistry", menuName = "VARCO/데이터/사운드 이벤트 목록", order = 2)]
    public class SoundEventRegistry : ScriptableObject
    {
        [Serializable] public class Entry { public string id; public AudioClip clip; [Range(0,1)] public float volume = 1f; }
        public List<Entry> events = new List<Entry>();

        public bool TryGet(string id, out AudioClip clip, out float vol)
        {
            clip = null; vol = 1f;
            foreach (var e in events)
            {
                if (e != null && e.id == id) { clip = e.clip; vol = e.volume; return clip != null; }
            }
            return false;
        }
    }
}
