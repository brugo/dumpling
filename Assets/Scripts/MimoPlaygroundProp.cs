using UnityEngine;

namespace MimoGarden
{
    /// <summary>A drop target owns only a visitor's pose, never its parent or saved recipe.</summary>
    public sealed class MimoPlaygroundProp : MonoBehaviour
    {
        public enum PropKind { Swing, Seat, Trampoline }

        public PropKind Kind { get; private set; }
        public string DisplayName { get; private set; }
        public Transform SeatAnchor { get; private set; }
        public Transform MotionPivot { get; private set; }
        public PetActor Occupant { get; private set; }
        public Vector3 DropPosition => SeatAnchor ? new Vector3(SeatAnchor.position.x, .105f, SeatAnchor.position.z) : transform.position;

        Vector2 dropHalfSize;
        Quaternion restPivotRotation, visitorRotation;
        Vector3 restPivotScale;
        float motionTime, amplitude, nextBounce, nextExpression;

        public void Configure(PropKind kind, string displayName, Transform seatAnchor, Vector2 halfSize, Transform motionPivot = null)
        {
            Kind = kind;
            DisplayName = displayName;
            SeatAnchor = seatAnchor;
            MotionPivot = motionPivot;
            dropHalfSize = halfSize;
            if (MotionPivot)
            {
                restPivotRotation = MotionPivot.localRotation;
                restPivotScale = MotionPivot.localScale;
            }
        }

        public bool ContainsDrop(Vector3 groundPoint)
        {
            if (!isActiveAndEnabled || !SeatAnchor) return false;
            Vector3 offset = Quaternion.Inverse(transform.rotation) * (groundPoint - DropPosition);
            return Mathf.Abs(offset.x) <= dropHalfSize.x && Mathf.Abs(offset.z) <= dropHalfSize.y;
        }

        public bool TryOccupy(PetActor pet)
        {
            if (!pet || !isActiveAndEnabled || !SeatAnchor || (Occupant && Occupant != pet)) return false;
            if (Occupant == pet) return true;
            Occupant = pet;
            visitorRotation = pet.transform.rotation;
            pet.CancelTouchMotion();
            motionTime = 0f;
            nextBounce = Time.time + .12f;
            nextExpression = Time.time + 2f;
            pet.SetExpression(Kind == PropKind.Seat ? "happy" : "giggle");
            ApplyVisitorPose();
            return true;
        }

        public void Release(PetActor pet)
        {
            if (!Occupant || Occupant != pet) return;
            PetActor visitor = Occupant;
            Occupant = null;
            visitor.CancelTouchMotion();
            Vector3 ground = visitor.transform.position;
            ground.y = .105f;
            visitor.transform.position = ground;
            visitor.transform.rotation = visitorRotation;
            visitor.SetExpression("happy");
        }

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, .05f);
            // A friend remains on its toy while the child visits another room with a different friend.
            if (Occupant && !Occupant.gameObject.activeInHierarchy) return;
            if (Occupant && Occupant.IsBeingCarried) Release(Occupant);
            amplitude = Mathf.MoveTowards(amplitude, Occupant ? 1f : 0f, dt * .75f);
            motionTime += dt;
            if (Kind == PropKind.Swing && MotionPivot)
                MotionPivot.localRotation = restPivotRotation * Quaternion.Euler(Mathf.Sin(motionTime * 2.15f) * 16f * amplitude, 0f, 0f);
            if (!Occupant)
            {
                if (Kind == PropKind.Trampoline && MotionPivot) MotionPivot.localScale = restPivotScale;
                return;
            }
            if (Kind == PropKind.Trampoline)
            {
                if (Time.time >= nextBounce && Occupant.LiftHeight < .06f)
                {
                    Occupant.Jump();
                    nextBounce = Time.time + .68f;
                }
                if (MotionPivot)
                {
                    float compression = Occupant.LiftHeight < .08f ? .07f : 0f;
                    Vector3 scale = restPivotScale;
                    scale.y *= 1f - compression;
                    MotionPivot.localScale = Vector3.Lerp(MotionPivot.localScale, scale, 1f - Mathf.Exp(-18f * dt));
                }
            }
            if (Time.time >= nextExpression)
            {
                Occupant.SetExpression(Kind == PropKind.Seat ? "happy" : "giggle");
                nextExpression = Time.time + 2.4f;
            }
            ApplyVisitorPose();
        }

        void ApplyVisitorPose()
        {
            if (!Occupant || !SeatAnchor) return;
            Occupant.transform.SetPositionAndRotation(SeatAnchor.position, SeatAnchor.rotation);
        }

        void OnDestroy()
        {
            if (Occupant) Release(Occupant);
        }
    }

    /// <summary>Excludes only the animated assembly from the mobile static room batch.</summary>
    public sealed class MimoPlaygroundMovingPart : MonoBehaviour { }
}
