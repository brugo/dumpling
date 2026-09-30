using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MimoGarden
{
    /// <summary>Opt-in player validation, using real care buttons and the same gesture path as touch.</summary>
    public sealed class MimoInteractionCheck : MonoBehaviour
    {
        [Serializable]
        sealed class Report
        {
            public bool passed;
            public string[] checks;
            public string[] errors;
            public string[] screenshots;
            public string[] measurements;
            public string version;
            public int width;
            public int height;
            public float elapsedSeconds;
            public string isolatedSavePath;
        }

        static string outputDirectory;
        readonly List<string> checks = new List<string>();
        readonly List<string> errors = new List<string>();
        readonly List<string> screenshots = new List<string>();
        readonly List<string> measurements = new List<string>();
        float started;
        bool finished;
        MimoExperience experience;
        PetActor actor;
        Camera sceneCamera;

        static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "--mimo-interaction-test") >= 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void IsolateBeforeSceneAwake()
        {
            if (!Enabled) return;
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--mimo-test-output");
            outputDirectory = index >= 0 && index + 1 < args.Length
                ? Path.GetFullPath(args[index + 1])
                : Path.Combine(Application.temporaryCachePath, "MimoInteractionValidation");
            // A fresh path also prevents a previous test from skipping the creation flow.
            PetStorage.SavePathOverride = Path.Combine(outputDirectory,
                "isolated-interaction-pet-" + DateTime.UtcNow.Ticks + ".json");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Enabled) new GameObject("Mimo opt-in interaction verification").AddComponent<MimoInteractionCheck>();
        }

        void Awake()
        {
            Application.runInBackground = true;
            started = Time.realtimeSinceStartup;
            Application.logMessageReceived += OnLog;
        }

        void OnDestroy() { Application.logMessageReceived -= OnLog; }

        void OnLog(string message, string stack, LogType kind)
        {
            if ((kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) && !errors.Contains(message))
                errors.Add(message);
        }

        void Update()
        {
            if (!finished && Time.realtimeSinceStartup - started > 110f)
            {
                errors.Add("Interaction verification timed out after 110 seconds.");
                Finish();
            }
        }

        void Check(bool condition, string description)
        {
            if (condition) checks.Add(description);
            else errors.Add(description);
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(outputDirectory);
            yield return new WaitForSecondsRealtime(1.2f);
            experience = FindAnyObjectByType<MimoExperience>();
            Check(experience != null, "The interactive experience is present.");
            Check(Screen.width > Screen.height, "The player is in landscape orientation.");
            Check(!string.IsNullOrWhiteSpace(PetStorage.SavePathOverride) &&
                  Path.GetFullPath(PetStorage.SavePath).StartsWith(Path.GetFullPath(outputDirectory), StringComparison.OrdinalIgnoreCase),
                "Save isolation was configured before the scene started.");
            if (experience == null) { Finish(); yield break; }

            yield return experience.RunSmokeTest();
            actor = experience.Actor;
            sceneCamera = Camera.main;
            Check(actor != null && sceneCamera != null && experience.CurrentScreen == "Home",
                "Creation reaches the care room with a live character and camera.");
            if (actor == null || sceneCamera == null) { Finish(); yield break; }

            actor.Initialize(new PetSave
            {
                created = true, petName = "Mimo", colorIndex = 0, earsIndex = 0, tailIndex = 0,
                hunger = .80f, cleanliness = 1f, joy = .90f, recipeSeed = 7359
            });
            yield return new WaitForSecondsRealtime(1.8f);
            Check(actor.Data.colorIndex == 0 && actor.Data.earsIndex == 0 && actor.Data.tailIndex == 0,
                "Interaction checks use the original pink body without optional appendages.");

            yield return CheckCareButton("Comidinha", "Food", "interaction-food.png");
            yield return CheckCareButton("Banho", "Bath", "interaction-bath.png");
            yield return CheckCareButton("Brincar", "Play", "interaction-play.png");

            Vector3 originalPosition = actor.transform.position;
            Quaternion originalRotation = actor.transform.rotation;
            yield return CheckDragging(originalPosition);
            actor.transform.position = originalPosition;
            actor.transform.rotation = originalRotation;
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(.3f);
            yield return CheckSquashing();
            yield return CheckRotation();
            experience.ResetPetInteraction();
            actor.transform.position = originalPosition;
            actor.transform.rotation = originalRotation;
            actor.SetExpression("happy");
            yield return new WaitForSecondsRealtime(1.4f);
            CheckComposition("Final play-room rest");
            yield return Capture("interaction-final-rest.png");
            Check(PetStorage.Save(actor.Data), "The isolated interaction state saves successfully.");
            yield return null;
            Finish();
        }

        IEnumerator CheckCareButton(string label, string expected, string screenshot)
        {
            Button button = FindObjectsByType<Button>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.gameObject.activeInHierarchy && candidate.interactable &&
                    candidate.GetComponentsInChildren<Text>(true).Any(text => text.text.Trim() == label));
            Check(button != null, "The visible " + label + " button can be found.");
            if (button == null) yield break;
            button.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.2f);
            Check(experience.ActiveEnvironment == expected,
                label + " switches through its actual UI event to the " + expected + " environment.");
            Check(experience.ActiveCareEnvironmentCount == 1,
                expected + " has exactly one active care environment.");
            CheckComposition(expected + " at rest");
            yield return Capture(screenshot);
        }

        IEnumerator CheckDragging(Vector3 groundPosition)
        {
            ClickInteractionMode("Mover");
            Check(experience.InteractionMode == "Mover", "The move interaction mode is selected.");
            Vector2 start;
            bool hitFound = FindBodyHit(out start);
            Check(hitFound, "A camera ray hits the visible body for the move gesture.");
            if (!hitFound) yield break;
            bool began = experience.BeginPetGesture(start);
            Check(began, "A touch beginning on the body starts the shared gesture path.");
            if (!began) yield break;
            float beganAt = Time.realtimeSinceStartup;
            Vector2 destination = start + new Vector2(-Screen.width * .10f, Screen.height * .12f);
            while (Time.realtimeSinceStartup - beganAt < .65f)
            {
                float fraction = Mathf.Clamp01((Time.realtimeSinceStartup - beganAt) / .35f);
                experience.MovePetGesture(Vector2.Lerp(start, destination, fraction));
                yield return null;
            }
            float horizontalDistance = Vector2.Distance(new Vector2(actor.transform.position.x, actor.transform.position.z),
                new Vector2(groundPosition.x, groundPosition.z));
            measurements.Add("Drag horizontal distance: " + horizontalDistance.ToString("F3") + "; lift: " + actor.LiftHeight.ToString("F3"));
            Check(experience.IsPetDragging && actor.IsBeingCarried,
                "Dragging remains active across frames without real mouse input cancelling the synthetic gesture.");
            Check(horizontalDistance > .15f, "Dragging changes the character's actual world position.");
            Check(actor.LiftHeight > .04f, "The carried character lifts above its resting floor.");
            yield return Capture("interaction-drag-lift.png");
            experience.EndPetGesture();
            yield return new WaitForSecondsRealtime(1.8f);
            Check(!experience.IsPetDragging && !actor.IsBeingCarried, "Releasing the touch ends carrying.");
            Check(actor.LiftHeight < .04f && Mathf.Abs(actor.transform.position.y - groundPosition.y) < .04f,
                "The released character settles onto the same ground plane.");
            Check(float.IsFinite(actor.transform.position.x) && float.IsFinite(actor.ModelRoot.lossyScale.y),
                "Drag and drop keep the transform finite.");
            yield return Capture("interaction-drop-rest.png");
        }

        IEnumerator CheckSquashing()
        {
            ClickInteractionMode("Amassar");
            Check(experience.InteractionMode == "Amassar", "The squash interaction mode is selected.");
            Vector2 start;
            if (!FindBodyHit(out start)) { Check(false, "A visible body hit exists for squashing."); yield break; }
            float normalHeight = VisibleBounds().size.y;
            bool began = experience.BeginPetGesture(start);
            Check(began, "The squash gesture begins from an actual body hit.");
            if (!began) yield break;
            float beganAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - beganAt < .65f)
            {
                experience.MovePetGesture(start + new Vector2(14f, -36f));
                yield return null;
            }
            float squashedHeight = VisibleBounds().size.y;
            measurements.Add("Squash height before: " + normalHeight.ToString("F3") + "; held: " + squashedHeight.ToString("F3") +
                "; squash amount: " + actor.SquashAmount.ToString("F3"));
            Check(actor.SquashAmount > .15f && squashedHeight < normalHeight * .91f,
                "Holding in squash mode visibly compresses the character's geometry.");
            Check(!actor.IsBeingCarried, "Squashing does not unintentionally pick up the character.");
            yield return Capture("interaction-squish.png");
            experience.EndPetGesture();
            yield return new WaitForSecondsRealtime(1.6f);
            float restoredHeight = VisibleBounds().size.y;
            Check(Mathf.Abs(actor.SquashAmount) < .055f && Mathf.Abs(restoredHeight - normalHeight) < normalHeight * .10f,
                "After release the squash spring returns to the original height.");
            yield return Capture("interaction-squish-released.png");
        }

        IEnumerator CheckRotation()
        {
            ClickInteractionMode("Girar");
            Check(experience.InteractionMode == "Girar", "The rotate interaction mode is selected.");
            Vector2 start;
            if (!FindBodyHit(out start)) { Check(false, "A visible body hit exists for rotation."); yield break; }
            Quaternion before = actor.transform.rotation;
            Vector3 positionBefore = actor.transform.position;
            bool began = experience.BeginPetGesture(start);
            Check(began, "The rotate gesture begins from an actual body hit.");
            if (!began) yield break;
            float beganAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - beganAt < .5f)
            {
                float fraction = Mathf.Clamp01((Time.realtimeSinceStartup - beganAt) / .35f);
                experience.MovePetGesture(start + Vector2.right * (Screen.width * .12f * fraction));
                yield return null;
            }
            float rotation = Quaternion.Angle(before, actor.transform.rotation);
            measurements.Add("Rotation gesture degrees: " + rotation.ToString("F2"));
            Check(rotation > 10f, "A horizontal rotation gesture changes the character's yaw.");
            Check(Vector3.Distance(positionBefore, actor.transform.position) < .04f && !actor.IsBeingCarried,
                "Rotation leaves the character standing in the same place.");
            experience.EndPetGesture();
            yield return Capture("interaction-rotated.png");
        }

        void ClickInteractionMode(string label)
        {
            Button button = FindObjectsByType<Button>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.gameObject.activeInHierarchy && candidate.interactable &&
                    candidate.GetComponentsInChildren<Text>(true).Any(text => text.text.Trim() == label));
            Check(button != null, "The visible " + label + " interaction-mode button can be found.");
            if (button != null) button.onClick.Invoke();
        }

        bool FindBodyHit(out Vector2 screenPoint)
        {
            Physics.SyncTransforms();
            Bounds bounds = VisibleBounds();
            Vector3 center = sceneCamera.WorldToScreenPoint(bounds.center);
            float[] offsets = { 0f, -.12f, .12f, -.25f, .25f };
            foreach (float vertical in offsets)
                foreach (float horizontal in offsets)
                {
                    Vector2 candidate = new Vector2(center.x + horizontal * Screen.height, center.y + vertical * Screen.height);
                    Ray ray = sceneCamera.ScreenPointToRay(candidate);
                    RaycastHit hit;
                    if (Physics.Raycast(ray, out hit, 40f) && hit.collider.GetComponentInParent<PetActor>() == actor)
                    {
                        screenPoint = candidate;
                        return true;
                    }
                }
            screenPoint = Vector2.zero;
            return false;
        }

        Bounds VisibleBounds()
        {
            Renderer[] renderers = actor.ModelRoot.GetComponentsInChildren<Renderer>(false);
            Bounds bounds = new Bounds(actor.transform.position, Vector3.zero);
            bool first = true;
            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        void CheckComposition(string context)
        {
            Bounds bounds = VisibleBounds();
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            bool inFront = true;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = sceneCamera.WorldToScreenPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                        inFront &= corner.z > sceneCamera.nearClipPlane;
                        minimum = Vector2.Min(minimum, new Vector2(corner.x, corner.y));
                        maximum = Vector2.Max(maximum, new Vector2(corner.x, corner.y));
                    }
            Rect projected = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
            RectTransform sidebar = FindObjectsByType<RectTransform>(FindObjectsSortMode.None)
                .FirstOrDefault(rect => rect.name == "Touch tray" && rect.gameObject.activeInHierarchy);
            Check(sidebar != null, context + ": the care sidebar exists.");
            Rect cameraViewport = sceneCamera.pixelRect;
            Check(inFront && projected.xMin >= cameraViewport.xMin && projected.yMin >= cameraViewport.yMin &&
                  projected.xMax <= cameraViewport.xMax && projected.yMax <= cameraViewport.yMax,
                context + ": the complete renderer bounds fit inside the camera viewport.");
            if (sidebar == null) return;
            Vector3[] corners = new Vector3[4];
            sidebar.GetWorldCorners(corners);
            Vector2 sidebarMinimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 sidebarMaximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector3 corner in corners)
            {
                Vector2 screenCorner = RectTransformUtility.WorldToScreenPoint(null, corner);
                sidebarMinimum = Vector2.Min(sidebarMinimum, screenCorner);
                sidebarMaximum = Vector2.Max(sidebarMaximum, screenCorner);
            }
            Rect sidebarRect = Rect.MinMaxRect(sidebarMinimum.x, sidebarMinimum.y, sidebarMaximum.x, sidebarMaximum.y);
            measurements.Add(context + " actor px=" + projected + "; camera px=" + cameraViewport + "; sidebar px=" + sidebarRect);
            Check(sidebarRect.center.x > Screen.width * .7f && sidebarRect.height > sidebarRect.width,
                context + ": care controls occupy a vertical sidebar on the right.");
            Check(!projected.Overlaps(sidebarRect), context + ": the character stays completely outside the sidebar.");
        }

        IEnumerator Capture(string filename)
        {
            yield return new WaitForEndOfFrame();
            Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
            if (image)
            {
                File.WriteAllBytes(Path.Combine(outputDirectory, filename), image.EncodeToPNG());
                Destroy(image);
                screenshots.Add(filename);
            }
            else errors.Add("Could not capture " + filename);
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            var report = new Report
            {
                passed = errors.Count == 0, checks = checks.ToArray(), errors = errors.ToArray(),
                screenshots = screenshots.ToArray(), measurements = measurements.ToArray(),
                version = Application.unityVersion, width = Screen.width, height = Screen.height,
                elapsedSeconds = Time.realtimeSinceStartup - started, isolatedSavePath = PetStorage.SavePath
            };
            try
            {
                Directory.CreateDirectory(outputDirectory);
                File.WriteAllText(Path.Combine(outputDirectory, "interaction-checks.json"), JsonUtility.ToJson(report, true));
            }
            catch (Exception exception) { Debug.LogError("Interaction report could not be written: " + exception.Message); }
            Debug.Log("MIMO_INTERACTION " + (report.passed ? "PASS" : "FAIL") + " checks=" + checks.Count + " errors=" + errors.Count);
            Application.Quit(report.passed ? 0 : 1);
        }
    }
}
