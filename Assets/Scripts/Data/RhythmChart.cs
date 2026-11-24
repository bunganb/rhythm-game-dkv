using System;
using UnityEngine;

namespace GamelanRhythm
{
    [Serializable]
    public sealed class RhythmChart
    {
        public int version;
        public string songId;
        public ChartNote[] notes;
    }

    [Serializable]
    public sealed class ChartNote
    {
        public float time;
        public int playerId;
        public int noteIndex;
    }

    public static class ChartValidator
    {
        public static bool TryLoad(SongData song, out RhythmChart chart, out string error)
        {
            chart = null;
            if (song == null) { error = "Song asset is missing"; return false; }
            if (song.chartJson == null) { error = song.name + " has no chart JSON"; return false; }
            if (song.noteTravelDuration < 0.2f || float.IsNaN(song.noteTravelDuration) || float.IsInfinity(song.noteTravelDuration))
            { error = song.name + " has invalid note travel duration"; return false; }
            if (float.IsNaN(song.chartAudioOffset) || float.IsInfinity(song.chartAudioOffset))
            { error = song.name + " has invalid chart/audio offset"; return false; }
            try { chart = JsonUtility.FromJson<RhythmChart>(song.chartJson.text); }
            catch (Exception e) { error = song.name + " chart JSON could not be parsed: " + e.Message; return false; }
            if (chart == null || chart.version != 1) { error = song.name + " chart must use version 1"; return false; }
            if (chart.songId != song.songId) { error = song.name + " chart songId does not match " + song.songId; return false; }
            if (chart.notes == null || chart.notes.Length == 0) { error = song.name + " chart has no notes"; return false; }
            if (chart.notes.Length > 10000) { error = song.name + " chart exceeds 10000 notes"; return false; }
            float previous = -1;
            for (int i = 0; i < chart.notes.Length; i++)
            {
                var note = chart.notes[i];
                if (note == null || float.IsNaN(note.time) || float.IsInfinity(note.time) || note.time < 0)
                { error = song.name + " note " + i + " has invalid time"; return false; }
                if (note.time < previous) { error = song.name + " chart notes must be sorted by time (index " + i + ")"; return false; }
                if (note.playerId != 1 && note.playerId != 2)
                { error = song.name + " note " + i + " has invalid playerId"; return false; }
                if (note.noteIndex < 1 || note.noteIndex > 7)
                { error = song.name + " note " + i + " has invalid noteIndex"; return false; }
                previous = note.time;
            }
            error = null;
            return true;
        }
    }
}
