using System.IO;
using GamelanRhythm;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MvpBuilder
{
    const string DataFolder = "Assets/Data/Songs";
    const string ResourcesFolder = "Assets/Resources";
    const string ScenePath = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Tools/Gamelan Rhythm/Build MVP")]
    public static void Build()
    {
        if (!Directory.Exists(ResourcesFolder)) Directory.CreateDirectory(ResourcesFolder);
        AssetDatabase.Refresh();
        EnsureCircleSprite();
        var demo = UpsertSong("DemoTest", "demo_test", "Demo / Test", "Standard", "DemoTestChart.json");
        var alternate = UpsertSong("AlternateTest", "alternate_test", "Alternate Test", "Short", "AlternateTestChart.json");
        var easy = UpsertTeyeSong("TeyeSosbudEasy", "teye_sosbud_easy", "Teye Sosbud", "Easy", "TeyeSosbudEasyChart.json", "Assets/Audio/Teye Sosbud Easy.mp3");
        var hard = UpsertTeyeSong("TeyeSosbudHard", "teye_sosbud_hard", "Teye Sosbud", "Hard", "TeyeSosbudHardChart.json", "Assets/Audio/Teye Sosbud Hard.mp3");
        const string databasePath = ResourcesFolder + "/SongDatabase.asset";
        var database = AssetDatabase.LoadAssetAtPath<SongDatabase>(databasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<SongDatabase>();
            AssetDatabase.CreateAsset(database, databasePath);
        }
        database.songs.Clear();
        database.songs.Add(demo);
        database.songs.Add(alternate);
        database.songs.Add(easy);
        database.songs.Add(hard);
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();

        var current = SceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 0, -10);
        camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
        camera.GetComponent<Camera>().backgroundColor = new Color(0.055f, 0.083f, 0.12f);
        var light = new GameObject("Directional Light", typeof(Light));
        light.GetComponent<Light>().type = LightType.Directional;
        var root = new GameObject("Gamelan Rhythm Game");
        var game = root.AddComponent<GamelanGame>();
        game.database = database;
        game.GenerateEditableUi();
        BindButton(root, "Game Canvas/Safe Area/Main Menu/Play", game.OpenSongs);
        BindButton(root, "Game Canvas/Safe Area/Main Menu/Exit", game.ExitGame);
        BindButton(root, "Game Canvas/Safe Area/Song Selection/Back", game.OpenMenu);
        BindButton(root, "Game Canvas/Safe Area/Player Setup/Start", game.StartSong);
        BindButton(root, "Game Canvas/Safe Area/Player Setup/Back", game.OpenSongs);
        BindButton(root, "Game Canvas/Safe Area/Results/Replay", game.StartSong);
        BindButton(root, "Game Canvas/Safe Area/Results/Menu", game.OpenMenu);
        EditorSceneManager.SaveScene(scene, ScenePath);
        var existing = EditorBuildSettings.scenes;
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>
        { new EditorBuildSettingsScene(ScenePath, true) };
        foreach (var entry in existing)
            if (entry.path != ScenePath) list.Add(entry);
        EditorBuildSettings.scenes = list.ToArray();
        if (!string.IsNullOrEmpty(current) && current != ScenePath)
            EditorSceneManager.OpenScene(current);
        Debug.Log("Gamelan Rhythm MVP assets and main scene built. Open " + ScenePath + " and press Play.");
    }

    [MenuItem("Tools/Gamelan Rhythm/Import Teye Sosbud Songs")]
    public static void ImportTeyeSosbudSongs()
    {
        AssetDatabase.Refresh();
        var easy = UpsertTeyeSong("TeyeSosbudEasy", "teye_sosbud_easy", "Teye Sosbud", "Easy", "TeyeSosbudEasyChart.json", "Assets/Audio/Teye Sosbud Easy.mp3");
        var hard = UpsertTeyeSong("TeyeSosbudHard", "teye_sosbud_hard", "Teye Sosbud", "Hard", "TeyeSosbudHardChart.json", "Assets/Audio/Teye Sosbud Hard.mp3");

        const string databasePath = ResourcesFolder + "/SongDatabase.asset";
        var database = AssetDatabase.LoadAssetAtPath<SongDatabase>(databasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<SongDatabase>();
            AssetDatabase.CreateAsset(database, databasePath);
        }

        database.songs.RemoveAll(song => song == null || song.songId == easy.songId || song.songId == hard.songId);
        database.songs.Add(easy);
        database.songs.Add(hard);
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log("Imported Teye Sosbud Easy and Hard. Easy: 98 notes; Hard: 114 notes. Frame timestamps were converted at 30 fps.");
    }

    static SongData UpsertTeyeSong(string assetName, string id, string title, string difficulty, string chartFile, string audioPath)
    {
        var song = UpsertSong(assetName, id, title, difficulty, chartFile);
        song.audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
        if (song.audioClip == null) Debug.LogError("Missing audio clip: " + audioPath);
        if (!ChartValidator.TryLoad(song, out var chart, out var error))
            Debug.LogError("Invalid Teye Sosbud chart " + id + ": " + error);
        else
            Debug.Log("Validated " + difficulty + " chart: " + chart.notes.Length + " notes.");
        EditorUtility.SetDirty(song);
        return song;
    }

    static SongData UpsertSong(string assetName, string id, string title, string difficulty, string chartFile)
    {
        string path = DataFolder + "/" + assetName + ".asset";
        var song = AssetDatabase.LoadAssetAtPath<SongData>(path);
        if (song == null)
        {
            song = ScriptableObject.CreateInstance<SongData>();
            AssetDatabase.CreateAsset(song, path);
        }
        song.songId = id;
        song.title = title;
        song.difficulty = difficulty;
        song.chartJson = AssetDatabase.LoadAssetAtPath<TextAsset>(DataFolder + "/" + chartFile);
        song.noteTravelDuration = 2f;
        if (song.chartJson == null) Debug.LogError("Missing chart file: " + chartFile);
        EditorUtility.SetDirty(song);
        return song;
    }

    static void EnsureCircleSprite()
    {
        const string path = ResourcesFolder + "/Circle.png";
        if (File.Exists(path)) return;
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32, 32));
                byte alpha = (byte)(255 * Mathf.Clamp01(32 - distance));
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    static void BindButton(GameObject root, string path, UnityAction callback)
    {
        var button = root.transform.Find(path).GetComponent<Button>();
        UnityEventTools.AddPersistentListener(button.onClick, callback);
        EditorUtility.SetDirty(button);
    }

    [MenuItem("Tools/Gamelan Rhythm/Run Logic Checks")]
    public static void RunLogicChecks()
    {
        var chart = new RhythmChart
        {
            version = 1, songId = "logic_check",
            notes = new[]
            {
                new ChartNote { time = 1, playerId = 1, noteIndex = 1 },
                new ChartNote { time = 1, playerId = 1, noteIndex = 2 },
                new ChartNote { time = 1, playerId = 2, noteIndex = 1 },
                new ChartNote { time = 2, playerId = 2, noteIndex = 6 }
            }
        };
        var session = new RhythmSession(chart);
        Require(session.Press(1, 6, 1, out _) == Judgment.None, "Wrong key must not score");
        Require(session.Press(1, 1, 1, out _) == Judgment.Perfect, "Player 1 perfect");
        Require(session.Press(1, 1, 1, out _) == Judgment.None, "Duplicate press must not score");
        Require(session.Press(1, 2, 1.1f, out _) == Judgment.Good, "Chord key must remain available");
        Require(session.Press(2, 1, 1, out _) == Judgment.Perfect, "Player 2 simultaneous note");
        Require(session.players[0].score == 170 && session.players[1].score == 100, "Scores must be independent");
        int misses = 0;
        session.Expire(2.16f, _ => misses++);
        session.Expire(2.20f, _ => misses++);
        Require(misses == 1 && session.players[1].miss == 1 && session.Complete, "Miss must resolve exactly once");
        Require(Mathf.Abs(session.players[0].Accuracy - 0.85f) < 0.001f, "Weighted accuracy");

        var song = ScriptableObject.CreateInstance<SongData>();
        song.songId = "logic_check";
        song.noteTravelDuration = 2;
        song.chartJson = new TextAsset(JsonUtility.ToJson(chart));
        Require(ChartValidator.TryLoad(song, out _, out _), "Valid chart rejected");
        chart.notes[0].noteIndex = 8;
        song.chartJson = new TextAsset(JsonUtility.ToJson(chart));
        Require(!ChartValidator.TryLoad(song, out _, out _), "Invalid note index accepted");
        Object.DestroyImmediate(song.chartJson);
        Object.DestroyImmediate(song);
        Debug.Log("Gamelan Rhythm logic checks passed: judgment, chords, players, misses, accuracy, and validation.");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new System.Exception("Gamelan Rhythm logic check failed: " + message);
    }
}
