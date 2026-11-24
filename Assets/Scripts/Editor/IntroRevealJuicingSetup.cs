using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using GamelanRhythm;

public static class IntroRevealJuicingSetup
{
    const string Folder = "Assets/Animations/IntroReveal";
    const string ClipPath = Folder + "/IntroRevealSequence.anim";
    const string ControllerPath = Folder + "/IntroReveal.controller";
    const float SlideDuration = 0.45f;
    const float TextP1Delay = 0.55f;
    const float RevealP2Delay = 1.10f;
    const float TextP2Delay = 1.65f;
    const float ClipEnd = 4.10f;

    [MenuItem("Tools/Gamelan Rhythm/Setup Intro Reveal Juicing")]
    public static void Setup()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Gameplay")
            throw new InvalidOperationException("Open the Gameplay scene before setting up Intro Reveal animations.");

        var canvas = FindSceneObject("Intro Reveal Canvas", scene);
        var ui = canvas != null ? canvas.transform.Find("Safe Area/Intro Reveal UI") as RectTransform : null;
        if (ui == null) throw new InvalidOperationException("Intro Reveal Canvas/Safe Area/Intro Reveal UI was not found.");

        var revealP1 = ui.Find("Reveal-p1") as RectTransform;
        var textP1 = ui.Find("text-p1") as RectTransform;
        var revealP2 = ui.Find("Reveal-p2") as RectTransform;
        var textP2 = ui.Find("text-p2") as RectTransform;
        if (revealP1 == null || textP1 == null || revealP2 == null || textP2 == null)
            throw new InvalidOperationException("Reveal-p1, text-p1, Reveal-p2, and text-p2 must be direct children of Intro Reveal UI.");

        var canvasRoot = FindSceneObject("Gameplay UI Canvases", scene);
        var transition = canvasRoot != null ? canvasRoot.GetComponent<GameplayPreparationTransition>() : null;
        if (transition == null) throw new InvalidOperationException("GameplayPreparationTransition was not found on Gameplay UI Canvases.");

        if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Animations", "IntroReveal");

        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null)
        {
            clip = new AnimationClip { name = "IntroRevealSequence", frameRate = 60f };
            AssetDatabase.CreateAsset(clip, ClipPath);
        }
        else
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                AnimationUtility.SetEditorCurve(clip, binding, null);
        }

        var travel = Mathf.Max(ui.rect.width, Screen.width);
        AddSlide(clip, ui, revealP1, travel, 0f);
        AddSlide(clip, ui, textP1, travel, TextP1Delay);
        AddSlide(clip, ui, revealP2, -travel, RevealP2Delay);
        AddSlide(clip, ui, textP2, -travel, TextP2Delay);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        settings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var machine = controller.layers[0].stateMachine;
        AnimatorState sequence = null;
        foreach (var child in machine.states)
            if (child.state.name == "Intro Reveal Sequence") sequence = child.state;
        if (sequence == null) sequence = machine.AddState("Intro Reveal Sequence", new Vector3(240f, 80f, 0f));
        sequence.motion = clip;
        machine.defaultState = sequence;

        var animator = ui.GetComponent<Animator>();
        if (animator == null) animator = Undo.AddComponent<Animator>(ui.gameObject);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var serializedTransition = new SerializedObject(transition);
        serializedTransition.FindProperty("introRevealDuration").floatValue = ClipEnd;
        serializedTransition.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Intro Reveal sequence assigned: Reveal-p1, text-p1, Reveal-p2, text-p2, then a 2 second hold.");
    }

    static void AddSlide(AnimationClip clip, RectTransform root, RectTransform target, float offset, float delay)
    {
        var path = AnimationUtility.CalculateTransformPath(target, root);
        var x = target.anchoredPosition.x;
        var from = x + offset;
        var arrive = delay + SlideDuration;
        AnimationCurve curve;
        if (delay <= 0f)
        {
            curve = new AnimationCurve(
                new Keyframe(0f, from, 0f, 0f),
                new Keyframe(SlideDuration, x, 0f, 0f),
                new Keyframe(ClipEnd, x, 0f, 0f));
        }
        else
        {
            curve = new AnimationCurve(
                new Keyframe(0f, from, 0f, 0f),
                new Keyframe(delay, from, 0f, 0f),
                new Keyframe(arrive, x, 0f, 0f),
                new Keyframe(ClipEnd, x, 0f, 0f));
        }

        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve(path, typeof(RectTransform), "m_AnchoredPosition.x"), curve);
    }

    static GameObject FindSceneObject(string objectName, UnityEngine.SceneManagement.Scene scene)
    {
        foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            if (candidate.name == objectName && candidate.scene == scene) return candidate;
        return null;
    }
}
