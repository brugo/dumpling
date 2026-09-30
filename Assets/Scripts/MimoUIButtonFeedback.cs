using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MimoGarden
{
    /// <summary>Only the picture animates, keeping the touch target stable.</summary>
    public sealed class MimoUIButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public RectTransform artwork;
        Button button;
        bool pressed, hovered;
        void Awake() { button = GetComponent<Button>(); }
        void Update()
        {
            if (!artwork) return;
            float target = button && button.interactable ? pressed ? .88f : hovered ? 1.06f : 1f : 1f;
            artwork.localScale = Vector3.Lerp(artwork.localScale, Vector3.one * target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        }
        public void OnPointerDown(PointerEventData e) { pressed = true; }
        public void OnPointerUp(PointerEventData e) { pressed = false; }
        public void OnPointerEnter(PointerEventData e) { hovered = true; }
        public void OnPointerExit(PointerEventData e) { pressed = hovered = false; }
        void OnDisable() { pressed = hovered = false; if (artwork) artwork.localScale = Vector3.one; }
    }
}
