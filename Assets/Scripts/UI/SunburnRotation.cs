using UnityEngine;

namespace GamelanRhythm
{
    /// <summary>Continuously rotates a decorative UI element at an editor-adjustable speed.</summary>
    public sealed class SunburnRotation : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = 18f;

        void Update()
        {
            transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime, Space.Self);
        }
    }
}
