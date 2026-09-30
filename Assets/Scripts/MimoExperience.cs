using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MimoGarden
{
    /// <summary>A complete, offline first-play experience. Everything here is real 3D except the touch UI.</summary>
    public sealed partial class MimoExperience : MonoBehaviour
    {
        enum Place { Laboratory, Birth, Naming, Home }
        enum Care { Food, Bath, Play }

        readonly Color ink = Hex("233052"), muted = Hex("526585"), cream = Hex("FFF8E8");
        readonly Color purple = Hex("6542D7"), palePurple = Hex("D9D4FF");
        readonly Color[] colors = PetCatalog.Colors;
        readonly string[] colorNames = PetCatalog.ColorNames;
        readonly List<Floaty> floaties = new List<Floaty>();
        readonly List<Image> needBars = new List<Image>();

        Camera sceneCamera;
        UniversalAdditionalCameraData finishCameraData;
        Canvas canvas;
        RectTransform safeArea, header, tray, topRight, toast;
        Text title, subtitle, toastText;
        Font font;
        Sprite rounded, circle;
        Material brewMaterial;
        Transform studioShell, laboratory, home, bowl, brew, brewParts, actorRoot, tool, ball;
        PetActor actor;
        PetSave saved, draft;
        InputField nameInput;
        Place place;
        Care care = Care.Play;
        int step;
        float toastUntil, saveAt, nextBubble, nextAutoSpark;
        bool holding, draggingFood;
        Vector2 pointerStart;
        float pointerTime;
        Vector3 toolRest;
        Coroutine revealRoutine, snackRoutine;
        Vector2 lastScreen;
        bool wasPaused;
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        readonly Dictionary<float, Mesh> starMeshes = new Dictionary<float, Mesh>();
        int activeEffects;

        public PetActor Actor { get { return actor; } }
        public string CurrentScreen { get { return place.ToString(); } }
        public int CurrentRecipeStep { get { return step; } }

        struct Floaty
        {
            public Transform transform;
            public Vector3 origin;
            public float phase;
        }

        void Awake()
        {
            MimoPerformance.Configure(gameObject);
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            font = Resources.Load<Font>("UI/Nunito");
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rounded = MakeSprite(false);
            circle = MakeSprite(true);
            BuildCameraAndLight();
            BuildWorld();
            BuildCareEnvironments();
            BuildCanvas();
            actorRoot = new GameObject("Mimo — our little friend").transform;
            actor = actorRoot.gameObject.AddComponent<PetActor>();
            actor.StateChanged += RefreshNeeds;
            LoadPlayground();
            if (saved.created) EnterHome();
            else EnterLaboratory();
            if (MimoPerformance.Lightweight) OptimizeMobileWorld();
            saveAt = Time.unscaledTime + 12f;
        }

        void OnDestroy()
        {
            SaveCare();
            foreach (PetActor pet in playgroundPets) if (pet) pet.StateChanged -= RefreshNeeds;
            if (creationActor) creationActor.StateChanged -= RefreshNeeds;
            ReleaseCreationRitual();
            ReleaseBathTools();
            ReleaseMobileWorld();
            foreach (Mesh mesh in starMeshes.Values) if (mesh) Destroy(mesh);
            foreach (Material material in materials.Values) if (material) Destroy(material);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                if (IsFriendDialogOpen) CancelFriendDialog();
                ReleaseTouch(); CancelCreationRitual(); ResetBathTools(); CancelSnack(); SaveCare(); wasPaused = true;
            }
            else if (wasPaused)
            {
                wasPaused = false;
                if (place == Place.Home && saved != null && saved.created && actor != null)
                {
                    foreach (PetActor pet in playgroundPets)
                    {
                        float cleanlinessBeforeAbsence = pet.Data.cleanliness;
                        PetStorage.ApplyOfflineTime(pet.Data, DateTime.UtcNow.Ticks);
                        float addedDirt = Mathf.Max(0, cleanlinessBeforeAbsence - pet.Data.cleanliness);
                        if (addedDirt > 0) pet.AddDirt(addedDirt);
                    }
                    RefreshNeeds();
                }
            }
        }
        void OnApplicationQuit() { SaveCare(); }

        void Update()
        {
            if (IsFriendDialogOpen && Input.GetKeyDown(KeyCode.Escape)) CancelFriendDialog();
            if (lastScreen.x != Screen.width || lastScreen.y != Screen.height || Mathf.Abs(lastCanvasScale - canvas.scaleFactor) > .001f || lastSafeArea != Screen.safeArea) ApplySafeArea();
            float t = Time.time;
            if (place == Place.Laboratory) UpdateCreationRitual();
            TickBathTools();
            TickPlayground();
            foreach (Floaty f in floaties)
            {
                if (!f.transform || !f.transform.gameObject.activeInHierarchy) continue;
                f.transform.localPosition = f.origin + Vector3.up * (Mathf.Sin(t * 1.1f + f.phase) * .055f);
                f.transform.Rotate(0, Time.deltaTime * 12f, 0, Space.Self);
            }
            if (ball && place == Place.Home) ball.localRotation = Quaternion.Euler(0, t * 5f, Mathf.Sin(t * .65f) * 3f);
            if (toast && toast.gameObject.activeSelf && Time.unscaledTime > toastUntil) toast.gameObject.SetActive(false);
            if (Time.unscaledTime >= saveAt) { SaveCare(); saveAt = Time.unscaledTime + 12f; }
            if (place == Place.Home) HandleTouch();
            else if (place == Place.Laboratory) HandleCreationTouch();
            if (place == Place.Home && Time.time > nextAutoSpark)
            {
                nextAutoSpark = Time.time + 4.5f;
                SpawnSpark(actorRoot.position + new Vector3(UnityEngine.Random.Range(-.85f, .85f), 1.9f, -.25f), colors[Mathf.Clamp(saved.colorIndex, 0, colors.Length - 1)], .8f, .055f);
            }
        }

        void LateUpdate()
        {
            // Only the two clear finishes need the scene-color copy. Solid recipes keep the original mobile budget.
            bool refract = actor && actor.gameObject.activeInHierarchy && actor.IsTranslucentFinish;
            foreach (PetActor pet in playgroundPets) if (pet.gameObject.activeInHierarchy && pet.IsTranslucentFinish) refract = true;
            if (finishCameraData) finishCameraData.requiresColorOption = refract ? CameraOverrideOption.On : CameraOverrideOption.Off;
            if (actor) actor.SetSceneRefraction(refract);
            foreach (PetActor pet in playgroundPets) pet.SetSceneRefraction(refract && pet.gameObject.activeInHierarchy);
            UpdateSelectionHalo();
        }

        static Color Hex(string value) { Color c; ColorUtility.TryParseHtmlString("#" + value, out c); return c; }
        PetSave Clone(PetSave source) { return JsonUtility.FromJson<PetSave>(JsonUtility.ToJson(source)); }

        void BuildCameraAndLight()
        {
            foreach (Camera existing in FindObjectsByType<Camera>(FindObjectsSortMode.None)) existing.gameObject.SetActive(false);
            sceneCamera = new GameObject("Mimo camera — landscape composition").AddComponent<Camera>();
            sceneCamera.tag = "MainCamera";
            sceneCamera.transform.position = new Vector3(0, 3.75f, -9.5f);
            sceneCamera.transform.LookAt(new Vector3(0, 1.35f, .2f));
            sceneCamera.fieldOfView = 39f;
            sceneCamera.nearClipPlane = .1f;
            sceneCamera.farClipPlane = 45f;
            sceneCamera.backgroundColor = Hex("C3E9F2");
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.allowHDR = !MimoPerformance.Lightweight;
            sceneCamera.allowMSAA = !MimoPerformance.Lightweight;
            UniversalAdditionalCameraData cameraData = sceneCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            finishCameraData = cameraData;
            cameraData.renderPostProcessing = !MimoPerformance.Lightweight;
            cameraData.antialiasing = MimoPerformance.Lightweight ? AntialiasingMode.None : AntialiasingMode.FastApproximateAntialiasing;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;
            if (!MimoPerformance.Lightweight)
            {
            Volume grading = new GameObject("Soft afternoon color grade").AddComponent<Volume>();
            grading.isGlobal = true; grading.priority = 1;
            grading.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Tonemapping tonemapping = grading.profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);
            ColorAdjustments colorGrade = grading.profile.Add<ColorAdjustments>(true);
            colorGrade.postExposure.Override(.05f);
            colorGrade.contrast.Override(8f);
            colorGrade.saturation.Override(12f);
            }
            sceneCamera.gameObject.AddComponent<AudioListener>();
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("C5BDCE");
            RenderSettings.ambientEquatorColor = Hex("B0A2B5");
            RenderSettings.ambientGroundColor = Hex("8C7E88");
            RenderSettings.ambientIntensity = .55f;
            Light key = new GameObject("Warm afternoon key").AddComponent<Light>();
            key.type = LightType.Directional; key.color = Hex("FFF2DF"); key.intensity = MimoPerformance.Lightweight ? 1f : .85f;
            key.transform.rotation = Quaternion.Euler(47, -38, 0);
            key.shadows = MimoPerformance.Lightweight ? LightShadows.None : LightShadows.Soft; key.shadowStrength = .4f; key.shadowBias = .04f;
            if (MimoPerformance.Lightweight) return;
            Light fill = new GameObject("Lilac bounce").AddComponent<Light>();
            fill.type = LightType.Directional; fill.color = Hex("E5DDFF"); fill.intensity = .23f;
            fill.transform.rotation = Quaternion.Euler(30, 135, 0);
            fill.shadows = LightShadows.None;
            Light rim = new GameObject("Pearl rim light").AddComponent<Light>();
            rim.type = LightType.Directional; rim.color = Hex("FFF8E5"); rim.intensity = .2f;
            rim.transform.rotation = Quaternion.Euler(30, 20, 0); rim.shadows = LightShadows.None;
        }

        Material Mat(string key, Color color, float smooth = .35f, float metallic = 0)
        {
            Material material;
            if (materials.TryGetValue(key, out material)) return material;
            Shader shader = Shader.Find(MimoPerformance.Lightweight ? "Universal Render Pipeline/Simple Lit" : "Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            material = new Material(shader) { name = key, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smooth);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smooth);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            materials.Add(key, material); return material;
        }

        Transform Group(string name, Transform parent = null)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false); return t;
        }

        Transform Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject o = GameObject.CreatePrimitive(type); o.name = name;
            o.transform.SetParent(parent, false); o.transform.localPosition = position; o.transform.localScale = scale;
            o.GetComponent<Renderer>().sharedMaterial = material;
            if (MimoPerformance.Lightweight)
            {
                if (type == PrimitiveType.Sphere) o.GetComponent<MeshFilter>().sharedMesh = GetMobileSphereMesh();
                o.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                o.GetComponent<Renderer>().receiveShadows = false;
            }
            Collider collider = o.GetComponent<Collider>(); if (collider) Destroy(collider);
            return o.transform;
        }

        Transform Pill(string name, Transform parent, Vector3 p, Vector3 s, Material m)
        { return Shape(name, PrimitiveType.Sphere, parent, p, s, m); }
        Transform Box(string name, Transform parent, Vector3 p, Vector3 s, Material m)
        { return Shape(name, PrimitiveType.Cube, parent, p, s, m); }
        Transform Disk(string name, Transform parent, Vector3 p, Vector3 s, Material m)
        { return Shape(name, PrimitiveType.Cylinder, parent, p, s, m); }

        void BuildWorld()
        {
            Material floor = Mat("Warm ivory", Hex("E4E8F0"), .25f);
            Material wall = Mat("Vanilla plaster", Hex("26ABC0"), .18f);
            Transform room = studioShell = Group("The shared sunlit studio");
            Box("Endless soft floor", room, new Vector3(0, -.13f, .8f), new Vector3(22, .2f, 20), floor);
            Box("Back wall", room, new Vector3(0, 3, 4.25f), new Vector3(17, 8, .25f), wall);
            Arch("Peach alcove", room, new Vector3(0, .01f, 4.08f), 7.5f, 5.4f, Mat("Alcove peach", Hex("7351CB")));
            Box("Skirting", room, new Vector3(0, .15f, 4f), new Vector3(16, .24f, .16f), Mat("Skirting cream", Hex("FFDC55")));
            MakeWindow(room);

            laboratory = Group("01 — a little laboratory");
            Material wood = Mat("Milk oak", Hex("E29B36"), .35f);
            Material lavender = Mat("Ceramic lilac", Hex("8151E8"), .5f);
            Material pearl = Mat("Pearl ceramic", Hex("FFF7DA"), .7f, .08f);
            Disk("Pedestal shadow trim", laboratory, new Vector3(0, .035f, .22f), new Vector3(4.3f, .045f, 3.5f), Mat("Rosy pedestal edge", Hex("6542D7")));
            Disk("Round creation table", laboratory, new Vector3(0, .15f, .22f), new Vector3(4.18f, .13f, 3.42f), pearl);
            Disk("Concentric inlay", laboratory, new Vector3(0, .289f, .22f), new Vector3(3.48f, .008f, 2.89f), Mat("Pale lilac inlay", Hex("CED7F8"), .55f));
            bowl = Group("Mixing bowl", laboratory);
            bowl.localPosition = new Vector3(0, .3f, 0);
            MakeBowl(bowl, pearl);
            brewMaterial = Mat("A living pearl mixture", colors[0], .75f, .14f);
            brew = Pill("The mixture", laboratory, new Vector3(0, .78f, 0), new Vector3(1.86f, .72f, 1.62f), brewMaterial);
            brewParts = Group("Recipe buds", laboratory);
            Disk("Small jar stand left", laboratory, new Vector3(-2.63f, .57f, 1.9f), new Vector3(1.6f, .06f, 1.25f), wood);
            Disk("Small jar stand right", laboratory, new Vector3(2.63f, .57f, 1.9f), new Vector3(1.6f, .06f, 1.25f), wood);
            for (int side = -1; side <= 1; side += 2)
                for (int j = 0; j < 3; j++)
                    Pill("Rounded table leg", laboratory, new Vector3(side * 2.63f + (j - 1) * .4f, .26f, 1.9f + (j % 2) * .27f), new Vector3(.16f, .65f, .16f), wood);
            MakeJar(laboratory, new Vector3(-2.96f, .67f, 1.9f), .9f, colors[4], 0);
            MakeJar(laboratory, new Vector3(-2.28f, .67f, 1.96f), 1.17f, colors[3], 1);
            MakeJar(laboratory, new Vector3(2.42f, .67f, 1.94f), 1f, colors[7], 2);
            MakeJar(laboratory, new Vector3(3.03f, .67f, 1.97f), .74f, Hex("F3DBA2"), 3);
            Box("Floating shelf", laboratory, new Vector3(2.36f, 2.49f, 3.75f), new Vector3(2.45f, .11f, .5f), wood);
            MakeJar(laboratory, new Vector3(1.72f, 2.56f, 3.7f), .7f, colors[1], 4);
            MakeJar(laboratory, new Vector3(2.35f, 2.56f, 3.7f), .92f, colors[5], 5);
            Star("A wish on the shelf", laboratory, new Vector3(3.02f, 2.88f, 3.64f), .31f, Mat("Golden wishes", Hex("E8C579"), .58f));
            MakePlant(laboratory, new Vector3(-3.8f, 0, 2.9f), .73f);
            FloatStar(laboratory, new Vector3(-1.48f, 2.1f, .18f), .14f, 1f);
            FloatStar(laboratory, new Vector3(1.28f, 1.74f, .45f), .12f, 3f);
            FloatStar(laboratory, new Vector3(.6f, 2.77f, 1.1f), .09f, 5f);

            home = Group("02 — a home full of little things");
            Material rugEdge = Mat("Sage rug rim", Hex("B5CDBD"), .05f);
            Material rug = Mat("Cloud rug", Hex("E5ECDA"), .05f);
            Disk("Sage rug border", home, new Vector3(0, .012f, .1f), new Vector3(5.4f, .035f, 3.9f), rugEdge);
            Disk("Soft oval rug", home, new Vector3(0, .061f, .1f), new Vector3(5.14f, .03f, 3.62f), rug);
            for (int i = 0; i < 13; i++)
            {
                float a = Mathf.PI * 2f * i / 13;
                Pill("Rug stitching", home, new Vector3(Mathf.Cos(a) * 2.45f, .096f, .1f + Mathf.Sin(a) * 1.71f), new Vector3(.035f, .016f, .14f), Mat("Rug stitching cream", Hex("FCF7E9")));
            }
            MakeSofa(home);
            MakePlant(home, new Vector3(-3.11f, 0, 1.8f), 1.2f);
            Disk("Tiny round bedside table", home, new Vector3(-2.26f, .69f, 2.75f), new Vector3(1.12f, .07f, 1.12f), wood);
            Disk("Bedside table stem", home, new Vector3(-2.26f, .32f, 2.75f), new Vector3(.21f, .32f, .21f), wood);
            Disk("Bedside table foot", home, new Vector3(-2.26f, .045f, 2.75f), new Vector3(.65f, .045f, .65f), wood);
            Pill("Cloud lamp", home, new Vector3(-2.26f, .98f, 2.73f), new Vector3(.59f, .47f, .47f), Mat("Cloud lamp shade", Hex("FFF5CD"), .5f));
            Disk("Lamp foot", home, new Vector3(-2.26f, .77f, 2.75f), new Vector3(.33f, .045f, .33f), lavender);
            ball = Group("The little striped ball", home); ball.localPosition = new Vector3(2.09f, .31f, -.6f);
            Pill("Lavender play ball", ball, Vector3.zero, Vector3.one * .56f, lavender);
            Pill("Peach patch", ball, new Vector3(-.055f, .03f, -.045f), new Vector3(.58f, .25f, .54f), Mat("Ball peach stripe", Hex("F0CDBD"), .44f)).localRotation = Quaternion.Euler(22, 12, 19);
            MakeBook(home, new Vector3(3.38f, .06f, .98f), new Vector3(.7f, .1f, .51f), Hex("CEBDDA"), -10);
            MakeBook(home, new Vector3(3.32f, .18f, .96f), new Vector3(.66f, .1f, .51f), Hex("EBCAAB"), 9);
            Star("Wall wishing star", home, new Vector3(2.56f, 3.46f, 3.81f), .3f, Mat("Wall star", Hex("E1B67C"), .4f));
            Pill("Wall moon", home, new Vector3(1.8f, 3.77f, 3.93f), new Vector3(.44f, .44f, .06f), Mat("Wall moon cream", Hex("FFF0D3")));
            home.gameObject.SetActive(false);
        }

        void MakeWindow(Transform room)
        {
            Material rim = Mat("Window frame", Hex("FFF7E5"), .3f);
            Arch("Arched window frame", room, new Vector3(-2.32f, 1.38f, 3.88f), 1.98f, 2.42f, rim);
            Arch("Morning through the window", room, new Vector3(-2.32f, 1.49f, 3.84f), 1.72f, 2.16f, Mat("Morning blue", Hex("C7DFDF"), .2f));
            Box("Window central bar", room, new Vector3(-2.32f, 2.47f, 3.78f), new Vector3(.075f, 2.03f, .1f), rim);
            Box("Window crossbar", room, new Vector3(-2.32f, 2.51f, 3.77f), new Vector3(1.76f, .075f, .1f), rim);
            Box("Window sill", room, new Vector3(-2.32f, 1.43f, 3.69f), new Vector3(2.13f, .1f, .49f), rim);
            Pill("Distant round cloud", room, new Vector3(-2.75f, 3.04f, 3.81f), new Vector3(.47f, .19f, .025f), Mat("Cloud white", Hex("F5F7EF")));
            Pill("Distant cloud puff", room, new Vector3(-2.62f, 3.11f, 3.806f), new Vector3(.22f, .22f, .025f), Mat("Cloud white", Hex("F5F7EF")));
        }

        void MakeSofa(Transform parent)
        {
            Transform sofa = Group("A marshmallow sofa", parent); sofa.localPosition = new Vector3(2.71f, 0, 2.32f); sofa.localRotation = Quaternion.Euler(0, -13, 0);
            Material fabric = Mat("Soft apricot fabric", Hex("E5BFAE"), .1f), cushion = Mat("Lilac cushion", Hex("C7B5D5"), .1f);
            Material wood = Mat("Milk oak", Hex("E29B36"));
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Pill("Sofa wood foot", sofa, new Vector3(x * .68f, .17f, z * .36f), new Vector3(.17f, .33f, .17f), wood);
            Pill("Rounded sofa base", sofa, new Vector3(0, .48f, 0), new Vector3(2.15f, .56f, 1.15f), fabric);
            Pill("Cushy sofa back", sofa, new Vector3(0, 1.02f, .36f), new Vector3(2.1f, 1.08f, .4f), fabric);
            Pill("Left sofa arm", sofa, new Vector3(-.88f, .77f, -.01f), new Vector3(.35f, .58f, 1.0f), fabric);
            Pill("Right sofa arm", sofa, new Vector3(.88f, .77f, -.01f), new Vector3(.35f, .58f, 1.0f), fabric);
            Pill("Sofa seat", sofa, new Vector3(0, .74f, -.13f), new Vector3(1.63f, .28f, .84f), Mat("Sofa cream seat", Hex("F5DFCA"), .08f));
            Pill("One sleepy cushion", sofa, new Vector3(.34f, 1.05f, .035f), new Vector3(.6f, .55f, .24f), cushion).localRotation = Quaternion.Euler(0, 0, -13);
        }

        void MakePlant(Transform parent, Vector3 position, float scale)
        {
            Transform plant = Group("Happy little plant", parent); plant.localPosition = position; plant.localScale = Vector3.one * scale;
            Material pot = Mat("Peach plant pot", Hex("DDB49E"), .4f), leaf = Mat("Sage leaves", Hex("92B5A0"), .25f), leaf2 = Mat("Mint leaves", Hex("AFCCAE"), .25f);
            Disk("Ceramic pot", plant, new Vector3(0, .24f, 0), new Vector3(.69f, .24f, .69f), pot);
            Disk("Pot lip", plant, new Vector3(0, .46f, 0), new Vector3(.75f, .055f, .75f), pot);
            Disk("Soil", plant, new Vector3(0, .497f, 0), new Vector3(.57f, .012f, .57f), Mat("Pot soil", Hex("907D73")));
            for (int i = 0; i < 7; i++)
            {
                float a = i * 2.399f;
                Vector3 center = new Vector3(Mathf.Sin(a) * .28f, .95f + (i % 3) * .14f, Mathf.Cos(a) * .27f);
                Pill("Plump leaf", plant, center, new Vector3(.23f, .91f - (i % 2) * .13f, .13f), i % 2 == 0 ? leaf : leaf2).localRotation = Quaternion.Euler(Mathf.Cos(a) * 34, a * Mathf.Rad2Deg, Mathf.Sin(a) * 32);
            }
        }

        void MakeBook(Transform parent, Vector3 p, Vector3 s, Color c, float angle)
        {
            Transform book = Group("A book of lovely things", parent); book.localPosition = p; book.localRotation = Quaternion.Euler(0, angle, 0);
            Box("Book pages", book, Vector3.zero, s, Mat("Book pages", Hex("FFF7E7")));
            Material cover = Mat("Book cover " + c, c);
            Box("Book cover top", book, Vector3.up * s.y * .58f, new Vector3(s.x + .04f, .026f, s.z + .03f), cover);
            Box("Book cover bottom", book, Vector3.down * s.y * .58f, new Vector3(s.x + .04f, .026f, s.z + .03f), cover);
        }

        void MakeJar(Transform parent, Vector3 p, float s, Color color, int index)
        {
            Transform jar = Group("Ingredient jar " + index, parent); jar.localPosition = p; jar.localScale = Vector3.one * s;
            Material liquid = Mat("Ingredient color " + index, color, .65f, .08f), ceramic = Mat("Jar pearl", Hex("FDF0E5"), .65f);
            Pill("Milk glass bottle", jar, new Vector3(0, .37f, 0), new Vector3(.61f, .78f, .56f), liquid);
            Disk("Bottle neck", jar, new Vector3(0, .71f, 0), new Vector3(.25f, .105f, .25f), ceramic);
            Pill("Round cork", jar, new Vector3(0, .84f, 0), new Vector3(.33f, .2f, .33f), Mat("Maple cork", Hex("CCAC89"), .25f));
            Pill("Cream jar seal", jar, new Vector3(0, .4f, -.259f), new Vector3(.23f, .25f, .055f), ceramic);
            Star("Seal star", jar, new Vector3(0, .4f, -.294f), .08f, Mat("Jar seal plum", Hex("A28AB4")));
            Pill("Bottle glint", jar, new Vector3(-.13f, .49f, -.218f), new Vector3(.055f, .24f, .028f), Mat("Bottle glow", Hex("FFF9EC"), .7f));
        }

        void MakeBowl(Transform parent, Material material)
        {
            const int slices = 64;
            Vector2[] section = { new Vector2(.3f, .015f), new Vector2(.56f, .055f), new Vector2(.79f, .2f), new Vector2(.96f, .43f), new Vector2(1.04f, .62f), new Vector2(.98f, .66f), new Vector2(.91f, .43f), new Vector2(.72f, .24f), new Vector2(.45f, .16f), new Vector2(.3f, .15f) };
            Vector3[] vertices = new Vector3[slices * section.Length];
            var triangles = new List<int>();
            for (int j = 0; j < section.Length; j++)
                for (int i = 0; i < slices; i++)
                {
                    float a = 2f * Mathf.PI * i / slices;
                    vertices[j * slices + i] = new Vector3(Mathf.Cos(a) * section[j].x, section[j].y, Mathf.Sin(a) * section[j].x);
                    if (j == section.Length - 1) continue;
                    int a0 = j * slices + i, a1 = j * slices + (i + 1) % slices, b0 = a0 + slices, b1 = a1 + slices;
                    triangles.Add(a0); triangles.Add(b0); triangles.Add(a1); triangles.Add(a1); triangles.Add(b0); triangles.Add(b1);
                }
            Mesh mesh = new Mesh { name = "A real rounded mixing bowl", vertices = vertices, triangles = triangles.ToArray() }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            GameObject go = new GameObject("Handmade bowl"); go.transform.SetParent(parent, false); go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material;
            Disk("Bowl base", parent, new Vector3(0, .06f, 0), new Vector3(.88f, .075f, .88f), material);
            for (int i = 0; i < 7; i++)
            {
                float a = Mathf.PI * (.16f + .68f * i / 6);
                Pill("Little bowl dot", parent, new Vector3(Mathf.Cos(a) * .948f, .445f, -Mathf.Sin(a) * .948f), Vector3.one * .06f, Mat("Bowl lilac dots", Hex("BBA4CC"), .4f));
            }
        }

        void Arch(string name, Transform parent, Vector3 p, float width, float height, Material material)
        {
            var points = new List<Vector3> { new Vector3(-width / 2, 0, 0), new Vector3(width / 2, 0, 0) };
            for (int i = 0; i <= 32; i++)
            {
                float a = Mathf.PI * i / 32;
                points.Add(new Vector3(Mathf.Cos(a) * width / 2, height - width / 2 + Mathf.Sin(a) * width / 2, 0));
            }
            var indices = new List<int>();
            for (int i = 1; i < points.Count - 1; i++) { indices.Add(0); indices.Add(i + 1); indices.Add(i); }
            Mesh mesh = new Mesh { name = name, vertices = points.ToArray(), triangles = indices.ToArray() }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            GameObject o = new GameObject(name); o.transform.SetParent(parent, false); o.transform.localPosition = p;
            o.AddComponent<MeshFilter>().sharedMesh = mesh; o.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        Transform Star(string name, Transform parent, Vector3 position, float radius, Material material)
        {
            GameObject o = new GameObject(name); o.transform.SetParent(parent, false); o.transform.localPosition = position;
            Mesh mesh;
            if (!starMeshes.TryGetValue(radius, out mesh))
            {
            Vector3[] vertices = new Vector3[22];
            for (int face = 0; face < 2; face++)
            {
                vertices[face * 11] = new Vector3(0, 0, face == 0 ? -.025f : .025f);
                for (int i = 0; i < 10; i++)
                {
                    float a = Mathf.PI / 2 + i * Mathf.PI / 5, r = radius * (i % 2 == 0 ? 1 : .47f);
                    vertices[face * 11 + i + 1] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, vertices[face * 11].z);
                }
            }
            var indices = new List<int>();
            for (int i = 0; i < 10; i++)
            {
                int a = i + 1, b = (i + 1) % 10 + 1;
                indices.AddRange(new[] { 0, b, a, 11, a + 11, b + 11, a, b, b + 11, a, b + 11, a + 11 });
            }
            mesh = new Mesh { name = "Little solid star", vertices = vertices, triangles = indices.ToArray() }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            starMeshes.Add(radius, mesh);
            }
            o.AddComponent<MeshFilter>().sharedMesh = mesh; o.AddComponent<MeshRenderer>().sharedMaterial = material; return o.transform;
        }

        void FloatStar(Transform parent, Vector3 position, float radius, float phase)
        {
            Transform t = Star("A floating wish", parent, position, radius, Mat("Floating wish gold", Hex("DCC087"), .65f));
            floaties.Add(new Floaty { transform = t, origin = position, phase = phase });
        }

        void BuildCanvas()
        {
            if (!FindFirstObjectByType<EventSystem>())
            {
                GameObject events = new GameObject("Touch and pointer events"); events.AddComponent<EventSystem>(); events.AddComponent<StandaloneInputModule>();
            }
            GameObject root = new GameObject("Mimo UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = .5f;
            safeArea = Rect("Safe landscape area", root.transform); Stretch(safeArea);
            header = Rect("Heading", safeArea); SetRect(header, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -30), new Vector2(590, 138));
            Text brand = Label("MIMO  /  PEQUENOS ENCANTOS", header, 13, ink, FontStyle.Bold); SetRect(brand.rectTransform, Vector2.up, Vector2.up, Vector2.up, Vector2.zero, new Vector2(540, 24));
            title = Label("", header, 36, ink, FontStyle.Bold); SetRect(title.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(0, -32), new Vector2(670, 52));
            title.resizeTextForBestFit = true; title.resizeTextMinSize = 23; title.resizeTextMaxSize = 36;
            subtitle = Label("", header, 17, ink); SetRect(subtitle.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(2, -87), new Vector2(640, 38));
            topRight = Rect("Context", safeArea); SetRect(topRight, Vector2.one, Vector2.one, Vector2.one, new Vector2(-34, -32), new Vector2(434, 118));
            tray = Rect("Touch tray", safeArea); tray.anchorMin = new Vector2(0, 0); tray.anchorMax = new Vector2(1, 0); tray.pivot = new Vector2(.5f, 0); tray.offsetMin = new Vector2(28, 22); tray.offsetMax = new Vector2(-28, 226);
            Image trayImage = tray.gameObject.AddComponent<Image>(); trayImage.sprite = rounded; trayImage.type = UnityEngine.UI.Image.Type.Sliced; trayImage.color = new Color(1f, .99f, .968f, .98f);
            Shadow shadow = tray.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(.29f, .21f, .36f, .07f); shadow.effectDistance = new Vector2(0, -5);
            toast = Rect("A little whisper", safeArea); SetRect(toast, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -170), new Vector2(540, 43));
            Image toastBG = toast.gameObject.AddComponent<Image>(); toastBG.sprite = rounded; toastBG.type = UnityEngine.UI.Image.Type.Sliced; toastBG.color = new Color(1, .99f, .96f, .91f); toastBG.raycastTarget = false;
            toastText = Label("", toast, 17, ink); Stretch(toastText.rectTransform, 16, 6); toastText.alignment = TextAnchor.MiddleCenter;
            toast.gameObject.SetActive(false); BuildSidebarChrome(); ApplySafeArea();
        }

        void ApplySafeArea()
        {
            lastScreen = new Vector2(Screen.width, Screen.height);
            Rect area = Screen.safeArea;
            if (Screen.width < 1 || Screen.height < 1) return;
            safeArea.anchorMin = new Vector2(area.x / Screen.width, area.y / Screen.height);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            float aspect = (float)Screen.width / Screen.height;
            sceneCamera.fieldOfView = aspect < 1.45f ? 43f : 39f;
            LayoutSidebar();
        }

        RectTransform Rect(string name, Transform parent)
        {
            RectTransform r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false); return r;
        }

        void SetRect(RectTransform r, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
        { r.anchorMin = min; r.anchorMax = max; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size; }
        void Stretch(RectTransform r, float horizontal = 0, float vertical = 0)
        { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(horizontal, vertical); r.offsetMax = new Vector2(-horizontal, -vertical); }

        Text Label(string text, Transform parent, int size, Color color, FontStyle style = FontStyle.Normal)
        {
            Text label = Rect(text.Length > 40 ? text.Substring(0, 40) : text, parent).gameObject.AddComponent<Text>();
            label.font = font; label.text = text; label.fontSize = size; label.fontStyle = style; label.color = color;
            label.alignment = TextAnchor.MiddleLeft; label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate; label.raycastTarget = false; return label;
        }

        Image Image(string name, Transform parent, Color c, bool round = false)
        {
            Image image = Rect(name, parent).gameObject.AddComponent<Image>(); image.sprite = round ? circle : rounded; image.type = round ? UnityEngine.UI.Image.Type.Simple : UnityEngine.UI.Image.Type.Sliced; image.color = c; return image;
        }

        Button Button(string text, Transform parent, Color bg, Color fg, Action action, int fontSize = 18)
        {
            Image image = Image(text, parent, bg); Button b = image.gameObject.AddComponent<Button>(); b.targetGraphic = image;
            ColorBlock block = b.colors; block.normalColor = Color.white; block.highlightedColor = new Color(1, 1, 1, 1); block.pressedColor = new Color(.91f, .87f, .95f, 1); block.selectedColor = Color.white; block.fadeDuration = .12f; b.colors = block;
            b.onClick.AddListener(() => action());
            Text label = Label(text, image.transform, fontSize, fg, FontStyle.Bold); Stretch(label.rectTransform, 8, 5); label.alignment = TextAnchor.MiddleCenter;
            return b;
        }

        void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) { GameObject child = parent.GetChild(i).gameObject; child.SetActive(false); Destroy(child); }
        }

        Sprite MakeSprite(bool isCircle)
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false); tex.name = isCircle ? "UI circle" : "UI rounded corner"; tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - 31.5f), dy = Mathf.Abs(y - 31.5f);
                float distance = isCircle ? Mathf.Sqrt(dx * dx + dy * dy) - 30.5f : new Vector2(Mathf.Max(dx - 12f, 0), Mathf.Max(dy - 12f, 0)).magnitude - 19f;
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(.75f - distance));
            }
            tex.SetPixels(pixels); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, isCircle ? Vector4.zero : new Vector4(25, 25, 25, 25));
        }

        public void EnterLaboratory()
        {
            ShowLaboratory(false);
        }

        void FrameCreationTable(float revealToFriend = 0f)
        {
            sceneCamera.fieldOfView = 39f;
            float blend = Mathf.Clamp01(revealToFriend);
            sceneCamera.transform.position = Vector3.Lerp(new Vector3(0, 5.8f, -7.8f), new Vector3(0, 3.75f, -9.5f), blend);
            sceneCamera.transform.LookAt(Vector3.Lerp(new Vector3(0, .85f, .15f), new Vector3(0, 1.35f, .2f), blend));
        }

        void ShowLaboratory(bool keepRecipe)
        {
            if (IsFriendDialogOpen) CloseFriendDialog();
            SaveCare();
            CancelSnack();
            ReleaseTouch();
            ResetBathTools();
            PrepareCreationActor();
            if (revealRoutine != null) { StopCoroutine(revealRoutine); revealRoutine = null; }
            if (!keepRecipe || draft == null)
            {
                step = 0;
                draft = new PetSave { colorIndex = 0, created = false, cleanliness = 1f, hunger = .9f, joy = 1f };
            }
            place = Place.Laboratory;
            FrameCreationTable();
            laboratory.gameObject.SetActive(true); ShowCareEnvironment("Laboratory");
            actorRoot.gameObject.SetActive(false); actorRoot.localRotation = Quaternion.identity; actorRoot.localScale = Vector3.one; actorRoot.localPosition = new Vector3(0, .29f, 0);
            bowl.gameObject.SetActive(false); brew.gameObject.SetActive(false); brewParts.gameObject.SetActive(false);
            if (tool) Destroy(tool.gameObject);
            title.text = "Uma receita. Uma surpresa.";
            subtitle.text = "Arraste os ingredientes. Mexa em círculos. Descubra quem vai nascer.";
            ResetCreationRitual(keepRecipe); BuildRecipeUI();
            Toast("Arraste uma garrafinha para a vaporeira para começar.", 5f);
        }

        public void BeginBirth()
        {
            if (place != Place.Laboratory || revealRoutine != null) return;
            if (!CanFinalizeRecipe) { Toast("Coloque uma cor e mexa a receita antes de finalizar.", 3f); return; }
            try
            {
                // The character is configured out of sight; the reveal is the first preview.
                actor.ClearSoap();
                actor.Initialize(draft);
            }
            catch (Exception error)
            {
                RecoverBirth(error);
                return;
            }
            BeginCreationReveal();
            place = Place.Birth; Clear(tray); Clear(topRight);
            tray.gameObject.SetActive(false); toast.gameObject.SetActive(false);
            title.text = "Está nascendo um encanto..."; subtitle.text = "Da sua imaginação, para o seu abraço.";
            actorRoot.localPosition = new Vector3(0, .29f, 0); actorRoot.localRotation = Quaternion.identity; actorRoot.localScale = Vector3.one * .015f;
            actorRoot.gameObject.SetActive(false);
            revealRoutine = StartCoroutine(Birth());
        }

        IEnumerator Birth()
        {
            // Let StartCoroutine assign its handle before completion or recovery can clear it.
            yield return null;
            IEnumerator reveal = AnimateBirth();
            while (true)
            {
                bool hasNext = false;
                object next = null;
                Exception failure = null;
                try
                {
                    hasNext = reveal.MoveNext();
                    if (hasNext) next = reveal.Current;
                }
                catch (Exception error) { failure = error; }
                if (failure != null) { RecoverBirth(failure); yield break; }
                if (!hasNext) { revealRoutine = null; yield break; }
                yield return next;
            }
        }

        void RecoverBirth(Exception error)
        {
            Debug.LogError("Não foi possível criar o Mimo. Voltando aos ingredientes sem perder a receita.\n" + error, this);
            revealRoutine = null;
            ShowLaboratory(true);
            Toast("A magia tropeçou. Sua receita está aqui para tentar de novo.", 5f);
        }

        IEnumerator AnimateBirth()
        {
            for (float elapsed = 0; elapsed < 3.1f; elapsed += Time.unscaledDeltaTime)
            {
                float p = elapsed / 3.1f;
                FrameCreationTable(Mathf.SmoothStep(0, 1, Mathf.Clamp01((p - .3f) / .6f)));
                SetCreationRevealProgress(p);
                actorRoot.gameObject.SetActive(p >= .4f);
                float born = Mathf.SmoothStep(0, 1, Mathf.Clamp01((p - .4f) / .5f));
                float bounce = born >= .99f ? Mathf.Sin((p - .85f) * 27f) * .018f : 0;
                actorRoot.localScale = Vector3.one * Mathf.Max(.015f, born + bounce);
                if (UnityEngine.Random.value < Time.deltaTime * (MimoPerformance.Lightweight ? 10 : 30)) SpawnSpark(new Vector3(UnityEngine.Random.Range(-1.35f, 1.35f), UnityEngine.Random.Range(.9f, 2.5f), UnityEngine.Random.Range(-.9f, .6f)), colors[draft.colorIndex], 1.1f, .07f);
                yield return null;
            }
            CompleteCreationReveal();
            FrameCreationTable(1f);
            actorRoot.localScale = Vector3.one; bowl.gameObject.SetActive(false); brew.gameObject.SetActive(false); brewParts.gameObject.SetActive(false); brewParts.localScale = Vector3.one;
            actor.Pet(); Burst(actorRoot.position + Vector3.up * 2.1f, 18, Hex("E3C87D"), .075f);
            BuildNamingUI();
        }

        public void ConfirmName()
        {
            if (place != Place.Naming || IsFriendDialogOpen) return;
            string chosen = nameInput ? nameInput.text.Trim() : "Mimo";
            draft.petName = string.IsNullOrWhiteSpace(chosen) ? "Mimo" : chosen;
            draft.created = true;
            if (playgroundPets.Count >= PetStorage.MaxPets) { ShowReplacementChooser(); return; }
            if (!CommitCreation(draft)) return;
            EnterHome();
            actor.Pet(); Toast("Bem-vindo a casa, " + saved.petName + ".", 5f);
        }

        public void EnterHome()
        {
            if (IsFriendDialogOpen) CloseFriendDialog();
            CancelSnack();
            if (revealRoutine != null) { StopCoroutine(revealRoutine); revealRoutine = null; }
            CancelCreationRitual(true); ResetBathTools();
            ReleaseTouch(); place = Place.Home;
            if (playgroundPets.Count == 0) { EnterLaboratory(); return; }
            actor = playgroundPets.Find(p => p.Data.petId == collection.activePetId) ?? playgroundPets[playgroundPets.Count - 1];
            actorRoot = actor.transform; saved = actor.Data;
            laboratory.gameObject.SetActive(false); ShowCareEnvironment("Play");
            care = Care.Play; ShowPlaygroundFriends(); UpdateFriendHeading();
            BuildHomeUI(); RefreshNeeds(); SetTool(); Toast("Toque para fazer carinho. Arraste para passear pelo jardim.", 5.5f);
        }

        void RefreshNeeds()
        {
            if (place != Place.Home || actor == null || actor.Data == null || needBars.Count != 3) return;
            float[] values = { actor.Data.hunger, actor.Data.cleanliness, actor.Data.joy };
            for (int i = 0; i < 3; i++)
                if (needBars[i]) needBars[i].rectTransform.anchorMax = new Vector2(Mathf.Max(.06f, Mathf.Clamp01(values[i])), 1);
        }

        void SelectCare(Care selected)
        {
            if (IsFriendDialogOpen) CloseFriendDialog();
            ReleaseTouch(); CancelSnack(); RememberGardenPositions();
            if (selected != Care.Play) StopPetAction(actor);
            care = selected;
            ShowPlaygroundFriends();
            ShowCareEnvironment(care.ToString());
            title.text = care == Care.Food ? "Hora do nhac!" : care == Care.Bath ? "Um banho de carinho." : "Vamos brincar, " + saved.petName + "?";
            UpdateFriendHeading();
            BuildHomeUI(); SetTool();
            if (care == Care.Bath) Toast("Passe a esponja para cobrir de espuma. Depois, enxágue.", 4.5f);
            else if (care == Care.Food) Toast("Uma frutinha doce para sua pequena criação.", 4f);
            else Toast("Toque para fazer carinho. Arraste para passear pelo jardim.", 4f);
        }

        public void MainCareAction()
        {
            if (place != Place.Home) return;
            if (care == Care.Food) FeedSnack();
            else if (care == Care.Bath)
            {
                actor.AddDirt(.7f); actor.Jump(); Burst(actorRoot.position + new Vector3(0, .45f, -.2f), 12, Hex("B09B84"), .08f); Toast("Opa, uma aventura na lama! Hora do banho.", 4f);
            }
            else { actor.Jump(); Burst(actorRoot.position + new Vector3(0, .35f, -.2f), 12, Hex("DCC38D"), .065f); Toast("Mais alto, " + saved.petName + "!", 3f); }
            RefreshNeeds();
        }

        void SetTool()
        {
            if (tool) { Destroy(tool.gameObject); tool = null; }
            ResetBathTools();
            if (care == Care.Play || place != Place.Home) return;
            if (care == Care.Bath) { SetBathTool(); return; }
            tool = Group(care == Care.Food ? "A strawberry to share" : "The little washing sponge");
            toolRest = new Vector3(-1.75f, .55f, -1.5f); tool.position = toolRest;
            if (care == Care.Food)
            {
                Pill("Sweet strawberry", tool, Vector3.zero, new Vector3(.4f, .45f, .38f), Mat("Strawberry pink", Hex("E59CAB"), .4f));
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.PI * .4f;
                    Pill("Strawberry leaf", tool, new Vector3(Mathf.Cos(a) * .08f, .2f, Mathf.Sin(a) * .08f), new Vector3(.1f, .07f, .25f), Mat("Strawberry leaves", Hex("93B897"))).localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
                }
                for (int i = 0; i < 5; i++)
                    Pill("Strawberry seed", tool, new Vector3(Mathf.Sin(i * 1.8f) * .115f, .1f - (i % 3) * .09f, -.173f), new Vector3(.026f, .038f, .016f), Mat("Strawberry seeds", Hex("FFE9C5")));
            }
            else
            {
                Pill("Squishy yellow sponge", tool, Vector3.zero, new Vector3(.54f, .27f, .37f), Mat("Sponge butter", Hex("EDD79D"), .08f));
                for (int i = 0; i < 5; i++)
                    Pill("Sponge little pore", tool, new Vector3(-.16f + i * .078f, .01f + (i % 2) * .056f, -.172f), Vector3.one * .033f, Mat("Sponge pore", Hex("C7B17C"), .05f));
                Pill("Sponge foam", tool, new Vector3(-.19f, .105f, -.055f), Vector3.one * .17f, Mat("White foam", Hex("FFFEF5"), .65f));
                Pill("Sponge foam small", tool, new Vector3(-.08f, .115f, -.01f), Vector3.one * .12f, Mat("White foam", Hex("FFFEF5"), .65f));
            }
        }

        bool PointerOverUI()
        {
            if (EventSystem.current == null) return false;
            if (Input.touchCount > 0) return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            return EventSystem.current.IsPointerOverGameObject();
        }

        bool HitPet(Vector2 position, out RaycastHit hit)
        {
            Ray ray = sceneCamera.ScreenPointToRay(position);
            if (Physics.Raycast(ray, out hit, 30f)) return hit.collider.GetComponentInParent<PetActor>() != null;
            return false;
        }

        void FeedSnack()
        {
            if (snackRoutine != null) return;
            snackRoutine = StartCoroutine(Snack());
        }

        void CancelSnack()
        {
            if (snackRoutine != null) { StopCoroutine(snackRoutine); snackRoutine = null; }
            if (tool) { tool.localScale = Vector3.one; tool.rotation = Quaternion.identity; }
        }

        IEnumerator Snack()
        {
            if (!tool) SetTool();
            Transform fruit = tool;
            Vector3 start = fruit ? fruit.position : new Vector3(-1.5f, .6f, -1.4f);
            Vector3 destination = actorRoot.TransformPoint(new Vector3(0, 1.05f, -.86f));
            actor.SetExpression("hungry");
            for (float t = 0; t < .62f; t += Time.deltaTime)
            {
                if (fruit) { float p = t / .62f; fruit.position = Vector3.Lerp(start, destination, Mathf.SmoothStep(0, 1, p)) + Vector3.up * Mathf.Sin(p * Mathf.PI) * .52f; fruit.Rotate(0, 0, Time.deltaTime * 115); fruit.localScale = Vector3.one * (1 - .76f * Mathf.Clamp01((p - .65f) / .35f)); }
                yield return null;
            }
            actor.Feed(); Burst(destination, 9, Hex("EBCDA3"), .045f); Toast("Nhac! Barriguinha feliz.", 3f);
            if (fruit) { fruit.position = toolRest; fruit.rotation = Quaternion.identity; fruit.localScale = Vector3.one; }
            snackRoutine = null; RefreshNeeds(); SaveCare();
        }

        void SpawnBubble(Vector3 position)
        {
            if (MimoPerformance.Lightweight && activeEffects >= 12) return;
            Transform bubble = Pill("A soap bubble", null, position, Vector3.one * UnityEngine.Random.Range(.045f, .12f), Mat("Soap pearl", Hex("EBF6F2"), .95f, .18f));
            StartCoroutine(FloatAndFade(bubble, UnityEngine.Random.insideUnitSphere * .15f + Vector3.up * .43f, .8f));
        }

        void Burst(Vector3 origin, int count, Color color, float scale)
        {
            if (MimoPerformance.Lightweight) count = Mathf.Min(count, 6);
            for (int i = 0; i < count; i++)
                SpawnSpark(origin + UnityEngine.Random.insideUnitSphere * .5f, color, UnityEngine.Random.Range(.65f, 1.2f), scale);
        }

        void SpawnSpark(Vector3 position, Color color, float duration, float scale)
        {
            if (MimoPerformance.Lightweight && activeEffects >= 12) return;
            Material m = Mat("Spark " + ColorUtility.ToHtmlStringRGB(color), color, .65f, .12f);
            Transform spark = Star("A little burst of delight", null, position, scale, m);
            StartCoroutine(FloatAndFade(spark, UnityEngine.Random.insideUnitSphere * .5f + Vector3.up * .7f, duration));
        }

        IEnumerator FloatAndFade(Transform obj, Vector3 drift, float duration)
        {
            activeEffects++;
            Renderer effectRenderer = obj.GetComponent<Renderer>();
            if (effectRenderer) { effectRenderer.shadowCastingMode = ShadowCastingMode.Off; effectRenderer.receiveShadows = false; }
            Vector3 start = obj.position, scale = obj.localScale;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                if (!obj) { activeEffects--; yield break; }
                float p = t / duration; obj.position = start + drift * p; obj.localScale = scale * Mathf.Sin(Mathf.PI * Mathf.Clamp01(p)); obj.Rotate(0, Time.deltaTime * 70, Time.deltaTime * 27); yield return null;
            }
            if (obj) Destroy(obj.gameObject);
            activeEffects--;
        }

        void Toast(string text, float seconds)
        {
            toastText.text = text; toast.gameObject.SetActive(true); toastUntil = Time.unscaledTime + seconds;
        }

        void SaveCare()
        {
            if (saved != null && saved.created && place == Place.Home && actor != null && actor.Data != null)
            {
                saved = actor.Data;
                collection.activePetId = saved.petId;
                collection.pets.Clear();
                foreach (PetActor pet in playgroundPets) if (pet && pet.Data.created) collection.pets.Add(pet.Data);
                PetStorage.SaveCollection(collection);
            }
        }

        /// <summary>Deterministic, observable smoke path for the editor test harness. No production startup shortcut.</summary>
        public IEnumerator RunSmokeTest()
        {
            EnterLaboratory(); yield return null;
            yield return MimoRitualCheck.AddAndStir(this, 0, 1);
            yield return MimoRitualCheck.AddAndStir(this, 1, 1);
            yield return MimoRitualCheck.AddAndStir(this, 2, 1);
            BeginBirth();
            while (place == Place.Birth) yield return null;
            if (nameInput) nameInput.text = "Mimo";
            ConfirmName(); yield return null;
            SelectCare(Care.Food); MainCareAction(); yield return new WaitForSeconds(.8f);
            SelectCare(Care.Bath); MainCareAction(); yield return null;
            SelectCare(Care.Play); actor.Pet(); actor.Jump(); yield return new WaitForSeconds(.6f);
            RefreshNeeds(); SaveCare();
        }
    }
}
