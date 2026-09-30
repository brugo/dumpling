using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MimoGarden
{
    /// <summary>Opt-in player acceptance checks, using the same drag/stir/surface APIs as touch input.</summary>
    public sealed class MimoRitualCheck : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed, mobileDevice, ordinarySaveUnchanged;
            public string version, device, gpu;
            public int width, height;
            public float elapsedSeconds, soapCoverageBeforeRinse, soapCoverageAfterRinse;
            public string[] checks, errors, screenshots;
        }
        static string output, ordinaryPath;
        static byte[] ordinaryBytes;
        static bool ordinaryExisted;
        static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "--mimo-ritual-test") >= 0;
        readonly List<string> checks = new List<string>(), errors = new List<string>(), screenshots = new List<string>();
        MimoExperience experience;
        PetActor actor;
        Camera camera;
        bool finished;
        float started, covered, rinsed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Isolate()
        {
            if (!Enabled) return;
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--mimo-test-output");
            output = index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1])
                : Path.Combine(Application.temporaryCachePath, "MimoRitualValidation");
            Directory.CreateDirectory(output);
            ordinaryPath = Path.Combine(Application.persistentDataPath, "mimo-pet.json");
            ordinaryExisted = File.Exists(ordinaryPath);
            ordinaryBytes = ordinaryExisted ? File.ReadAllBytes(ordinaryPath) : null;
            PetStorage.SavePathOverride = Path.Combine(output, "isolated-ritual-" + DateTime.UtcNow.Ticks + ".json");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (Enabled) new GameObject("Mimo recipe and bath acceptance").AddComponent<MimoRitualCheck>(); }
        void Awake() { started = Time.realtimeSinceStartup; Application.runInBackground = true; Application.logMessageReceived += Log; }
        void OnDestroy() { Application.logMessageReceived -= Log; }
        void Log(string message, string stack, LogType type)
        { if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !errors.Contains(message)) errors.Add(message); }
        void Check(bool pass, string description) { if (pass) checks.Add(description); else errors.Add(description); }
        void Update() { if (!finished && Time.realtimeSinceStartup - started > 180f) { errors.Add("Ritual check timed out."); Finish(); } }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(1.1f);
            experience = FindAnyObjectByType<MimoExperience>(); camera = Camera.main;
            Check(experience && camera, "Experience and camera start.");
            if (!experience || !camera) { Finish(); yield break; }
            actor = experience.Actor;
            Check(Screen.width > Screen.height, "Landscape presentation.");
            Check(PetCatalog.Colors.Length == 8 && PetCatalog.CategoryCount == 8, "Eight pastel colors and eight ingredient categories.");
            Check(PetCatalog.Options(7).Length >= 6, "At least six independent material finishes.");
            foreach (Color color in PetCatalog.Colors)
            {
                Color.RGBToHSV(color, out float h, out float s, out float v);
                Check(s <= .48f && v >= .72f, "Every body color has a light, soft palette: " + ColorUtility.ToHtmlStringRGB(color));
            }
            CheckMigration();
            experience.EnterLaboratory(); yield return null; actor = experience.Actor;
            Check(!actor.gameObject.activeInHierarchy, "The character stays hidden before any ingredient is chosen.");
            Check(!experience.CanFinalizeRecipe, "An empty steamer cannot produce a character.");
            experience.BeginBirth();
            Check(experience.CurrentScreen == "Laboratory", "Finalizing an empty recipe is rejected.");
            yield return Capture("01-empty-steamer.png");

            Vector2 source = new Vector2(Screen.width - 90, Screen.height * .45f);
            experience.BeginIngredientDrag(0, 1, source);
            experience.MoveIngredientDrag(new Vector2(15, Screen.height - 15));
            experience.EndIngredientDrag(new Vector2(15, Screen.height - 15));
            yield return new WaitForSecondsRealtime(.7f);
            Check(experience.AddedIngredientCount == 0 && !experience.PendingStir, "Dropping outside the steamer leaves the recipe untouched.");
            experience.SelectRecipeCategory(0);
            Vector2 drop = experience.CreationDropScreenPosition;
            experience.BeginIngredientDrag(0, 1, source);
            for (int i = 1; i <= 12; i++) { experience.MoveIngredientDrag(Vector2.Lerp(source, drop, i / 12f)); yield return null; }
            experience.EndIngredientDrag(drop);
            yield return new WaitForSecondsRealtime(.7f);
            Check(experience.CreationStation.IsPouring && experience.CreationStation.LiquidFill > 0,
                "The bottle pour is animated while the real liquid level rises.");
            yield return Capture("02-bottle-pouring.png");
            while (experience.CreationStation.IsPouring) yield return null;
            Check(experience.PendingStir, "Poured liquid waits for a real stirring gesture.");
            Check(!actor.gameObject.activeInHierarchy, "Pouring does not reveal the character.");
            Check(!experience.CanFinalizeRecipe, "An unmixed ingredient blocks finalization.");
            experience.BeginBirth();
            Check(experience.CurrentScreen == "Laboratory", "Finalization is blocked during mixing.");
            Vector2 a = experience.CreationStirScreenPoint(0f), b = experience.CreationStirScreenPoint(Mathf.PI);
            experience.BeginStir(a);
            for (int i = 0; i <= 12; i++) { experience.MoveStir(Vector2.Lerp(a, b, i / 12f)); yield return null; }
            experience.EndStir();
            Check(experience.PendingStir && experience.StirProgress < 1f, "A straight swipe cannot stand in for circular stirring.");
            yield return Stir(experience);
            Check(!experience.PendingStir && experience.CanFinalizeRecipe, "Circular motion incorporates the color and enables finishing.");
            yield return Capture("02-mixed-color.png");

            yield return AddAndStir(experience, 1, 1);
            yield return AddAndStir(experience, 6, 2);
            yield return AddAndStir(experience, 7, 3);
            Check(experience.AddedIngredientCount >= 4, "Liquid, ears, wings and finish all enter the recipe.");
            Check(experience.DraftRecipe.earsIndex == 1 && experience.DraftRecipe.wingsIndex == 2 && experience.DraftRecipe.finishIndex == 3,
                "Dropped ingredients preserve the intended recipe.");
            Check(!actor.gameObject.activeInHierarchy, "Even the complete recipe keeps its result secret.");
            CheckCreationScroll();
            yield return Capture("03-ready-for-surprise.png");
            experience.BeginBirth();
            Check(experience.CurrentScreen == "Birth", "A fully mixed recipe starts its reveal.");
            yield return new WaitForSecondsRealtime(.65f);
            Check(!actor.gameObject.activeInHierarchy, "The closed-steamer anticipation conceals the result.");
            yield return Capture("04-steaming.png");
            yield return new WaitForSecondsRealtime(2.9f);
            Check(experience.CurrentScreen == "Naming" && actor.gameObject.activeInHierarchy, "The surprise reveals a live character and naming controls.");
            Check(actor.Data.colorIndex == 1 && actor.Data.earsIndex == 1 && actor.Data.wingsIndex == 2 && actor.Data.finishIndex == 3,
                "The revealed character matches the actual mixed ingredients.");
            yield return Capture("05-surprise-reveal.png");
            experience.ConfirmName(); yield return null; actor = experience.Actor;
            Check(experience.CurrentScreen == "Home" && PetStorage.Load().finishIndex == 3, "The complete recipe and finish persist after naming.");
            PetSave original = experience.SavedRecipe;
            experience.EnterLaboratory(); yield return null;
            yield return AddAndStir(experience, 0, 5);
            experience.EnterHome(); yield return null; actor = experience.Actor;
            Check(SameRecipe(original, actor.Data), "Cancelling a different recipe preserves the existing friend.");

            PetSave showcase = actor.Data.Copy();
            showcase.earsIndex = showcase.tailIndex = showcase.glassesIndex = showcase.hatIndex = showcase.wingsIndex = 0;
            showcase.colorIndex = 0; showcase.cleanliness = 1f;
            for (int finish = 0; finish < PetCatalog.Options(7).Length; finish++)
            {
                showcase.finishIndex = finish; actor.Initialize(showcase.Copy());
                yield return new WaitForSecondsRealtime(.3f);
                Check(actor.FinishIndex == finish, "Material finish applied: " + PetCatalog.Options(7)[finish]);
                Check(actor.GetComponentsInChildren<Renderer>().All(r => !r.enabled || (r.sharedMaterial && r.sharedMaterial.shader.isSupported)),
                    "Every active material is supported for finish " + finish);
                if (finish == 2 || finish == 3) Check(actor.IsTranslucentFinish, "Jelly finish uses translucency: " + finish);
                if (finish == 3) Check(actor.InternalGlitterCount > 0, "Jelly glitter contains internal sparkles.");
                yield return Capture("finish-" + finish + ".png");
            }

            Click("Banho"); yield return new WaitForSecondsRealtime(.6f);
            actor.Initialize(new PetSave { created = true, colorIndex = 0, finishIndex = 1, cleanliness = .3f, hunger = .8f, joy = .9f });
            experience.SelectBathMode("Soap");
            float dirty = actor.Data.cleanliness;
            yield return PaintBody(true, 2);
            covered = actor.SoapCoverage;
            Check(covered > .45f, "Scrubbing different body surfaces creates broad persistent soap coverage.");
            Check(Mathf.Abs(actor.Data.cleanliness - dirty) < .025f, "Applying soap does not instantly wash the dirt away.");
            yield return Capture("06-covered-in-soap.png");
            float soapPatch = actor.SoapAtUV(new Vector2(.5f, .5f));
            experience.SendMessage("OnApplicationPause", true);
            experience.SendMessage("OnApplicationPause", false);
            Check(Mathf.Abs(actor.SoapCoverage - covered) < .001f && Mathf.Abs(actor.SoapAtUV(new Vector2(.5f, .5f)) - soapPatch) < .001f,
                "Pausing and resuming preserves the accumulated foam at its original surface locations.");
            actor.BeginSquish(); yield return new WaitForSecondsRealtime(.5f);
            Check(actor.SoapCoverage >= covered - .01f, "Soap stays attached while the character is squeezed.");
            actor.EndSquish(); yield return new WaitForSecondsRealtime(.9f);
            experience.SelectBathMode("Rinse");
            Check(experience.BathMode == "Rinse", "Rinse action selects the shower tool.");
            yield return PaintBody(false, 3);
            rinsed = actor.SoapCoverage;
            Check(rinsed < covered * .2f, "Rinsing surface areas removes their accumulated foam.");
            Check(actor.Data.cleanliness > dirty + .2f, "Rinsing soap removes actual dirt and restores cleanliness.");
            yield return Capture("07-rinsed-clean.png");
            Click("Brincar"); yield return new WaitForSecondsRealtime(.4f);
            Check(experience.ActiveEnvironment == "Play", "Leaving the bathroom still opens the garden.");
            yield return Capture("08-ready-to-play.png");
            yield return PaintBody(true, 1);
            Check(actor.SoapCoverage > .1f, "The previous character has foam before a separate creation is tested.");
            experience.EnterLaboratory(); actor = experience.Actor; yield return AddAndStir(experience, 0, 2);
            experience.BeginBirth(); yield return new WaitForSecondsRealtime(3.5f);
            Check(experience.CurrentScreen == "Naming" && actor.SoapCoverage < .001f,
                "A newly created friend never inherits the previous character's bath foam.");
            Finish();
        }

        public static IEnumerator AddIngredient(MimoExperience app, int category, int option)
        {
            app.SelectRecipeCategory(category);
            Vector2 start = new Vector2(Screen.width - 100, Screen.height * .4f), end = app.CreationDropScreenPosition;
            app.BeginIngredientDrag(category, option, start);
            for (int i = 1; i <= 12; i++) { app.MoveIngredientDrag(Vector2.Lerp(start, end, i / 12f)); yield return null; }
            app.EndIngredientDrag(end);
            float deadline = Time.realtimeSinceStartup + 4f;
            while (!app.PendingStir && Time.realtimeSinceStartup < deadline) yield return null;
            while (app.CreationStation && app.CreationStation.IsPouring && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSecondsRealtime(.15f);
        }

        public static IEnumerator Stir(MimoExperience app)
        {
            app.BeginStir(app.CreationStirScreenPoint(0));
            for (int i = 1; i <= 120 && app.PendingStir; i++)
            {
                app.MoveStir(app.CreationStirScreenPoint(i * Mathf.PI * 2f / 40f));
                yield return null;
            }
            app.EndStir(); yield return new WaitForSecondsRealtime(.25f);
        }
        public static IEnumerator AddAndStir(MimoExperience app, int category, int option)
        { yield return AddIngredient(app, category, option); yield return Stir(app); }

        IEnumerator PaintBody(bool soap, int passes)
        {
            for (int pass = 0; pass < passes; pass++) for (int side = 0; side < 4; side++)
            {
                actor.transform.rotation = Quaternion.Euler(0, side * 90, 0); Physics.SyncTransforms(); yield return null;
                Bounds bounds = actor.VisibleBounds;
                Vector3 min = camera.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.center.z));
                Vector3 max = camera.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z));
                for (int row = 1; row < 9; row++) for (int col = 1; col < 9; col++)
                {
                    Vector2 p = new Vector2(Mathf.Lerp(min.x, max.x, col / 10f), Mathf.Lerp(min.y, max.y, row / 10f));
                    if (!Physics.Raycast(camera.ScreenPointToRay(p), out RaycastHit hit, 30f) || hit.collider.GetComponentInParent<PetActor>() != actor) continue;
                    if (soap) actor.SoapAt(hit, .6f); else actor.RinseAt(hit, .9f);
                }
                yield return new WaitForSecondsRealtime(.12f);
            }
            actor.transform.rotation = Quaternion.identity; yield return new WaitForSecondsRealtime(.2f);
        }

        void CheckCreationScroll()
        {
            ScrollRect scroll = FindObjectsByType<ScrollRect>(FindObjectsSortMode.None).FirstOrDefault(s => s.gameObject.activeInHierarchy);
            Check(scroll && scroll.vertical && !scroll.horizontal, "Ingredient choices retain a vertical scroll area.");
            if (!scroll) return;
            var evt = new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, -20) };
            ExecuteEvents.Execute(scroll.gameObject, evt, ExecuteEvents.scrollHandler);
            Canvas.ForceUpdateCanvases();
            Check(scroll.verticalNormalizedPosition < .99f || scroll.content.rect.height <= scroll.viewport.rect.height,
                "The ingredient list accepts scrolling when its contents overflow.");
            foreach (Button button in scroll.GetComponentsInChildren<Button>())
                Check(((RectTransform)button.transform).rect.height >= 52, "Touch target remains large: " + button.name);
        }
        void Click(string label)
        {
            Button button = FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.gameObject.activeInHierarchy && b.interactable
                && b.GetComponentsInChildren<Text>(true).Any(t => t.text.Trim() == label));
            Check(button, "Visible action exists: " + label); if (button) button.onClick.Invoke();
        }
        void CheckMigration()
        {
            string path = PetStorage.SavePath;
            File.WriteAllText(path, "{\"schemaVersion\":2,\"petName\":\"Old friend\",\"colorIndex\":11,\"earsIndex\":2,\"tailIndex\":7,\"eyeIndex\":3,\"glassesIndex\":2,\"hatIndex\":4,\"wingsIndex\":2,\"created\":true,\"hunger\":0.8,\"cleanliness\":0.9,\"joy\":0.9}");
            PetSave migrated = PetStorage.Load();
            Check(migrated.schemaVersion == 3 && migrated.colorIndex == 3 && migrated.finishIndex == 0,
                "Schema 2 colors migrate safely to the pastel catalogue and default material.");
            Check(migrated.petName == "Old friend" && migrated.earsIndex == 2 && migrated.tailIndex == 7 && migrated.eyeIndex == 3
                && migrated.glassesIndex == 2 && migrated.hatIndex == 4 && migrated.wingsIndex == 2, "Migration preserves identity and all existing accessories.");
            File.Delete(path);
        }
        static bool SameRecipe(PetSave first, PetSave second) => first != null && second != null && first.petName == second.petName
            && Enumerable.Range(0, PetCatalog.CategoryCount).All(c => PetCatalog.Get(first, c) == PetCatalog.Get(second, c));
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
            if (!texture) { errors.Add("Screenshot failed: " + name); yield break; }
            File.WriteAllBytes(Path.Combine(output, name), texture.EncodeToPNG()); Destroy(texture); screenshots.Add(name);
        }
        void Finish()
        {
            if (finished) return; finished = true;
            bool unchanged = ordinaryExisted ? File.Exists(ordinaryPath) && ordinaryBytes.SequenceEqual(File.ReadAllBytes(ordinaryPath)) : !File.Exists(ordinaryPath);
            Check(unchanged, "The user's ordinary save remains byte-for-byte untouched.");
            var report = new Report { passed = errors.Count == 0, version = Application.version, mobileDevice = Application.isMobilePlatform,
                ordinarySaveUnchanged = unchanged, device = SystemInfo.deviceModel, gpu = SystemInfo.graphicsDeviceName,
                width = Screen.width, height = Screen.height, elapsedSeconds = Time.realtimeSinceStartup - started,
                soapCoverageBeforeRinse = covered, soapCoverageAfterRinse = rinsed,
                checks = checks.ToArray(), errors = errors.ToArray(), screenshots = screenshots.ToArray() };
            File.WriteAllText(Path.Combine(output, "ritual-checks.json"), JsonUtility.ToJson(report, true));
            Debug.Log("MIMO_RITUAL " + (report.passed ? "PASS" : "FAIL") + " checks=" + checks.Count + " errors=" + errors.Count);
            Application.Quit(report.passed ? 0 : 1);
        }
    }
}
