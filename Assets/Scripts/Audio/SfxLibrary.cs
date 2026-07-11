using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Named clip registry wired by editor automation from Assets/Audio/SFX.
/// </summary>
[CreateAssetMenu(fileName = "SfxLibrary", menuName = "Escape/Sfx Library")]
public class SfxLibrary : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public string id;
        public AudioClip clip;
    }

    public Entry[] entries;

    Dictionary<string, AudioClip> lookup;

    public AudioClip Get(string id)
    {
        if (lookup == null)
        {
            lookup = new Dictionary<string, AudioClip>(entries.Length);
            foreach (Entry e in entries)
                if (e.clip != null && !lookup.ContainsKey(e.id))
                    lookup.Add(e.id, e.clip);
        }
        lookup.TryGetValue(id, out AudioClip clip);
        return clip;
    }
}
