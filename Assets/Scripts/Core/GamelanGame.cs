using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GamelanRhythm
{
    public sealed class GamelanGame : MonoBehaviour
    {
        public SongDatabase database;
        [Header("Timing (seconds)")]
        [Range(0.01f, 0.15f)] public float perfectWindow = 0.060f;
        [Range(0.05f, 0.3f)] public float goodWindow = 0.150f;
        [Range(1f, 5f)] public float countdownSeconds = 3f;
        [Header("Optional feedback audio")]
        public AudioClip perfectSfx;
        public AudioClip goodSfx;
        public AudioClip missVoice;
        [Range(0, 1)] public float sfxVolume = 0.7f;
        [Range(1f, 1.5f)] public float missVoiceCooldown = 1.25f;

        enum Page { Menu, Songs, Setup, Playing, Results }
        Page page;
        SongData selectedSong;
        RhythmSession session;
        readonly List<Image> noteImages = new List<Image>();
        readonly PlayerView[] views = new PlayerView[2];
        [SerializeField] RectTransform safeRoot;
        Rect lastSafeArea;
        [SerializeField] GameObject menuPage, songsPage, setupPage, playPage, resultsPage;
        [SerializeField] RectTransform songList;
        [SerializeField] Button songButtonTemplate;
        [SerializeField] TMP_Text countdownLabel, progressLabel, resultOne, resultTwo, errorLabel;
        Animator parallaxAnimator;
        CanvasGroup menuCanvasGroup, songsCanvasGroup;
        Button parallaxClickArea;
        Button authoredPlayButton, authoredExitButton;
        bool usesAuthoredMenu;
        bool parallaxDown, parallaxTransitioning;
        const float ParallaxDuration = 1.25f;
        const float ParallaxPageSwapTime = 0.28f;
        AudioSource music, effects, voice;
        double dspStart;
        double lastVoiceDsp = -100;
        float lastNoteTime;
        bool interruptedByPause;

        static readonly Color Background = new Color(0.055f, 0.083f, 0.12f);
        static readonly Color PanelColor = new Color(0.10f, 0.16f, 0.22f);
        static readonly Color Gold = new Color(1f, 0.77f, 0.31f);
        static readonly Color Aqua = new Color(0.33f, 0.87f, 0.83f);

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            var authoredCanvas = transform.Find("Game Canvas");
            usesAuthoredMenu = authoredCanvas != null && FindNamed(authoredCanvas, "Play") != null &&
                               FindNamed(authoredCanvas, "Exit") != null;
            if (database == null) database = Resources.Load<SongDatabase>("SongDatabase");
            music = gameObject.AddComponent<AudioSource>();
            effects = gameObject.AddComponent<AudioSource>();
            voice = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = effects.playOnAwake = voice.playOnAwake = false;
            if (usesAuthoredMenu)
            {
                // The menu scene owns its Canvas hierarchy. Do not recreate the
                // old generated Safe Area or menu pages when those references are empty.
                EnsureEventSystem();
            }
            else if (safeRoot == null) BuildUi();
            else BindSceneUi();
            BindParallaxUi();
            // This authored menu has its own level select canvas. Let that
            // screen flow own PLAY while keeping the existing gameplay flow
            // available for scenes that do not have a level select canvas.
            if (usesAuthoredMenu && transform.Find("Level Select Canvas") != null &&
                GetComponent<LevelSelectTransition>() == null)
                gameObject.AddComponent<LevelSelectTransition>();
            BindButtons();
            Show(Page.Menu);
        }

        // Called by the editor builder. All UI objects are saved in the scene and remain editable.
        public void GenerateEditableUi()
        {
            BuildUi();
            menuPage.SetActive(true);
            songsPage.SetActive(false);
            setupPage.SetActive(false);
            playPage.SetActive(false);
            resultsPage.SetActive(false);
        }

        void BindSceneUi()
        {
            var zones = playPage.transform;
            views[0] = new PlayerView(this, zones.Find("Player 1"), 1);
            views[1] = new PlayerView(this, zones.Find("Player 2"), 2);
            var module = GetComponentInChildren<InputSystemUIInputModule>(true);
            if (module != null) module.AssignDefaultActions();
        }

        void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var eventGo = new GameObject("EventSystem", typeof(EventSystem));
            eventGo.transform.SetParent(transform, false);
            eventGo.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        void BindButtons()
        {
            if (usesAuthoredMenu)
            {
                var authoredCanvas = transform.Find("Game Canvas");
                if (GetComponent<LevelSelectTransition>() == null)
                    Bind(authoredCanvas.gameObject, "Play", OpenSongs);
                Bind(authoredCanvas.gameObject, "Exit", ExitGame);
            }
            else
            {
                Bind(menuPage, "Play", OpenSongs);
                Bind(menuPage, "Exit", ExitGame);
            }
            Bind(songsPage, "Back", OpenMenu);
            Bind(setupPage, "Start", StartSong);
            Bind(setupPage, "Back", OpenSongs);
            Bind(resultsPage, "Replay", StartSong);
            Bind(resultsPage, "Menu", OpenMenu);
            if (parallaxClickArea != null)
            {
                parallaxClickArea.onClick.RemoveListener(ScrollParallaxDown);
                parallaxClickArea.onClick.AddListener(ScrollParallaxDown);
            }
        }

        void BindParallaxUi()
        {
            var authoredCanvas = transform.Find("Game Canvas");
            Transform canvasRoot = authoredCanvas;
            if (canvasRoot == null)
            {
                if (safeRoot == null || safeRoot.parent == null) return;
                canvasRoot = safeRoot.parent;
            }
            parallaxAnimator = canvasRoot.GetComponent<Animator>();
            menuCanvasGroup = menuPage != null ? menuPage.GetComponent<CanvasGroup>() : null;
            songsCanvasGroup = songsPage != null ? songsPage.GetComponent<CanvasGroup>() : null;
            var clickArea = canvasRoot.Find("Parallax Click Area");
            if (clickArea != null) parallaxClickArea = clickArea.GetComponent<Button>();
            if (usesAuthoredMenu)
            {
                var play = FindNamed(canvasRoot, "Play");
                var exit = FindNamed(canvasRoot, "Exit");
                authoredPlayButton = play != null ? play.GetComponent<Button>() : null;
                authoredExitButton = exit != null ? exit.GetComponent<Button>() : null;
            }
            if (parallaxAnimator != null) parallaxAnimator.Rebind();
        }

        static Transform FindNamed(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindNamed(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        static void Bind(GameObject pageRoot, string buttonName, Action action)
        {
            if (pageRoot == null) return;
            var target = FindNamed(pageRoot.transform, buttonName);
            if (target == null) return;
            var button = target.GetComponent<Button>();
            if (button == null) return;
            if (button.onClick.GetPersistentEventCount() > 0) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action());
        }

        public void OpenMenu()
        {
            if (parallaxTransitioning) return;
            if (parallaxAnimator != null && parallaxDown)
            {
                parallaxTransitioning = true;
                SetParallaxButtonsInteractable(false);
                parallaxAnimator.SetTrigger("SlideTop");
                StartCoroutine(FinishReturnToTop());
                return;
            }
            Show(Page.Menu);
            RefreshParallaxClickArea();
        }
        public void ExitGame()
        {
            // The menu's EXIT button also serves as the way back from the scrolled layout.
            if (parallaxDown)
            {
                OpenMenu();
                return;
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Update()
        {
            if (safeRoot != null && Screen.safeArea != lastSafeArea) ApplySafeArea();
            if (page != Page.Playing || session == null) return;
            double elapsed = AudioSettings.dspTime - dspStart;
            float songTime = (float)elapsed - selectedSong.chartAudioOffset;
            if (elapsed < 0) countdownLabel.text = Math.Max(1, (int)Math.Ceiling(-elapsed)).ToString();
            else countdownLabel.text = elapsed < 0.55 ? "GO!" : "";
            progressLabel.text = elapsed < 0.55 ? "" : Math.Max(0, songTime).ToString("0.0") + " / " + lastNoteTime.ToString("0.0") + "s";
            session.Expire(songTime, OnMiss);
            for (int i = 0; i < noteImages.Count; i++)
            {
                var image = noteImages[i];
                if (image == null) continue;
                var note = session.chart.notes[i];
                float travel = selectedSong.noteTravelDuration;
                bool visible = !session.resolved[i] && songTime >= note.time - travel && songTime <= note.time + goodWindow;
                if (image.gameObject.activeSelf != visible) image.gameObject.SetActive(visible);
                if (visible)
                {
                    float t = (songTime - (note.time - travel)) / travel;
                    views[note.playerId - 1].PositionNote(image, t);
                }
            }
            double audioEnd = selectedSong.audioClip == null ? 0 : selectedSong.audioClip.length;
            if (session.Complete && elapsed > Math.Max(lastNoteTime + selectedSong.chartAudioOffset + goodWindow + 0.35f, audioEnd)) Finish();
        }

        void OnApplicationPause(bool paused)
        {
            // The Unity Editor reports window focus changes as application pauses.
            // Keep Play Mode sessions alive while switching between Editor windows.
            if (Application.isEditor) return;

            if (paused && page == Page.Playing)
            {
                interruptedByPause = true;
                music.Stop();
            }
            else if (!paused && interruptedByPause)
            {
                interruptedByPause = false;
                session = null;
                Show(Page.Menu);
                const string message = "Song stopped when the app was paused. Start again from song selection.";
                Debug.LogWarning(message);
                errorLabel.text = message;
            }
        }

        void BuildUi()
        {
            var canvasGo = transform.Find("Game Canvas")?.gameObject;
            usesAuthoredMenu = canvasGo != null && FindNamed(canvasGo.transform, "Play") != null && FindNamed(canvasGo.transform, "Exit") != null;
            if (canvasGo == null)
            {
                canvasGo = new GameObject("Game Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasGo.transform.SetParent(transform, false);
            }
            var canvas = canvasGo.GetComponent<Canvas>();
            if (canvas != null) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
            if (!usesAuthoredMenu)
            {
                Image fill = Ui.Image(canvasGo.transform, "Background", Background, Vector2.zero, Vector2.one);
                fill.raycastTarget = false;
            }
            safeRoot = FindNamed(canvasGo.transform, "Safe Area") as RectTransform;
            if (safeRoot == null) safeRoot = Ui.Rect(canvasGo.transform, "Safe Area", Vector2.zero, Vector2.one);
            ApplySafeArea();
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var eventGo = new GameObject("EventSystem", typeof(EventSystem));
                eventGo.transform.SetParent(transform, false);
                eventGo.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            if (usesAuthoredMenu)
            {
                var menu = safeRoot.Find("Main Menu");
                if (menu == null) menu = Ui.Rect(safeRoot, "Main Menu", Vector2.zero, Vector2.one);
                menuPage = menu.gameObject;
                if (menuPage.GetComponent<CanvasGroup>() == null) menuPage.AddComponent<CanvasGroup>();
            }
            else menuPage = BuildMenu();
            songsPage = BuildSongs();
            setupPage = BuildSetup();
            playPage = BuildPlay();
            resultsPage = BuildResults();
            errorLabel = Ui.Text(safeRoot, "Error", "", 28, Color.white, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.01f), new Vector2(0.9f, 0.1f));
            errorLabel.raycastTarget = false;
        }

        void ApplySafeArea()
        {
            if (safeRoot == null) return;
            lastSafeArea = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            safeRoot.anchorMin = new Vector2(lastSafeArea.xMin / Screen.width, lastSafeArea.yMin / Screen.height);
            safeRoot.anchorMax = new Vector2(lastSafeArea.xMax / Screen.width, lastSafeArea.yMax / Screen.height);
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        }

        GameObject BuildMenu()
        {
            var root = Ui.Rect(safeRoot, "Main Menu", Vector2.zero, Vector2.one).gameObject;
            Ui.Text(root.transform, "Title", "GAMELAN RHYTHM", 78, Gold, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.62f), new Vector2(0.9f, 0.85f));
            Ui.Text(root.transform, "Subtitle", "Two players  •  One screen  •  Timed notes", 32, Aqua, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.53f), new Vector2(0.9f, 0.62f));
            Ui.Button(root.transform, "Play", "PLAY", new Vector2(0.38f, 0.32f), new Vector2(0.62f, 0.45f), Gold, () => OpenSongs());
            Ui.Button(root.transform, "Exit", "EXIT", new Vector2(0.42f, 0.15f), new Vector2(0.58f, 0.24f), PanelColor, () => Application.Quit());
            return root;
        }

        GameObject BuildSongs()
        {
            var existing = safeRoot.Find("Song Selection");
            var root = existing != null ? existing.gameObject : Ui.Rect(safeRoot, "Song Selection", Vector2.zero, Vector2.one).gameObject;
            Ui.Text(root.transform, "Title", "SELECT A SONG", 56, Gold, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.78f), new Vector2(0.9f, 0.92f));
            songList = Ui.Rect(root.transform, "Song List", new Vector2(0.22f, 0.21f), new Vector2(0.78f, 0.75f));
            songButtonTemplate = Ui.Button(songList, "Song Entry Template", "SONG", new Vector2(0, 0.84f),
                new Vector2(1, 1), PanelColor, null);
            songButtonTemplate.gameObject.SetActive(false);
            Ui.Button(root.transform, "Back", "EXIT", new Vector2(0.03f, 0.04f), new Vector2(0.18f, 0.15f), PanelColor, () => Show(Page.Menu));
            return root;
        }

        GameObject BuildSetup()
        {
            var root = Ui.Rect(safeRoot, "Player Setup", Vector2.zero, Vector2.one).gameObject;
            Ui.Text(root.transform, "Title", "READY TO PLAY?", 62, Gold, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.76f), new Vector2(0.9f, 0.91f));
            Ui.Text(root.transform, "Players", "TOP: PLAYER 2  •  PEKING\n\nBOTTOM: PLAYER 1  •  SARON\n\nTap the numbered key when its note reaches the circle.",
                38, Color.white, TextAlignmentOptions.Center, new Vector2(0.18f, 0.32f), new Vector2(0.82f, 0.72f));
            Ui.Button(root.transform, "Start", "START", new Vector2(0.39f, 0.13f), new Vector2(0.61f, 0.25f), Gold, StartSong);
            Ui.Button(root.transform, "Back", "BACK", new Vector2(0.03f, 0.04f), new Vector2(0.18f, 0.15f), PanelColor, OpenSongs);
            return root;
        }

        GameObject BuildPlay()
        {
            var root = Ui.Rect(safeRoot, "Gameplay", Vector2.zero, Vector2.one).gameObject;
            views[0] = new PlayerView(this, root.transform, 1, "SARON", new Vector2(0, 0), new Vector2(1, 0.495f), false);
            views[1] = new PlayerView(this, root.transform, 2, "PEKING", new Vector2(0, 0.505f), new Vector2(1, 1), true);
            Ui.Image(root.transform, "Center Divider", Gold, new Vector2(0, 0.495f), new Vector2(1, 0.505f)).raycastTarget = false;
            countdownLabel = Ui.Text(root.transform, "Countdown", "", 90, Gold, TextAlignmentOptions.Center,
                new Vector2(0.43f, 0.42f), new Vector2(0.57f, 0.58f));
            progressLabel = Ui.Text(root.transform, "Progress", "", 23, Color.white, TextAlignmentOptions.Center,
                new Vector2(0.41f, 0.485f), new Vector2(0.59f, 0.515f));
            return root;
        }

        GameObject BuildResults()
        {
            var root = Ui.Rect(safeRoot, "Results", Vector2.zero, Vector2.one).gameObject;
            Ui.Text(root.transform, "Title", "RESULTS", 66, Gold, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.82f), new Vector2(0.9f, 0.94f));
            resultOne = Ui.Text(root.transform, "Player 1 Results", "", 36, Color.white, TextAlignmentOptions.Center,
                new Vector2(0.07f, 0.28f), new Vector2(0.48f, 0.8f));
            resultTwo = Ui.Text(root.transform, "Player 2 Results", "", 36, Color.white, TextAlignmentOptions.Center,
                new Vector2(0.52f, 0.28f), new Vector2(0.93f, 0.8f));
            Ui.Button(root.transform, "Replay", "REPLAY", new Vector2(0.26f, 0.08f), new Vector2(0.46f, 0.2f), Gold, StartSong);
            Ui.Button(root.transform, "Menu", "MENU", new Vector2(0.54f, 0.08f), new Vector2(0.74f, 0.2f), PanelColor, () => Show(Page.Menu));
            return root;
        }

        void Show(Page next)
        {
            page = next;
            if (menuPage != null) menuPage.SetActive(next == Page.Menu);
            if (songsPage != null) songsPage.SetActive(next == Page.Songs);
            if (setupPage != null) setupPage.SetActive(next == Page.Setup);
            if (playPage != null) playPage.SetActive(next == Page.Playing);
            if (resultsPage != null) resultsPage.SetActive(next == Page.Results);
            if (next != Page.Playing) music.Stop();
            if (errorLabel != null) errorLabel.text = "";
            RefreshParallaxClickArea();
        }

        public void OpenSongs()
        {
            // A persistent Inspector event can still call OpenSongs from the
            // authored Play button. Route it to the level select flow instead
            // of touching the song list that belongs to the gameplay UI.
            if (transform.Find("Level Select Canvas") != null)
            {
                var levelSelect = GetComponent<LevelSelectTransition>();
                if (levelSelect == null) levelSelect = gameObject.AddComponent<LevelSelectTransition>();
                levelSelect.OpenLevelSelect();
                return;
            }

            if (parallaxTransitioning) return;
            if (parallaxAnimator != null && page == Page.Menu)
            {
                parallaxTransitioning = true;
                SetParallaxButtonsInteractable(false);
                OpenSongsNow();
                parallaxAnimator.SetTrigger("OpenSongs");
                StartCoroutine(FinishOpenSongs());
                return;
            }
            OpenSongsNow();
        }

        void OpenSongsNow()
        {
            if (songList == null)
            {
                Debug.LogError("Song list UI is missing from this scene.", this);
                return;
            }

            foreach (Transform child in songList)
                if (child != songButtonTemplate.transform) Destroy(child.gameObject);
            string databaseError = null;
            if (database == null || !database.Validate(out databaseError))
            {
                Show(Page.Songs);
                Error(database == null ? "SongDatabase missing. Run Tools > Gamelan Rhythm > Build MVP." : databaseError);
                return;
            }
            for (int i = 0; i < database.songs.Count; i++)
            {
                var song = database.songs[i];
                bool valid = ChartValidator.TryLoad(song, out _, out string validationError);
                float top = 1f - i * 0.19f;
                var button = Instantiate(songButtonTemplate, songList);
                button.gameObject.name = "Song " + i;
                button.gameObject.SetActive(true);
                button.image.color = valid ? PanelColor : new Color(0.28f, 0.15f, 0.17f);
                var rect = button.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, top - 0.16f);
                rect.anchorMax = new Vector2(1, top);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                button.GetComponentInChildren<TMP_Text>().text = valid ? song.title + "  /  " + song.difficulty : song.title + "  (INVALID)";
                button.onClick.AddListener(() => { selectedSong = song; Show(Page.Setup); });
                button.interactable = valid;
                if (!valid) Debug.LogError(validationError, song);
            }
            Show(Page.Songs);
        }

        public void ScrollParallaxDown()
        {
            if (parallaxAnimator == null || page != Page.Menu || parallaxDown || parallaxTransitioning) return;
            parallaxTransitioning = true;
            SetParallaxButtonsInteractable(false);
            if (parallaxClickArea != null) parallaxClickArea.gameObject.SetActive(false);
            parallaxAnimator.SetTrigger("SlideDown");
            StartCoroutine(FinishScrollDown());
        }

        IEnumerator FinishScrollDown()
        {
            yield return new WaitForSecondsRealtime(ParallaxDuration);
            parallaxDown = true;
            parallaxTransitioning = false;
            if (menuCanvasGroup != null) menuCanvasGroup.interactable = true;
            if (songsCanvasGroup != null) songsCanvasGroup.interactable = false;
            if (authoredPlayButton != null) authoredPlayButton.interactable = true;
            if (authoredExitButton != null) authoredExitButton.interactable = true;
            RefreshParallaxClickArea();
        }

        IEnumerator FinishOpenSongs()
        {
            yield return new WaitForSecondsRealtime(ParallaxDuration);
            parallaxDown = true;
            parallaxTransitioning = false;
            if (menuCanvasGroup != null) menuCanvasGroup.interactable = false;
            if (songsCanvasGroup != null) songsCanvasGroup.interactable = true;
            if (authoredPlayButton != null) authoredPlayButton.interactable = false;
            if (authoredExitButton != null) authoredExitButton.interactable = false;
            RefreshParallaxClickArea();
        }

        IEnumerator FinishReturnToTop()
        {
            yield return new WaitForSecondsRealtime(ParallaxPageSwapTime);
            Show(Page.Menu);

            yield return new WaitForSecondsRealtime(ParallaxDuration - ParallaxPageSwapTime);
            parallaxDown = false;
            parallaxTransitioning = false;
            if (menuCanvasGroup != null) menuCanvasGroup.interactable = true;
            if (songsCanvasGroup != null) songsCanvasGroup.interactable = false;
            if (authoredPlayButton != null) authoredPlayButton.interactable = true;
            if (authoredExitButton != null) authoredExitButton.interactable = true;
            RefreshParallaxClickArea();
        }

        void SetParallaxButtonsInteractable(bool interactable)
        {
            if (menuCanvasGroup != null) menuCanvasGroup.interactable = interactable;
            if (songsCanvasGroup != null) songsCanvasGroup.interactable = interactable;
            if (authoredPlayButton != null) authoredPlayButton.interactable = interactable;
            if (authoredExitButton != null) authoredExitButton.interactable = interactable;
        }

        void RefreshParallaxClickArea()
        {
            if (parallaxClickArea != null)
                parallaxClickArea.gameObject.SetActive(page == Page.Menu && !parallaxTransitioning);
        }

        public void RestoreMenuAfterLevelSelect()
        {
            // LevelSelectTransition disables the whole main-menu Canvas while
            // it is away. Rebind its animator when returning so any parallax
            // clip/state left active before navigation cannot keep the menu
            // click area in a stale state.
            if (parallaxAnimator != null)
            {
                parallaxAnimator.Rebind();
                parallaxAnimator.Update(0f);
            }

            parallaxDown = false;
            parallaxTransitioning = false;
            page = Page.Menu;
            if (menuCanvasGroup != null) menuCanvasGroup.interactable = true;
            if (songsCanvasGroup != null) songsCanvasGroup.interactable = false;
            if (authoredPlayButton != null) authoredPlayButton.interactable = true;
            if (authoredExitButton != null) authoredExitButton.interactable = true;
            RefreshParallaxClickArea();
        }

        public void StartSong()
        {
            if (!ChartValidator.TryLoad(selectedSong, out var chart, out string error)) { Error(error); return; }
            session = new RhythmSession(chart) { perfectWindow = Mathf.Min(perfectWindow, goodWindow), goodWindow = goodWindow };
            lastNoteTime = chart.notes[chart.notes.Length - 1].time;
            lastVoiceDsp = -100;
            for (int i = 0; i < 2; i++) views[i].Reset();
            foreach (var image in noteImages) if (image != null) Destroy(image.gameObject);
            noteImages.Clear();
            foreach (var note in chart.notes) noteImages.Add(views[note.playerId - 1].CreateNote(note.noteIndex));
            music.Stop();
            music.clip = selectedSong.audioClip;
            dspStart = AudioSettings.dspTime + countdownSeconds;
            if (music.clip != null) music.PlayScheduled(dspStart);
            Show(Page.Playing);
        }

        public void Press(int playerId, int noteIndex)
        {
            if (page != Page.Playing || session == null) return;
            float songTime = (float)(AudioSettings.dspTime - dspStart) - selectedSong.chartAudioOffset;
            var judgment = session.Press(playerId, noteIndex, songTime, out _);
            if (judgment == Judgment.None) { views[playerId - 1].WrongTap(); return; }
            views[playerId - 1].Judge(judgment, session.players[playerId - 1]);
            var clip = judgment == Judgment.Perfect ? perfectSfx : goodSfx;
            if (clip != null) effects.PlayOneShot(clip, sfxVolume);
        }

        void OnMiss(int eventIndex)
        {
            int player = session.chart.notes[eventIndex].playerId - 1;
            views[player].Judge(Judgment.Miss, session.players[player]);
            double now = AudioSettings.dspTime;
            if (missVoice != null && now - lastVoiceDsp >= missVoiceCooldown)
            {
                voice.PlayOneShot(missVoice, sfxVolume);
                lastVoiceDsp = now;
            }
        }

        void Finish()
        {
            var one = session.players[0];
            var two = session.players[1];
            resultOne.text = ResultBlock("PLAYER 1 • SARON", one);
            resultTwo.text = ResultBlock("PLAYER 2 • PEKING", two);
            Show(Page.Results);
        }

        static string ScoreLine(PlayerScore score) => score.score + " pts  •  " + (score.Accuracy * 100).ToString("0.0") + "%";
        static string ResultBlock(string title, PlayerScore score) => title + "\n\n" + ScoreLine(score) + "\n\n" +
            "Perfect  " + score.perfect + "\nGood  " + score.good + "\nMiss  " + score.miss + "\n\nMax combo  " + score.maxCombo;
        void Error(string message)
        {
            Debug.LogError(message);
            if (errorLabel != null) errorLabel.text = message;
        }

        sealed class PlayerView
        {
            readonly GamelanGame game;
            readonly int playerId;
            readonly RectTransform lane;
            readonly Image noteTemplate;
            readonly TMP_Text score, popup;
            readonly Image flash;
            readonly Color accent;
            Coroutine popupRoutine;

            public PlayerView(GamelanGame game, Transform parent, int id, string instrument, Vector2 min, Vector2 max, bool rotated)
            {
                this.game = game;
                playerId = id;
                accent = id == 1 ? Gold : Aqua;
                var zone = Ui.Image(parent, "Player " + id, id == 1 ? new Color(0.09f, 0.16f, 0.22f) : new Color(0.08f, 0.20f, 0.22f), min, max);
                zone.raycastTarget = false;
                if (rotated) zone.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
                Ui.Text(zone.transform, "Name", "P" + id + "  •  " + instrument, 35, accent, TextAlignmentOptions.Left,
                    new Vector2(0.06f, 0.79f), new Vector2(0.45f, 0.96f));
                score = Ui.Text(zone.transform, "Score", "0  •  COMBO 0", 30, Color.white, TextAlignmentOptions.Right,
                    new Vector2(0.56f, 0.79f), new Vector2(0.94f, 0.96f));
                lane = Ui.Rect(zone.transform, "Note Lane", new Vector2(0.18f, 0.45f), new Vector2(0.82f, 0.73f));
                Ui.Image(lane, "Track", new Color(0.035f, 0.07f, 0.11f), Vector2.zero, Vector2.one).raycastTarget = false;
                var target = Ui.Circle(lane, "Hit Target", accent, new Vector2(0.12f, 0.5f), new Vector2(0.12f, 0.5f));
                target.rectTransform.sizeDelta = new Vector2(96, 96);
                target.raycastTarget = false;
                var center = Ui.Circle(lane, "Target Center", new Color(0.035f, 0.07f, 0.11f),
                    new Vector2(0.12f, 0.5f), new Vector2(0.12f, 0.5f));
                center.rectTransform.sizeDelta = new Vector2(72, 72);
                center.raycastTarget = false;
                noteTemplate = Ui.Circle(lane, "Note Template", accent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                noteTemplate.rectTransform.sizeDelta = new Vector2(92, 92);
                noteTemplate.raycastTarget = false;
                Ui.Text(noteTemplate.transform, "Number", "1", 48, Background, TextAlignmentOptions.Center,
                    Vector2.zero, Vector2.one).raycastTarget = false;
                noteTemplate.gameObject.SetActive(false);
                popup = Ui.Text(zone.transform, "Judgment", "", 54, accent, TextAlignmentOptions.Center,
                    new Vector2(0.37f, 0.7f), new Vector2(0.63f, 0.9f));
                flash = Ui.Image(zone.transform, "Flash", new Color(accent.r, accent.g, accent.b, 0), new Vector2(0.17f, 0.44f), new Vector2(0.83f, 0.74f));
                flash.raycastTarget = false;
                for (int key = 1; key <= 6; key++)
                {
                    float left = 0.055f + (key - 1) * 0.15f;
                    var hit = Ui.Image(zone.transform, "Key " + key, new Color(0.17f, 0.25f, 0.3f),
                        new Vector2(left, 0.07f), new Vector2(left + 0.135f, 0.39f));
                    var face = Ui.Image(hit.transform, "Face", accent, new Vector2(0.03f, 0.06f), new Vector2(0.97f, 0.94f));
                    face.raycastTarget = false;
                    Ui.Text(face.transform, "Number", key.ToString(), 54, Background, TextAlignmentOptions.Center,
                        Vector2.zero, Vector2.one).raycastTarget = false;
                    var input = hit.gameObject.AddComponent<InstrumentKey>();
                    input.Initialize(game, id, key, face);
                }
            }

            public PlayerView(GamelanGame game, Transform zone, int id)
            {
                this.game = game;
                playerId = id;
                accent = id == 1 ? Gold : Aqua;
                lane = zone.Find("Note Lane").GetComponent<RectTransform>();
                noteTemplate = lane.Find("Note Template").GetComponent<Image>();
                score = zone.Find("Score").GetComponent<TMP_Text>();
                popup = zone.Find("Judgment").GetComponent<TMP_Text>();
                flash = zone.Find("Flash").GetComponent<Image>();
                foreach (var key in zone.GetComponentsInChildren<InstrumentKey>(true))
                {
                    int index = int.Parse(key.gameObject.name.Substring(4));
                    key.Initialize(game, id, index, key.transform.Find("Face").GetComponent<Image>());
                }
            }

            public Image CreateNote(int key)
            {
                var image = UnityEngine.Object.Instantiate(noteTemplate, lane);
                image.gameObject.name = "Note " + key;
                image.GetComponentInChildren<TMP_Text>(true).text = key.ToString();
                image.gameObject.SetActive(false);
                return image;
            }

            public void PositionNote(Image image, float fraction)
            {
                float width = lane.rect.width;
                image.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(width * 0.40f, -width * 0.38f, fraction), 0);
            }

            public void Reset()
            {
                score.text = "0  •  COMBO 0";
                popup.text = "";
                flash.color = new Color(accent.r, accent.g, accent.b, 0);
            }

            public void Judge(Judgment judgment, PlayerScore state)
            {
                score.text = state.score + "  •  COMBO " + state.combo;
                string message = judgment == Judgment.Perfect ? "JOS!" : judgment == Judgment.Good ? "KURANG!" : "MELESET!";
                Color color = judgment == Judgment.Perfect ? Gold : judgment == Judgment.Good ? Aqua : new Color(0.9f, 0.4f, 0.43f);
                Feedback(message, color, judgment == Judgment.Perfect ? 1f : 0.65f);
            }

            public void WrongTap() => Feedback("", new Color(0.5f, 0.58f, 0.62f), 0.15f);

            void Feedback(string message, Color color, float strength)
            {
                if (popupRoutine != null) game.StopCoroutine(popupRoutine);
                popupRoutine = game.StartCoroutine(Animate(message, color, strength));
            }

            IEnumerator Animate(string message, Color color, float strength)
            {
                popup.text = message;
                popup.color = color;
                float duration = 0.34f;
                for (float time = 0; time < duration; time += Time.unscaledDeltaTime)
                {
                    float t = time / duration;
                    popup.rectTransform.localScale = Vector3.one * (1.25f - 0.25f * Mathf.Clamp01(t * 4));
                    flash.color = new Color(color.r, color.g, color.b, strength * 0.22f * (1 - t));
                    yield return null;
                }
                popup.text = "";
                popup.rectTransform.localScale = Vector3.one;
                flash.color = new Color(color.r, color.g, color.b, 0);
                popupRoutine = null;
            }
        }
    }

    static class Ui
    {
        static Sprite circleSprite;

        public static Image Circle(Transform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            var image = Image(parent, name, color, min, max);
            if (circleSprite == null) circleSprite = Resources.Load<Sprite>("Circle");
            if (circleSprite == null)
            {
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.name = "Generated Circle";
                texture.filterMode = FilterMode.Bilinear;
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                        byte alpha = (byte)(255 * Mathf.Clamp01(size / 2f - distance));
                        pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                    }
                texture.SetPixels32(pixels);
                texture.Apply();
                circleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            }
            image.sprite = circleSprite;
            return image;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static Image Image(Transform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            var rect = Rect(parent, name, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static TMP_Text Text(Transform parent, string name, string value, float size, Color color,
            TextAlignmentOptions alignment, Vector2 min, Vector2 max)
        {
            var rect = Rect(parent, name, min, max);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = value;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Max(14, size * 0.55f);
            label.fontSizeMax = size;
            label.raycastTarget = false;
            return label;
        }

        public static Button Button(Transform parent, string name, string caption, Vector2 min, Vector2 max,
            Color color, Action onClick)
        {
            var image = Image(parent, name, color, min, max);
            var button = image.gameObject.AddComponent<Button>();
            if (onClick != null) button.onClick.AddListener(() => onClick());
            Text(image.transform, "Caption", caption, 38, Color.white, TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            return button;
        }
    }
}
