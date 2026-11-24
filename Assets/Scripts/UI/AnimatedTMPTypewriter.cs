using TMPro;
using UnityEngine;

namespace GamelanRhythm
{
    /// <summary>
    /// Receives a float curve from an Animator and applies it as TMP's visible character count.
    /// A float is used because TMP's integer field is not supported by Unity's discrete curve binding.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimatedTMPTypewriter : MonoBehaviour
    {
        [SerializeField, Min(0f)] float visibleCharacterCount;

        TMP_Text target;

        void Awake()
        {
            target = GetComponent<TMP_Text>();
            ApplyVisibleCharacterCount();
        }

        void LateUpdate()
        {
            ApplyVisibleCharacterCount();
        }

        void ApplyVisibleCharacterCount()
        {
            if (target == null) target = GetComponent<TMP_Text>();
            if (target == null) return;

            target.maxVisibleCharacters = Mathf.Max(0, Mathf.FloorToInt(visibleCharacterCount));
        }
    }
}
