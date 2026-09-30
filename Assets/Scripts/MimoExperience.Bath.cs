using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

namespace MimoGarden
{
    public sealed partial class MimoExperience
    {
        public string BathMode { get; private set; } = "Soap";
        public bool IsBathToolHeld => place == Place.Home && care == Care.Bath && holding && !petGesture;
        public bool IsWaterRunning => bathWaterRoot && bathWaterRoot.gameObject.activeSelf;
        public Vector3 BathToolWorldPosition => tool ? tool.position : Vector3.zero;
        Bounds bathToolPickBounds;
        Mesh bathWaterMesh;
        Transform bathWaterRoot;
        Vector3[] bathWaterVertices;
        Vector3 bathWaterStart, bathWaterEnd;
        float bathNoticeAt;
        bool bathFlow;

        public void SelectBathMode(string mode)
        {
            if (place != Place.Home || care != Care.Bath) return;
            ReleaseTouch(); BathMode = mode == "Rinse" ? "Rinse" : "Soap";
            SetTool(); BuildHomeUI();
            Toast(BathMode == "Soap" ? "Pegue a esponja. Espalhe a espuma por todo o Mimo!" : "Pegue o chuveirinho e leve a água até a espuma.", 4.5f);
        }

        void SetBathTool()
        {
            if (place != Place.Home || care != Care.Bath) return;
            tool = Group(BathMode == "Soap" ? "Bath sponge — pick me up" : "Bath hand shower — pick me up");
            toolRest = new Vector3(-2.05f, BathMode == "Soap" ? .62f : .94f, -1.18f); tool.position = toolRest;
            if (BathMode == "Soap")
            {
                Material butter = Mat("Bath sponge golden", Hex("FFCE45"), .12f), pore = Mat("Bath sponge pores", Hex("D99825"), .08f), foam = Mat("Bath fresh foam", Hex("F2FCFF"), .52f);
                Pill("Rounded squishy sponge", tool, Vector3.zero, new Vector3(.73f, .34f, .49f), butter);
                for (int i = 0; i < 6; i++)
                    Pill("Little sponge pore", tool, new Vector3(-.24f + i * .09f, -.02f + (i % 2) * .08f, -.225f), new Vector3(.047f, .035f, .016f), pore);
                for (int i = 0; i < 3; i++)
                    Pill("Soap on the sponge", tool, new Vector3(-.21f + i * .15f, .17f + (i % 2) * .04f, -.035f), Vector3.one * (.16f + i * .018f), foam);
                bathToolPickBounds = new Bounds(new Vector3(0, .05f, 0), new Vector3(.87f, .63f, .64f));
            }
            else
            {
                Material teal = Mat("Bath shower turquoise", Hex("12BFC5"), .42f), white = Mat("Bath shower pearl face", Hex("EAFBFF"), .65f), blue = Mat("Bath shower nozzles", Hex("3484B4"), .32f);
                Pill("Shower comfortable handle", tool, new Vector3(0, -.17f, .01f), new Vector3(.17f, .67f, .18f), teal).localRotation = Quaternion.Euler(0, 0, -8);
                Pill("Round shower head", tool, new Vector3(.025f, .28f, .01f), new Vector3(.57f, .57f, .18f), teal);
                Pill("Pearl spray plate", tool, new Vector3(.025f, .28f, -.087f), new Vector3(.47f, .47f, .065f), white);
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.PI * 2f / 5f;
                    Pill("Shower spray nozzle", tool, new Vector3(.025f + Mathf.Cos(a) * .125f, .28f + Mathf.Sin(a) * .125f, -.124f), new Vector3(.042f, .042f, .022f), blue);
                }
                Transform hose = Group("Curly soft shower hose", tool);
                LineRenderer line = hose.gameObject.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.sharedMaterial = teal;
                line.positionCount = 8; line.SetPositions(new[] { new Vector3(-.04f, -.45f, .01f), new Vector3(-.1f, -.64f, .03f), new Vector3(.08f, -.76f, .09f), new Vector3(.4f, -.71f, .11f), new Vector3(.52f, -.5f, .12f), new Vector3(.46f, -.3f, .15f), new Vector3(.35f, -.24f, .17f), new Vector3(.32f, -.37f, .18f) });
                line.startWidth = line.endWidth = .055f; line.numCapVertices = 3; line.numCornerVertices = 3;
                line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
                bathToolPickBounds = new Bounds(new Vector3(.09f, -.07f, 0), new Vector3(.86f, 1.3f, .58f));
            }
            EnsureBathWater(); ResetBathTools();
        }

        void BuildBathHomeUI()
        {
            SidebarStart("HomeBath"); BuildFriendPicker();
            CreateRailScroll(159, 184, 490);
            PictureCard("Ensaboar", 0, 2, 4, Hex("FFF0CD"), () => SelectBathMode("Soap"), BathMode == "Soap", 130);
            PictureCard("Enxaguar", 1, 2, 4, Hex("E2F6FA"), () => SelectBathMode("Rinse"), BathMode == "Rinse", 130);
            ScrollRailLabel(BathMode == "Soap" ? "Pegue a esponjinha e espalhe a espuma." : "Pegue o chuveirinho e lave a espuma.", 148, 68, 18);
            Button turn = RailButton("Virar o Mimo", 226, Hex("ECE2FB"), () =>
            {
                ResetPetInteraction(); actorRoot.Rotate(0, 180, 0, Space.World);
                Toast("O outro ladinho também quer carinho!", 3f);
            }, 68);
            PictureButton(turn, "Virar o Mimo", false, 64);
            PictureHands(326);
            RailAction("Brincar na lama", MainCareAction); PictureNavigation();
            FinishSidebarScroll();
        }

        public bool BeginBathToolGesture(Vector2 screenPosition)
        {
            if (IsFriendDialogOpen || place != Place.Home || care != Care.Bath || !tool || IsGestureOverUI(screenPosition)) return false;
            Ray worldRay = sceneCamera.ScreenPointToRay(screenPosition);
            Ray localRay = new Ray(tool.InverseTransformPoint(worldRay.origin), tool.InverseTransformDirection(worldRay.direction));
            if (!bathToolPickBounds.IntersectRay(localRay, out float distance) || distance < 0) return false;
            ResetPetInteraction();
            holding = true; draggingFood = false; gesturePosition = screenPosition;
            return true;
        }

        public void DragBathToolGesture(Vector2 screenPosition, float deltaTime)
        {
            if (!IsBathToolHeld || !tool) return;
            gesturePosition = screenPosition;
            if (IsGestureOverUI(screenPosition)) { StopBathToolFlow(); return; }
            Ray ray = sceneCamera.ScreenPointToRay(screenPosition);
            Plane plane = new Plane(Vector3.forward, new Vector3(0, 0, actorRoot.position.z - 1.05f));
            if (plane.Raycast(ray, out float distance)) tool.position = ray.GetPoint(distance);
            bool hitsPet = HitPet(screenPosition, out RaycastHit hit);
            float dt = Mathf.Min(.075f, Mathf.Max(0, deltaTime));
            if (BathMode == "Soap")
            {
                StopBathToolFlow();
                if (hitsPet)
                {
                    tool.position = hit.point + hit.normal * .15f;
                    tool.rotation = Quaternion.LookRotation(-hit.normal, Vector3.up) * Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 17f) * 15f);
                    actor.SoapAt(hit, dt);
                    if (actor.SoapCoverage > .45f && Time.time > bathNoticeAt)
                    { bathNoticeAt = Time.time + 12f; Toast("Quanta espuma! Vire o Mimo ou escolha Enxaguar.", 3.5f); }
                }
                else tool.rotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 4f) * 5f);
            }
            else
            {
                tool.rotation = Quaternion.Euler(-18, 0, -14);
                if (hitsPet) tool.position = hit.point + new Vector3(.1f, .67f, -.2f);
                bathWaterStart = tool.TransformPoint(new Vector3(.025f, .25f, -.14f));
                bathWaterEnd = hitsPet ? hit.point + hit.normal * .018f : bathWaterStart + Vector3.down * Mathf.Min(1.15f, Mathf.Max(.18f, bathWaterStart.y - .12f));
                bathFlow = true; bathWaterRoot.gameObject.SetActive(true);
                if (hitsPet)
                {
                    actor.RinseAt(hit, dt);
                    if (actor.SoapCoverage < .012f && actor.Data.cleanliness > .96f && Time.time > bathNoticeAt)
                    { bathNoticeAt = Time.time + 10f; actor.SetExpression("love"); Toast("Limpinho! Um abraço cheiroso para você.", 3f); }
                }
            }
        }

        public void EndBathToolGesture()
        {
            if (care == Care.Bath) ResetPetInteraction();
        }

        void StopBathToolFlow()
        {
            bathFlow = false; if (bathWaterRoot) bathWaterRoot.gameObject.SetActive(false);
        }

        void ResetBathTools(bool clearSoap = false)
        {
            StopBathToolFlow();
            if (clearSoap && actor) actor.ClearSoap();
            if (care == Care.Bath && tool) { tool.position = toolRest; tool.rotation = Quaternion.identity; }
        }

        void EnsureBathWater()
        {
            if (bathWaterMesh) return;
            const int dashes = 20;
            bathWaterVertices = new Vector3[dashes * 4]; var normals = new Vector3[bathWaterVertices.Length]; var triangles = new int[dashes * 6];
            for (int i = 0; i < dashes; i++)
            {
                int v = i * 4, t = i * 6; normals[v] = normals[v + 1] = normals[v + 2] = normals[v + 3] = Vector3.back;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1; triangles[t + 3] = v; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }
            bathWaterMesh = new Mesh { name = "Flowing shower water — one mesh", vertices = bathWaterVertices, normals = normals, triangles = triangles }; bathWaterMesh.MarkDynamic();
            bathWaterRoot = Group("Visible falling bath water"); bathWaterRoot.gameObject.AddComponent<MeshFilter>().sharedMesh = bathWaterMesh;
            MeshRenderer renderer = bathWaterRoot.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = Mat("Bath running water", Hex("70DFFF"), .55f);
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            bathWaterRoot.gameObject.SetActive(false);
        }

        void TickBathTools()
        {
            if (!bathFlow || !bathWaterMesh) return;
            if (!IsBathToolHeld || place != Place.Home || care != Care.Bath) { StopBathToolFlow(); return; }
            Vector3 right = sceneCamera.transform.right;
            for (int i = 0; i < 20; i++)
            {
                int jet = i % 5, dash = i / 5; float phase = Mathf.Repeat(Time.time * 1.9f + dash * .25f + jet * .065f, 1f);
                Vector3 spread = right * (jet - 2) * .045f;
                Vector3 start = Vector3.Lerp(bathWaterStart + spread, bathWaterEnd + spread * 1.35f, phase);
                Vector3 end = Vector3.Lerp(bathWaterStart + spread, bathWaterEnd + spread * 1.35f, Mathf.Min(1, phase + .14f));
                Vector3 width = right * (.012f + (jet % 2) * .004f); int v = i * 4;
                bathWaterVertices[v] = start - width; bathWaterVertices[v + 1] = end - width; bathWaterVertices[v + 2] = end + width; bathWaterVertices[v + 3] = start + width;
            }
            bathWaterMesh.vertices = bathWaterVertices;
            Bounds bounds = new Bounds(bathWaterStart, Vector3.one * .4f); bounds.Encapsulate(bathWaterEnd); bathWaterMesh.bounds = bounds;
        }

        void ReleaseBathTools()
        {
            StopBathToolFlow();
            if (bathWaterRoot) Destroy(bathWaterRoot.gameObject); if (bathWaterMesh) Destroy(bathWaterMesh);
            bathWaterRoot = null; bathWaterMesh = null; bathWaterVertices = null;
        }
    }
}
