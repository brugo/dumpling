using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MimoGarden
{
    /// <summary>One drag owner: a leftward pull picks up the item; vertical motion belongs to the rail.</summary>
    public sealed class MimoIngredientDrag : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        MimoExperience experience;
        ScrollRect scroll;
        CanvasGroup fade;
        int category, index, pointerId = int.MinValue;
        enum Route { None, Scroll, Ingredient, Rejected }
        Route route;
        public void Configure(MimoExperience owner, ScrollRect rail, int ingredientCategory, int ingredientIndex, Canvas ui)
        { experience = owner; scroll = rail; category = ingredientCategory; index = ingredientIndex; fade = gameObject.AddComponent<CanvasGroup>(); }
        public void OnInitializePotentialDrag(PointerEventData e)
        { e.useDragThreshold = true; if (scroll) scroll.OnInitializePotentialDrag(e); }
        public void OnBeginDrag(PointerEventData e)
        {
            if (pointerId != int.MinValue) return;
            pointerId = e.pointerId;
            Vector2 delta = e.position - e.pressPosition;
            // EventSystem has already applied pixelDragThreshold. A second, canvas-scaled
            // threshold here would permanently misroute a slow leftward pull as scrolling.
            if (delta.x < 0 && -delta.x > Mathf.Abs(delta.y) * 1.05f)
            {
                if (scroll) scroll.StopMovement();
                route = experience && experience.BeginIngredientDrag(category, index, e.position) ? Route.Ingredient : Route.Rejected;
                if (route == Route.Ingredient && fade) fade.alpha = .48f;
            }
            else { route = Route.Scroll; if (scroll) scroll.OnBeginDrag(e); }
            e.eligibleForClick = false;
        }
        public void OnDrag(PointerEventData e)
        {
            if (pointerId != e.pointerId) return;
            if (route == Route.Scroll) { if (scroll) scroll.OnDrag(e); }
            else if (route == Route.Ingredient && experience) experience.MoveIngredientDrag(e.position);
        }
        public void OnEndDrag(PointerEventData e)
        {
            if (pointerId != e.pointerId) return;
            if (route == Route.Scroll) { if (scroll) scroll.OnEndDrag(e); }
            else if (route == Route.Ingredient && experience) experience.EndIngredientDrag(e.position);
            route = Route.None; pointerId = int.MinValue;
            if (fade) fade.alpha = 1;
            e.eligibleForClick = false;
        }
        void CancelRoute()
        {
            if (route == Route.Ingredient && experience) experience.CancelIngredientDrag();
            if (route == Route.Scroll && scroll) scroll.StopMovement();
            route = Route.None; pointerId = int.MinValue; if (fade) fade.alpha = 1;
        }
        void OnDisable() { CancelRoute(); }
        void OnApplicationFocus(bool focused) { if (!focused) CancelRoute(); }
        void OnApplicationPause(bool paused) { if (paused) CancelRoute(); }
    }
}
