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
        public Vector3 BathWaterWorldStart => bathWaterStart;
        public Vector3 BathWaterWorldEnd => bathWaterEnd;
        Bounds bathToolPickBounds;
        MimoShowerWater showerWater;
        Transform bathWaterRoot;
        Vector3 bathWaterStart, bathWaterEnd;
        float bathNoticeAt;
        bool bathFlow;

        public void SelectBathMode(string mode)
        {
            if (place != Place.Home || care != Care.Bath) return;
            ReleaseTouch(); BathMode = mode == "Rinse" ? "Rinse" : "Soap";
            SetTool(); BuildHomeUI();
            Toast(BathMode == "Soap" ? "Pegue a esponja. Espalhe as bolhas pelo Mimo!" : "Segure o cabo comprido e leve a chuva até o Mimo.", 4.5f);
        }

        void SetBathTool()
        {
            if (place != Place.Home || care != Care.Bath) return;
            tool = Group(BathMode == "Soap" ? "Bath sponge — pick me up" : "Bath hand shower — pick me up");
            toolRest = new Vector3(BathMode == "Soap" ? -2.05f : -2.7f, BathMode == "Soap" ? .62f : .55f, -1.18f); tool.position = toolRest;
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
                PlayBeam("Long shower handle — grip here", tool, new Vector3(0,-.22f,0), new Vector3(.85f,.96f,.01f), .24f, teal);
                Pill("Soft handle grip", tool, Vector3.zero, new Vector3(.34f,.57f,.32f), teal).localRotation = Quaternion.Euler(0,0,-36);
                for (int i = 0; i < 4; i++) Pill("Grip rib", tool, new Vector3(-.10f+i*.065f,-.14f+i*.085f,-.12f), new Vector3(.30f,.034f,.07f), white).localRotation=Quaternion.Euler(0,0,-36);
                Pill("Large rain shower head", tool, new Vector3(1.05f,1.12f,0), new Vector3(1.03f,.48f,.72f), teal);
                Pill("Wide downward spray plate", tool, new Vector3(1.05f,.94f,-.045f), new Vector3(.88f,.12f,.61f), white);
                for (int i = 0; i < 18; i++)
                {
                    float a = i * Mathf.PI * 2f / 18f;
                    Pill("Rain shower nozzle", tool, new Vector3(1.05f+Mathf.Cos(a)*.34f,.885f,-.045f+Mathf.Sin(a)*.20f), new Vector3(.052f,.018f,.052f), blue);
                }
                Transform hose = Group("Curly soft shower hose", tool);
                LineRenderer line = hose.gameObject.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.sharedMaterial = teal;
                line.positionCount = 8; line.SetPositions(new[] { new Vector3(-.14f,-.20f,.01f),new Vector3(-.30f,-.50f,.04f),new Vector3(-.22f,-.66f,.08f),new Vector3(.16f,-.69f,.10f),new Vector3(.53f,-.59f,.12f),new Vector3(.70f,-.39f,.14f),new Vector3(.62f,-.21f,.15f),new Vector3(.44f,-.18f,.17f) });
                line.startWidth = line.endWidth = .08f; line.numCapVertices = 5; line.numCornerVertices = 5;
                line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
                bathToolPickBounds = new Bounds(new Vector3(.55f,.36f,0), new Vector3(1.98f,1.9f,.88f));
            }
            EnsureBathWater(); ResetBathTools();
        }

        void BuildBathHomeUI()
        {
            SidebarStart("HomeBath"); BuildFriendPicker();
            CreateRailScroll(159, 184, 490);
            PictureCard("Ensaboar", 0, 2, 4, Hex("FFF0CD"), () => SelectBathMode("Soap"), BathMode == "Soap", 130);
            PictureCard("Enxaguar", 1, 2, 4, Hex("E2F6FA"), () => SelectBathMode("Rinse"), BathMode == "Rinse", 130);
            ScrollRailLabel(BathMode == "Soap" ? "Passe a esponja e cubra o Mimo de bolhas." : "Segure o cabo. A chuva lava uma área bem grande.", 148, 68, 18);
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
                tool.rotation = Quaternion.identity;
                bathWaterStart = tool.TransformPoint(new Vector3(1.05f,.88f,-.045f));
                Vector2 waterAim = sceneCamera.WorldToScreenPoint(bathWaterStart + Vector3.down * .90f);
                hitsPet = FindBroadRinseHit(waterAim, out hit);
                bathWaterEnd = hitsPet ? hit.point + hit.normal * .04f : bathWaterStart + Vector3.down * Mathf.Min(1.55f, Mathf.Max(.30f,bathWaterStart.y-.12f));
                bathFlow = true; bathWaterRoot.gameObject.SetActive(true);
                if (hitsPet)
                {
                    actor.RinseAt(hit, dt);
                    if (actor.SoapCoverage < .012f && actor.Data.cleanliness > .96f && Time.time > bathNoticeAt)
                    { bathNoticeAt = Time.time + 10f; actor.SetExpression("love"); Toast("Limpinho! Um abraço cheiroso para você.", 3f); }
                }
            }
        }

        bool FindBroadRinseHit(Vector2 aim, out RaycastHit hit)
        {
            if (HitPet(aim,out hit)) return true;
            Bounds bounds = actor.VisibleBounds;
            Vector2 center = sceneCamera.WorldToScreenPoint(bounds.center);
            Vector2 edge = sceneCamera.WorldToScreenPoint(bounds.center + sceneCamera.transform.right*bounds.extents.x + Vector3.up*bounds.extents.y);
            Vector2 extents = new Vector2(Mathf.Abs(edge.x-center.x),Mathf.Abs(edge.y-center.y));
            if (Mathf.Abs(aim.x-center.x) > extents.x*1.7f || Mathf.Abs(aim.y-center.y) > extents.y*1.7f) return false;
            Vector2 inside = new Vector2(Mathf.Clamp(aim.x,center.x-extents.x*.60f,center.x+extents.x*.60f),Mathf.Clamp(aim.y,center.y-extents.y*.60f,center.y+extents.y*.60f));
            return HitPet(inside,out hit) || HitPet(center,out hit);
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
            if (showerWater) return;
            bathWaterRoot = Group("Abundant clear shower water");
            showerWater = bathWaterRoot.gameObject.AddComponent<MimoShowerWater>(); showerWater.Initialize();
            bathWaterRoot.gameObject.SetActive(false);
        }

        void TickBathTools()
        {
            if (!bathFlow || !showerWater) return;
            if (!IsBathToolHeld || place != Place.Home || care != Care.Bath) { StopBathToolFlow(); return; }
            showerWater.Animate(bathWaterStart,bathWaterEnd,sceneCamera);
        }

        void ReleaseBathTools()
        {
            StopBathToolFlow();
            if (bathWaterRoot) Destroy(bathWaterRoot.gameObject);
            bathWaterRoot = null; showerWater = null;
        }
    }
}
