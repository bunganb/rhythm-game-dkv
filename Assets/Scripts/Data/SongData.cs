using UnityEngine;

namespace GamelanRhythm
{
    [CreateAssetMenu(menuName = "Gamelan Rhythm/Song")]
    public sealed class SongData : ScriptableObject
    {
        public string songId;
        public string title;
        public string difficulty = "Test";
        public AudioClip audioClip;
        public TextAsset chartJson;
        [Min(1)] public float bpm = 120;
        [Tooltip("Positive seconds delay chart hits relative to the audio. A chart time of 1 with offset 0.1 is judged 1.1 seconds after audio starts.")]
        public float chartAudioOffset;
        [Min(0.2f)] public float noteTravelDuration = 2f;
    }
}
