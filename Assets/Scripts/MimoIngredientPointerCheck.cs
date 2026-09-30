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
    /// <summary>Optional regression of the actual card drag handlers and their shared scroll owner.</summary>
    public sealed class MimoIngredientPointerCheck : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed, ordinarySaveUnchanged;
            public string version, platform;
            public int width, height;
            public string[] checks, errors;
        }
        static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "--mimo-ingredient-pointer-test") >= 0;
        static string output, ordinaryPath;
        static byte[] originalBytes;
        static bool originalExisted;
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        float started;
        bool finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Isolate()
        {
            if (!Enabled) return;
            string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--mimo-test-output");
            output = at >= 0 && at + 1 < args.Length ? Path.GetFullPath(args[at + 1]) : Path.Combine(Application.temporaryCachePath, "MimoIngredientPointerCheck");
            Directory.CreateDirectory(output);
            ordinaryPath = Path.Combine(Application.persistentDataPath, "mimo-pet.json");
            originalExisted = File.Exists(ordinaryPath); originalBytes = originalExisted ? File.ReadAllBytes(ordinaryPath) : null;
            PetStorage.SavePathOverride = Path.Combine(output, "isolated-pointer-" + DateTime.UtcNow.Ticks + ".json");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (Enabled) new GameObject("Ingredient pointer routing verification").AddComponent<MimoIngredientPointerCheck>(); }
        void Awake() { started = Time.realtimeSinceStartup; Application.runInBackground = true; Application.logMessageReceived += Log; }
        void OnDestroy() { Application.logMessageReceived -= Log; }
        void Update() { if (!finished && Time.realtimeSinceStartup - started > 35) { errors.Add("Pointer routing check timed out."); Finish(); } }
        void Log(string message, string stack, LogType type)
        { if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !errors.Contains(message)) errors.Add(message); }
        void Check(bool result, string description) { if (result) checks.Add(description); else errors.Add(description); }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(.8f);
            MimoExperience app = FindAnyObjectByType<MimoExperience>();
            Check(app && EventSystem.current, "The real experience and UI event system are available.");
            if (!app || !EventSystem.current) { Finish(); yield break; }
            app.EnterLaboratory(); yield return new WaitForEndOfFrame(); Canvas.ForceUpdateCanvases();
            // Use the real category buttons and raycaster at both ends of the ingredient list.
            for (int category = 0; category < PetCatalog.CategoryCount; category++)
            {
                Button tab = CategoryButton(category);
                Check(tab && !tab.GetComponentInParent<ScrollRect>(), "Category stays outside ingredient scrolling: " + category);
                if (!tab) { Finish(); yield break; }
                RectTransform rect = (RectTransform)tab.transform;
                MimoIngredientIcon picture = tab.GetComponentInChildren<MimoIngredientIcon>();
                Check(rect.rect.height >= 96 && rect.rect.width * rect.rect.height > 7400,
                    "Category has a larger touch area than the previous text tab: " + category);
                Check(picture && picture.sprite && picture.CategoryPreview && picture.Category == category && picture.rectTransform.rect.height >= 76
                    && tab.GetComponentsInChildren<Text>().Any(t => t.fontSize >= 12), "Category uses generated artwork with a readable short label: " + category);
                Vector3[] corners = new Vector3[4]; rect.GetWorldCorners(corners);
                Check(Screen.safeArea.Contains((Vector2)corners[0] + Vector2.one) && Screen.safeArea.Contains((Vector2)corners[2] - Vector2.one),
                    "The complete category button fits on the screen: " + category);
                Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                PointerEventData tap = Pointer(30 + category, point);
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(tap, hits);
                Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == tab.gameObject,
                    "The category picture routes taps to its actual button: " + category + " (top hit: " + (hits.Count > 0 ? hits[0].gameObject.name : "none") + ")");
                ExecuteEvents.Execute(tab.gameObject, tap, ExecuteEvents.pointerClickHandler);
                // A real tap can only hit the replacement hierarchy after its canvas renders.
                yield return new WaitForEndOfFrame(); Canvas.ForceUpdateCanvases();
                MimoIngredientDrag first = Card("Option " + category + ".0 ");
                Check(first, "Tapping the picture opens its ingredient choices: " + category);
                if (!first) { Finish(); yield break; }
                ScrollRect categoryScroll = first.GetComponentInParent<ScrollRect>();
                Check(categoryScroll && RectTransformUtility.RectangleContainsScreenPoint(categoryScroll.viewport, Center(first)),
                    "Changing category brings its first ingredient into view: " + category);
                if (!categoryScroll) { Finish(); yield break; }
                categoryScroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
                MimoIngredientDrag last = Card("Option " + category + "." + (PetCatalog.Options(category).Length - 1) + " ");
                Check(last && RectTransformUtility.RectangleContainsScreenPoint(categoryScroll.viewport, Center(last)),
                    "The final ingredient is reachable by scrolling: " + category);
                Check(CategoryButton(0) && CategoryButton(7), "All categories remain available at the bottom of the list: " + category);
                if (category == 3)
                {
                    categoryScroll.verticalNormalizedPosition = 1;
                    yield return new WaitForEndOfFrame();
                    Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(output, "menu-eyes.png"), capture.EncodeToPNG()); Destroy(capture);
                }
            }
            ExecuteEvents.Execute(CategoryButton(0).gameObject, Pointer(40, Vector2.zero), ExecuteEvents.pointerClickHandler);
            yield return new WaitForEndOfFrame(); Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            Texture2D menuCapture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output, "menu-colors.png"), menuCapture.EncodeToPNG()); Destroy(menuCapture);
            MimoIngredientDrag card = Card("Option 0.0 ");
            Check(card, "A color card has the production ingredient drag handler.");
            if (!card) { Finish(); yield break; }
            ScrollRect scroll = card.GetComponentInParent<ScrollRect>();
            Check(scroll && scroll.vertical && !scroll.horizontal, "The card shares the single vertical ingredient scroll area.");
            if (!scroll) { Finish(); yield break; }
            int drops = app.AddedIngredientCount;
            float beforeScroll = scroll.content.anchoredPosition.y;
            Vector2 start = Center(card);
            Check(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, start), "The tested color card starts within the actual viewport.");
            PointerEventData vertical = Pointer(11, start);
            ExecuteEvents.Execute(card.gameObject, vertical, ExecuteEvents.initializePotentialDrag);
            MoveEvent(vertical, start + Vector2.up * 22);
            ExecuteEvents.Execute(card.gameObject, vertical, ExecuteEvents.beginDragHandler);
            for (int i = 1; i <= 10; i++)
            {
                MoveEvent(vertical, start + Vector2.up * (22 + i * 14));
                ExecuteEvents.Execute(card.gameObject, vertical, ExecuteEvents.dragHandler); yield return null;
            }
            ExecuteEvents.Execute(card.gameObject, vertical, ExecuteEvents.endDragHandler);
            scroll.StopMovement(); Canvas.ForceUpdateCanvases();
            Check(scroll.content.anchoredPosition.y > beforeScroll + 50, "Dragging a card vertically scrolls its parent content.");
            Check(app.AddedIngredientCount == drops && !app.PendingStir && !app.CreationStation.IsDraggingIngredient,
                "Vertical scrolling never picks up or adds the color ingredient.");

            // This goes through the card's real routing component, not BeginIngredientDrag directly.
            start = Center(card);
            PointerEventData horizontal = Pointer(12, start);
            ExecuteEvents.Execute(card.gameObject, horizontal, ExecuteEvents.initializePotentialDrag);
            // Android can issue begin-drag just beyond the EventSystem's five-pixel
            // threshold, well before a larger canvas-scaled threshold would be reached.
            MoveEvent(horizontal, start + Vector2.left * 6);
            ExecuteEvents.Execute(card.gameObject, horizontal, ExecuteEvents.beginDragHandler);
            Check(app.CreationStation.IsDraggingIngredient, "A slow six-pixel leftward begin-drag picks up the ingredient without being locked into scrolling.");
            Check(card.GetComponent<CanvasGroup>().alpha < .6f, "The held card visibly indicates that it is being dragged.");
            Check(!app.CanFinalizeRecipe, "Finalization is unavailable while carrying an ingredient.");
            Vector2 drop = app.CreationDropScreenPosition;
            for (int i = 1; i <= 12; i++)
            {
                MoveEvent(horizontal, Vector2.Lerp(start + Vector2.left * 6, drop, i / 12f));
                ExecuteEvents.Execute(card.gameObject, horizontal, ExecuteEvents.dragHandler); yield return null;
            }
            ExecuteEvents.Execute(card.gameObject, horizontal, ExecuteEvents.endDragHandler);
            Check(app.AddedIngredientCount == drops + 1 && app.PendingStir, "Dropping through the actual card handler adds exactly one ingredient and requires stirring.");
            Check(!horizontal.eligibleForClick, "The drag is not also treated as a tap on the ingredient button.");
            Check(card.GetComponent<CanvasGroup>().alpha > .99f, "Releasing the ingredient restores its card appearance.");
            Check(!app.CanFinalizeRecipe, "A poured but unstirred color cannot finalize the recipe.");
            while (app.CreationStation.IsPouring) yield return null;
            Check(app.BeginStir(app.CreationStirScreenPoint(0)), "The poured ingredient accepts a real stirring start.");
            for (int i = 1; i <= 90 && app.PendingStir; i++)
            { app.MoveStir(app.CreationStirScreenPoint(i * Mathf.PI * 2 / 45)); yield return null; }
            app.EndStir();
            Check(!app.PendingStir && app.CanFinalizeRecipe, "Circular movement completes the ingredient routed from the UI.");

            card = Card("Option 0.1 ");
            Check(card, "A second color card is available for cancellation checks.");
            if (!card) { Finish(); yield break; }
            start = Center(card);
            PointerEventData canceled = Pointer(13, start);
            ExecuteEvents.Execute(card.gameObject, canceled, ExecuteEvents.initializePotentialDrag);
            MoveEvent(canceled, start + Vector2.left * 32);
            ExecuteEvents.Execute(card.gameObject, canceled, ExecuteEvents.beginDragHandler);
            Check(app.CreationStation.IsDraggingIngredient, "A second card can begin another physical drag.");
            card.gameObject.SendMessage("OnApplicationFocus", false, SendMessageOptions.RequireReceiver);
            yield return null;
            Check(!app.CreationStation.IsDraggingIngredient && card.GetComponent<CanvasGroup>().alpha > .99f,
                "Losing focus cancels the held ingredient and restores its card.");
            Check(app.AddedIngredientCount == drops + 1 && app.DraftRecipe.colorIndex == 0,
                "Canceled focus does not add an ingredient or change the mixed color.");
            PointerEventData restarted = Pointer(14, Center(card));
            ExecuteEvents.Execute(card.gameObject, restarted, ExecuteEvents.initializePotentialDrag);
            MoveEvent(restarted, restarted.pressPosition + Vector2.left * 32);
            ExecuteEvents.Execute(card.gameObject, restarted, ExecuteEvents.beginDragHandler);
            Check(app.CreationStation.IsDraggingIngredient, "A fresh pointer works on the same card after focus cancellation.");
            MoveEvent(restarted, new Vector2(5, 5));
            ExecuteEvents.Execute(card.gameObject, restarted, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(card.gameObject, restarted, ExecuteEvents.endDragHandler);
            Check(app.AddedIngredientCount == drops + 1 && !app.PendingStir && app.DraftRecipe.colorIndex == 0,
                "Dropping a routed ingredient outside the steamer cancels without modifying the recipe.");
            Finish();
        }
        static MimoIngredientDrag Card(string prefix)
        {
            foreach (MimoIngredientDrag card in FindObjectsByType<MimoIngredientDrag>(FindObjectsSortMode.None))
                if (card.gameObject.activeInHierarchy && card.gameObject.name.StartsWith(prefix, StringComparison.Ordinal)) return card;
            return null;
        }
        static Button CategoryButton(int category)
        {
            foreach (Button button in FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (button.gameObject.activeInHierarchy && button.name.StartsWith("Category " + category + " ", StringComparison.Ordinal)) return button;
            return null;
        }
        static Vector2 Center(MimoIngredientDrag card)
        {
            RectTransform rect = (RectTransform)card.transform;
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }
        static PointerEventData Pointer(int id, Vector2 start)
        { return new PointerEventData(EventSystem.current) { pointerId = id, position = start, pressPosition = start, button = PointerEventData.InputButton.Left, eligibleForClick = true, useDragThreshold = true }; }
        static void MoveEvent(PointerEventData e, Vector2 to) { e.delta = to - e.position; e.position = to; }
        bool SaveUnchanged()
        {
            if (File.Exists(ordinaryPath) != originalExisted) return false;
            if (!originalExisted) return true;
            byte[] current = File.ReadAllBytes(ordinaryPath);
            if (current.Length != originalBytes.Length) return false;
            for (int i = 0; i < current.Length; i++) if (current[i] != originalBytes[i]) return false;
            return true;
        }
        void Finish()
        {
            if (finished) return; finished = true;
            bool unchanged = SaveUnchanged(); Check(unchanged, "The player's ordinary save remains byte-for-byte unchanged.");
            var report = new Report { passed = errors.Count == 0, ordinarySaveUnchanged = unchanged, version = Application.version,
                platform = Application.platform.ToString(), width = Screen.width, height = Screen.height, checks = checks.ToArray(), errors = errors.ToArray() };
            File.WriteAllText(Path.Combine(output, "ingredient-pointer-checks.json"), JsonUtility.ToJson(report, true));
            Debug.Log("MIMO_INGREDIENT_POINTER_RESULT " + (report.passed ? "PASSED" : "FAILED") + " checks=" + checks.Count + " errors=" + errors.Count);
            Application.Quit(report.passed ? 0 : 1);
        }
    }
}
