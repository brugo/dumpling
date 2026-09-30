using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MimoGarden
{
    /// <summary>Small reusable 3D workshop. Free touch movement drives a playful mixing animation.</summary>
    public sealed class MimoCreationStation : MonoBehaviour
    {
        enum Phase { Ready, Drag, Pour, Stir, Reveal }
        Phase phase;
        Camera view;
        Transform basket, lid, spoon, spoonTip, liquid, flow, ingredient, guide;
        Transform[] ripples, beads;
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        readonly Dictionary<int, Transform> ingredientModels = new Dictionary<int, Transform>();
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        Mesh sphereMesh, cylinderMesh, cubeMesh, ringMesh, surfaceMesh;
        Material mixtureMaterial, streamMaterial;
        Vector3[] surfaceBase, surfaceVertices;
        Vector3 dragStart, spoonPoint;
        Color liquidColor, nextColor;
        float elapsed, fill, targetFill, stirAngle, mixingProgress, spinRemaining, surfaceClock, rippleEnergy, reveal;
        Vector2 lastStirScreen;
        bool stirring, released;
        const float SurfaceRadius = 1.08f, StirRadius = .70f, RequiredTravelRadii = 3.2f;
        readonly Vector3 lidRest = new Vector3(1.60f, 1.15f, .78f);
        readonly Quaternion lidRestRotation = Quaternion.Euler(-68, 12, 14);

        public bool Busy => phase == Phase.Drag || phase == Phase.Pour || phase == Phase.Reveal;
        public bool PendingStir => phase == Phase.Pour || phase == Phase.Stir;
        public bool IsPouring => phase == Phase.Pour;
        public bool CanStir => phase == Phase.Stir;
        public bool IsDraggingIngredient => phase == Phase.Drag;
        public bool IsStirring => stirring;
        public int DragCategory { get; private set; }
        public int DragIndex { get; private set; }
        public int AddedIngredientCount { get; private set; }
        public int MixedIngredientCount { get; private set; }
        public float StirProgress => mixingProgress;
        public bool SpoonSpinning => spinRemaining > 0;
        public float LiquidFill => fill;
        public Vector3 WorldCenter => transform.TransformPoint(new Vector3(0, SurfaceY, 0));
        public Vector2 CenterScreen => view ? (Vector2)view.WorldToScreenPoint(WorldCenter) : Vector2.zero;
        public Vector2 DropScreen => CenterScreen;
        public float StirScreenRadius => view ? Mathf.Abs(StirScreenPoint(0).x - CenterScreen.x) : 0;
        float SurfaceY => .26f + fill * .47f;

        public void Initialize(Camera camera)
        {
            view = camera; transform.position = new Vector3(0, .29f, -.10f);
            sphereMesh = MakeSphere(); cylinderMesh = MakeCylinder(); cubeMesh = MakeCube();
            ringMesh = MakeRing(1f, .028f, 48, 6);
            BuildStation(); ResetStation();
        }
        public Vector2 StirScreenPoint(float radians)
        {
            return view.WorldToScreenPoint(transform.TransformPoint(new Vector3(Mathf.Cos(radians) * StirRadius, SurfaceY, Mathf.Sin(radians) * StirRadius)));
        }
        public void ResetStation()
        {
            CancelPointer(); phase = Phase.Ready; AddedIngredientCount = MixedIngredientCount = 0;
            fill = targetFill = 0; mixingProgress = spinRemaining = stirAngle = 0; reveal = rippleEnergy = 0;
            liquidColor = nextColor = C("FFE9C7"); SetColor(mixtureMaterial, liquidColor);
            basket.localScale = Vector3.one; basket.localPosition = Vector3.zero;
            lid.localPosition = lidRest; lid.localRotation = lidRestRotation; lid.localScale = Vector3.one;
            liquid.gameObject.SetActive(false); flow.gameObject.SetActive(false); guide.gameObject.SetActive(false);
            spoon.gameObject.SetActive(true); spoonPoint = new Vector3(.74f, .84f, -.05f); PlaceSpoon(spoonPoint, 0);
            foreach (Transform model in ingredientModels.Values) model.gameObject.SetActive(false);
            for (int i = 0; i < ripples.Length; i++) ripples[i].gameObject.SetActive(false);
        }
        public void RestoreForCreation()
        {
            // A reveal can be interrupted by recovery. Retain the mixed recipe, restore its workbench.
            if (phase == Phase.Reveal) phase = Phase.Ready;
            basket.localScale = Vector3.one; basket.localPosition = Vector3.zero;
            lid.localPosition = lidRest; lid.localRotation = lidRestRotation; lid.localScale = Vector3.one;
            spoon.gameObject.SetActive(true); liquid.gameObject.SetActive(fill > .001f);
            guide.gameObject.SetActive(phase == Phase.Stir);
        }

        public bool BeginIngredientDrag(int category, int index, Vector2 screen)
        {
            if (Busy || !gameObject.activeInHierarchy) return false;
            EndStir(); spinRemaining = 0;
            DragCategory = category; DragIndex = index; phase = Phase.Drag; elapsed = 0;
            int key = category * 100 + index;
            if (!ingredientModels.TryGetValue(key, out ingredient))
            { ingredient = BuildIngredient(category, index); ingredientModels.Add(key, ingredient); }
            ingredient.gameObject.SetActive(true); ingredient.localScale = Vector3.one;
            ingredient.rotation = Quaternion.identity; MoveIngredientDrag(screen); return true;
        }
        public void MoveIngredientDrag(Vector2 screen)
        {
            if (phase != Phase.Drag || !ingredient) return;
            Plane plane = new Plane(-view.transform.forward, WorldCenter + Vector3.up * .65f);
            Ray ray = view.ScreenPointToRay(screen);
            if (plane.Raycast(ray, out float distance)) ingredient.position = ray.GetPoint(distance);
            ingredient.localRotation = Quaternion.Euler(-12, Mathf.Sin(Time.unscaledTime * 3) * 6, -8);
        }
        public bool EndIngredientDrag(Vector2 screen)
        {
            if (phase != Phase.Drag) return false;
            if (!PointOnSurface(screen, out Vector3 local) || new Vector2(local.x, local.z).magnitude > 1.19f)
            { CancelDrag(); return false; }
            phase = Phase.Pour; elapsed = 0; dragStart = ingredient.localPosition;
            int pendingIngredients = AddedIngredientCount - MixedIngredientCount;
            // Keep work already done when another ingredient joins the same mixture.
            mixingProgress *= pendingIngredients / (float)(pendingIngredients + 1);
            spinRemaining = 0; stirring = false;
            AddedIngredientCount++; targetFill = Mathf.Min(1f, .50f + (AddedIngredientCount - 1) * .065f);
            nextColor = DragCategory == 0 ? PetCatalog.Colors[DragIndex] : liquidColor;
            if (DragCategory == 7 && DragIndex == 1) nextColor = Color.Lerp(liquidColor, Color.white, .16f);
            SetColor(streamMaterial, DragCategory == 0 ? nextColor : C("FFEAC7"));
            liquid.gameObject.SetActive(true); guide.gameObject.SetActive(false); return true;
        }
        public void CancelDrag()
        {
            if (phase != Phase.Drag) return;
            if (ingredient) ingredient.gameObject.SetActive(false);
            phase = AddedIngredientCount > MixedIngredientCount ? Phase.Stir : Phase.Ready;
            guide.gameObject.SetActive(phase == Phase.Stir);
        }
        public void CancelPointer() { CancelDrag(); EndStir(); }

        public bool BeginStir(Vector2 screen)
        {
            if (phase != Phase.Stir || !StirPoint(screen, out _)) return false;
            stirring = true; lastStirScreen = screen; return true;
        }
        public void MoveStir(Vector2 screen)
        {
            if (!stirring || phase != Phase.Stir) return;
            Vector2 previous = lastStirScreen; lastStirScreen = screen;
            if (!StirPoint(screen, out _) || !StirPoint(previous, out _)) return;
            float movement = Vector2.Distance(previous, screen);
            if (movement <= 0f) return;
            mixingProgress = Mathf.Clamp01(mixingProgress + movement / (Mathf.Max(32f, StirScreenRadius) * RequiredTravelRadii));
            spinRemaining = .55f; rippleEnergy = Mathf.Min(1, rippleEnergy + .25f);
            if (mixingProgress >= 1f)
            {
                phase = Phase.Ready; stirring = false; MixedIngredientCount = AddedIngredientCount;
                guide.gameObject.SetActive(false); rippleEnergy = 1f;
            }
        }
        public void EndStir() { stirring = false; }
        bool PointOnSurface(Vector2 screen, out Vector3 local)
        {
            local = default;
            if (!view.pixelRect.Contains(screen)) return false;
            Plane plane = new Plane(Vector3.up, WorldCenter); Ray ray = view.ScreenPointToRay(screen);
            if (!plane.Raycast(ray, out float distance)) return false;
            local = transform.InverseTransformPoint(ray.GetPoint(distance)); return true;
        }
        bool StirPoint(Vector2 screen, out Vector3 local)
        {
            if (!PointOnSurface(screen, out local)) return false;
            float radius = new Vector2(local.x, local.z).magnitude;
            return radius <= 1.40f;
        }

        public void Tick(float dt)
        {
            dt = Mathf.Min(dt, .08f); elapsed += dt; surfaceClock += dt;
            if (phase == Phase.Pour) AnimateIngredient(dt);
            fill = Mathf.MoveTowards(fill, targetFill, dt * .42f);
            liquidColor = Color.Lerp(liquidColor, nextColor, dt * (stirring ? 4 : 1.8f));
            if (surfaceClock >= .066f)
            {
                surfaceClock = 0; AnimateSurface(); SetColor(mixtureMaterial, liquidColor);
            }
            rippleEnergy = Mathf.MoveTowards(rippleEnergy, stirring ? .6f : 0, dt * .45f);
            if (spinRemaining > 0 && (phase == Phase.Stir || phase == Phase.Ready))
            {
                spinRemaining = Mathf.Max(0, spinRemaining - dt);
                stirAngle += dt * 360f;
                float angle = stirAngle * Mathf.Deg2Rad;
                Vector3 orbit = new Vector3(Mathf.Cos(angle) * StirRadius, SurfaceY + .015f, Mathf.Sin(angle) * StirRadius);
                spoonPoint = Vector3.Lerp(spoonPoint, orbit, dt * 18f); PlaceSpoon(spoonPoint, stirAngle);
            }
            else if (phase != Phase.Reveal)
            {
                Vector3 rest = phase == Phase.Stir ? new Vector3(.70f, SurfaceY + .015f, 0) : new Vector3(.85f, .83f, -.10f);
                spoonPoint = Vector3.Lerp(spoonPoint, rest, dt * 4); PlaceSpoon(spoonPoint, 0);
            }
            if (phase == Phase.Stir)
            {
                for (int i = 0; i < beads.Length; i++)
                {
                    float a = i * Mathf.PI * 2 / beads.Length;
                    beads[i].localPosition = new Vector3(Mathf.Cos(a) * StirRadius, SurfaceY + .012f, Mathf.Sin(a) * StirRadius);
                    bool done = i < Mathf.FloorToInt(StirProgress * beads.Length);
                    beads[i].localScale = Vector3.one * (done ? .095f : .047f);
                }
            }
            for (int i = 0; i < ripples.Length; i++)
            {
                bool visible = fill > .05f && (rippleEnergy > .03f || phase == Phase.Stir);
                ripples[i].gameObject.SetActive(visible);
                if (!visible) continue;
                float size = .27f + Mathf.Repeat(Time.unscaledTime * .30f + i * .29f, .63f);
                ripples[i].localPosition = new Vector3(0, SurfaceY + .012f + i * .002f, 0);
                ripples[i].localScale = new Vector3(size, .32f, size);
                ripples[i].localRotation = Quaternion.Euler(0, stirAngle + i * 40, 0);
            }
        }
        void AnimateIngredient(float dt)
        {
            bool bottle = DragCategory == 0 || DragCategory == 7;
            float approach = Mathf.SmoothStep(0, 1, elapsed / .32f);
            Vector3 pourPosition = new Vector3(.70f, 1.55f, .10f);
            if (bottle)
            {
                ingredient.localPosition = Vector3.Lerp(dragStart, pourPosition, approach);
                float tilt = Mathf.SmoothStep(0, 1, (elapsed - .18f) / .35f);
                ingredient.localRotation = Quaternion.Euler(-10, 0, 105 * tilt);
                bool flowing = elapsed > .38f && elapsed < 1.30f;
                flow.gameObject.SetActive(flowing);
                if (flowing)
                {
                    Vector3 from = ingredient.TransformPoint(new Vector3(0, .44f, 0));
                    Vector3 to = transform.TransformPoint(new Vector3(.17f, SurfaceY, .06f));
                    PositionCylinder(flow, from, to, .07f + Mathf.Sin(elapsed * 28f) * .008f); rippleEnergy = .6f;
                }
                if (elapsed > 1.30f) ingredient.localScale = Vector3.one * Mathf.Max(.01f, 1 - (elapsed - 1.30f) * 5);
            }
            else
            {
                float drop = Mathf.Clamp01((elapsed - .20f) / .60f);
                Vector3 start = Vector3.Lerp(dragStart, new Vector3(0, 1.70f, 0), approach);
                ingredient.localPosition = Vector3.Lerp(start, new Vector3(0, SurfaceY - .06f, 0), drop * drop);
                ingredient.localRotation = Quaternion.Euler(12 + drop * 40, elapsed * 100, Mathf.Sin(elapsed * 7) * 16);
                ingredient.localScale = Vector3.one * Mathf.Max(.01f, 1 - Mathf.Clamp01((drop - .65f) / .35f));
                if (drop > .7f) rippleEnergy = .85f;
            }
            if (elapsed >= (bottle ? 1.52f : 1.0f))
            {
                ingredient.gameObject.SetActive(false); flow.gameObject.SetActive(false);
                phase = Phase.Stir; guide.gameObject.SetActive(true); elapsed = 0;
            }
        }
        void AnimateSurface()
        {
            if (!surfaceMesh || !liquid.gameObject.activeSelf) return;
            float t = Time.unscaledTime;
            for (int i = 0; i < surfaceVertices.Length; i++)
            {
                Vector3 v = surfaceBase[i]; float r = new Vector2(v.x, v.z).magnitude;
                float angle = Mathf.Atan2(v.z, v.x);
                v.y = Mathf.Sin(r * 12 - t * 5 + angle * 2 + stirAngle * .03f) * (.004f + rippleEnergy * .019f) * Mathf.Clamp01(1.1f - r);
                v.y -= rippleEnergy * .042f * (1 - r / SurfaceRadius);
                surfaceVertices[i] = v;
            }
            surfaceMesh.vertices = surfaceVertices; surfaceMesh.RecalculateNormals();
            liquid.localPosition = Vector3.up * SurfaceY;
        }
        void PlaceSpoon(Vector3 point, float angle)
        {
            spoon.localPosition = point + Vector3.up * .03f;
            spoon.localRotation = Quaternion.Euler(0, -angle, -28);
        }
        public void BeginReveal()
        {
            CancelPointer(); phase = Phase.Reveal; reveal = 0;
            spoon.gameObject.SetActive(false); guide.gameObject.SetActive(false); flow.gameObject.SetActive(false);
        }
        public void SetRevealProgress(float progress)
        {
            if (phase != Phase.Reveal) return;
            reveal = Mathf.Clamp01(progress);
            float close = Mathf.SmoothStep(0, 1, reveal / .17f);
            float open = Mathf.SmoothStep(0, 1, Mathf.Clamp01((reveal - .26f) / .35f));
            lid.localPosition = Vector3.Lerp(lidRest, new Vector3(0, .91f, 0), close) + Vector3.up * (open * 2.5f);
            lid.localRotation = Quaternion.Slerp(lidRestRotation, Quaternion.identity, close) * Quaternion.Euler(open * -30, 0, Mathf.Sin(reveal * 70) * (1 - open) * 2);
            float shrink = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((reveal - .42f) / .42f));
            basket.localScale = Vector3.one * Mathf.Max(.01f, shrink);
            lid.localScale = Vector3.one * Mathf.Max(.01f, 1 - Mathf.Clamp01((reveal - .65f) / .20f));
            liquid.gameObject.SetActive(reveal < .45f);
            for (int i = 0; i < ripples.Length; i++) ripples[i].gameObject.SetActive(false);
        }
        public void CompleteReveal() { gameObject.SetActive(false); }

        void BuildStation()
        {
            Material bamboo = Mat("Honey bamboo", C("D9A35D"), .28f);
            Material light = Mat("Bamboo golden weave", C("F2CC8B"), .36f);
            Material dark = Mat("Caramel bindings", C("9D653B"), .28f);
            Material mint = Mat("Jade handles", C("64BFA9"), .48f);
            Material pearl = Mat("Ceramic spoon pearl", C("FFF3CF"), .62f);
            basket = Group("Bamboo steamer — hollow woven basket", transform);
            Mesh shell = Lathe("Hollow bamboo vessel", new[] { new Vector2(0, .05f), new Vector2(1.12f, .05f), new Vector2(1.24f, .17f), new Vector2(1.28f, .78f), new Vector2(1.18f, .83f), new Vector2(1.08f, .19f), new Vector2(0, .19f) }, 48);
            Part("Warm woven vessel", basket, shell, Vector3.zero, Vector3.one, bamboo);
            for (int i = 0; i < 40; i++)
            {
                float a = i * Mathf.PI * 2 / 40;
                Transform slat = Part("Bamboo grain", basket, cubeMesh, new Vector3(Mathf.Cos(a) * 1.245f, .46f, Mathf.Sin(a) * 1.245f), new Vector3(.032f, .54f, .025f), i % 4 == 0 ? dark : light);
                slat.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
            }
            Part("Lower binding", basket, ringMesh, new Vector3(0, .18f, 0), new Vector3(1.235f, 1.2f, 1.235f), dark);
            Part("Upper binding", basket, ringMesh, new Vector3(0, .71f, 0), new Vector3(1.282f, 1.2f, 1.282f), dark);
            Part("Rounded golden lip", basket, ringMesh, new Vector3(0, .82f, 0), new Vector3(1.225f, 2.4f, 1.225f), light);
            Part("Inner golden lip", basket, ringMesh, new Vector3(0, .79f, 0), new Vector3(1.16f, .85f, 1.16f), light);
            for (int side = -1; side <= 1; side += 2)
            {
                Transform handle = Part("Jade woven handle", basket, ringMesh, new Vector3(side * 1.32f, .54f, 0), new Vector3(.25f, .8f, .32f), mint);
                handle.localRotation = Quaternion.Euler(0, 0, 65 * side);
                Part("Handle knot", basket, sphereMesh, new Vector3(side * 1.25f, .54f, -.18f), Vector3.one * .14f, light);
            }
            BakeParts(basket);

            lid = Group("Bamboo lid with woven star", transform);
            Part("Domed bamboo cover", lid, sphereMesh, new Vector3(0, .07f, 0), new Vector3(2.46f, .24f, 2.46f), bamboo);
            Part("Lid bound edge", lid, ringMesh, Vector3.zero, new Vector3(1.23f, 2.1f, 1.23f), dark);
            for (int i = -5; i <= 5; i++)
            {
                float p = i * .185f, length = Mathf.Sqrt(1.12f * 1.12f - p * p) * 2;
                Part("Woven horizontal strip", lid, cubeMesh, new Vector3(0, .19f - Mathf.Abs(p) * .04f, p), new Vector3(length, .015f, .027f), light);
                Part("Woven vertical strip", lid, cubeMesh, new Vector3(p, .192f - Mathf.Abs(p) * .04f, 0), new Vector3(.027f, .015f, length), light);
            }
            Part("Lid jade knob", lid, sphereMesh, new Vector3(0, .26f, 0), new Vector3(.34f, .25f, .34f), mint);
            BakeParts(lid);

            mixtureMaterial = Mat("A magical pearly mixture", C("FFE9C7"), .72f);
            streamMaterial = Mat("Poured ingredient", PetCatalog.Colors[0], .75f);
            surfaceMesh = MakeSurface(); surfaceBase = surfaceMesh.vertices; surfaceVertices = new Vector3[surfaceBase.Length];
            liquid = Part("The swirling liquid", transform, surfaceMesh, Vector3.zero, Vector3.one, mixtureMaterial);
            flow = Part("A continuous poured stream", transform, cylinderMesh, Vector3.zero, Vector3.one, streamMaterial);
            spoon = Group("The mixing spoon spins with every movement", transform);
            spoonTip = Part("Spoon bowl", spoon, sphereMesh, Vector3.zero, new Vector3(.28f, .10f, .36f), pearl);
            Part("Spoon long jade handle", spoon, cylinderMesh, new Vector3(.05f, .47f, 0), new Vector3(.10f, .92f, .10f), mint);
            Part("Spoon handle round end", spoon, sphereMesh, new Vector3(.05f, .94f, 0), new Vector3(.15f, .20f, .14f), light);
            BakeParts(spoon);
            Material foam = Mat("Creamy ripple highlights", C("FFEECF"), .6f);
            ripples = new Transform[3];
            for (int i = 0; i < ripples.Length; i++) ripples[i] = Part("Liquid swirl " + i, transform, ringMesh, Vector3.zero, Vector3.one, foam);
            guide = Group("Mixing sparkle pearls", transform); beads = new Transform[12];
            for (int i = 0; i < beads.Length; i++) beads[i] = Part("Mixing progress pearl " + i, guide, sphereMesh, Vector3.zero, Vector3.one * .05f, light);
        }

        Transform BuildIngredient(int category, int index)
        {
            Transform root = Group("Ingredient " + category + "." + index, transform);
            Material cream = Mat("Bottle milk glass", C("FFF4D8"), .68f);
            Material gold = Mat("Ingredient cork", C("C79352"), .35f);
            Color color = category == 0 ? PetCatalog.Colors[index] : PetCatalog.Colors[(category + index + 3) % PetCatalog.Colors.Length];
            Material accent = Mat("Ingredient tint " + category + "." + index, color, .57f);
            Material ink = Mat("Ingredient dark detail", C("423351"), .4f);
            if (category == 0 || category == 7)
            {
                Part("Rounded potion bottle", root, sphereMesh, new Vector3(0, .02f, 0), new Vector3(.46f, .64f, .43f), accent);
                Part("Cream glass neck", root, cylinderMesh, new Vector3(0, .33f, 0), new Vector3(.20f, .20f, .20f), cream);
                Part("Little cork", root, cylinderMesh, new Vector3(0, .455f, 0), new Vector3(.23f, .075f, .23f), gold);
                Part("Round bottle seal", root, sphereMesh, new Vector3(0, .02f, -.21f), new Vector3(.19f, .23f, .027f), cream);
                Part("A glint in the bottle", root, sphereMesh, new Vector3(-.12f, .14f, -.15f), new Vector3(.035f, .18f, .015f), cream);
                if (category == 7)
                    for (int i = 0; i < 5; i++) Part("Texture crystal", root, i % 2 == 0 ? cubeMesh : sphereMesh, new Vector3(Mathf.Sin(i * 2.4f) * .16f, -.14f + i * .065f, -.20f), Vector3.one * .065f, gold);
            }
            else if (category == 1)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 scale = index == 4 ? new Vector3(.27f, .27f, .17f) : index == 5 ? new Vector3(.25f, .45f, .13f) : new Vector3(.22f, index == 1 ? .67f : .44f, .15f);
                    Transform ear = Part("Soft little ear", root, sphereMesh, new Vector3(side * .20f, .1f, 0), scale, accent);
                    ear.localRotation = Quaternion.Euler(0, 0, side * (index == 5 ? 30 : -12));
                    Part("Ear pink center", ear, sphereMesh, new Vector3(0, 0, -.42f), new Vector3(.52f, .66f, .20f), cream);
                }
            }
            else if (category == 2)
            {
                if (index == 1) Part("Fluffy tail puff", root, sphereMesh, Vector3.zero, Vector3.one * .47f, accent);
                else if (index == 6)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Transform fin = Part("Mermaid tail fin", root, sphereMesh, new Vector3(side * .18f, .08f, 0), new Vector3(.29f, .48f, .09f), accent);
                        fin.localRotation = Quaternion.Euler(0, 0, side * -36);
                    }
                }
                else
                {
                    for (int i = 0; i < 7; i++)
                    { float a = i * .5f; Part("Curled tail", root, sphereMesh, new Vector3(Mathf.Sin(a) * .22f, -.20f + i * .065f, 0), Vector3.one * (.22f - i * .012f), accent); }
                }
            }
            else if (category == 3 || category == 4)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    if (category == 4)
                    {
                        Transform lens = Part("Little round glasses", root, ringMesh, new Vector3(side * .20f, 0, 0), new Vector3(.18f, .18f, .18f), accent);
                        lens.localRotation = Quaternion.Euler(90, 0, 0);
                    }
                    else
                    {
                        Part("Twinkling eye", root, sphereMesh, new Vector3(side * .18f, 0, 0), new Vector3(.24f, .33f, .11f), ink);
                        Part("Eye sparkle", root, sphereMesh, new Vector3(side * .18f - .035f, .055f, -.055f), Vector3.one * .075f, cream);
                    }
                }
                if (category == 4) Part("Glasses bridge", root, cubeMesh, Vector3.zero, new Vector3(.16f, .045f, .045f), accent);
            }
            else if (category == 5)
            {
                Part("Hat brim", root, cylinderMesh, new Vector3(0, -.15f, 0), new Vector3(.70f, .08f, .45f), gold);
                if (index == 2)
                {
                    for (int i = -1; i <= 1; i++) Part("Crown point", root, sphereMesh, new Vector3(i * .20f, .07f, 0), new Vector3(.13f, .42f, .16f), gold);
                }
                else if (index == 4)
                {
                    for (int i = 0; i < 5; i++) { float a = i * Mathf.PI * .4f; Part("Flower petal", root, sphereMesh, new Vector3(Mathf.Cos(a) * .18f, Mathf.Sin(a) * .18f + .09f, -.04f), new Vector3(.23f, .23f, .12f), accent); }
                    Part("Flower middle", root, sphereMesh, new Vector3(0, .09f, -.10f), Vector3.one * .19f, gold);
                }
                else Part("Little rounded hat", root, sphereMesh, Vector3.zero, new Vector3(.49f, .48f, .42f), accent);
            }
            else
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Transform wing = Part("Upper butterfly wing", root, sphereMesh, new Vector3(side * .22f, .13f, 0), new Vector3(.36f, .52f, .08f), accent);
                    wing.localRotation = Quaternion.Euler(0, side * 10, side * -22);
                    Part("Lower butterfly wing", root, sphereMesh, new Vector3(side * .20f, -.15f, 0), new Vector3(.28f, .27f, .08f), cream);
                }
                Part("Butterfly jewel", root, sphereMesh, Vector3.zero, new Vector3(.12f, .48f, .12f), gold);
            }
            BakeParts(root); return root;
        }

        static Color C(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out Color c); return c; }
        static void SetColor(Material mat, Color color) { mat.color = color; if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color); }
        Material Mat(string key, Color color, float smooth)
        {
            if (materials.TryGetValue(key, out Material material)) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = key }; SetColor(material, color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smooth);
            materials.Add(key, material); return material;
        }
        Transform Group(string name, Transform parent)
        { Transform t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
        Transform Part(string name, Transform parent, Mesh mesh, Vector3 position, Vector3 scale, Material mat)
        {
            Transform t = Group(name, parent); t.localPosition = position; t.localScale = scale;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = t.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return t;
        }
        void BakeParts(Transform root)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>();
            foreach (MeshFilter f in filters)
            {
                Material mat = f.GetComponent<Renderer>().sharedMaterial;
                if (!groups.ContainsKey(mat)) groups.Add(mat, new List<CombineInstance>());
                groups[mat].Add(new CombineInstance { mesh = f.sharedMesh, transform = root.worldToLocalMatrix * f.transform.localToWorldMatrix });
            }
            foreach (var group in groups)
            {
                Mesh mesh = new Mesh { name = root.name + " / " + group.Key.name };
                mesh.CombineMeshes(group.Value.ToArray(), true, true); mesh.RecalculateBounds(); ownedMeshes.Add(mesh);
                Part("Batched " + group.Key.name, root, mesh, Vector3.zero, Vector3.one, group.Key);
            }
            foreach (MeshFilter f in filters) { f.GetComponent<Renderer>().enabled = false; Destroy(f.gameObject); }
        }
        void PositionCylinder(Transform cylinder, Vector3 a, Vector3 b, float width)
        {
            cylinder.position = (a + b) * .5f;
            cylinder.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
            cylinder.localScale = new Vector3(width, Vector3.Distance(a, b), width);
        }
        Mesh Mesh(string name, Vector3[] vertices, int[] triangles)
        {
            Mesh mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); ownedMeshes.Add(mesh); return mesh;
        }
        Mesh MakeSphere()
        {
            const int columns = 16, rows = 8;
            var vertices = new Vector3[(columns + 1) * (rows + 1)]; var triangles = new List<int>();
            for (int y = 0; y <= rows; y++) for (int x = 0; x <= columns; x++)
            {
                int i = y * (columns + 1) + x; float a = x * Mathf.PI * 2 / columns, b = y * Mathf.PI / rows;
                vertices[i] = new Vector3(Mathf.Cos(a) * Mathf.Sin(b), Mathf.Cos(b), Mathf.Sin(a) * Mathf.Sin(b)) * .5f;
                if (x == columns || y == rows) continue;
                int below = i + columns + 1;
                if (y > 0) triangles.AddRange(new[] { i, i + 1, below });
                if (y < rows - 1) triangles.AddRange(new[] { i + 1, below + 1, below });
            }
            return Mesh("Shared workshop sphere", vertices, triangles.ToArray());
        }
        Mesh MakeCylinder() { return Lathe("Shared workshop cylinder", new[] { new Vector2(0, -.5f), new Vector2(.5f, -.5f), new Vector2(.5f, .5f), new Vector2(0, .5f) }, 24); }
        Mesh MakeCube()
        {
            Vector3[] corners = { new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f), new Vector3(.5f,.5f,-.5f), new Vector3(-.5f,.5f,-.5f), new Vector3(-.5f,-.5f,.5f), new Vector3(.5f,-.5f,.5f), new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f) };
            int[] faces = { 0,3,2,1, 5,6,7,4, 4,7,3,0, 1,2,6,5, 3,7,6,2, 4,0,1,5 };
            var vertices = new Vector3[24]; var triangles = new int[36];
            for (int f = 0; f < 6; f++)
            {
                int j = f * 4, t = f * 6; for (int k = 0; k < 4; k++) vertices[j + k] = corners[faces[j + k]];
                triangles[t] = j; triangles[t + 1] = j + 1; triangles[t + 2] = j + 2; triangles[t + 3] = j; triangles[t + 4] = j + 2; triangles[t + 5] = j + 3;
            }
            return Mesh("Shared workshop cube", vertices, triangles);
        }
        Mesh Lathe(string name, Vector2[] profile, int segments)
        {
            var vertices = new Vector3[(segments + 1) * profile.Length]; var indices = new List<int>();
            for (int p = 0; p < profile.Length; p++) for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments; int v = p * (segments + 1) + i;
                vertices[v] = new Vector3(Mathf.Cos(a) * profile[p].x, profile[p].y, Mathf.Sin(a) * profile[p].x);
                if (p == profile.Length - 1 || i == segments) continue;
                int next = v + segments + 1;
                indices.AddRange(new[] { v, next, v + 1, v + 1, next, next + 1 });
            }
            return Mesh(name, vertices, indices.ToArray());
        }
        Mesh MakeRing(float radius, float tube, int segments, int sides)
        {
            var vertices = new Vector3[(segments + 1) * (sides + 1)]; var indices = new List<int>();
            for (int i = 0; i <= segments; i++) for (int j = 0; j <= sides; j++)
            {
                float a = i * Mathf.PI * 2 / segments, b = j * Mathf.PI * 2 / sides;
                int v = i * (sides + 1) + j; float r = radius + Mathf.Cos(b) * tube;
                vertices[v] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(b) * tube, Mathf.Sin(a) * r);
                if (i == segments || j == sides) continue;
                int next = v + sides + 1; indices.AddRange(new[] { v, v + 1, next, v + 1, next + 1, next });
            }
            return Mesh("Shared bamboo and ripple ring", vertices, indices.ToArray());
        }
        Mesh MakeSurface()
        {
            const int segments = 48;
            var vertices = new Vector3[1 + segments * 3]; var indices = new List<int>();
            for (int ring = 0; ring < 3; ring++) for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments; int v = 1 + ring * segments + i, n = 1 + ring * segments + (i + 1) % segments;
                vertices[v] = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (SurfaceRadius * (ring + 1) / 3);
                if (ring == 0) indices.AddRange(new[] { 0, n, v });
                else { int inner = v - segments, innerNext = n - segments; indices.AddRange(new[] { inner, n, v, inner, innerNext, n }); }
            }
            Mesh surface = Mesh("Living mixture surface", vertices, indices.ToArray()); surface.MarkDynamic(); return surface;
        }
        public void ReleaseResources()
        {
            if (released) return; released = true;
            foreach (Mesh mesh in ownedMeshes) if (mesh) Destroy(mesh);
            foreach (Material material in materials.Values) if (material) Destroy(material);
            ownedMeshes.Clear(); materials.Clear();
        }
        void OnDisable() { CancelPointer(); }
        void OnDestroy() { ReleaseResources(); }
    }
}
