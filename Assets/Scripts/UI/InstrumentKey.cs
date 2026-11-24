using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GamelanRhythm
{
    public sealed class InstrumentKey : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] GamelanGame game;
        [SerializeField] int playerId, noteIndex;
        [SerializeField] Image face;
        [SerializeField] Color baseColor;
        Vector3 baseScale;
        Coroutine pressRoutine;

        public void Initialize(GamelanGame owner, int player, int key, Image visual)
        {
            game = owner;
            playerId = player;
            noteIndex = key;
            face = visual;
            baseColor = visual.color;
            baseScale = visual.rectTransform.localScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pressRoutine != null) StopCoroutine(pressRoutine);
            pressRoutine = StartCoroutine(Animate());
            game.Press(playerId, noteIndex);
        }

        IEnumerator Animate()
        {
            face.rectTransform.localScale = Vector3.Scale(baseScale, new Vector3(1.12f, 0.85f, 1));
            face.color = Color.white;
            yield return new WaitForSecondsRealtime(0.06f);
            face.rectTransform.localScale = Vector3.Scale(baseScale, new Vector3(0.95f, 1.08f, 1));
            yield return new WaitForSecondsRealtime(0.06f);
            face.rectTransform.localScale = baseScale;
            face.color = baseColor;
            pressRoutine = null;
        }
    }
}
