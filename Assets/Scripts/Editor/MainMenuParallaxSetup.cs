using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using GamelanRhythm;

public static class MainMenuParallaxSetup
{
    const string ScenePath = "Assets/Scenes/MainMenu.unity";
    const string AssetFolder = "Assets/Art/MainMenu/Background";
    const string ControllerPath = AssetFolder + "/MainMenuParallax.controller";
    const float ClipDuration = 1.25f;
    const float LayerStagger = 0.035f;

    static readonly string[] StartChildrenInOrder =
        { "1", "2", "3", "4", "5", "6", "mid", "fore", "Safe Area", "LogoTop", "Logo", "Play", "Exit" };

    [MenuItem("Tools/Gamelan Rhythm/Setup Main Menu Parallax")]
    public static void Setup()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var canvas = GameObject.Find("Game Canvas");
        if (canvas == null) throw new System.InvalidOperationException("Game Canvas was not found in MainMenu.unity.");

        var start = canvas.transform.Find("start") as RectTransform;
        var stop = canvas.transform.Find("stop") as RectTransform;
        if (start == null || stop == null)
            throw new System.InvalidOperationException("Create RectTransform objects named 'start' and 'stop' under Game Canvas first.");
        var safeArea = canvas.transform.Find("Safe Area") ?? start.Find("Safe Area");
        if (safeArea == null) safeArea = EnsureUiRoot(start, "Safe Area");
        ConfigureStartGroup(canvas.transform, start, safeArea);
        if (start.GetComponent<MainMenuMouseParallax>() == null)
            Undo.AddComponent<MainMenuMouseParallax>(start.gameObject);
        var mainMenu = EnsureUiRoot(safeArea, "Main Menu");
        var songSelection = EnsureUiRoot(safeArea, "Song Selection");
        mainMenu.gameObject.SetActive(true);
        songSelection.gameObject.SetActive(true);

        EnsureCanvasGroup(start.Find("Play").gameObject, 1f, true);
        EnsureCanvasGroup(start.Find("Exit").gameObject, 1f, true);
        EnsureCanvasGroup(mainMenu.gameObject, 1f, true);
        EnsureCanvasGroup(songSelection.gameObject, 0f, false);
        var overlay = start.Find("overlay");
        if (overlay != null)
        {
            EnsureCanvasGroup(overlay.gameObject, 0f, false);
            overlay.GetComponent<CanvasGroup>().blocksRaycasts = false;
        }
        MakeBackgroundNonRaycast(canvas.transform);
        EnsureClickArea(canvas.transform);
        if (songSelection != null) SetExitCaption(songSelection);

        foreach (var path in new[]
        {
            ControllerPath,
            AssetFolder + "/ParallaxTop.anim",
            AssetFolder + "/ParallaxDown.anim",
            AssetFolder + "/ParallaxUp.anim",
            AssetFolder + "/ParallaxToSongs.anim",
            AssetFolder + "/ParallaxToSongsFromDown.anim"
        })
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);

        float startY = start.anchoredPosition.y;
        float stopY = stop.anchoredPosition.y;
        var topClip = CreateClip("ParallaxTop", canvas.transform, start, mainMenu, songSelection, startY, startY, "top", start);
        var downClip = CreateClip("ParallaxDown", canvas.transform, start, mainMenu, songSelection, startY, stopY, "scroll", start);
        var upClip = CreateClip("ParallaxUp", canvas.transform, start, mainMenu, songSelection, stopY, startY, "up", start);
        var songsFromTopClip = CreateClip("ParallaxToSongs", canvas.transform, start, mainMenu, songSelection, startY, stopY, "songs", start);
        var songsFromDownClip = CreateClip("ParallaxToSongsFromDown", canvas.transform, start, mainMenu, songSelection, stopY, stopY, "songs", start);
        AssetDatabase.CreateAsset(topClip, AssetFolder + "/ParallaxTop.anim");
        AssetDatabase.CreateAsset(downClip, AssetFolder + "/ParallaxDown.anim");
        AssetDatabase.CreateAsset(upClip, AssetFolder + "/ParallaxUp.anim");
        AssetDatabase.CreateAsset(songsFromTopClip, AssetFolder + "/ParallaxToSongs.anim");
        AssetDatabase.CreateAsset(songsFromDownClip, AssetFolder + "/ParallaxToSongsFromDown.anim");

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("SlideDown", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("SlideTop", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("OpenSongs", AnimatorControllerParameterType.Trigger);
        var machine = controller.layers[0].stateMachine;
        var topState = machine.AddState("Top", new Vector3(80, 80, 0));
        topState.motion = topClip;
        machine.defaultState = topState;
        var downState = machine.AddState("Parallax Down", new Vector3(280, 80, 0));
        downState.motion = downClip;
        var songsFromTopState = machine.AddState("Open Songs From Top", new Vector3(500, 80, 0));
        songsFromTopState.motion = songsFromTopClip;
        var songsFromDownState = machine.AddState("Open Songs From Down", new Vector3(500, 200, 0));
        songsFromDownState.motion = songsFromDownClip;
        var upState = machine.AddState("Parallax Up", new Vector3(500, 300, 0));
        upState.motion = upClip;
        AddTriggerTransition(topState, downState, "SlideDown");
        AddTriggerTransition(downState, upState, "SlideTop");
        AddTriggerTransition(topState, songsFromTopState, "OpenSongs");
        AddTriggerTransition(downState, songsFromDownState, "OpenSongs");
        AddTriggerTransition(songsFromTopState, upState, "SlideTop");
        AddTriggerTransition(songsFromDownState, upState, "SlideTop");
        AddExitTransition(upState, topState);

        var animator = canvas.GetComponent<Animator>();
        if (animator == null) animator = Undo.AddComponent<Animator>(canvas);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Main menu parallax is ready. Clicking Parallax Click Area moves the start group to stop, carrying layers and menu UI in sibling order.");
    }

    static AnimationClip CreateClip(string name, Transform animatorRoot, Transform parallaxArea, Transform mainMenu,
        Transform songSelection, float fromY, float toY, string mode, Transform staggerRoot = null)
    {
        var clip = new AnimationClip { name = name, frameRate = 60f };
        string areaPath = AnimationUtility.CalculateTransformPath(parallaxArea, animatorRoot);
        // Drive the RectTransform through its serialized UI position property so the Animator
        // binds reliably to the parallax root in the Canvas hierarchy.
        var rootCurve = SmoothCurve(fromY, toY);
        SetCurve(clip, areaPath, typeof(RectTransform), "m_AnchoredPosition.y", rootCurve);
        if (staggerRoot != null)
            AddStaggeredChildCurves(clip, animatorRoot, staggerRoot, fromY, toY, rootCurve);
        string menuPath = AnimationUtility.CalculateTransformPath(mainMenu, animatorRoot);
        string songsPath = AnimationUtility.CalculateTransformPath(songSelection, animatorRoot);
        string playPath = AnimationUtility.CalculateTransformPath(animatorRoot.Find("start/Play"), animatorRoot);
        string exitPath = AnimationUtility.CalculateTransformPath(animatorRoot.Find("start/Exit"), animatorRoot);
        var overlay = staggerRoot != null ? staggerRoot.Find("overlay") : null;
        string overlayPath = overlay != null ? AnimationUtility.CalculateTransformPath(overlay, animatorRoot) : null;
        if (mode == "top")
        {
            SetCurve(clip, playPath, typeof(CanvasGroup), "m_Alpha", ConstantCurve(1f));
            SetCurve(clip, exitPath, typeof(CanvasGroup), "m_Alpha", ConstantCurve(1f));
            if (mainMenu != null) SetCurve(clip, menuPath, typeof(CanvasGroup), "m_Alpha", ConstantCurve(1f));
            if (songSelection != null) SetCurve(clip, songsPath, typeof(CanvasGroup), "m_Alpha", ConstantCurve(0f));
            if (overlay != null) SetCurve(clip, overlayPath, typeof(CanvasGroup), "m_Alpha", ConstantCurve(0f));
        }
        else if (mode == "scroll")
        {
            SetCurve(clip, playPath, typeof(CanvasGroup), "m_Alpha", ConstantCurve(1f));
            SetCurve(clip, exitPath, typeof(CanvasGroup), "m_Alpha", ConstantCurve(1f));
            SetCurve(clip, menuPath, typeof(CanvasGroup), "m_Alpha", ConstantCurve(1f));
            SetCurve(clip, songsPath, typeof(CanvasGroup), "m_Alpha", ConstantCurve(0f));
            if (overlay != null) SetCurve(clip, overlayPath, typeof(CanvasGroup), "m_Alpha", FadeCurve(0f, 1f, 0.15f, 0.95f));
        }
        else if (mode == "songs")
        {
            SetCurve(clip, playPath, typeof(CanvasGroup), "m_Alpha", FadeCurve(1f, 0f, 0f, 0.28f));
            SetCurve(clip, exitPath, typeof(CanvasGroup), "m_Alpha", FadeCurve(1f, 0f, 0f, 0.28f));
            SetCurve(clip, menuPath, typeof(CanvasGroup), "m_Alpha", FadeCurve(1f, 0f, 0f, 0.28f));
            SetCurve(clip, songsPath, typeof(CanvasGroup), "m_Alpha", FadeCurve(0f, 1f, 0.28f, ClipDuration));
            if (overlay != null)
            {
                var overlayCurve = Mathf.Approximately(fromY, toY)
                    ? ConstantCurve(1f)
                    : FadeCurve(0f, 1f, 0.15f, 0.95f);
                SetCurve(clip, overlayPath, typeof(CanvasGroup), "m_Alpha", overlayCurve);
            }
        }
        else
        {
            SetCurve(clip, playPath, typeof(CanvasGroup), "m_Alpha", FadeCurve(0f, 1f, 0.28f, ClipDuration));
            SetCurve(clip, exitPath, typeof(CanvasGroup), "m_Alpha", FadeCurve(0f, 1f, 0.28f, ClipDuration));
            SetCurve(clip, menuPath, typeof(CanvasGroup), "m_Alpha", FadeCurve(0f, 1f, 0.28f, ClipDuration));
            SetCurve(clip, songsPath, typeof(CanvasGroup), "m_Alpha", FadeCurve(1f, 0f, 0f, 0.28f));
            if (overlay != null) SetCurve(clip, overlayPath, typeof(CanvasGroup), "m_Alpha", FadeCurve(1f, 0f, 0f, 0.32f));
        }

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        settings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    static RectTransform EnsureUiRoot(Transform parent, string name)
    {
        var existing = parent.Find(name) as RectTransform;
        if (existing != null) return existing;
        var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        return root;
    }

    static void ConfigureStartGroup(Transform canvas, RectTransform start, Transform safeArea)
    {
        var legacyArea = canvas.Find("Parallax Area");
        var background = start.Find("Background") ?? canvas.Find("Background");
        if (background == null && legacyArea != null) background = legacyArea.Find("Background");
        if (background != null && background.parent != canvas)
        {
            bool wasInsideStart = background.IsChildOf(start);
            background.SetParent(canvas, !wasInsideStart);
            if (wasInsideStart && background is RectTransform backgroundRect)
                backgroundRect.anchoredPosition3D = new Vector3(0f, -610f, 0f);
        }
        foreach (var name in StartChildrenInOrder)
        {
            var child = start.Find(name) ?? canvas.Find(name);
            if (child == null && legacyArea != null) child = legacyArea.Find(name);
            if (child != null && child != start && child.parent != start) child.SetParent(start, true);
        }
        if (safeArea.parent != start) safeArea.SetParent(start, true);
        foreach (var name in StartChildrenInOrder)
        {
            var child = start.Find(name);
            if (child != null) child.SetAsLastSibling();
        }
        var overlay = start.Find("overlay");
        if (overlay != null) overlay.SetAsLastSibling();
        var foregroundObjects = new List<Transform>();
        for (int i = 0; i < start.childCount; i++)
        {
            var child = start.GetChild(i);
            if (string.Equals(child.name, "not", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(child.name, "pemukul", System.StringComparison.OrdinalIgnoreCase))
                foregroundObjects.Add(child);
        }
        foreach (var child in foregroundObjects) child.SetAsLastSibling();
        start.SetAsLastSibling();
    }

    static void SetCurve(AnimationClip clip, string path, System.Type type, string property, AnimationCurve curve)
    {
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), curve);
    }

    static void AddStaggeredChildCurves(AnimationClip clip, Transform animatorRoot, Transform start,
        float fromY, float toY, AnimationCurve rootCurve)
    {
        float distance = toY - fromY;
        int sequenceIndex = 0;
        for (int i = 0; i < StartChildrenInOrder.Length; i++)
        {
            var childName = StartChildrenInOrder[i];
            // Keep menu controls editable in the Inspector. Their parent moves with the
            // parallax root, while their local positions remain whatever the designer sets.
            if (childName == "Play" || childName == "Exit") continue;

            var child = start.Find(childName);
            var rect = child as RectTransform;
            if (rect == null) continue;

            float baseY = rect.anchoredPosition.y;
            float delay = Mathf.Min(sequenceIndex * LayerStagger, ClipDuration * 0.65f);
            sequenceIndex++;
            var curve = new AnimationCurve();
            const int samples = 50;
            for (int sample = 0; sample <= samples; sample++)
            {
                float time = ClipDuration * sample / samples;
                float layerProgress = time <= delay ? 0f : Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(delay, ClipDuration, time));
                float childOffset = distance * layerProgress - (rootCurve.Evaluate(time) - fromY);
                curve.AddKey(new Keyframe(time, baseY + childOffset));
            }
            string path = AnimationUtility.CalculateTransformPath(rect, animatorRoot);
            SetCurve(clip, path, typeof(RectTransform), "m_AnchoredPosition.y", curve);
        }
    }

    static AnimationCurve SmoothCurve(float from, float to)
    {
        return AnimationCurve.EaseInOut(0f, from, ClipDuration, to);
    }

    static AnimationCurve ConstantCurve(float value)
    {
        return new AnimationCurve(new Keyframe(0f, value), new Keyframe(ClipDuration, value));
    }

    static AnimationCurve FadeCurve(float from, float to, float start, float end)
    {
        if (start <= 0f)
            return new AnimationCurve(new Keyframe(0f, from), new Keyframe(end, to), new Keyframe(ClipDuration, to));
        if (end >= ClipDuration)
            return new AnimationCurve(new Keyframe(0f, from), new Keyframe(start, from), new Keyframe(end, to));
        return new AnimationCurve(new Keyframe(0f, from), new Keyframe(start, from), new Keyframe(end, to), new Keyframe(ClipDuration, to));
    }

    static void AddTriggerTransition(AnimatorState from, AnimatorState to, string trigger)
    {
        var transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0f;
        transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    static void AddExitTransition(AnimatorState from, AnimatorState to)
    {
        var transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = 1f;
        transition.duration = 0f;
    }

    static void EnsureCanvasGroup(GameObject target, float alpha, bool interactable)
    {
        var group = target.GetComponent<CanvasGroup>();
        if (group == null) group = Undo.AddComponent<CanvasGroup>(target);
        group.alpha = alpha;
        group.interactable = interactable;
        group.blocksRaycasts = true;
    }

    static void MakeBackgroundNonRaycast(Transform canvas)
    {
        foreach (var image in canvas.GetComponentsInChildren<Image>(true))
        {
            if (image.GetComponentInParent<Button>() == null) image.raycastTarget = false;
        }
    }

    static void EnsureClickArea(Transform canvas)
    {
        var existing = canvas.Find("Parallax Click Area");
        GameObject clickArea;
        if (existing != null) clickArea = existing.gameObject;
        else
        {
            clickArea = new GameObject("Parallax Click Area", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            clickArea.transform.SetParent(canvas, false);
        }

        var rect = clickArea.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetAsFirstSibling();
        var image = clickArea.GetComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;
        var button = clickArea.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        clickArea.SetActive(true);
    }

    static void SetExitCaption(Transform songSelection)
    {
        var back = songSelection.Find("Back");
        if (back == null) return;
        var caption = back.GetComponentInChildren<TMP_Text>(true);
        if (caption != null) caption.text = "EXIT";
    }
}
