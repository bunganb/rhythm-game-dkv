# Gamelan Rhythm MVP

A two-player rhythm game for one landscape touchscreen. Player 1 (Saron) plays at the bottom; Player 2 (Peking) plays from the opposite side. Song selection includes Teye Sosbud Easy and Hard with audio, plus two silent test charts.

## Open and play

1. Open the project in Unity 6000.3.19f1.
2. Open `Assets/Scenes/MainMenu.unity` and press Play. The scene is first in Build Settings.
3. Choose **Teye Sosbud (Easy)**, **Teye Sosbud (Hard)**, or either test chart, review the fixed instrument assignments, and press Start.
4. After the 3–2–1 countdown, tap the numbered key when its note reaches the circular target. The results screen offers Replay and Menu.

If the generated scene or data assets need repair, run **Tools > Gamelan Rhythm > Build MVP**. It recreates the demo assets and **replaces `MainMenu.unity`**, so use it before making manual UI edits. It does not change `SampleScene.unity`. Run **Tools > Gamelan Rhythm > Run Logic Checks** for the focused timing and data checks.

## Timing and scoring

- Each song has one `AudioSettings.dspTime` timeline. If an AudioClip is assigned, it is started with `AudioSource.PlayScheduled`; otherwise the same scheduled timeline runs silently.
- `chartAudioOffset` is in seconds. **Positive means chart hits occur later than the audio**: chart time 1.0 with offset +0.1 is judged 1.1 seconds after audio starts. For a silent chart, leave it at zero.
- A note begins at `time - noteTravelDuration`; its position is recalculated from absolute song time every frame. Notes whose spawn time is before zero can appear during countdown.
- `JOS!` (Perfect) is within ±0.060 s and scores 100. `KURANG!` (Good) is within ±0.150 s and scores 70. An unresolved note after `time + 0.150 s` becomes `MELESET!` (Miss), scores zero, and resets combo. Windows are adjustable on the `GamelanGame` component.
- A tap only considers unresolved notes for the same player and key. Wrong taps animate the key and show a small muted flash, but never consume a note, score, or play the miss voice.
- Accuracy is `earned score / (number of chart notes × 100)`. Results are separate for each player.
- Teye Sosbud's source sheet stores CapCut's 30 fps timestamps as Excel times displayed like `0:12:21`. Those three fields mean **minutes:seconds:frames** here, even though Excel treats them as hours:minutes:seconds. They were converted to chart seconds as `minutes × 60 + seconds + frames / 30`; the final frame field was not treated as milliseconds.
- Active gameplay has no pause control. Switching focus between Unity Editor windows does not cancel Play Mode. If a built app is backgrounded, the song is stopped and the player returns to the menu on resume with an interruption message. Replay starts a new session and resets all note and score state.
- Optional `perfectSfx`, `goodSfx`, and `missVoice` AudioClips are assigned on the scene's `GamelanGame` component. Miss voice playback uses one shared cooldown (default 1.25 s) across both players; every miss still gets visual feedback. Music uses a separate AudioSource.

## Add a song without code changes

1. Create a JSON TextAsset in `Assets/Data/Songs/` using the schema below. Times are seconds from audio start, must be nonnegative, finite, and sorted ascending; equal timestamps support chords and simultaneous players. Use 1 or 2 for `playerId` and 1–6 for `noteIndex`.
2. Create a **Gamelan Rhythm > Song** asset. Set a unique `songId` matching the JSON, title, chart TextAsset, optional AudioClip, BPM metadata, calibration offset, and note travel duration.
3. Add that SongData asset to `Assets/Resources/SongDatabase.asset`. It will appear in song selection automatically. Invalid charts are disabled with an error in the Console.

```json
{
  "version": 1,
  "songId": "my_song",
  "notes": [
    { "time": 1.5, "playerId": 1, "noteIndex": 3 },
    { "time": 1.5, "playerId": 2, "noteIndex": 5 }
  ]
}
```

## Layout and testing

The imported Teye Sosbud charts contain 98 Easy notes (33 Saron, 65 Peking) and 114 Hard notes (57 for each instrument). The audio and chart are linked by the `TeyeSosbudEasy` and `TeyeSosbudHard` SongData assets. Use **Tools > Gamelan Rhythm > Import Teye Sosbud Songs** to restore those entries in the song database; **Build MVP** also includes them.

The Canvas, screens, text, buttons, keys, and note lane are saved as editable GameObjects in `MainMenu.unity`. Edit them directly in the Hierarchy and Inspector. The disabled `Song Entry Template` under Song Selection is cloned for each registered song; each player lane has a disabled `Note Template` cloned for chart notes. The scene uses a 1920×1080 CanvasScaler and `Screen.safeArea`. Player 2's complete panel is rotated 180°. The Input System UI module handles pointer events; each key acts on pointer down so two touches can be independent. The project allows landscape left and right orientation only.

Verified in the Unity Editor: scripts compile without errors, the menu → selection → setup → gameplay → results loop renders and completes in silent mode, 16:9 and 19.5:9 Game View layouts were inspected, all twelve key pointer-down handlers animate, and logic checks cover wrong keys, duplicate presses, chords, simultaneous players, misses, accuracy, and invalid note indices. Editor mouse and scripted pointer invocation do not establish physical multitouch behavior or audio synchronization on a device. Test those on Android/iOS hardware before release. No mobile build or real song playback was verified.
