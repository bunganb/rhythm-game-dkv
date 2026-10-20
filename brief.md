# Gamelan Rhythm — Playable MVP Implementation Brief

**Document:** `brief.md`  
**Purpose:** Master specification and execution plan for Codex  
**Target:** Existing Unity 2D URP project; Android/iOS phones and tablets, landscape  
**Scope:** Complete playable, extensible MVP with placeholder visuals and test chart; **no final song or composed music**.

## 1. Product vision and non-negotiable constraints

Build a local two-player gamelan rhythm game on **one shared touchscreen**. Players sit opposite one another: **Player 1 (Saron)** uses the bottom half and **Player 2 (Peking)** uses the top half with the entire player-facing presentation rotated 180°. Each player has **six tappable instrument keys** (numbered 1–6). A **horizontal rhythm lane** in each player area shows incoming notes traveling toward a circular hit target. The player must press the matching key **at the correct timestamp**; this is a timed rhythm game, **not free-play**. Both charts share one song clock but input, judgment, score, combo and feedback remain independent.

Follow the supplied low-fidelity reference when it is available in the project. If the reference cannot be accessed, use the written specification, document layout assumptions, and make placement/spacing Inspector-configurable. The low-fi suggests a menu, player/instrument setup, opposed player areas, instrument keys, horizontal note lanes, countdown, and custom hit feedback. Do not invent final art.

**No final music deliverable.** The MVP must still be testable end-to-end without music: use a deterministic sample chart with multiple notes per player, including simultaneous hits, and a silent scheduled clock or simple non-musical metronome/test pulse if useful. AudioClip assignment for future songs must work. Do not silently generate or bundle a full song. Procedural test SFX are allowed, but all real voice/instrument audio is replaceable via Inspector.

## 2. Definition of done

A user can open the Unity project, enter the main menu, choose a test song, start the game, see a 3-2-1-GO countdown, play two independent six-key horizontal rhythm charts on one landscape touchscreen, receive timing-based judgment and game-feel feedback, finish the chart, view separate player results, and replay or return to menu. The sample chart and silent/test audio mode are sufficient to demonstrate the entire loop. A developer can add a real song by creating/assigning audio and timestamp chart data **without editing gameplay scripts or building a new scene**.

## 3. Core game rules

- One shared song timeline, separate player-specific note streams.
- Six notes/keys per instrument; chart `noteIndex` is **1 through 6**; convert to zero-based only inside code.
- Chart note: `time` (target hit time in seconds), `playerId` (1 or 2), `noteIndex` (1..6). Optional unique event ID can be generated when loading. Equal timestamps are supported across players and different keys, including chords within one player.
- A key press checks only the nearest **unresolved** event for the same player and note index within the judgment window. Resolve each event at most once; no double scoring. Incorrect keys must not consume a different note.
- Default windows, adjustable in Inspector: **JOS!/Perfect** absolute error <= 0.060 s; **KURANG!/Good** <= 0.150 s; otherwise no successful hit. A note still unresolved after `hitTime + 0.150 s` becomes **MELESET!/Miss**. Avoid double Miss on wrong taps and expiry. Make policy for wrong taps explicit (small no-score feedback, no note consumption, no voice spam).
- Scores (starting defaults): Perfect 100, Good 70, Miss 0. Combo increases on Perfect/Good, resets on Miss. Track totals, hit counts and accuracy independently; define accuracy clearly (e.g. weighted score divided by maximum possible score). Display per-player results.
- Feedback text: **JOS!** (accurate), **KURANG!** (less accurate), **MELESET!** (miss). On Miss, play an **optional assigned voice clip** saying “Lolololo”; do not synthesize a final recorded voice. Use a configurable 1–1.5 s cooldown on the voice so simultaneous or dense misses do not overlap excessively. The visual Miss still appears for every missed note. Both players have independent visuals and the voice playback policy is explicitly defined.
- A key should squash/stretch immediately on press, independent of judgment. Stronger particles/flash and popup follow successful judgment; small muted feedback for miss. Never treat every tap as successful.
- Define an end condition: once the scheduled test chart is exhausted and the final judgment window has passed (and optional audio has ended), show Results. Allow replay, return to menu and safe restart.

## 4. Rhythm clock, chart and audio sync

Use `AudioSettings.dspTime` as the authoritative clock for real audio playback. Schedule an assigned song `AudioClip` with `AudioSource.PlayScheduled` sufficiently ahead of playback; song time = DSP now - scheduled start DSP time, adjusted by a clearly documented chart/audio calibration offset. **Define offset sign convention** in code and README. For silent/test mode, use a scheduled DSP start with no AudioClip so the same timeline logic runs. Countdown must lead into the scheduled start consistently. Pause/resume must not introduce timing drift: either implement a correct pause-aware clock/audio policy or disable pausing during active song with an explicit explanation and UI state; never pretend `AudioSource.Pause` alone freezes `dspTime`.

`spawnTime = hitTime - noteTravelDuration` (default 2 s). Compute note position **from absolute song time**, not accumulated `deltaTime`, so frame drops do not cause long-term drift. Handle notes with negative spawn times at song start (pre-roll) and note expiry deterministically. Visual travel direction must be correct **in each player's local coordinates**, taking 180-degree UI rotation into account. One circular target per player; key identity is visible on traveling notes. Do not accidentally build six separate lanes unless required by reference; start with one horizontal lane per player supporting numbered notes.

Data architecture:

- `SongData` ScriptableObject: unique song ID, title, optional `AudioClip`, `TextAsset` chart JSON, BPM metadata (not authoritative for timestamp timing), chart/audio offset, note travel duration, optional difficulty and preview metadata.
- `SongDatabase` ScriptableObject: collection of SongData references; validate duplicate IDs and null entries.
- `RhythmChart` serializable DTO parsed from JSON, containing version, songId and list of timestamp note events. Unity `JsonUtility` compatibility must be considered; use a wrapper object rather than top-level JSON array.
- Runtime `SongSession`/selection state holds selected SongData without hardcoding song references into gameplay. Do not create a singleton unless justified; avoid hidden persistent-state bugs.
- Validate chart version, unique song ID consistency, finite nonnegative times, player IDs 1/2, noteIndex 1..6, event count and sorting. Log actionable errors and prevent starting invalid charts.
- Adding a song: import audio, create chart JSON, create SongData asset, register in SongDatabase. No code changes. A minimal song selection screen can list SongData assets; include one `Demo/Test` SongData with chart and **no real song**.

Example chart (`TextAsset` JSON):

```json
{
  "version": 1,
  "songId": "demo_test",
  "notes": [
    { "time": 2.00, "playerId": 1, "noteIndex": 1 },
    { "time": 2.25, "playerId": 2, "noteIndex": 3 },
    { "time": 2.50, "playerId": 1, "noteIndex": 2 },
    { "time": 2.50, "playerId": 2, "noteIndex": 5 },
    { "time": 3.00, "playerId": 1, "noteIndex": 4 }
  ]
}
```

Expand the demo to at least 10–20 events **per player** with a few simultaneous hits and deliberate gaps; this is test data, not a composed song.

## 5. Screens and UX

1. **Main Menu:** placeholder game title, Play, Exit/Back as appropriate to platform.
2. **Song Selection:** data-driven list; includes Demo/Test; invalid songs cannot start; can be minimal.
3. **Player/Instrument Setup:** visually indicate P1=Saron and P2=Peking; instrument assignment is fixed in MVP, so do not imply unimplemented instrument swapping. Start/Ready button(s).
4. **Gameplay:** 2 opposed half-screen player areas; P2 content rotated 180 degrees; each has six large instrument keys, one horizontal note lane, circular hit target, player label, judgment popup, score/combo; center divider and unobtrusive shared countdown/progress. Avoid text overlap and unsafe edges. Touch targets should be comfortable on small landscape phones.
5. **Results:** score, accuracy, Perfect/Good/Miss counts, max combo for both players; Replay and Menu.

Use uGUI + TextMeshPro, `CanvasScaler` Scale With Screen Size (initial reference 1920x1080), anchors/layout groups as appropriate, safe-area handling, and test 16:9 and 19.5:9 landscape. UI rotation must not break pointer raycasts. One EventSystem and a compatible Input System UI module; support simultaneous multi-pointer presses on independent buttons. Editor mouse test is not evidence of true multitouch; verify on device when available.

## 6. Game juice and audio feedback

All feedback is modular and configurable, not embedded in rhythm timing logic:

- **Immediate press:** animate the visual child of the key, not its hitbox. Example squash `(1.12, 0.85, 1)` for 0.06 s, overshoot `(0.95, 1.08, 1)` then return to `(1,1,1)` over ~0.12 s. Restart cleanly on rapid presses. Add short color flash.
- **JOS!:** prominent popup scale/bounce, brief burst particles, strong flash, optional hit SFX.
- **KURANG!:** smaller popup, mild bounce/flash, optional softer hit SFX.
- **MELESET!:** muted popup/small shake; optional “Lolololo” voice via assigned AudioClip with cooldown. No mandatory voice asset.
- Prefer Coroutine + AnimationCurve and procedural UI placeholder sprites; no DOTween or other paid/external dependency required. Pool transient note and particle UI objects or keep allocation controlled for mobile.
- Provide adjustable durations, colors, particle counts, SFX volume and voice cooldown in Inspector. Keep effects readable and light; avoid full-screen shake or flashes that interfere with either player.
- The shared song audio should not be restarted or disrupted by hit feedback; independent AudioSource(s) for music and SFX/voice as needed.

## 7. Suggested modular code structure

```text
Assets/
  Scenes/
    MainMenu.unity
    Gameplay.unity
    Results.unity                  # or an equivalent Results panel
  Scripts/
    Core/      GameFlowController, SongSession
    Data/      SongData, SongDatabase, RhythmChart DTO, ChartValidator
    Rhythm/    SongClock, RhythmManager, NoteSpawner, NoteView, HitJudge
    Player/    PlayerInputHandler, PlayerScore
    UI/        InstrumentKeyButton, PlayerGameplayView, CountdownUI,
               SongSelectionUI, ResultsUI
    Feedback/  InstrumentKeyFeedback, JudgmentFeedbackController
    Editor/    PrototypeSceneBuilder (if reliable scene generation is needed)
  Data/
    Songs/     DemoTest.asset, SongDatabase.asset, DemoTestChart.json
  Audio/       (empty or optional non-musical test SFX)
```

Names are suggestions; favor clarity and consistency over rigid naming. If direct `.unity` serialization is risky, write an idempotent **Unity Editor** scene/prefab builder, placed under `Editor/`, and execute it to generate usable scenes. Do not require the user to hand-wire dozens of fields. Preserve existing URP settings and unrelated project files.

## 8. Execution phases — finish ALL phases, not just scaffolding

### Phase 0 — Inspect and plan
- Inspect Unity version, existing project hierarchy, installed Input System/TextMeshPro/URP packages, active input handling and build targets.
- Identify modifications, assumptions and technical risks. Do not replace project configuration indiscriminately.

### Phase 1 — Layout and input
- Implement menu/gameplay placeholders, two opposed player views, six keys each, rotated P2 view, responsive layout and multi-pointer input.
- Make every key report its player ID and note index. Add immediate squash/stretch feedback.
- Verify UI structure, mouse input and code compilation.

### Phase 2 — Data-driven catalog and chart
- Implement SongData, SongDatabase, chart DTO/validator, Demo/Test SongData, runtime song selection and sample timestamp chart.
- Demonstrate adding a second **test chart** through assets only, without code changes; do not create a second music track.

### Phase 3 — Timed gameplay
- Implement DSP-based song clock including silent mode, countdown, horizontal note spawning/positioning, input matching, judgment, auto-miss and simultaneous hits.
- Resolve each event exactly once; keep both player states independent.

### Phase 4 — Juice, scoring and results
- Implement custom JOS!/KURANG!/MELESET! popup and judgment effects, optional SFX and “Lolololo” voice slot/cooldown, score/combo/accuracy, completion and results/replay.

### Phase 5 — QA, mobile readiness and handoff
- Test 16:9 and 19.5:9 landscape layouts, UI rotation, rapid tapping, wrong keys, early/late taps, same-time events, missing AudioClip, invalid chart, empty chart, repeated replay, and frame-rate variability.
- Configure landscape orientation and supported mobile input settings without breaking existing project. Build if environment supports it; otherwise clearly identify steps that require the Unity Editor or physical device.
- Write concise setup/usage README and a list of what was actually verified vs not verified.

## 9. Acceptance test matrix

| Test | Expected result |
| --- | --- |
| Start Demo/Test from menu | Countdown then playable chart without final song audio |
| P1 presses key 1 | Only P1 key 1 input registered |
| P2 presses key 6 | Only P2 key 6 input registered despite 180-degree rotation |
| Two players tap at once | Independent touch IDs; both presses processed |
| Two notes at identical time | Both can be hit and scored independently |
| Perfectly timed correct key | JOS!, Perfect score, combo increase |
| Slightly off correct key | KURANG!, Good score, combo increase |
| Not pressed before expiry | MELESET!, combo reset, optional voice with cooldown |
| Wrong key | No unrelated note consumed or scored |
| Repeated rapid taps | No animation corruption or double scoring |
| Restart chart | All notes, score, clock, feedback and combo reset |
| No AudioClip | Silent DSP test mode still completes |
| Invalid chart data | Clear error; cannot start broken song |
| Add a new SongData/chart | New entry appears in selection without code changes |
| Wide phone and 16:9 tablet | Controls visible, reachable and not clipped |

## 10. Out of scope

- Final gamelan song, composition, music production or licensed music.
- Final instrument art, detailed character animation, localization and monetization.
- Online multiplayer, networking, account system, cloud save, leaderboards.
- Runtime song downloading, user-generated chart editor, automatic beat detection.
- Full accessibility/QA certification beyond basic readable UI and mobile touch targets.

## 11. Instructions to Codex

1. **Read this entire `brief.md` and inspect the existing Unity project before changing code.**
2. Execute Phases 0–5 in order, maintaining a compiling/runnable project after each phase. Do not stop after generating a plan or Phase 1 unless blocked by a genuine tool/environment limitation.
3. Use placeholder shapes, labels and sample timestamp data; do not create a final song. Ensure the demo remains playable without a music file.
4. Favor a real scene/prefab setup or an idempotent Editor scene generator; no piles of disconnected scripts requiring manual wiring.
5. Keep chart/song data separate from gameplay code. No hardcoded timestamps in gameplay scripts.
6. Do not claim compilation, device multitouch, playback synchronization or build success unless actually tested. Report environment limitations accurately.
7. Finish with: changed file list, how to open/run the MVP, how to add a song, chart schema, Inspector configuration, test results, known limitations and recommended next steps.
8. Preserve all unrelated existing assets and settings. Never delete or overwrite project content without reason.

**Final objective:** a genuinely playable, data-driven two-player gamelan rhythm MVP using test notes and placeholder visuals, ready for real audio and chart authoring later.
