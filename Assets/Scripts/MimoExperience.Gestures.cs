using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MimoGarden
{
    public sealed partial class MimoExperience
    {
        public string InteractionMode { get; private set; } = "Mover";
        public bool IsPetDragging => petGesture && gestureDragging;
        public bool IsPetGestureActive => petGesture;

        bool petGesture, gestureDragging, gestureRotating, physicalGesture, blockUntilRelease;
        int primaryFinger = -1, secondaryFinger = -1, gestureMouseButton;
        Vector2 gestureStart, gestureLast, gesturePosition, twoFingerOrigin;
        Vector3 gestureRootStart, gesturePlaneStart;
        float gestureStarted, gestureYaw, twoFingerAngle, touchSeenAt = -100f;
        Plane gestureFloor;
        readonly List<RaycastResult> gestureUIHits = new List<RaycastResult>();

        public void SetInteractionMode(string mode)
        {
            ResetPetInteraction();
            InteractionMode = mode == "Amassar" || mode == "Girar" ? mode : "Mover";
        }

        bool IsGestureOverUI(Vector2 position)
        {
            if (!EventSystem.current) return false;
            var pointer = new PointerEventData(EventSystem.current) { position = position };
            gestureUIHits.Clear();
            EventSystem.current.RaycastAll(pointer, gestureUIHits);
            return gestureUIHits.Count > 0;
        }

        /// <summary>Mouse, touch and regression checks all start through this same hit-tested path.</summary>
        public bool BeginPetGesture(Vector2 position, bool rotate = false)
        {
            if (IsFriendDialogOpen || place != Place.Home || !actor || !actorRoot || !sceneCamera || IsGestureOverUI(position)) return false;
            Physics.SyncTransforms();
            RaycastHit hit;
            if (!HitPet(position, out hit)) return false;
            PetActor touched = hit.collider.GetComponentInParent<PetActor>();
            if (touched != actor && !SelectPet(touched)) return false;
            // This same physical contact acquired the newly selected friend; it is not a menu carry-over.
            blockUntilRelease = false;
            ResetPetInteraction();
            if (!OccupiedProp(actor)) StopPetAction(actor);
            UpdateFriendHeading();
            petGesture = holding = true;
            gestureRotating = rotate || InteractionMode == "Girar";
            gestureStart = gestureLast = gesturePosition = position;
            gestureStarted = Time.unscaledTime;
            gestureRootStart = actorRoot.position;
            gestureYaw = actorRoot.eulerAngles.y;
            gestureRootStart.y = PetGround;
            gestureFloor = new Plane(Vector3.up, gestureRootStart);
            float distance;
            Ray ray = sceneCamera.ScreenPointToRay(position);
            gesturePlaneStart = gestureFloor.Raycast(ray, out distance) ? ray.GetPoint(distance) : gestureRootStart;
            if (gestureRotating) actor.SetExpression("surprised");
            else actor.BeginSquish();
            return true;
        }

        public void MovePetGesture(Vector2 position)
        {
            if (!petGesture || !actor || !actorRoot) return;
            gesturePosition = position;
            Vector2 total = position - gestureStart;
            Vector2 delta = position - gestureLast;
            if (gestureRotating)
            {
                actorRoot.rotation = Quaternion.Euler(0, gestureYaw - total.x * 300f / Mathf.Max(400f, sceneCamera.pixelWidth), 0);
                if (delta.sqrMagnitude > 6f) actor.SetExpression("giggle");
            }
            else if (InteractionMode == "Amassar")
            {
                float pressure = Mathf.Clamp01(.4f + (Time.unscaledTime - gestureStarted) * .35f + total.magnitude / Mathf.Max(150f, Screen.height * .25f));
                actor.SetSquishPressure(pressure, total / Mathf.Max(70f, Screen.height * .13f));
            }
            else
            {
                float threshold = Mathf.Max(14f, Screen.height * .017f);
                if (!gestureDragging && total.sqrMagnitude > threshold * threshold)
                {
                    gestureDragging = true;
                    StopPetAction(actor);
                    actor.BeginCarry();
                }
                if (gestureDragging)
                {
                    Ray ray = sceneCamera.ScreenPointToRay(position);
                    float distance;
                    if (gestureFloor.Raycast(ray, out distance))
                    {
                        Vector3 target = ClampPetFloor(gestureRootStart + ray.GetPoint(distance) - gesturePlaneStart);
                        Vector3 previous = actorRoot.position;
                        // Root movement owns the floor position; the actor owns lift, squash and wobble.
                        actorRoot.position = Vector3.Lerp(previous, target, 1f - Mathf.Exp(-23f * Mathf.Max(.008f, Time.unscaledDeltaTime)));
                        actor.SetCarryMotion((actorRoot.position - previous) / Mathf.Max(.008f, Time.unscaledDeltaTime));
                    }
                }
                else actor.SetSquishPressure(Mathf.Clamp01(.3f + (Time.unscaledTime - gestureStarted) * .4f), Vector2.zero);
            }
            gestureLast = position;
        }

        Vector3 ClampPetFloor(Vector3 point)
        {
            point.y = PetGround;
            if (care == Care.Play) point = ClampPlaygroundPosition(point);
            else { point.x = Mathf.Clamp(point.x, -4.5f, 4.5f); point.z = Mathf.Clamp(point.z, -2f, 3.2f); }
            // Camera viewport already excludes the action rail. Keep the entire squishy inside it.
            Vector3 middle = point + Vector3.up * (care == Care.Play ? 1f : 1.4f);
            Vector3 viewport = sceneCamera.WorldToViewportPoint(middle);
            Bounds visible = actor.VisibleBounds;
            float reach = Mathf.Max(care == Care.Play ? 1f : 1.5f, visible.extents.x + Mathf.Abs(visible.center.x - actorRoot.position.x) + .1f);
            float halfWidth = Mathf.Abs(sceneCamera.WorldToViewportPoint(middle + sceneCamera.transform.right * reach).x - viewport.x);
            float margin = Mathf.Clamp(halfWidth + .015f, .055f, .46f);
            viewport.x = Mathf.Clamp(viewport.x, margin, 1f - margin);
            point.x = sceneCamera.ViewportToWorldPoint(viewport).x;
            return point;
        }

        public void EndPetGesture()
        {
            if (!petGesture) return;
            bool dropped = gestureDragging;
            if (gestureDragging) actor.EndCarry();
            else if (!gestureRotating) actor.EndSquish();
            else actor.SetExpression("happy");
            if (!gestureDragging && !gestureRotating && (gesturePosition - gestureStart).sqrMagnitude < 400f)
                Burst(actorRoot.position + new Vector3(0, 2.05f, -.3f), 5, colors[actor.Data.colorIndex], .055f);
            petGesture = holding = gestureDragging = gestureRotating = physicalGesture = false;
            primaryFinger = secondaryFinger = -1;
            if (dropped) FinishPlaygroundDrop(gesturePosition);
        }

        public void ResetPetInteraction()
        {
            StopBathToolFlow();
            if (actor) actor.CancelTouchMotion();
            petGesture = gestureDragging = gestureRotating = physicalGesture = false;
            holding = draggingFood = false;
            primaryFinger = secondaryFinger = -1;
            if (tool && snackRoutine == null) { tool.position = toolRest; tool.rotation = Quaternion.identity; }
        }

        void ReleaseTouch()
        {
            ResetPetInteraction();
            // A finger still held across a menu/room change cannot become a new gesture.
            blockUntilRelease = Input.touchCount > 0 || Input.GetMouseButton(0) || Input.GetMouseButton(1);
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) { CloseShop(); ReleaseTouch(); CancelCreationRitual(); }
        }

        void OnDisable() { ResetPetInteraction(); CancelCreationRitual(); }

        void HandleTouch()
        {
            if (IsFriendDialogOpen) return;
            if (blockUntilRelease)
            {
                if (Input.touchCount == 0 && !Input.GetMouseButton(0) && !Input.GetMouseButton(1)) blockUntilRelease = false;
                return;
            }
            // Public gesture calls are also useful for accessibility/controllers and deterministic QA.
            // Only a physical input that acquired this gesture can release it.
            if (petGesture && !physicalGesture) { MovePetGesture(gesturePosition); return; }
            if (holding && care == Care.Bath && !physicalGesture) return;
            if (Input.touchCount > 0)
            {
                touchSeenAt = Time.unscaledTime;
                HandleFingers();
                return;
            }
            if (primaryFinger >= 0) { ResetPetInteraction(); return; }
            if (Time.unscaledTime - touchSeenAt < .25f) return; // Suppress Unity's synthesized touch mouse.
            Vector2 position = Input.mousePosition;
            bool secondary = Input.GetMouseButtonDown(1);
            if (secondary || Input.GetMouseButtonDown(0))
            {
                if (petGesture || holding || draggingFood) ResetPetInteraction();
                if (!IsGestureOverUI(position))
                {
                    gestureMouseButton = secondary ? 1 : 0;
                    BeginPhysicalGesture(position, secondary);
                }
            }
            if (physicalGesture && Input.GetMouseButton(gestureMouseButton)) MovePhysicalGesture(position);
            if (physicalGesture && (!Input.GetMouseButton(gestureMouseButton) || Input.GetMouseButtonUp(gestureMouseButton))) EndPhysicalGesture(position, false);
            if (Input.GetKeyDown(KeyCode.Space)) actor.Jump();
            if (Input.GetKeyDown(KeyCode.C)) actor.Pet();
        }

        bool FindFinger(int id, out Touch touch)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                touch = Input.GetTouch(i);
                if (touch.fingerId == id) return true;
            }
            touch = default(Touch); return false;
        }

        static bool FingerEnded(Touch touch) => touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;

        void HandleFingers()
        {
            if (primaryFinger < 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch fresh = Input.GetTouch(i);
                    if (fresh.phase != TouchPhase.Began || IsGestureOverUI(fresh.position)) continue;
                    BeginPhysicalGesture(fresh.position, false);
                    if (physicalGesture) { primaryFinger = fresh.fingerId; break; }
                }
            }
            if (primaryFinger < 0) return;
            Touch first;
            if (!FindFinger(primaryFinger, out first)) { ReleaseTouch(); return; }
            if (secondaryFinger >= 0)
            {
                Touch second;
                if (!FindFinger(secondaryFinger, out second) || FingerEnded(first) || FingerEnded(second))
                { EndPhysicalGesture(first.position, first.phase == TouchPhase.Canceled); blockUntilRelease = true; return; }
                Vector2 centroid = (first.position + second.position) * .5f;
                Vector2 span = second.position - first.position;
                float twist = Mathf.DeltaAngle(twoFingerAngle, Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg);
                MovePetGesture(centroid);
                actorRoot.rotation = Quaternion.Euler(0, gestureYaw - (centroid.x - twoFingerOrigin.x) * 300f / Mathf.Max(400f, sceneCamera.pixelWidth) + twist, 0);
                return;
            }
            if (FingerEnded(first)) { EndPhysicalGesture(first.position, first.phase == TouchPhase.Canceled); return; }
            if (petGesture)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch second = Input.GetTouch(i);
                    if (second.fingerId == primaryFinger || second.phase != TouchPhase.Began || IsGestureOverUI(second.position)) continue;
                    actor.CancelTouchMotion(); gestureDragging = false; gestureRotating = true;
                    secondaryFinger = second.fingerId;
                    gestureStart = gestureLast = twoFingerOrigin = (first.position + second.position) * .5f;
                    gestureYaw = actorRoot.eulerAngles.y;
                    Vector2 span = second.position - first.position;
                    twoFingerAngle = Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg;
                    actor.SetExpression("surprised");
                    return;
                }
            }
            MovePhysicalGesture(first.position);
        }

        void BeginPhysicalGesture(Vector2 position, bool rotate)
        {
            if (!rotate && BeginBathToolGesture(position)) { physicalGesture = true; return; }
            if (BeginPetGesture(position, rotate)) { physicalGesture = true; return; }
            if (rotate || place != Place.Home || care == Care.Play || IsGestureOverUI(position) || snackRoutine != null) return;
            if (care == Care.Bath) return; // Bath tools must be picked up from their visible geometry.
            // Tools are picked up from the surrounding scene; touching Mimo itself remains tactile.
            holding = care == Care.Bath; draggingFood = care == Care.Food;
            physicalGesture = holding || draggingFood;
            gesturePosition = position;
        }

        void MovePhysicalGesture(Vector2 position)
        {
            if (petGesture) { MovePetGesture(position); return; }
            if (!holding && !draggingFood) return;
            if (care == Care.Bath) { DragBathToolGesture(position, Time.deltaTime); return; }
            RaycastHit hit;
            bool hitsPet = HitPet(position, out hit);
            bool overUI = IsGestureOverUI(position);
            if (tool)
            {
                Ray ray = sceneCamera.ScreenPointToRay(position);
                Plane plane = new Plane(Vector3.forward, new Vector3(0, 0, actorRoot.position.z - .95f));
                float distance;
                if (plane.Raycast(ray, out distance)) tool.position = ray.GetPoint(distance);
                if (hitsPet) tool.position = hit.point + hit.normal * .2f;
                tool.rotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 18) * 12);
            }
        }

        void EndPhysicalGesture(Vector2 position, bool canceled)
        {
            StopBathToolFlow();
            if (canceled) { ResetPetInteraction(); return; }
            if (petGesture) EndPetGesture();
            else
            {
                RaycastHit hit;
                if (draggingFood && !IsGestureOverUI(position) && HitPet(position, out hit)) FeedSnack();
                holding = draggingFood = physicalGesture = false;
                primaryFinger = secondaryFinger = -1;
            }
            if (tool && snackRoutine == null) { tool.position = toolRest; tool.rotation = Quaternion.identity; }
        }
    }
}
