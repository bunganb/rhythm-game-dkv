using UnityEngine;
using UnityEngine.InputSystem;

namespace GamelanRhythm
{
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class MainMenuMouseParallax : MonoBehaviour
    {
        static readonly string[] LayerNames = { "1", "2", "3", "4", "5", "6", "mid", "fore" };
        static readonly float[] LayerStrengths = { 0.2f, 0.32f, 0.44f, 0.56f, 0.68f, 0.8f, 0.95f, 1.1f };

        [SerializeField] Vector2 maxOffset = new Vector2(10f, 8f);
        [SerializeField, Min(0.01f)] float smoothTime = 0.12f;

        RectTransform[] layers;
        Vector2[] lastOffsets;
        Vector2 smoothOffset;
        Vector2 smoothVelocity;

        void Awake() => CacheLayers();

        void OnEnable() => CacheLayers();

        void LateUpdate()
        {
            if (!Application.isPlaying || Screen.width <= 0 || Screen.height <= 0) return;
            if (layers == null || lastOffsets == null) CacheLayers();

            Vector2 mouse = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 normalizedMouse = new Vector2(
                Mathf.Clamp((mouse.x / Screen.width - 0.5f) * 2f, -1f, 1f),
                Mathf.Clamp((mouse.y / Screen.height - 0.5f) * 2f, -1f, 1f));
            Vector2 targetOffset = Vector2.Scale(normalizedMouse, maxOffset);
            smoothOffset = Vector2.SmoothDamp(
                smoothOffset, targetOffset, ref smoothVelocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

            for (int i = 0; i < layers.Length; i++)
            {
                RectTransform layer = layers[i];
                if (layer == null) continue;

                Vector2 offset = smoothOffset * LayerStrengths[i];
                Vector2 position = layer.anchoredPosition;

                // Animator drives each layer's Y curve. Add the fresh Y offset each frame.
                // X is not animated, so remove the previous X offset before applying the new one.
                position.x += offset.x - lastOffsets[i].x;
                position.y += offset.y;
                layer.anchoredPosition = position;
                lastOffsets[i] = offset;
            }
        }

        void OnDisable()
        {
            if (layers == null || lastOffsets == null) return;

            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null) continue;
                Vector2 position = layers[i].anchoredPosition - lastOffsets[i];
                layers[i].anchoredPosition = position;
                lastOffsets[i] = Vector2.zero;
            }
        }

        void CacheLayers()
        {
            layers = new RectTransform[LayerNames.Length];
            lastOffsets = new Vector2[LayerNames.Length];
            for (int i = 0; i < LayerNames.Length; i++)
                layers[i] = transform.Find(LayerNames[i]) as RectTransform;
        }
    }
}
