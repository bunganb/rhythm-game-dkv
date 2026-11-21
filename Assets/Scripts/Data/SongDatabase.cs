using System.Collections.Generic;
using UnityEngine;

namespace GamelanRhythm
{
    [CreateAssetMenu(menuName = "Gamelan Rhythm/Song Database")]
    public sealed class SongDatabase : ScriptableObject
    {
        public List<SongData> songs = new List<SongData>();

        public bool Validate(out string error)
        {
            var ids = new HashSet<string>();
            for (int i = 0; i < songs.Count; i++)
            {
                var song = songs[i];
                if (song == null) { error = "Song database contains a null entry at index " + i; return false; }
                if (string.IsNullOrWhiteSpace(song.songId)) { error = "Song at index " + i + " has no ID"; return false; }
                if (!ids.Add(song.songId)) { error = "Duplicate song ID: " + song.songId; return false; }
            }
            error = null;
            return true;
        }
    }
}
