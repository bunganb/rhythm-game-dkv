using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace GamelanRhythm
{
    /// <summary>
    /// Slides between the authored main menu and level select canvas, then pops in
    /// the level select elements. UI layout remains editable in the scene.
    /// </summary>
    public sealed class LevelSelectTransition : MonoBehaviour
    {
        static bool openLevelSelectOnNextMainMenuLoad;

        [SerializeField] RectTransform mainMenuCanvas;
        [SerializeField] RectTransform levelSelectCanvas;
        [SerializeField] RectTransform introVideoCanvas;
        [SerializeField, Min(0.1f)] float slideDuration = 0.7f;
        [SerializeField, Min(0.05f)] float popDuration = 0.24f;
        [SerializeField, Min(0f)] float popStagger = 0.07f;
        [SerializeField, Min(0f)] float videoNextButtonDelay = 14f;
        [SerializeField] string gameplaySceneName = "Gameplay";

        static readonly string[] PopOrder =
        {
            "song-1", "song-2",
            "decoration", "decoration (1)", "decoration (2)", "decoration (3)",
            "label-1", "label-2", "back-to-home-btn", "next-button",
            "level-title-head"
        };

        static readonly string[] SlideDownOrder = { "side_left", "side_right" };

        readonly List<PopItem> popItems = new List<PopItem>();
        readonly Dictionary<RectTransform, Coroutine> activeBounces = new Dictionary<RectTransform, Coroutine>();
        CanvasGroup levelCanvasGroup;
        VideoPlayer introVideoPlayer;
        Button introVideoNextButton;
        Vector2 menuHomePosition;
        Vector2 levelHomePosition;
        Vector2 videoHomePosition;
        bool isTransitioning;
        bool isIntroVideoPlaying;
        float slideDistance;

        sealed class PopItem
        {
            public RectTransform rect;
            public CanvasGroup group;
            public Vector3 targetScale;
            public Vector2 targetPosition;
            public bool slidesDown;
        }

        void Awake()
        {
            if (mainMenuCanvas == null)
            {
                var menu = transform.Find("Game Canvas");
                if (menu != null) mainMenuCanvas = menu as RectTransform;
            }
            if (levelSelectCanvas == null)
            {
                var level = transform.Find("Level Select Canvas");
                if (level != null) levelSelectCanvas = level as RectTransform;
            }
            if (introVideoCanvas == null)
            {
                var video = FindNamed(transform, "video_anim");
                if (video != null) introVideoCanvas = video as RectTransform;
            }

            if (mainMenuCanvas == null || levelSelectCanvas == null)
            {
                Debug.LogError("LevelSelectTransition needs Game Canvas and Level Select Canvas.", this);
                enabled = false;
                return;
            }

            menuHomePosition = mainMenuCanvas.anchoredPosition;
            levelHomePosition = levelSelectCanvas.anchoredPosition;
            mainMenuCanvas.gameObject.SetActive(true);
            if (introVideoCanvas != null)
            {
                videoHomePosition = introVideoCanvas.anchoredPosition;
                introVideoPlayer = introVideoCanvas.GetComponentInChildren<VideoPlayer>(true);
                var videoNext = FindNamed(introVideoCanvas, "next-button");
                introVideoNextButton = videoNext != null ? videoNext.GetComponent<Button>() : null;
                if (introVideoPlayer != null)
                    introVideoPlayer.loopPointReached += OnIntroVideoFinished;
                if (introVideoNextButton != null)
                {
                    introVideoNextButton.onClick.RemoveListener(SkipIntroVideo);
                    introVideoNextButton.onClick.AddListener(SkipIntroVideo);
                    introVideoNextButton.gameObject.SetActive(false);
                }
                introVideoCanvas.gameObject.SetActive(false);
            }
            slideDistance = Mathf.Max(mainMenuCanvas.rect.width, levelSelectCanvas.rect.width);
            if (slideDistance <= 0f) slideDistance = Screen.width;
            levelCanvasGroup = levelSelectCanvas.GetComponent<CanvasGroup>();
            if (levelCanvasGroup == null) levelCanvasGroup = levelSelectCanvas.gameObject.AddComponent<CanvasGroup>();
            CachePopItems();
            SetLevelSelectHidden();
            BindButtons();

            if (openLevelSelectOnNextMainMenuLoad)
            {
                openLevelSelectOnNextMainMenuLoad = false;
                StartCoroutine(SlideToLevelSelect());
            }
        }

        void OnDestroy()
        {
            if (introVideoPlayer != null)
                introVideoPlayer.loopPointReached -= OnIntroVideoFinished;
        }

        public static void OpenLevelSelectAfterSceneLoad()
        {
            openLevelSelectOnNextMainMenuLoad = true;
        }

        void BindButtons()
        {
            var playTransform = FindNamed(mainMenuCanvas, "Play");
            var playButton = playTransform != null ? playTransform.GetComponent<Button>() : null;
            if (playButton != null)
            {
                playButton.onClick.RemoveAllListeners();
                playButton.onClick.AddListener(OpenLevelSelect);
            }

            var backTransform = FindNamed(levelSelectCanvas, "back-to-home-btn");
            var backButton = backTransform != null ? backTransform.GetComponent<Button>() : null;
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(ReturnHome);
            }

            var nextTransform = FindNamed(levelSelectCanvas, "next-button");
            var nextButton = nextTransform != null ? nextTransform.GetComponent<Button>() : null;
            if (nextButton != null) nextButton.onClick.AddListener(EnterGameplay);
        }

        void CachePopItems()
        {
            var uiRoot = levelSelectCanvas.Find("Safe Area/UI Root");
            if (uiRoot == null) uiRoot = levelSelectCanvas.Find("UI Root");
            if (uiRoot == null) uiRoot = levelSelectCanvas;
            if (uiRoot == null) return;

            foreach (string itemName in PopOrder)
                CachePopItem(uiRoot, itemName, false);

            foreach (string itemName in SlideDownOrder)
                CachePopItem(uiRoot, itemName, true);

            // Song buttons are currently selection placeholders. Keep their
            // existing Button/Inspector actions intact and add touch feedback.
            foreach (var item in popItems)
            {
                if (item.rect.name != "song-1" && item.rect.name != "song-2") continue;
                var button = item.rect.GetComponent<Button>();
                if (button != null) button.onClick.AddListener(() => Bounce(item));
            }
        }

        void CachePopItem(Transform uiRoot, string itemName, bool slidesDown)
        {
            var itemTransform = FindNamed(uiRoot, itemName);
            if (itemTransform == null) return;

            var rect = itemTransform as RectTransform;
            if (rect == null) return;
            var group = rect.GetComponent<CanvasGroup>();
            if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();

            popItems.Add(new PopItem
            {
                rect = rect,
                group = group,
                targetScale = rect.localScale,
                targetPosition = rect.anchoredPosition,
                slidesDown = slidesDown
            });
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

        public void OpenLevelSelect()
        {
            if (!enabled || isTransitioning || isIntroVideoPlaying) return;
            if (introVideoCanvas != null && introVideoPlayer != null)
            {
                StartCoroutine(PlayIntroVideo());
                return;
            }
            StartCoroutine(SlideToLevelSelect());
        }

        IEnumerator PlayIntroVideo()
        {
            isTransitioning = true;
            mainMenuCanvas.gameObject.SetActive(false);
            levelSelectCanvas.gameObject.SetActive(false);
            if (introVideoCanvas.localScale.sqrMagnitude < 0.0001f)
                introVideoCanvas.localScale = Vector3.one;
            introVideoCanvas.anchoredPosition = videoHomePosition;
            introVideoCanvas.gameObject.SetActive(true);
            if (introVideoNextButton != null)
                introVideoNextButton.gameObject.SetActive(false);

            introVideoPlayer.Stop();
            // The authored VideoPlayer has no RenderTexture or RawImage target.
            // Draw it on the camera plane while its Canvas supplies the skip UI.
            introVideoPlayer.renderMode = VideoRenderMode.CameraNearPlane;
            introVideoPlayer.targetCamera = Camera.main;
            introVideoPlayer.Play();
            isIntroVideoPlaying = true;
            isTransitioning = false;
            StartCoroutine(RevealIntroVideoNextButton());
            yield break;
        }

        IEnumerator RevealIntroVideoNextButton()
        {
            yield return new WaitForSecondsRealtime(videoNextButtonDelay);
            if (isIntroVideoPlaying && introVideoNextButton != null)
                introVideoNextButton.gameObject.SetActive(true);
        }

        void OnIntroVideoFinished(VideoPlayer player)
        {
            SkipIntroVideo();
        }

        public void SkipIntroVideo()
        {
            if (!enabled || !isIntroVideoPlaying || isTransitioning) return;
            isIntroVideoPlaying = false;
            StartCoroutine(SlideVideoToLevelSelect());
        }

        IEnumerator SlideVideoToLevelSelect()
        {
            isTransitioning = true;
            SetPopItemsHidden();
            introVideoCanvas.gameObject.SetActive(true);
            levelSelectCanvas.gameObject.SetActive(true);
            introVideoCanvas.anchoredPosition = videoHomePosition;
            levelSelectCanvas.anchoredPosition = levelHomePosition + Vector2.right * slideDistance;
            levelCanvasGroup.alpha = 1f;
            levelCanvasGroup.interactable = false;
            levelCanvasGroup.blocksRaycasts = false;

            float elapsed = 0f;
            while (elapsed < slideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / slideDuration);
                float eased = t * t * (3f - 2f * t);
                introVideoCanvas.anchoredPosition = Vector2.LerpUnclamped(
                    videoHomePosition, videoHomePosition - Vector2.right * slideDistance, eased);
                levelSelectCanvas.anchoredPosition = Vector2.LerpUnclamped(
                    levelHomePosition + Vector2.right * slideDistance, levelHomePosition, eased);
                yield return null;
            }

            introVideoCanvas.anchoredPosition = videoHomePosition;
            if (introVideoPlayer != null) introVideoPlayer.Stop();
            introVideoCanvas.gameObject.SetActive(false);
            levelSelectCanvas.anchoredPosition = levelHomePosition;
            yield return PlayPopSequence();
            levelCanvasGroup.interactable = true;
            levelCanvasGroup.blocksRaycasts = true;
            isTransitioning = false;
        }

        public void ReturnHome()
        {
            if (!enabled || isTransitioning) return;
            StartCoroutine(SlideToHome());
        }

        public void EnterGameplay()
        {
            if (!enabled || isTransitioning) return;
            StartCoroutine(SlideToGameplay());
        }

        IEnumerator SlideToGameplay()
        {
            isTransitioning = true;
            levelCanvasGroup.interactable = false;
            levelCanvasGroup.blocksRaycasts = false;

            yield return Slide(menuHomePosition, menuHomePosition,
                levelHomePosition, levelHomePosition - Vector2.right * slideDistance);

            GameplayPreparationTransition.EnterFromLevelSelectAfterSceneLoad();
            SceneManager.LoadScene(gameplaySceneName);
        }

        IEnumerator SlideToLevelSelect()
        {
            isTransitioning = true;
            SetPopItemsHidden();
            mainMenuCanvas.gameObject.SetActive(true);
            levelSelectCanvas.gameObject.SetActive(true);
            mainMenuCanvas.anchoredPosition = menuHomePosition;
            levelSelectCanvas.anchoredPosition = levelHomePosition + Vector2.right * slideDistance;
            levelCanvasGroup.alpha = 1f;
            levelCanvasGroup.interactable = false;
            levelCanvasGroup.blocksRaycasts = false;

            yield return Slide(menuHomePosition, menuHomePosition - Vector2.right * slideDistance,
                levelHomePosition + Vector2.right * slideDistance, levelHomePosition);

            mainMenuCanvas.gameObject.SetActive(false);
            levelSelectCanvas.anchoredPosition = levelHomePosition;
            yield return PlayPopSequence();
            levelCanvasGroup.interactable = true;
            levelCanvasGroup.blocksRaycasts = true;
            isTransitioning = false;
        }

        IEnumerator SlideToHome()
        {
            isTransitioning = true;
            SetPopItemsHidden();
            mainMenuCanvas.gameObject.SetActive(true);
            levelSelectCanvas.gameObject.SetActive(true);
            mainMenuCanvas.anchoredPosition = menuHomePosition - Vector2.right * slideDistance;
            levelSelectCanvas.anchoredPosition = levelHomePosition;
            levelCanvasGroup.alpha = 1f;
            levelCanvasGroup.interactable = false;
            levelCanvasGroup.blocksRaycasts = false;

            yield return Slide(menuHomePosition - Vector2.right * slideDistance, menuHomePosition,
                levelHomePosition, levelHomePosition + Vector2.right * slideDistance);

            mainMenuCanvas.anchoredPosition = menuHomePosition;
            levelSelectCanvas.anchoredPosition = levelHomePosition;
            levelSelectCanvas.gameObject.SetActive(false);
            levelCanvasGroup.alpha = 0f;
            var game = GetComponent<GamelanGame>();
            if (game != null) game.RestoreMenuAfterLevelSelect();
            isTransitioning = false;
        }

        IEnumerator Slide(Vector2 menuFrom, Vector2 menuTo, Vector2 levelFrom, Vector2 levelTo)
        {
            float elapsed = 0f;
            while (elapsed < slideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / slideDuration);
                float eased = t * t * (3f - 2f * t);
                mainMenuCanvas.anchoredPosition = Vector2.LerpUnclamped(menuFrom, menuTo, eased);
                levelSelectCanvas.anchoredPosition = Vector2.LerpUnclamped(levelFrom, levelTo, eased);
                yield return null;
            }

            mainMenuCanvas.anchoredPosition = menuTo;
            levelSelectCanvas.anchoredPosition = levelTo;
        }

        IEnumerator PlayPopSequence()
        {
            SetPopItemsHidden();
            foreach (var item in popItems)
                if (item.slidesDown) StartCoroutine(SlideDown(item));

            for (int i = 0; i < popItems.Count; i++)
            {
                var item = popItems[i];
                if (item.slidesDown) continue;
                item.rect.gameObject.SetActive(true);
                item.group.alpha = 0f;
                item.group.interactable = false;
                item.group.blocksRaycasts = false;
                item.rect.localScale = Vector3.zero;
                StartCoroutine(Pop(item));
                if (i < popItems.Count - 1) yield return new WaitForSecondsRealtime(popStagger);
            }

            yield return new WaitForSecondsRealtime(popDuration);
            foreach (var item in popItems)
            {
                item.group.alpha = 1f;
                item.group.interactable = true;
                item.group.blocksRaycasts = true;
            }
        }

        IEnumerator Pop(PopItem item)
        {
            float elapsed = 0f;
            while (elapsed < popDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / popDuration);
                // Ease-out back curve gives a small overshoot before settling.
                const float overshoot = 1.35f;
                float shifted = t - 1f;
                float eased = 1f + (overshoot + 1f) * shifted * shifted * shifted + overshoot * shifted * shifted;
                item.rect.localScale = item.targetScale * Mathf.Max(0f, eased);
                item.group.alpha = t;
                yield return null;
            }

            item.rect.localScale = item.targetScale;
            item.group.alpha = 1f;
        }

        IEnumerator SlideDown(PopItem item)
        {
            const float slideDownDistance = 220f;
            float elapsed = 0f;
            item.rect.localScale = item.targetScale;
            item.rect.anchoredPosition = item.targetPosition + Vector2.up * slideDownDistance;
            item.group.interactable = false;
            item.group.blocksRaycasts = false;

            float duration = Mathf.Max(0.35f, popDuration * 1.8f);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                item.rect.anchoredPosition = Vector2.LerpUnclamped(
                    item.targetPosition + Vector2.up * slideDownDistance, item.targetPosition, eased);
                item.group.alpha = t;
                yield return null;
            }

            item.rect.anchoredPosition = item.targetPosition;
            item.group.alpha = 1f;
            item.group.interactable = true;
            item.group.blocksRaycasts = true;
        }

        void Bounce(PopItem item)
        {
            if (!isActiveAndEnabled || item == null || item.rect == null) return;
            if (activeBounces.TryGetValue(item.rect, out var running))
            {
                if (running != null) StopCoroutine(running);
                item.rect.localScale = item.targetScale;
            }

            activeBounces[item.rect] = StartCoroutine(PlayBounce(item));
        }

        IEnumerator PlayBounce(PopItem item)
        {
            const float duration = 0.34f;
            const float peak = 1.18f;
            const float dip = 0.94f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale;
                if (t < 0.38f)
                {
                    float phase = t / 0.38f;
                    phase = 1f - (1f - phase) * (1f - phase);
                    scale = Mathf.Lerp(1f, peak, phase);
                }
                else if (t < 0.7f)
                {
                    float phase = (t - 0.38f) / 0.32f;
                    phase = phase * phase * (3f - 2f * phase);
                    scale = Mathf.Lerp(peak, dip, phase);
                }
                else
                {
                    float phase = (t - 0.7f) / 0.3f;
                    phase = phase * phase * (3f - 2f * phase);
                    scale = Mathf.Lerp(dip, 1f, phase);
                }

                item.rect.localScale = item.targetScale * scale;
                yield return null;
            }

            item.rect.localScale = item.targetScale;
            activeBounces.Remove(item.rect);
        }

        void SetPopItemsHidden()
        {
            foreach (var item in popItems)
            {
                item.rect.localScale = item.slidesDown ? item.targetScale : Vector3.zero;
                item.rect.anchoredPosition = item.slidesDown
                    ? item.targetPosition + Vector2.up * 220f
                    : item.targetPosition;
                item.group.alpha = 0f;
                item.group.interactable = false;
                item.group.blocksRaycasts = false;
            }
        }

        void SetLevelSelectHidden()
        {
            levelSelectCanvas.anchoredPosition = levelHomePosition + Vector2.right * slideDistance;
            levelSelectCanvas.gameObject.SetActive(true);
            levelCanvasGroup.alpha = 0f;
            levelCanvasGroup.interactable = false;
            levelCanvasGroup.blocksRaycasts = false;
            SetPopItemsHidden();
        }
    }
}
