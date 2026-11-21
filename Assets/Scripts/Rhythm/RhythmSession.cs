using System;

namespace GamelanRhythm
{
    public enum Judgment { None, Perfect, Good, Miss }

    public sealed class PlayerScore
    {
        public int score, combo, maxCombo, perfect, good, miss, total;
        public float Accuracy => total == 0 ? 0 : (float)score / (total * 100f);
        public void Apply(Judgment judgment)
        {
            if (judgment == Judgment.Perfect) { score += 100; perfect++; combo++; }
            else if (judgment == Judgment.Good) { score += 70; good++; combo++; }
            else if (judgment == Judgment.Miss) { miss++; combo = 0; }
            if (combo > maxCombo) maxCombo = combo;
        }
    }

    public sealed class RhythmSession
    {
        public readonly RhythmChart chart;
        public readonly bool[] resolved;
        public readonly PlayerScore[] players = { new PlayerScore(), new PlayerScore() };
        public float perfectWindow = 0.060f;
        public float goodWindow = 0.150f;
        public int resolvedCount;

        public RhythmSession(RhythmChart chart)
        {
            this.chart = chart;
            resolved = new bool[chart.notes.Length];
            foreach (var note in chart.notes) players[note.playerId - 1].total++;
        }

        public Judgment Press(int playerId, int noteIndex, float songTime, out int eventIndex)
        {
            eventIndex = -1;
            float best = goodWindow + 0.0001f;
            for (int i = 0; i < chart.notes.Length; i++)
            {
                var note = chart.notes[i];
                if (resolved[i] || note.playerId != playerId || note.noteIndex != noteIndex) continue;
                float distance = Math.Abs(note.time - songTime);
                if (distance > goodWindow || distance >= best) continue;
                best = distance;
                eventIndex = i;
            }
            if (eventIndex < 0) return Judgment.None;
            resolved[eventIndex] = true;
            resolvedCount++;
            var result = best <= perfectWindow ? Judgment.Perfect : Judgment.Good;
            players[playerId - 1].Apply(result);
            return result;
        }

        public void Expire(float songTime, Action<int> onMiss)
        {
            for (int i = 0; i < chart.notes.Length; i++)
            {
                if (resolved[i] || songTime <= chart.notes[i].time + goodWindow) continue;
                resolved[i] = true;
                resolvedCount++;
                players[chart.notes[i].playerId - 1].Apply(Judgment.Miss);
                onMiss?.Invoke(i);
            }
        }

        public bool Complete => resolvedCount == chart.notes.Length;
    }
}
