using System;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using GamelanRhythm;

public static class PreparationJuicingSetup
{
    const string Folder = "Assets/Animations/Preparation";
    const string EntrancePath = Folder + "/PreparationEntrance.anim";
    const string MascotPopPath = Folder + "/MascotPop.anim";
    const string MascotIdlePath = Folder + "/MascotIdle.anim";
    const string EntranceControllerPath = Folder + "/PreparationEntrance.controller";
    const string MascotControllerPath = Folder + "/Mascot.controller";

    [MenuItem("Tools/Gamelan Rhythm/Setup Preparation Juicing")]
    public static void Setup()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Gameplay")
            throw new InvalidOperationException("Open the Gameplay scene before setting up preparation animations.");

        var canvas = FindSceneObject("Preparation Canvas", scene);
        var ui = canvas != null ? canvas.transform.Find("Safe Area/Preparation UI") : null;
        if (ui == null) throw new InvalidOperationException("Preparation Canvas/Safe Area/Preparation UI was not found.");

        var mascot = ui.Find("Mascot");
        var bubble = ui.Find("bubble_chat");
        var typingText = bubble != null ? bubble.Find("Text (TMP)") : null;
        var media = ui.Find("Media");
        var player1 = ui.Find("p1");
        var player2 = ui.Find("p2");
        var arrowUp = ui.Find("arrow_up");
        var arrowDown = ui.Find("arrow_down");
        if (mascot == null || bubble == null || typingText == null || media == null || player1 == null || player2 == null || arrowUp == null || arrowDown == null)
            throw new InvalidOperationException("One or more preparation animation targets are missing.");

        var text = typingText.GetComponent<TextMeshProUGUI>();
        if (text == null) throw new InvalidOperationException("bubble_chat/Text (TMP) needs a TextMeshProUGUI component.");
        if (typingText.GetComponent<AnimatedTMPTypewriter>() == null) Undo.AddComponent<AnimatedTMPTypewriter>(typingText.gameObject);
        if (AssetDatabase.LoadMainAssetAtPath(EntrancePath) != null ||
            AssetDatabase.LoadMainAssetAtPath(MascotPopPath) != null ||
            AssetDatabase.LoadMainAssetAtPath(MascotIdlePath) != null ||
            AssetDatabase.LoadMainAssetAtPath(EntranceControllerPath) != null ||
            AssetDatabase.LoadMainAssetAtPath(MascotControllerPath) != null)
        {
            RefreshAnimations();
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Animations", "Preparation");

        var entrance = NewClip("PreparationEntrance");
        const float clipEnd = 3.8f;
        AddSlide(entrance, ui, bubble, 0.34f, 135f, 105f, 0.62f, clipEnd);

        var bubblePath = Path(ui, bubble);
        Set(entrance, bubblePath, typeof(RectTransform), "m_LocalScale.x", Curve(
            Key(0f, 0.68f), Key(0.48f, 0.68f), Key(0.61f, 1.1f), Key(0.76f, 1f), Key(clipEnd, 1f)));
        Set(entrance, bubblePath, typeof(RectTransform), "m_LocalScale.y", Curve(
            Key(0f, 0.68f), Key(0.48f, 0.68f), Key(0.61f, 1.1f), Key(0.76f, 1f), Key(clipEnd, 1f)));
        Set(entrance, bubblePath, typeof(RectTransform), "m_LocalScale.z", Curve(Key(0f, 1f), Key(clipEnd, 1f)));

        AddSlide(entrance, ui, media, 1.05f, 0f, 0f, 0.42f, clipEnd);
        var mediaPath = Path(ui, media);
        var mediaScale = Curve(Key(0f, 0.72f), Key(1.05f, 0.72f), Key(1.28f, 1.12f), Key(1.43f, 1f), Key(clipEnd, 1f));
        Set(entrance, mediaPath, typeof(RectTransform), "m_LocalScale.x", mediaScale);
        Set(entrance, mediaPath, typeof(RectTransform), "m_LocalScale.y", mediaScale);
        Set(entrance, mediaPath, typeof(RectTransform), "m_LocalScale.z", Curve(Key(0f, 1f), Key(clipEnd, 1f)));

        // Player 1 and the up arrow enter from above; player 2 and the down arrow enter from below.
        AddSlide(entrance, ui, player1, 1.10f, 0f, 245f, 0.48f, clipEnd);
        AddSlide(entrance, ui, player2, 1.22f, 0f, -245f, 0.48f, clipEnd);
        AddSlide(entrance, ui, arrowUp, 1.34f, 0f, 150f, 0.42f, clipEnd);
        AddSlide(entrance, ui, arrowDown, 1.46f, 0f, -150f, 0.42f, clipEnd);

        var textPath = Path(ui, typingText);
        var charCount = text.text.Length;
        Set(entrance, textPath, typeof(AnimatedTMPTypewriter), "visibleCharacterCount",
            TypingFloatCurve(charCount, 0.92f, 3.15f, clipEnd));
        SaveClip(entrance, EntrancePath, false);

        var pop = NewClip("MascotPop");
        EnsureCanvasGroup(mascot);
        Set(pop, "", typeof(CanvasGroup), "m_Alpha", Curve(Key(0f, 0f), Key(0.28f, 1f), Key(0.7f, 1f)));
        var popScale = Curve(Key(0f, 0.72f), Key(0.38f, 1.13f), Key(0.7f, 1f));
        Set(pop, "", typeof(RectTransform), "m_LocalScale.x", popScale);
        Set(pop, "", typeof(RectTransform), "m_LocalScale.y", popScale);
        Set(pop, "", typeof(RectTransform), "m_LocalScale.z", Curve(Key(0f, 1f), Key(0.7f, 1f)));
        SaveClip(pop, MascotPopPath, false);

        var idle = NewClip("MascotIdle");
        var sway = Curve(Key(0f, -2.5f), Key(0.9f, 2.5f), Key(1.8f, -2.5f));
        sway.preWrapMode = WrapMode.Loop;
        sway.postWrapMode = WrapMode.Loop;
        Set(idle, "", typeof(RectTransform), "localEulerAnglesRaw.z", sway);
        SaveClip(idle, MascotIdlePath, true);

        var entranceController = AnimatorController.CreateAnimatorControllerAtPath(EntranceControllerPath);
        var entranceState = entranceController.layers[0].stateMachine.AddState("Entrance");
        entranceState.motion = entrance;
        entranceController.layers[0].stateMachine.defaultState = entranceState;
        var entranceAnimator = EnsureAnimator(ui);
        entranceAnimator.runtimeAnimatorController = entranceController;

        var mascotController = AnimatorController.CreateAnimatorControllerAtPath(MascotControllerPath);
        var machine = mascotController.layers[0].stateMachine;
        var popState = machine.AddState("Pop");
        popState.motion = pop;
        machine.defaultState = popState;
        var idleState = machine.AddState("Idle");
        idleState.motion = idle;
        var toIdle = popState.AddTransition(idleState);
        toIdle.hasExitTime = true;
        toIdle.exitTime = 1f;
        toIdle.hasFixedDuration = true;
        toIdle.duration = 0f;
        EnsureAnimator(mascot).runtimeAnimatorController = mascotController;

        RefreshAnimations();
    }

    [MenuItem("Tools/Gamelan Rhythm/Refresh Preparation Juicing")]
    public static void RefreshAnimations()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var canvas = FindSceneObject("Preparation Canvas", scene);
        var ui = canvas != null ? canvas.transform.Find("Safe Area/Preparation UI") : null;
        if (ui == null) throw new InvalidOperationException("Preparation Canvas/Safe Area/Preparation UI was not found.");

        var bubble = ui.Find("bubble_chat");
        var text = bubble != null ? bubble.Find("Text (TMP)") : null;
        var mascot = ui.Find("Mascot");
        var media = ui.Find("Media");
        var player1 = ui.Find("p1");
        var player2 = ui.Find("p2");
        var arrowUp = ui.Find("arrow_up");
        var arrowDown = ui.Find("arrow_down");
        if (bubble == null || text == null || mascot == null || media == null || player1 == null || player2 == null || arrowUp == null || arrowDown == null)
            throw new InvalidOperationException("One or more preparation animation targets are missing.");

        var entrance = AssetDatabase.LoadAssetAtPath<AnimationClip>(EntrancePath);
        var pop = AssetDatabase.LoadAssetAtPath<AnimationClip>(MascotPopPath);
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(MascotIdlePath);
        var entranceController = AssetDatabase.LoadAssetAtPath<AnimatorController>(EntranceControllerPath);
        var mascotController = AssetDatabase.LoadAssetAtPath<AnimatorController>(MascotControllerPath);
        if (entrance == null || pop == null || idle == null || entranceController == null || mascotController == null)
            throw new InvalidOperationException("Preparation Animator assets are incomplete. Run Setup Preparation Juicing first.");

        ClearCurves(entrance);
        const float clipEnd = 5.1f;
        AddSlide(entrance, ui, bubble, 0.84f, -135f, 105f, 0.56f, clipEnd);
        var bubblePath = Path(ui, bubble);
        var bubbleScale = Curve(Key(0f, 0.68f), Key(0.84f, 0.68f), Key(1.4f, 1.1f), Key(1.56f, 1f), Key(clipEnd, 1f));
        Set(entrance, bubblePath, typeof(RectTransform), "m_LocalScale.x", bubbleScale);
        Set(entrance, bubblePath, typeof(RectTransform), "m_LocalScale.y", bubbleScale);
        Set(entrance, bubblePath, typeof(RectTransform), "m_LocalScale.z", Curve(Key(0f, 1f), Key(clipEnd, 1f)));

        AddSlide(entrance, ui, media, 1.66f, 0f, 0f, 0.42f, clipEnd);
        var mediaPath = Path(ui, media);
        var mediaScale = Curve(Key(0f, 0.72f), Key(1.66f, 0.72f), Key(1.89f, 1.12f), Key(2.04f, 1f), Key(clipEnd, 1f));
        Set(entrance, mediaPath, typeof(RectTransform), "m_LocalScale.x", mediaScale);
        Set(entrance, mediaPath, typeof(RectTransform), "m_LocalScale.y", mediaScale);
        Set(entrance, mediaPath, typeof(RectTransform), "m_LocalScale.z", Curve(Key(0f, 1f), Key(clipEnd, 1f)));

        AddSlide(entrance, ui, player1, 1.88f, 0f, 245f, 0.48f, clipEnd);
        AddSlide(entrance, ui, player2, 2.12f, 0f, -245f, 0.48f, clipEnd);
        AddSlide(entrance, ui, arrowUp, 2.36f, 0f, 150f, 0.42f, clipEnd);
        AddSlide(entrance, ui, arrowDown, 2.60f, 0f, -150f, 0.42f, clipEnd);

        var tmp = text.GetComponent<TextMeshProUGUI>();
        if (tmp == null) throw new InvalidOperationException("bubble_chat/Text (TMP) needs a TextMeshProUGUI component.");
        if (text.GetComponent<AnimatedTMPTypewriter>() == null) Undo.AddComponent<AnimatedTMPTypewriter>(text.gameObject);
        var charCount = tmp.text.Length;
        const float typingStart = 1.64f;
        const float typingEnd = 4.44f;
        Set(entrance, Path(ui, text), typeof(AnimatedTMPTypewriter), "visibleCharacterCount",
            TypingFloatCurve(charCount, typingStart, typingEnd, clipEnd));
        SaveClipSettings(entrance, false);

        var entranceState = entranceController.layers[0].stateMachine.defaultState;
        if (entranceState != null) entranceState.motion = entrance;
        var states = mascotController.layers[0].stateMachine.states;
        foreach (var child in states)
        {
            if (child.state.name == "Pop") child.state.motion = pop;
            if (child.state.name == "Idle") child.state.motion = idle;
        }
        var uiAnimator = EnsureAnimator(ui);
        uiAnimator.runtimeAnimatorController = entranceController;
        var mascotAnimator = EnsureAnimator(mascot);
        mascotAnimator.runtimeAnimatorController = mascotController;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Preparation juicing refreshed: mascot first, bubble from upper left, then typing and staggered UI entrances.");
    }

    static void AddSlide(AnimationClip clip, Transform root, Transform target, float delay, float xOffset, float yOffset, float duration, float end)
    {
        var rect = (RectTransform)target;
        var x = rect.anchoredPosition.x;
        var y = rect.anchoredPosition.y;
        EnsureCanvasGroup(target);
        var path = Path(root, target);
        Set(clip, path, typeof(CanvasGroup), "m_Alpha",
            Curve(Key(0f, 0f), Key(delay, 0f), Key(delay + duration * 0.55f, 1f), Key(end, 1f)));
        Set(clip, path, typeof(RectTransform), "m_AnchoredPosition.x", Curve(Key(0f, x + xOffset), Key(delay + duration, x), Key(end, x)));
        Set(clip, path, typeof(RectTransform), "m_AnchoredPosition.y", AnimationCurve.EaseInOut(0f, y + yOffset, delay + duration, y));
    }

    static void EnsureCanvasGroup(Transform target)
    {
        if (target.GetComponent<CanvasGroup>() == null) Undo.AddComponent<CanvasGroup>(target.gameObject);
    }

    static Animator EnsureAnimator(Transform target)
    {
        var animator = target.GetComponent<Animator>();
        return animator != null ? animator : Undo.AddComponent<Animator>(target.gameObject);
    }

    static string Path(Transform root, Transform target) => AnimationUtility.CalculateTransformPath(target, root);

    static GameObject FindSceneObject(string objectName, UnityEngine.SceneManagement.Scene scene)
    {
        foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            if (candidate.name == objectName && candidate.scene == scene) return candidate;
        return null;
    }

    static Keyframe Key(float time, float value) => new Keyframe(time, value);
    static AnimationCurve Curve(params Keyframe[] keys) => new AnimationCurve(keys);
    static AnimationClip NewClip(string name) => new AnimationClip { name = name, frameRate = 60f };

    static void Set(AnimationClip clip, string path, Type component, string property, AnimationCurve curve)
    {
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, component, property), curve);
    }

    static AnimationCurve TypingFloatCurve(int characterCount, float startTime, float endTime, float holdUntil)
    {
        var slope = characterCount / (endTime - startTime);
        return new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(startTime, 0f, 0f, slope),
            new Keyframe(endTime, characterCount, slope, 0f),
            new Keyframe(holdUntil, characterCount, 0f, 0f));
    }

    static void SaveClip(AnimationClip clip, string path, bool loop)
    {
        SaveClipSettings(clip, loop);
        AssetDatabase.CreateAsset(clip, path);
    }

    static void SaveClipSettings(AnimationClip clip, bool loop)
    {
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.loopBlend = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    static void ClearCurves(AnimationClip clip)
    {
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            AnimationUtility.SetEditorCurve(clip, binding, null);
    }
}
