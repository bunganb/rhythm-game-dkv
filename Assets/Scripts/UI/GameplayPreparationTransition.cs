using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GamelanRhythm
{
    /// <summary>
    /// Slides between the editable Preparation and Intro Reveal canvases.
    /// Back returns to MainMenu and opens its Level Select screen.
    /// </summary>
    public sealed class GameplayPreparationTransition : MonoBehaviour
    {
        static bool enterFromLevelSelectOnNextLoad;

        [SerializeField] RectTransform preparationCanvas;
        [SerializeField] RectTransform introRevealCanvas;
        [SerializeField] RectTransform instrumentRevealCanvas;
        [SerializeField] RectTransform instrumentInfoCanvas;
        [SerializeField] RectTransform tutorial1Canvas;
        [SerializeField] RectTransform tutorial2Canvas;
        [SerializeField] RectTransform countdownCanvas;
        [SerializeField] RectTransform gameplayCanvas;
        [SerializeField] RectTransform appreciationCanvas;
        [SerializeField] RectTransform badgeCanvas;
        [SerializeField, Min(0.1f)] float slideDuration = 0.7f;
        [SerializeField, Min(0f)] float introRevealDuration = 4.1f;
        [SerializeField, Min(0f)] float instrumentPostPopDelay = 2f;
        [SerializeField, Min(0.01f)] float instrumentRevealPopDuration = 0.38f;
        [SerializeField, Min(0.01f)] float countdownPopDuration = 0.32f;
        [SerializeField, Min(0f)] float countdownHoldDuration = 0.2f;
        [SerializeField, Min(1f)] float countdownPopScale = 1.2f;
        [SerializeField] string mainMenuSceneName = "MainMenu";

        CanvasGroup preparationGroup;
        RectTransform[] stages;
        CanvasGroup[] stageGroups;
        Vector2[] stageHomePositions;
        RectTransform currentStage;
        Vector2 preparationHomePosition;
        float slideDistance;
        bool isTransitioning;
        Transform startTextP1;
        Transform startTextP2;
        readonly Transform[] countdownP1 = new Transform[3];
        readonly Transform[] countdownP2 = new Transform[3];
        Vector3 startTextP1Scale;
        Vector3 startTextP2Scale;
        readonly Vector3[] countdownP1Scales = new Vector3[3];
        readonly Vector3[] countdownP2Scales = new Vector3[3];
        readonly List<Transform> instrumentLabels = new List<Transform>();
        readonly List<Transform> instrumentStars = new List<Transform>();
        readonly Dictionary<Transform, Vector3> instrumentRevealScales = new Dictionary<Transform, Vector3>();

        void Awake()
        {
            if (preparationCanvas == null)
            {
                var found = transform.Find("Preparation Canvas");
                if (found != null) preparationCanvas = found as RectTransform;
            }
            if (introRevealCanvas == null)
            {
                var found = transform.Find("Intro Reveal Canvas");
                if (found != null) introRevealCanvas = found as RectTransform;
            }
            instrumentRevealCanvas = FindCanvas(instrumentRevealCanvas, "Instrument Reveal Canvas");
            instrumentInfoCanvas = FindCanvas(instrumentInfoCanvas, "Instrument Info Canvas");
            tutorial1Canvas = FindCanvas(tutorial1Canvas, "Tutorial 1 Canvas");
            tutorial2Canvas = FindCanvas(tutorial2Canvas, "Tutorial 2 Canvas");
            countdownCanvas = FindCanvas(countdownCanvas, "Countdown Canvas");
            gameplayCanvas = FindCanvas(gameplayCanvas, "Gameplay Canvas");
            if (gameplayCanvas == null)
                gameplayCanvas = FindCanvas(gameplayCanvas, "Gameplay HUD Canvas");
            appreciationCanvas = FindCanvas(appreciationCanvas, "Appreciation Canvas");
            badgeCanvas = FindCanvas(badgeCanvas, "Badge Canvas");

            if (preparationCanvas == null || introRevealCanvas == null)
            {
                Debug.LogError("GameplayPreparationTransition needs Preparation Canvas and Intro Reveal Canvas.", this);
                enabled = false;
                return;
            }

            stages = new[] { preparationCanvas, introRevealCanvas, instrumentRevealCanvas,
                instrumentInfoCanvas, tutorial1Canvas, tutorial2Canvas, countdownCanvas, gameplayCanvas,
                appreciationCanvas, badgeCanvas };
            stageGroups = new CanvasGroup[stages.Length];
            stageHomePositions = new Vector2[stages.Length];
            for (int i = 0; i < stages.Length; i++)
            {
                if (stages[i] == null) continue;
                stageHomePositions[i] = stages[i].anchoredPosition;
                stageGroups[i] = GetOrAddCanvasGroup(stages[i]);
                bool isPreparation = stages[i] == preparationCanvas;
                stages[i].gameObject.SetActive(isPreparation);
                SetGroup(stageGroups[i], isPreparation);
            }
            preparationGroup = GroupFor(preparationCanvas);
            preparationHomePosition = stageHomePositions[IndexOf(preparationCanvas)];
            slideDistance = 0f;
            foreach (var stage in stages)
                if (stage != null) slideDistance = Mathf.Max(slideDistance, stage.rect.width);
            if (slideDistance <= 0f) slideDistance = Screen.width;
            currentStage = preparationCanvas;
            CacheCountdownElements();
            CacheInstrumentRevealElements();
            var introBack = FindNamed(introRevealCanvas, "back-to-home");
            if (introBack != null) introBack.gameObject.SetActive(false);
            BindButtons();

            if (enterFromLevelSelectOnNextLoad)
            {
                enterFromLevelSelectOnNextLoad = false;
                StartCoroutine(SlidePreparationIn());
            }
        }

        RectTransform FindCanvas(RectTransform existing, string objectName)
        {
            if (existing != null) return existing;
            var found = FindNamed(transform, objectName);
            return found as RectTransform;
        }

        CanvasGroup GroupFor(RectTransform canvas)
        {
            for (int i = 0; i < stages.Length; i++)
                if (stages[i] == canvas) return stageGroups[i];
            return null;
        }

        int IndexOf(RectTransform canvas)
        {
            for (int i = 0; i < stages.Length; i++)
                if (stages[i] == canvas) return i;
            return -1;
        }

        public static void EnterFromLevelSelectAfterSceneLoad()
        {
            enterFromLevelSelectOnNextLoad = true;
        }

        static CanvasGroup GetOrAddCanvasGroup(RectTransform canvas)
        {
            var group = canvas.GetComponent<CanvasGroup>();
            return group != null ? group : canvas.gameObject.AddComponent<CanvasGroup>();
        }

        void CacheCountdownElements()
        {
            startTextP1 = FindNamed(countdownCanvas, "start-text-p1");
            startTextP2 = FindNamed(countdownCanvas, "start-text-p2");
            startTextP1Scale = CacheAndHide(startTextP1);
            startTextP2Scale = CacheAndHide(startTextP2);

            for (int number = 1; number <= 3; number++)
            {
                countdownP1[number - 1] = FindNamed(countdownCanvas, $"countdown-p1-{number}");
                countdownP2[number - 1] = FindNamed(countdownCanvas, $"countdown-p2-{number}");
                countdownP1Scales[number - 1] = CacheAndHide(countdownP1[number - 1]);
                countdownP2Scales[number - 1] = CacheAndHide(countdownP2[number - 1]);
            }
        }

        static Vector3 CacheAndHide(Transform element)
        {
            if (element == null) return Vector3.one;
            Vector3 originalScale = element.localScale;
            element.gameObject.SetActive(false);
            return originalScale;
        }

        void BindButtons()
        {
            var back = FindNamed(preparationCanvas, "back-button");
            var backButton = back != null ? back.GetComponent<Button>() : null;
            if (backButton != null) backButton.onClick.AddListener(BackToLevelSelect);

            var next = FindNamed(preparationCanvas, "next-button");
            var nextButton = next != null ? next.GetComponent<Button>() : null;
            if (nextButton != null) nextButton.onClick.AddListener(() => ShowStage(introRevealCanvas));

            // Wire only the transition controls that already exist in the scene.
            BindForwardButton(instrumentRevealCanvas, "next-button", instrumentInfoCanvas);
            BindForwardButton(instrumentInfoCanvas, "button-next", tutorial1Canvas);
            BindForwardButton(tutorial1Canvas, "button-next", tutorial2Canvas);
            BindForwardButton(tutorial2Canvas, "button-next", countdownCanvas);
            BindForwardButton(appreciationCanvas, "next-button", badgeCanvas);
            BindHomeButton(instrumentRevealCanvas, "button-home");
            BindHomeButton(instrumentInfoCanvas, "button-home");

            // Appreciation's home control may still be named back-button in the scene.
            BindHomeToMainMenu(appreciationCanvas);
            BindForwardToLevelSelect(badgeCanvas, "next-button");
            BindHomeToMainMenu(badgeCanvas);
        }

        void BindForwardButton(RectTransform source, string buttonName, RectTransform destination)
        {
            if (source == null || destination == null) return;
            var found = FindNamed(source, buttonName);
            var button = found != null ? found.GetComponent<Button>() : null;
            if (button != null) button.onClick.AddListener(() => ShowStage(destination));
        }

        void BindHomeButton(RectTransform source, string buttonName)
        {
            if (source == null) return;
            var found = FindNamed(source, buttonName);
            var button = found != null ? found.GetComponent<Button>() : null;
            if (button != null) button.onClick.AddListener(BackToLevelSelect);
        }

        void BindForwardToLevelSelect(RectTransform source, string buttonName)
        {
            if (source == null) return;
            var found = FindNamed(source, buttonName);
            var button = found != null ? found.GetComponent<Button>() : null;
            if (button != null) button.onClick.AddListener(ReturnToLevelSelect);
        }

        void BindHomeToMainMenu(RectTransform source)
        {
            if (source == null) return;
            Transform found = FindNamed(source, "button-home") ?? FindNamed(source, "home-button") ??
                              FindNamed(source, "home") ?? FindNamed(source, "back-button");
            var button = found != null ? found.GetComponent<Button>() : null;
            if (button != null) button.onClick.AddListener(ReturnToMainMenu);
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

        public void ShowIntroReveal()
        {
            ShowStage(introRevealCanvas);
        }

        public void ShowAppreciation()
        {
            ShowStage(appreciationCanvas);
        }

        public void ReturnToLevelSelect()
        {
            if (!enabled || isTransitioning) return;
            StartCoroutine(SlideAwayAndLoad(true));
        }

        public void ReturnToMainMenu()
        {
            if (!enabled || isTransitioning) return;
            StartCoroutine(SlideAwayAndLoad(false));
        }

        public void ShowStage(RectTransform destination)
        {
            if (!enabled || isTransitioning || destination == null || destination == currentStage) return;
            StartCoroutine(SlideToStage(destination));
        }

        public void BackToLevelSelect()
        {
            if (!enabled || isTransitioning) return;
            StartCoroutine(SlideBackToLevelSelect());
        }

        IEnumerator SlideToStage(RectTransform destination)
        {
            int fromIndex = IndexOf(currentStage);
            int toIndex = IndexOf(destination);
            if (fromIndex < 0 || toIndex < 0) yield break;

            isTransitioning = true;
            var source = currentStage;
            var sourceGroup = stageGroups[fromIndex];
            var destinationGroup = stageGroups[toIndex];
            Vector2 sourceHome = stageHomePositions[fromIndex];
            Vector2 destinationHome = stageHomePositions[toIndex];

            if (destination == introRevealCanvas)
                PrepareIntroRevealForSlideIn();
            else if (destination == instrumentRevealCanvas)
                PrepareInstrumentRevealForSlideIn();

            destination.gameObject.SetActive(true);
            destination.anchoredPosition = destinationHome + Vector2.right * slideDistance;
            SetGroup(sourceGroup, false);
            SetGroup(destinationGroup, false);

            float elapsed = 0f;
            while (elapsed < slideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / slideDuration);
                float eased = t * t * (3f - 2f * t);
                source.anchoredPosition = Vector2.LerpUnclamped(sourceHome,
                    sourceHome - Vector2.right * slideDistance, eased);
                destination.anchoredPosition = Vector2.LerpUnclamped(
                    destinationHome + Vector2.right * slideDistance, destinationHome, eased);
                yield return null;
            }

            source.anchoredPosition = sourceHome;
            source.gameObject.SetActive(false);
            destination.anchoredPosition = destinationHome;
            SetGroup(destinationGroup, true);
            currentStage = destination;
            isTransitioning = false;

            if (destination == introRevealCanvas)
            {
                PlayIntroRevealSequence();
                StartCoroutine(ContinueAfterIntroReveal());
            }
            else if (destination == instrumentRevealCanvas)
                StartCoroutine(PlayInstrumentRevealSequence());
            else if (destination == countdownCanvas)
                StartCoroutine(PlayCountdownSequence());
            else if (destination == gameplayCanvas)
            {
                var rhythmGame = FindFirstObjectByType<GamelanGame>();
                if (rhythmGame != null) rhythmGame.BeginGameplayStage();
            }
        }

        IEnumerator ContinueAfterIntroReveal()
        {
            yield return new WaitForSecondsRealtime(introRevealDuration);
            if (currentStage == introRevealCanvas && !isTransitioning)
                ShowStage(instrumentRevealCanvas);
        }

        void PlayIntroRevealSequence()
        {
            var animator = GetIntroRevealAnimator();
            if (animator == null) return;

            animator.enabled = true;
            animator.Rebind();
            animator.Play("Intro Reveal Sequence", 0, 0f);
            animator.Update(0f);
        }

        void PrepareIntroRevealForSlideIn()
        {
            var animator = GetIntroRevealAnimator();
            if (animator == null) return;

            animator.enabled = false;
            var clips = animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.animationClips
                : null;
            if (clips != null && clips.Length > 0)
                clips[0].SampleAnimation(animator.gameObject, 0f);
        }

        Animator GetIntroRevealAnimator()
        {
            var introUi = introRevealCanvas != null
                ? introRevealCanvas.Find("Safe Area/Intro Reveal UI")
                : null;
            return introUi != null ? introUi.GetComponent<Animator>() : null;
        }

        void CacheInstrumentRevealElements()
        {
            if (instrumentRevealCanvas == null) return;
            var ui = instrumentRevealCanvas.Find("Safe Area/Instrument Reveal UI");
            if (ui == null) return;

            foreach (Transform element in ui.GetComponentsInChildren<Transform>(true))
            {
                string elementName = element.name.ToLowerInvariant();
                if (elementName == "label") instrumentLabels.Add(element);
                else if (elementName.StartsWith("star-decoration")) instrumentStars.Add(element);
            }

            CacheInstrumentScales(instrumentLabels);
            CacheInstrumentScales(instrumentStars);
        }

        void CacheInstrumentScales(List<Transform> elements)
        {
            foreach (var element in elements)
                if (element != null && !instrumentRevealScales.ContainsKey(element))
                    instrumentRevealScales.Add(element, element.localScale);
        }

        void PrepareInstrumentRevealForSlideIn()
        {
            foreach (var pair in instrumentRevealScales)
                if (pair.Key != null) pair.Key.localScale = pair.Value * 0.65f;
        }

        IEnumerator PlayInstrumentRevealSequence()
        {
            yield return StartCoroutine(PopInstrumentElements(instrumentLabels));
            yield return StartCoroutine(PopInstrumentElements(instrumentStars));
            yield return new WaitForSecondsRealtime(instrumentPostPopDelay);
            if (currentStage == instrumentRevealCanvas && !isTransitioning)
                ShowStage(instrumentInfoCanvas);
        }

        IEnumerator PopInstrumentElements(List<Transform> elements)
        {
            if (elements.Count == 0) yield break;
            float halfDuration = instrumentRevealPopDuration * 0.5f;
            float elapsed = 0f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                foreach (var element in elements)
                {
                    if (element == null || !instrumentRevealScales.TryGetValue(element, out var scale)) continue;
                    element.localScale = Vector3.LerpUnclamped(scale * 0.65f, scale * 1.16f, t);
                }
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                foreach (var element in elements)
                {
                    if (element == null || !instrumentRevealScales.TryGetValue(element, out var scale)) continue;
                    element.localScale = Vector3.LerpUnclamped(scale * 1.16f, scale, t);
                }
                yield return null;
            }

            foreach (var element in elements)
                if (element != null && instrumentRevealScales.TryGetValue(element, out var scale))
                    element.localScale = scale;
        }

        IEnumerator PlayCountdownSequence()
        {
            if (startTextP1 != null) StartCoroutine(PopElement(startTextP1, startTextP1Scale));
            if (startTextP2 != null) StartCoroutine(PopElement(startTextP2, startTextP2Scale));
            yield return new WaitForSecondsRealtime(countdownPopDuration + countdownHoldDuration);
            HideElement(startTextP1);
            HideElement(startTextP2);

            for (int number = 3; number >= 1; number--)
            {
                int index = number - 1;
                yield return StartCoroutine(PopAndHold(countdownP1[index], countdownP1Scales[index]));
                yield return StartCoroutine(PopAndHold(countdownP2[index], countdownP2Scales[index]));
            }

            if (currentStage == countdownCanvas && !isTransitioning && gameplayCanvas != null)
                ShowStage(gameplayCanvas);
            else if (gameplayCanvas == null)
                Debug.LogWarning("Countdown finished, but no Gameplay Canvas or Gameplay HUD Canvas exists yet.", this);
        }

        IEnumerator PopAndHold(Transform element, Vector3 originalScale)
        {
            if (element == null) yield break;
            yield return StartCoroutine(PopElement(element, originalScale));
            yield return new WaitForSecondsRealtime(countdownHoldDuration);
            HideElement(element);
        }

        IEnumerator PopElement(Transform element, Vector3 originalScale)
        {
            if (element == null) yield break;
            element.gameObject.SetActive(true);
            float halfDuration = countdownPopDuration * 0.5f;
            float elapsed = 0f;
            Vector3 startScale = originalScale * 0.65f;
            Vector3 overshootScale = originalScale * countdownPopScale;
            element.localScale = startScale;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                element.localScale = Vector3.LerpUnclamped(startScale, overshootScale,
                    Mathf.Clamp01(elapsed / halfDuration));
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                element.localScale = Vector3.LerpUnclamped(overshootScale, originalScale,
                    Mathf.Clamp01(elapsed / halfDuration));
                yield return null;
            }
            element.localScale = originalScale;
        }

        static void HideElement(Transform element)
        {
            if (element != null) element.gameObject.SetActive(false);
        }

        IEnumerator SlidePreparationIn()
        {
            isTransitioning = true;
            preparationCanvas.gameObject.SetActive(true);
            preparationCanvas.anchoredPosition = preparationHomePosition + Vector2.right * slideDistance;
            SetGroup(preparationGroup, true);

            float elapsed = 0f;
            while (elapsed < slideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / slideDuration);
                float eased = t * t * (3f - 2f * t);
                preparationCanvas.anchoredPosition = Vector2.LerpUnclamped(
                    preparationHomePosition + Vector2.right * slideDistance,
                    preparationHomePosition, eased);
                yield return null;
            }

            preparationCanvas.anchoredPosition = preparationHomePosition;
            isTransitioning = false;
        }

        IEnumerator SlideBackToLevelSelect()
        {
            yield return SlideAwayAndLoad(true);
        }

        IEnumerator SlideAwayAndLoad(bool openLevelSelect)
        {
            int index = IndexOf(currentStage);
            if (index < 0) yield break;

            isTransitioning = true;
            SetGroup(stageGroups[index], false);
            Vector2 home = stageHomePositions[index];
            float elapsed = 0f;
            while (elapsed < slideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / slideDuration);
                float eased = t * t * (3f - 2f * t);
                currentStage.anchoredPosition = Vector2.LerpUnclamped(
                    home, home - Vector2.right * slideDistance, eased);
                yield return null;
            }

            currentStage.anchoredPosition = home - Vector2.right * slideDistance;
            if (openLevelSelect) LevelSelectTransition.OpenLevelSelectAfterSceneLoad();
            SceneManager.LoadScene(mainMenuSceneName);
        }

        static void SetGroup(CanvasGroup group, bool interactable)
        {
            if (group == null) return;
            group.alpha = 1f;
            group.interactable = interactable;
            group.blocksRaycasts = interactable;
        }
    }
}
