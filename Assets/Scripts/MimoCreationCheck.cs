using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MimoGarden
{
    /// <summary>Opt-in catalogue/appearance validation in Home. Ritual UI has its own regression run.</summary>
    public sealed class MimoCreationCheck : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public string[] checks, errors, screenshots, geometry;
            public string unityVersion, isolatedSavePath;
            public int width, height;
            public float elapsedSeconds;
        }

        static string outputDirectory, isolatedSavePath;
        static bool fixtureSaved;
        readonly List<string> checks = new List<string>(), errors = new List<string>(), screenshots = new List<string>(), geometry = new List<string>();
        readonly Dictionary<int, HashSet<string>> signatures = new Dictionary<int, HashSet<string>>();
        MimoExperience experience;
        PetActor actor;
        Camera sceneCamera;
        float started;
        bool finished;
        string fairySignature, butterflySignature;
        static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "--mimo-creation-test") >= 0;

        static PetSave NeutralPet() => new PetSave {
            created = true, petName = "Mimo catálogo", colorIndex = 0, cleanliness = 1f,
            hunger = .9f, joy = 1f, recipeSeed = 7359
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void IsolateSaveBeforeAwake()
        {
            if (!Enabled) return;
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--mimo-test-output");
            outputDirectory = index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1])
                : Path.Combine(Application.temporaryCachePath, "MimoCreationValidation");
            isolatedSavePath = Path.Combine(outputDirectory, "isolated-catalogue-pet-" + DateTime.UtcNow.Ticks + ".json");
            PetStorage.SavePathOverride = isolatedSavePath;
            // Start in Home with a disposable character. No creation-preview assumptions or UI shortcuts.
            fixtureSaved = PetStorage.Save(NeutralPet());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Enabled) new GameObject("Mimo opt-in catalogue and appearance verification").AddComponent<MimoCreationCheck>();
        }

        void Awake() { Application.runInBackground = true; started = Time.realtimeSinceStartup; Application.logMessageReceived += OnLog; }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
        void OnLog(string message, string stack, LogType kind)
        {
            if ((kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) && !errors.Contains(message)) errors.Add(message);
        }
        void Check(bool success, string message) { if (success) checks.Add(message); else errors.Add(message); }
        void Update()
        {
            if (!finished && Time.realtimeSinceStartup - started > 150f)
            { errors.Add("Catalogue appearance validation timed out after 150 seconds."); Finish(); }
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(outputDirectory);
            yield return new WaitForSecondsRealtime(1.2f);
            experience = FindAnyObjectByType<MimoExperience>(); sceneCamera = Camera.main;
            actor = experience ? experience.Actor : null;
            Check(fixtureSaved && PetStorage.SavePath == isolatedSavePath, "The disposable Home fixture was isolated before scene startup.");
            Check(Screen.width > Screen.height, "Catalogue appearance runs in tablet landscape orientation.");
            Check(experience && actor && sceneCamera && experience.CurrentScreen == "Home", "The appearance fixture starts in Home with a real 3D character.");
            if (!actor || !sceneCamera || !experience) { Finish(); yield break; }
            TestSerializationAndMigration();
            int rendererCount = actor.GetComponentsInChildren<Renderer>(true).Length;
            HashSet<Mesh> originalMeshes = ActorMeshes();

            for (int category = 0; category < PetCatalog.CategoryCount; category++)
            {
                signatures[category] = new HashSet<string>();
                for (int option = 0; option < PetCatalog.Options(category).Length; option++)
                {
                    PetSave recipe = NeutralPet();
                    PetCatalog.Set(recipe, category, option);
                    actor.Initialize(recipe);
                    yield return null;
                    string label = PetCatalog.CategoryNames[category] + " / " + PetCatalog.Options(category)[option];
                    Check(SameRecipe(actor.Data, recipe) && actor.Data.created, "The complete persisted recipe renders correctly: " + label);
                    Check(actor.FinishMaterialsReady, "The pastel surface shader is supported: " + label);
                    CheckSelectedAppearance(category, option, label);
                    Bounds bounds = actor.VisibleBounds;
                    Check(Finite(bounds.center) && Finite(bounds.size) && bounds.size.y > .5f,
                        "Selected geometry has finite, nonempty neutral bounds: " + label);
                    if (category == 7) CheckWashableFinish(label);
                }
            }
            for (int category = 0; category < PetCatalog.CategoryCount; category++)
                Check(signatures[category].Count == PetCatalog.Options(category).Length,
                    PetCatalog.CategoryNames[category] + " has distinct actual material/geometry configurations for every option.");
            Check(!string.IsNullOrEmpty(fairySignature) && !string.IsNullOrEmpty(butterflySignature) && fairySignature != butterflySignature,
                "Fairy and butterfly wings differ in actual mesh geometry, beyond their names and colors.");
            Check(actor.GetComponentsInChildren<Renderer>(true).Length == rendererCount && originalMeshes.SetEquals(ActorMeshes()),
                "Changing every recipe option reuses character renderers and meshes without rebuilding or leaking geometry.");

            for (int eye = 0; eye < PetCatalog.EyeNames.Length; eye++)
                yield return ShowCombination(new[] { 3, 0, 0, eye, 0, 0, 0, 1 }, "catalogue-eyes-" + eye + ".png");
            yield return ShowCombination(new[] { 4, 6, 7, 3, 0, 0, 1, 2 }, "catalogue-fairy-jelly.png");
            yield return ShowCombination(new[] { 7, 6, 5, 4, 0, 0, 3, 4 }, "catalogue-dragon-sugar.png");
            yield return ShowCombination(new[] { 1, 3, 7, 5, 4, 5, 5, 5 }, "catalogue-cosmic-glow.png");
            yield return ShowCombination(new[] { 5, 5, 4, 1, 1, 1, 0, 0 }, "catalogue-peach-puppy.png");
            yield return ShowCombination(new[] { 3, 2, 3, 2, 0, 2, 2, 3 }, "catalogue-butterfly-glitter.png");
            Check(PetStorage.Save(actor.Data), "The complete eight-category appearance saves to isolated storage.");
            Check(SameRecipe(PetStorage.Load(), actor.Data), "The displayed appearance survives a real disk round-trip.");
            Finish();
        }

        void TestSerializationAndMigration()
        {
            int[] expectedCounts = { 8, 7, 8, 6, 6, 6, 6, 6 };
            Check(PetCatalog.CategoryCount == expectedCounts.Length && PetCatalog.CategoryNames.Length == expectedCounts.Length,
                "The catalogue contains eight named ingredient categories.");
            for (int category = 0; category < Mathf.Min(PetCatalog.CategoryCount, expectedCounts.Length); category++)
            {
                Check(PetCatalog.Options(category).Length == expectedCounts[category], PetCatalog.CategoryNames[category] + " has its expected number of choices.");
                for (int option = 0; option < PetCatalog.Options(category).Length; option++)
                {
                    PetSave source = NeutralPet(); PetCatalog.Set(source, category, option);
                    PetSave restored = JsonUtility.FromJson<PetSave>(JsonUtility.ToJson(source));
                    PetStorage.Sanitize(restored);
                    Check(restored.schemaVersion == 3 && SameRecipe(source, restored),
                        "Schema-three JSON retains every recipe field for " + PetCatalog.CategoryNames[category] + " " + option + ".");
                }
            }
            var negative = new PetSave { colorIndex = -20, earsIndex = -20, tailIndex = -20, eyeIndex = -20, glassesIndex = -20, hatIndex = -20, wingsIndex = -20, finishIndex = -20 };
            var high = new PetSave { colorIndex = 100, earsIndex = 100, tailIndex = 100, eyeIndex = 100, glassesIndex = 100, hatIndex = 100, wingsIndex = 100, finishIndex = 100 };
            PetStorage.Sanitize(negative); PetStorage.Sanitize(high);
            Check(Enumerable.Range(0, 8).All(c => PetCatalog.Get(negative, c) == 0), "Negative recipe values sanitize safely across all eight categories.");
            Check(Enumerable.Range(0, 8).All(c => PetCatalog.Get(high, c) == expectedCounts[c] - 1), "Oversized recipe values clamp to their own category limits.");
            string originalOverride = PetStorage.SavePathOverride;
            try
            {
                PetStorage.SavePathOverride = Path.Combine(outputDirectory, "catalogue-storage-fixture.json");
                high.created = true; high.petName = "All options";
                Check(PetStorage.Save(high) && SameRecipe(PetStorage.Load(), high), "Maximum indices survive a real schema-three disk save.");
                File.WriteAllText(PetStorage.SavePath,
                    "{\"schemaVersion\":1,\"petName\":\"Old friend\",\"colorIndex\":2,\"earsIndex\":1,\"tailIndex\":2,\"created\":true,\"hunger\":0.8,\"cleanliness\":0.9,\"joy\":0.9,\"savedUtcTicks\":0,\"recipeSeed\":42}");
                PetSave legacy = PetStorage.Load();
                Check(legacy.schemaVersion == 3 && legacy.created && legacy.petName == "Old friend" && legacy.colorIndex == 2 && legacy.earsIndex == 1 && legacy.tailIndex == 2 && legacy.recipeSeed == 42,
                    "Schema-one characters retain their name, recipe, creation flag and identity during migration.");
                Check(legacy.eyeIndex == 0 && legacy.glassesIndex == 0 && legacy.hatIndex == 0 && legacy.wingsIndex == 0 && legacy.finishIndex == 0,
                    "Old schema-one characters receive classic eyes, no accessories and the soft finish.");
                int[] migratedColors = { 0,1,2,3,4,5,6,7,7,1,4,3 };
                for (int color = 0; color < migratedColors.Length; color++)
                {
                    var old = new PetSave { schemaVersion = 2, petName = "Legacy pastel", created = true, colorIndex = color,
                        earsIndex = 6, tailIndex = 7, eyeIndex = 5, glassesIndex = 4, hatIndex = 3, wingsIndex = 2,
                        hunger = .72f, cleanliness = .61f, joy = .84f, savedUtcTicks = 0, recipeSeed = 9042 };
                    File.WriteAllText(PetStorage.SavePath, JsonUtility.ToJson(old));
                    PetSave migrated = PetStorage.Load();
                    Check(migrated.schemaVersion == 3 && migrated.colorIndex == migratedColors[color] && migrated.finishIndex == 0,
                        "Schema-two color " + color + " migrates to its intended pastel family.");
                    Check(migrated.petName == old.petName && migrated.created && migrated.earsIndex == 6 && migrated.tailIndex == 7 &&
                        migrated.eyeIndex == 5 && migrated.glassesIndex == 4 && migrated.hatIndex == 3 && migrated.wingsIndex == 2 &&
                        Mathf.Abs(migrated.hunger - .72f) < .0001f && Mathf.Abs(migrated.cleanliness - .61f) < .0001f &&
                        Mathf.Abs(migrated.joy - .84f) < .0001f && migrated.recipeSeed == 9042,
                        "Migrating old color " + color + " preserves every unrelated customization and care value.");
                    PetStorage.Sanitize(migrated);
                    Check(migrated.colorIndex == migratedColors[color], "The color migration is idempotent for legacy index " + color + ".");
                }
            }
            finally { PetStorage.SavePathOverride = originalOverride; }
        }

        Renderer BodyRenderer() => actor.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.enabled && r.name == "Body");

        void CheckSelectedAppearance(int category, int option, string label)
        {
            if (category == 0 || category == 7)
            {
                Renderer body = BodyRenderer();
                Check(body && body.gameObject.activeInHierarchy, "The selected surface is on a visible body: " + label);
                if (!body) return;
                Material material = body.sharedMaterial;
                Texture2D skin = material.GetTexture("_BaseMap") as Texture2D;
                if (category == 0)
                {
                    Check(skin && skin.isReadable, "The selected color retains its washable skin map: " + label);
                    if (!skin) return;
                    Color color = skin.GetPixel(skin.width / 2, skin.height / 2);
                    signatures[category].Add(ColorUtility.ToHtmlStringRGBA(color));
                    Check((new Vector3(color.r, color.g, color.b) - new Vector3(PetCatalog.Colors[option].r, PetCatalog.Colors[option].g, PetCatalog.Colors[option].b)).magnitude < .02f,
                        "The clean skin texture matches the chosen pastel color without doubled tint: " + label);
                }
                else
                {
                    string signature = string.Join("/", new[] { "_Opacity", "_Smoothness", "_SpecularStrength", "_RimStrength", "_Iridescence", "_Grain" }
                        .Select(property => material.GetFloat(property).ToString("F3", CultureInfo.InvariantCulture)));
                    signatures[category].Add(signature);
                    geometry.Add(label + " material=" + signature + " internal crystals=" + actor.InternalGlitterCount + " glow=" + actor.HasExternalGlow);
                    Check(actor.FinishIndex == option && actor.FinishName == PetCatalog.FinishNames[option], "The saved finish and rendered surface agree: " + label);
                    bool jelly = option == 2 || option == 3;
                    Check(jelly ? material.renderQueue >= 3000 && material.GetFloat("_Opacity") < .99f
                            && material.GetFloat("_Transmission") > .5f && material.GetFloat("_RefractionStrength") > 0
                        : material.renderQueue < 3000 && material.GetFloat("_Opacity") >= .99f && material.GetFloat("_Transmission") == 0,
                        "Transparency is real and restricted to the jelly finishes: " + label);
                    Check((actor.InternalGlitterCount > 0) == (option == 3) && actor.HasExternalGlow == (option == 5),
                        "Internal glitter and outer glow are independently selected effects: " + label);
                }
                return;
            }
            GameObject group = null;
            if (category == 1) group = actor.ActiveEarsObject;
            else if (category == 2) group = actor.ActiveTailObject;
            else if (category == 3) group = actor.ActiveEyesObject;
            else
            {
                string prefix = category == 4 ? "Glasses." : category == 5 ? "Hat." : "Wings.";
                Transform[] active = actor.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name.StartsWith(prefix, StringComparison.Ordinal) && t.gameObject.activeInHierarchy).ToArray();
                Check(active.Length == (option == 0 ? 0 : 1), "Exactly the selected accessory family is active: " + label);
                if (active.Length > 0) group = active[0].gameObject;
            }
            if (option == 0 && category != 3)
            {
                Check(!group || !group.activeInHierarchy, "Selecting none hides the corresponding body part: " + label);
                signatures[category].Add("none"); return;
            }
            Check(group && group.activeInHierarchy, "The selected physical part is active: " + label);
            if (!group) return;
            Renderer[] renderers = group.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled).ToArray();
            Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(sceneCamera);
            Check(renderers.Length > 0 && renderers.Any(r => GeometryUtility.TestPlanesAABB(frustum, r.bounds)), "The selected part contains visible rendered geometry: " + label);
            string meshSignature = GeometrySignature(group, true);
            signatures[category].Add(meshSignature); geometry.Add(label + "=" + meshSignature);
            if (category == 6 && option == 1) fairySignature = GeometrySignature(group, false);
            if (category == 6 && option == 2) butterflySignature = GeometrySignature(group, false);
        }

        void CheckWashableFinish(string label)
        {
            actor.AddDirt(.5f); float before = actor.Data.cleanliness; int hits = 0;
            Physics.SyncTransforms();
            for (int y = 3; y <= 7; y++) for (int x = 3; x <= 7; x++)
            {
                Ray ray = sceneCamera.ViewportPointToRay(new Vector3(x / 10f, y / 10f, 0));
                if (Physics.Raycast(ray, out RaycastHit hit, 30) && hit.collider.GetComponentInParent<PetActor>() == actor && actor.WashAt(hit, .8f)) hits++;
            }
            Check(hits > 0 && actor.Data.cleanliness > before, "The finish preserves exact UV hits and real dirt removal: " + label);
        }

        HashSet<Mesh> ActorMeshes() => new HashSet<Mesh>(actor.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh).Select(f => f.sharedMesh));
        static bool Finite(Vector3 vector) => float.IsFinite(vector.x) && float.IsFinite(vector.y) && float.IsFinite(vector.z);
        static bool SameRecipe(PetSave a, PetSave b) => a != null && b != null && Enumerable.Range(0, PetCatalog.CategoryCount).All(c => PetCatalog.Get(a, c) == PetCatalog.Get(b, c));

        static string GeometrySignature(GameObject group, bool includeArrangement)
        {
            ulong hash = 1469598103934665603UL; int count = 0, vertexCount = 0;
            foreach (Renderer renderer in group.GetComponentsInChildren<Renderer>(false))
            {
                if (!renderer.enabled) continue;
                MeshFilter filter = renderer.GetComponent<MeshFilter>(); SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
                Mesh mesh = filter ? filter.sharedMesh : skinned ? skinned.sharedMesh : null;
                if (!mesh) continue;
                count++; vertexCount += mesh.vertexCount; Hash(ref hash, mesh.vertexCount); Hash(ref hash, mesh.bounds.size);
                if (mesh.isReadable)
                {
                    Vector3[] points = mesh.vertices; int stride = Mathf.Max(1, points.Length / 512);
                    for (int i = 0; i < points.Length; i += stride) Hash(ref hash, points[i]);
                }
                if (includeArrangement)
                {
                    Matrix4x4 local = group.transform.worldToLocalMatrix * renderer.localToWorldMatrix;
                    for (int component = 0; component < 16; component++) Hash(ref hash, Mathf.RoundToInt(local[component] * 1000f));
                }
            }
            return count + "/" + vertexCount + "/" + hash.ToString("X16", CultureInfo.InvariantCulture);
        }
        static void Hash(ref ulong hash, Vector3 value) { Hash(ref hash, Mathf.RoundToInt(value.x * 10000)); Hash(ref hash, Mathf.RoundToInt(value.y * 10000)); Hash(ref hash, Mathf.RoundToInt(value.z * 10000)); }
        static void Hash(ref ulong hash, int value) { unchecked { hash ^= (uint)value; hash *= 1099511628211UL; } }

        IEnumerator ShowCombination(int[] recipe, string filename)
        {
            PetSave pet = NeutralPet();
            for (int category = 0; category < recipe.Length; category++) PetCatalog.Set(pet, category, recipe[category]);
            actor.Initialize(pet); actor.ClearSoap(); actor.SetExpression("happy");
            yield return new WaitForSecondsRealtime(.6f);
            Check(SameRecipe(actor.Data, pet), "The complete sample combination is rendered: " + filename);
            Bounds bounds = actor.VisibleBounds;
            Check(GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(sceneCamera), bounds), "The sample character remains inside the camera view: " + filename);
            yield return Capture(filename);
        }

        IEnumerator Capture(string filename)
        {
            yield return new WaitForEndOfFrame();
            Texture2D image = MimoRenderCapture.Scene(sceneCamera);
            if (!image) { errors.Add("Could not capture " + filename); yield break; }
            Color32[] pixels = image.GetPixels32(); int low = 765, high = 0;
            for (int i = 0; i < pixels.Length; i += Mathf.Max(1, pixels.Length / 4096))
            { int value = pixels[i].r + pixels[i].g + pixels[i].b; low = Mathf.Min(low, value); high = Mathf.Max(high, value); }
            Check(high > 100 && high - low > 40, "The game screenshot contains rendered color variation: " + filename);
            File.WriteAllBytes(Path.Combine(outputDirectory, filename), image.EncodeToPNG()); Destroy(image); screenshots.Add(filename);
        }

        void Finish()
        {
            if (finished) return; finished = true;
            PetStorage.SavePathOverride = isolatedSavePath;
            var report = new Report { passed = errors.Count == 0, checks = checks.ToArray(), errors = errors.ToArray(), screenshots = screenshots.ToArray(), geometry = geometry.ToArray(),
                unityVersion = Application.unityVersion, width = Screen.width, height = Screen.height, elapsedSeconds = Time.realtimeSinceStartup - started, isolatedSavePath = PetStorage.SavePath };
            try { Directory.CreateDirectory(outputDirectory); File.WriteAllText(Path.Combine(outputDirectory, "creation-checks.json"), JsonUtility.ToJson(report, true)); }
            catch (Exception error) { Debug.LogError("Catalogue report could not be written: " + error.Message); Application.Quit(1); return; }
            Debug.Log("MIMO_CREATION " + (report.passed ? "PASS" : "FAIL") + " checks=" + checks.Count + " errors=" + errors.Count);
            Application.Quit(report.passed ? 0 : 1);
        }
    }
}
