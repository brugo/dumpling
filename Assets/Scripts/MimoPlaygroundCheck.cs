using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MimoGarden
{
    /// <summary>Opt-in integration acceptance through actual creation, selection, gesture and bath paths.</summary>
    public sealed class MimoPlaygroundCheck : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed, ordinarySaveUnchanged, mobileProfile;
            public string version, unityVersion, device, gpu, isolatedSavePath, captureMethod;
            public int width, height;
            public float elapsedSeconds;
            public float fiveFriendsAverageFps, fiveFriendsMinimumWindowFps, fiveFriendsP95Milliseconds;
            public string[] checks, errors, screenshots, measurements;
        }

        static string output, ordinaryPath, isolatedPath;
        static byte[] ordinaryBytes, backupBytes;
        static bool ordinaryExisted, backupExisted;
        static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "--mimo-playground-test") >= 0;
        readonly List<string> checks = new List<string>(), errors = new List<string>(), screenshots = new List<string>(), measurements = new List<string>();
        MimoExperience experience;
        Camera sceneCamera;
        float started;
        bool finished;
        float fiveFriendsAverageFps, fiveFriendsMinimumWindowFps, fiveFriendsP95Milliseconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Isolate()
        {
            if (!Enabled) return;
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--mimo-test-output");
            output = index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1]) : Path.Combine(Application.temporaryCachePath, "MimoPlaygroundValidation");
            Directory.CreateDirectory(output);
            ordinaryPath = Path.Combine(Application.persistentDataPath, "mimo-pet.json");
            ordinaryExisted = File.Exists(ordinaryPath); ordinaryBytes = ordinaryExisted ? File.ReadAllBytes(ordinaryPath) : null;
            backupExisted = File.Exists(ordinaryPath + ".bak"); backupBytes = backupExisted ? File.ReadAllBytes(ordinaryPath + ".bak") : null;
            isolatedPath = Path.Combine(output, "isolated-playground-" + DateTime.UtcNow.Ticks + ".json");
            PetStorage.SavePathOverride = isolatedPath;
            // A real 0.4.1 single-friend save exercises startup migration, not a prebuilt new-format fixture.
            File.WriteAllText(isolatedPath, "{\"schemaVersion\":3,\"petName\":\"Aurora\",\"colorIndex\":4,\"earsIndex\":1,\"tailIndex\":2,\"eyeIndex\":2,\"glassesIndex\":0,\"hatIndex\":0,\"wingsIndex\":0,\"finishIndex\":0,\"created\":true,\"hunger\":0.62,\"cleanliness\":0.73,\"joy\":0.82,\"savedUtcTicks\":0,\"recipeSeed\":41309}");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (Enabled) new GameObject("Mimo playground acceptance").AddComponent<MimoPlaygroundCheck>(); }
        void Awake() { started = Time.realtimeSinceStartup; Application.runInBackground = true; Application.logMessageReceived += Log; }
        void OnDestroy() { Application.logMessageReceived -= Log; }
        void Log(string message, string stack, LogType type)
        { if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !errors.Contains(message)) errors.Add(message); }
        void Check(bool pass, string description) { if (pass) checks.Add(description); else errors.Add(description); }
        void Update() { if (!finished && Time.realtimeSinceStartup - started > 240f) { errors.Add("Playground acceptance timed out."); Finish(); } }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(1.2f);
            experience = FindAnyObjectByType<MimoExperience>(); sceneCamera = Camera.main;
            Check(experience && sceneCamera && experience.CurrentScreen == "Home", "The existing single friend opens directly into the real playground.");
            Check(PetStorage.SavePath == isolatedPath, "Storage isolation is installed before the experience starts.");
            if (!experience || !sceneCamera) { Finish(); yield break; }
            Check(experience.PlaygroundPets.Count == 1 && experience.Actor.Data.petName == "Aurora", "Startup migration retains the child's original creation.");
            PetActor original = experience.Actor;
            PetSave migrated = PetStorage.Load();
            Check(migrated.created && migrated.colorIndex == 4 && migrated.earsIndex == 1 && migrated.tailIndex == 2 && migrated.eyeIndex == 2 && migrated.recipeSeed == 41309,
                "Migration preserves appearance, name, creation flag and recipe identity.");
            Check(Mathf.Abs(migrated.hunger - .62f) < .005f && Mathf.Abs(migrated.cleanliness - .73f) < .005f && Mathf.Abs(migrated.joy - .82f) < .005f,
                "Migration preserves the original care values.");
            string originalId = migrated.petId;
            Check(!string.IsNullOrWhiteSpace(originalId) && PetStorage.Load().petId == originalId, "The migrated friend receives a stable persisted ID.");
            CheckStorageFixtures();

            yield return Create("Lumi", 1, 2);
            Check(experience.PlaygroundPets.Count == 2 && experience.PlaygroundPets.Contains(original), "A second confirmed creation keeps the first live actor.");
            yield return Create("Pingo", 7, 3);
            Check(experience.PlaygroundPets.Count == 3 && experience.PlaygroundPets.Contains(original), "A third confirmed creation leaves all five friends together.");
            yield return Create("Brisa",4,0);
            yield return Create("Mel",2,4);
            Check(experience.PlaygroundPets.Count == 5 && experience.PlaygroundPets.Contains(original), "Five confirmed creations remain together without replacing anyone.");
            Check(experience.HasThemeParkBackdrop, "The generated theme park image is loaded into the real playground.");
            CheckVisibleRoster("The first five friends");
            yield return Capture("01-three-creations.png");
            string[] beforeCancel = experience.PlaygroundPets.Select(p => p.Data.petId).ToArray();
            string selectedBeforeCancel = experience.Actor.Data.petId;
            experience.EnterLaboratory(); yield return null;
            yield return MimoRitualCheck.AddAndStir(experience, 0, 5);
            experience.EnterHome(); yield return new WaitForSecondsRealtime(.4f);
            Check(beforeCancel.SequenceEqual(experience.PlaygroundPets.Select(p => p.Data.petId)) && experience.Actor.Data.petId == selectedBeforeCancel,
                "Leaving an unfinished recipe preserves all five identities and the selected friend.");
            yield return Create("Nuvem", 3, 1);
            PetActor[] pets = experience.PlaygroundPets.ToArray();
            Check(pets.Length == 5 && pets.Select(p => p.Data.petName).SequenceEqual(new[] { "Lumi", "Pingo", "Brisa", "Mel", "Nuvem" }),
                "Replacing the explicitly chosen oldest friend keeps the other four and the new creation, in creation order.");
            Check(pets.All(p => p.Data.petId != originalId), "Only the explicitly chosen creation leaves the playable roster.");
            Check(PetStorage.LoadCollection().pets.Select(p => p.petId).SequenceEqual(pets.Select(p => p.Data.petId)), "The complete five-friend roster survives a disk reload.");
            CheckVisibleRoster("The latest five friends");
            if (pets.Length != 5) { Finish(); yield break; }
            yield return Capture("02-five-friends-theme-park.png");

            foreach (PetActor pet in pets)
            {
                if (FindBodyHit(pet, out Vector2 touch))
                {
                    Check(experience.BeginPetGesture(touch), "Touching the visible body acquires " + pet.Data.petName + ".");
                    Check(experience.Actor == pet, "Scene touch selects the correct creation: " + pet.Data.petName + ".");
                    experience.EndPetGesture();
                }
                else Check(false, "A screen ray can reach " + pet.Data.petName + " without an overlapping creation blocking it.");
                yield return new WaitForSecondsRealtime(.2f);
            }
            yield return CheckMovement(pets[0]);
            yield return CheckActions(pets);
            yield return CheckProps(pets);
            yield return CheckIndependentCare(pets);

            Click("Brincar"); yield return new WaitForSecondsRealtime(.5f);
            CheckVisibleRoster("Returning from each friend's bath");
            yield return CheckFiveFriendsPerformance();
            experience.SendMessage("SaveCare");
            PetCollection finalCollection = PetStorage.LoadCollection();
            Check(finalCollection.pets.Count == 5 && finalCollection.activePetId == experience.Actor.Data.petId,
                "Saving the group retains all five friends and the active selection.");
            foreach (PetActor pet in pets)
            {
                PetSave reloaded = finalCollection.pets.FirstOrDefault(p => p.petId == pet.Data.petId);
                Check(reloaded != null && reloaded.petName == pet.Data.petName && reloaded.finishIndex == pet.Data.finishIndex && Mathf.Abs(reloaded.cleanliness - pet.Data.cleanliness) < .01f,
                    "The independent identity, material and care state reload correctly for " + pet.Data.petName + ".");
            }
            yield return Capture("12-five-friends-after-care.png");
            Finish();
        }

        IEnumerator Create(string name, int color, int finish)
        {
            experience.EnterLaboratory(); yield return null;
            yield return MimoRitualCheck.AddAndStir(experience, 0, color);
            yield return MimoRitualCheck.AddAndStir(experience, 7, finish);
            Check(experience.CanFinalizeRecipe, "The real mixed recipe can create " + name + ".");
            experience.BeginBirth();
            float deadline = Time.realtimeSinceStartup + 6f;
            while (experience.CurrentScreen == "Birth" && Time.realtimeSinceStartup < deadline) yield return null;
            Check(experience.CurrentScreen == "Naming", "The surprise reveal reaches naming for " + name + ".");
            InputField field = FindObjectsByType<InputField>(FindObjectsSortMode.None).FirstOrDefault(f => f.gameObject.activeInHierarchy);
            Check(field, "The visible name field exists for " + name + ".");
            if (field) field.text = name;
            PetActor replacement = experience.PlaygroundPets.Count == PetStorage.MaxPets ? experience.PlaygroundPets[0] : null;
            experience.ConfirmName(); yield return null;
            if (replacement)
            {
                Check(experience.IsChoosingReplacement, "A full playground asks which friend to replace before committing " + name + ".");
                experience.ChooseFriendToReplace(replacement);
                Check(experience.PendingRemovalPetId == replacement.Data.petId, "The chosen replacement is identified by its persistent ID.");
                experience.ConfirmFriendRemoval();
            }
            yield return new WaitForSecondsRealtime(.35f);
            Check(experience.CurrentScreen == "Home" && experience.Actor.Data.petName == name && experience.Actor.Data.colorIndex == color && experience.Actor.Data.finishIndex == finish,
                "Confirmation commits and selects the complete new recipe: " + name + ".");
        }

        IEnumerator CheckFiveFriendsPerformance()
        {
            CheckRenderedContactShadows();
            foreach (PetActor pet in experience.PlaygroundPets) { pet.ClearSoap(); experience.SetPetAction(pet,MimoExperience.PlaygroundAction.Tag); }
            yield return new WaitForSecondsRealtime(3f);
            var frames = new List<float>(); float windowSeconds = 0, totalSeconds = 0; int windowFrames = 0;
            fiveFriendsMinimumWindowFps = float.MaxValue;
            float until = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                float dt = Time.unscaledDeltaTime; frames.Add(dt); totalSeconds += dt; windowSeconds += dt; windowFrames++;
                if (windowSeconds >= 1f) { fiveFriendsMinimumWindowFps = Mathf.Min(fiveFriendsMinimumWindowFps,windowFrames/windowSeconds); windowSeconds = 0; windowFrames = 0; }
            }
            frames.Sort(); fiveFriendsAverageFps = frames.Count/Mathf.Max(.001f,totalSeconds);
            fiveFriendsP95Milliseconds = frames[Mathf.Clamp(Mathf.CeilToInt(frames.Count*.95f)-1,0,frames.Count-1)]*1000;
            measurements.Add("Five friends playing together: average FPS="+fiveFriendsAverageFps.ToString("F2")+"; minimum 1s window FPS="+fiveFriendsMinimumWindowFps.ToString("F2")+"; p95 frame ms="+fiveFriendsP95Milliseconds.ToString("F2")+".");
            Check(experience.MobileVisibleContactShadows == 5,"All five moving friends retain their soft contact shadows.");
            if (Application.isMobilePlatform)
                Check(fiveFriendsAverageFps >= 30 && fiveFriendsMinimumWindowFps >= 30 && fiveFriendsP95Milliseconds <= 33.334f,"Five friends with shadows stay above 30 FPS throughout the measured physical-tablet windows.");
            yield return Capture("13-five-playing-with-shadows.png");
            foreach (PetActor pet in experience.PlaygroundPets) experience.StopPetAction(pet);
        }

        void CheckRenderedContactShadows()
        {
            MeshRenderer[] contacts = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(r => r.gameObject.activeInHierarchy && r.gameObject.name.StartsWith("Mimo soft ground contact ")).ToArray();
            Texture2D shaded = null, plain = null;
            try
            {
                shaded = MimoRenderCapture.Scene(sceneCamera);
                foreach (MeshRenderer contact in contacts) contact.enabled = false;
                plain = MimoRenderCapture.Scene(sceneCamera);
                Color32[] a = shaded.GetPixels32(), b = plain.GetPixels32(); int darkened = 0;
                for (int i = 0; i < a.Length; i++)
                    if (b[i].r+b[i].g+b[i].b-a[i].r-a[i].g-a[i].b >= 12) darkened++;
                Check(contacts.Length == 5 && darkened > shaded.width*shaded.height*.0005f,
                    "Five contact shadows visibly darken the rendered park ground ("+darkened+" pixels), beyond merely existing in the scene.");
                measurements.Add("Visible contact-shadow pixels: "+darkened+" at "+shaded.width+"x"+shaded.height+".");
            }
            finally
            {
                foreach (MeshRenderer contact in contacts) contact.enabled = true;
                if (shaded) Destroy(shaded); if (plain) Destroy(plain);
            }
        }

        void CheckStorageFixtures()
        {
            string savedPath = PetStorage.SavePathOverride;
            try
            {
                PetStorage.SavePathOverride = Path.Combine(output, "storage-fixture-" + DateTime.UtcNow.Ticks + ".json");
                var oldest = new PetSave { created = true, petName = "Fixture 0", hunger = .31f, cleanliness = .41f };
                Check(PetStorage.Save(oldest), "An isolated first creation can be saved.");
                string firstId = oldest.petId;
                for (int i = 1; i < PetStorage.MaxPets; i++) Check(PetStorage.Save(new PetSave { created = true, petName = "Fixture " + i, recipeSeed = 8000 + i, hunger = .4f + i * .1f, cleanliness = .3f + i * .1f }), "The storage layer accepts distinct creation " + i + ".");
                var fourth = new PetSave { created = true, petName = "Fixture 5", recipeSeed = 8005, hunger = .7f, cleanliness = .6f };
                byte[] fullBefore = File.ReadAllBytes(PetStorage.SavePath);
                Check(!PetStorage.Save(fourth) && fullBefore.SequenceEqual(File.ReadAllBytes(PetStorage.SavePath)), "A sixth creation without an explicit replacement leaves the full save untouched.");
                Check(PetStorage.Save(fourth, firstId), "Storage commits the sixth creation only with an explicit replacement ID.");
                PetCollection collection = PetStorage.LoadCollection();
                Check(collection.pets.Count == 5 && collection.pets.Select(p => p.petName).SequenceEqual(new[] { "Fixture 1", "Fixture 2", "Fixture 3", "Fixture 4", "Fixture 5" }), "Replacing the chosen oldest ID retains the other four complete recipes and the new friend.");
                Check(collection.pets.Select(p => p.petId).Distinct().Count() == 5 && collection.pets.All(p => !string.IsNullOrWhiteSpace(p.petId)), "Separate creations have stable distinct IDs, even without hand-authored identifiers.");
                Check(!PetStorage.Save(oldest) && PetStorage.LoadCollection().pets.All(p => p.petId != firstId), "A late save from an evicted actor cannot resurrect it or evict a newer friend.");
                string[] order = collection.pets.Select(p => p.petId).ToArray();
                collection.pets[0].hunger = .27f; collection.activePetId = collection.pets[0].petId;
                Check(PetStorage.SaveCollection(collection), "Independent care and selection save as one collection.");
                PetCollection reload = PetStorage.LoadCollection();
                Check(order.SequenceEqual(reload.pets.Select(p => p.petId)) && reload.activePetId == order[0] && Mathf.Abs(reload.pets[0].hunger - .27f) < .001f && Mathf.Abs(reload.pets[1].hunger - .6f) < .001f,
                    "Updating one friend preserves creation order and another friend's care values.");
                Check(PetStorage.SaveCollection(reload), "The collection writes a complete recovery backup.");
                File.WriteAllText(PetStorage.SavePath, "{incomplete");
                PetCollection recovered = PetStorage.LoadCollection();
                Check(order.SequenceEqual(recovered.pets.Select(p => p.petId)) && recovered.activePetId == order[0], "A corrupt primary file recovers the complete roster and selection from its backup.");
            }
            finally { PetStorage.SavePathOverride = savedPath; }
        }

        IEnumerator CheckMovement(PetActor pet)
        {
            experience.SetInteractionMode("Mover"); experience.SelectPet(pet);
            yield return DragTo(pet, new Vector3(-4.7f, .105f, -1.55f));
            Vector3 left = pet.transform.position;
            yield return DragTo(pet, new Vector3(4.7f, .105f, -1.55f));
            Vector3 right = pet.transform.position;
            Check(right.x - left.x > 5.5f, "Dragging reaches a broad lateral area of the playground.");
            yield return DragTo(pet, new Vector3(0, .105f, 4.6f));
            Vector3 back = pet.transform.position; float backHeight = ProjectedHeight(pet);
            yield return Capture("03-friend-at-back.png");
            yield return DragTo(pet, new Vector3(0, .105f, -4.6f));
            Vector3 front = pet.transform.position; float frontHeight = ProjectedHeight(pet);
            Check(back.z - front.z > 8f, "Dragging reaches the back and near foreground through real perspective depth.");
            Check(backHeight < frontHeight * .88f, "The same creation appears smaller at the back of the room.");
            Check(!experience.IsPetDragging && !pet.IsBeingCarried && pet.LiftHeight < .05f, "Releasing after a long drag returns the creation to the floor.");
            Vector3 limited = experience.ClampPlaygroundPosition(new Vector3(100, .105f, 100));
            Check(Finite(limited) && limited.x <= 5.6f && limited.z <= 5.1f, "Extreme pointer motion stays within the playable world.");
            Vector3 nearLimit = experience.ClampPlaygroundPosition(new Vector3(-100, .105f, -100));
            Check(Finite(nearLimit) && nearLimit.x >= -5.6f && nearLimit.z >= -5.1f, "The foreground and left boundaries remain safe during extreme pointer motion.");
            measurements.Add("Drag positions left=" + left + "; right=" + right + "; back=" + back + "; front=" + front + "; apparent heights back=" + backHeight.ToString("F1") + "px front=" + frontHeight.ToString("F1") + "px.");
            yield return Capture("04-friend-at-front.png");
        }

        IEnumerator CheckActions(PetActor[] pets)
        {
            PetActor jumping = pets[0], running = pets[1];
            experience.SelectPet(jumping); Click("Pular sem parar");
            Check(experience.GetPetAction(jumping) == MimoExperience.PlaygroundAction.Jump, "The selected creation starts continuous jumping through its action menu.");
            experience.SelectPet(pets[2]);
            int takeoffs = 0; bool airborne = false; float maximum = 0;
            for (float elapsed = 0; elapsed < 3.8f; elapsed += Time.unscaledDeltaTime)
            {
                maximum = Mathf.Max(maximum, jumping.LiftHeight);
                bool next = jumping.LiftHeight > .10f; if (next && !airborne) takeoffs++; airborne = next;
                yield return null;
            }
            Check(takeoffs >= 3 && maximum > .2f && experience.GetPetAction(jumping) == MimoExperience.PlaygroundAction.Jump,
                "Continuous jumping repeats several takeoffs after a different friend is selected.");
            experience.SelectPet(running); Click("Pega-pega");
            Check(experience.GetPetAction(running) == MimoExperience.PlaygroundAction.Tag, "Another creation starts playing tag through its action menu while its friend jumps.");
            Vector3 previous = running.transform.position; float distance = 0, lift = 0; int hops = 0; bool raised = false;
            for (float elapsed = 0; elapsed < 5f; elapsed += Time.unscaledDeltaTime)
            {
                Vector3 now = running.transform.position; distance += Vector2.Distance(new Vector2(now.x, now.z), new Vector2(previous.x, previous.z)); previous = now;
                lift = Mathf.Max(lift, running.LiftHeight); bool next = running.LiftHeight > .08f; if (next && !raised) hops++; raised = next;
                yield return null;
            }
            Check(distance > 1.4f && lift > .1f && hops >= 2, "Tag travels around the map using repeated small forward hops.");
            Check(experience.GetPetAction(jumping) == MimoExperience.PlaygroundAction.Jump && experience.GetPetAction(running) == MimoExperience.PlaygroundAction.Tag,
                "Different friends keep independent simultaneous play actions.");
            measurements.Add("Continuous jump takeoffs=" + takeoffs + "; tag hops=" + hops + "; tag distance=" + distance.ToString("F2") + ".");
            yield return Capture("05-independent-play-actions.png");
            experience.SelectPet(running); Click("Descansar");
            foreach (PetActor pet in pets.Where(p => p != running)) experience.StopPetAction(pet);
            yield return new WaitForSecondsRealtime(.9f);
            Vector3 stopped = running.transform.position; yield return new WaitForSecondsRealtime(.8f);
            Check(experience.GetPetAction(running) == MimoExperience.PlaygroundAction.Idle && Vector3.Distance(stopped, running.transform.position) < .03f,
                "Stopping play ends autonomous movement.");
        }

        IEnumerator CheckProps(PetActor[] pets)
        {
            MimoPlaygroundProp[] props = FindObjectsByType<MimoPlaygroundProp>(FindObjectsSortMode.None);
            MimoPlaygroundProp swing = props.FirstOrDefault(p => p.Kind == MimoPlaygroundProp.PropKind.Swing);
            MimoPlaygroundProp seat = props.FirstOrDefault(p => p.Kind == MimoPlaygroundProp.PropKind.Seat);
            MimoPlaygroundProp trampoline = props.FirstOrDefault(p => p.Kind == MimoPlaygroundProp.PropKind.Trampoline);
            Check(swing && seat && trampoline, "The finished playground has a real swing, seating and trampoline.");
            if (!swing) yield break;
            Vector3 destination = swing.SeatAnchor.position; destination.y = .105f;
            yield return DragTo(pets[0], destination);
            Check(swing.Occupant == pets[0], "Dropping a creation over the swing attaches that exact friend.");
            bool hitSwingingFriend = FindBodyHit(pets[0], out Vector2 swingingTouch);
            Check(hitSwingingFriend && experience.BeginPetGesture(swingingTouch), "A friend on the swing can be selected by an ordinary tap.");
            experience.EndPetGesture(); yield return null;
            Check(swing.Occupant == pets[0], "Tapping the swinging friend does not remove it from its seat.");
            Quaternion firstAngle = swing.MotionPivot.rotation; Vector3 firstPosition = pets[0].transform.position;
            experience.SelectPet(pets[1]); yield return new WaitForSecondsRealtime(.73f);
            Check(swing.Occupant == pets[0] && Quaternion.Angle(firstAngle, swing.MotionPivot.rotation) > 2f && Vector3.Distance(firstPosition, pets[0].transform.position) > .025f,
                "The occupied swing keeps moving with its friend after another creation is selected.");
            Check(!experience.TryPlaceOnProp(pets[1], swing) && swing.Occupant == pets[0], "A second creation cannot silently replace the occupied swing.");
            if (seat) Check(experience.TryPlaceOnProp(pets[1], seat) && seat.Occupant == pets[1], "A second friend can sit while the first swings.");
            if (trampoline) Check(experience.TryPlaceOnProp(pets[2], trampoline) && trampoline.Occupant == pets[2], "A third friend can use the trampoline independently.");
            yield return new WaitForSecondsRealtime(.5f);
            yield return Capture("06-three-interactive-props.png");
            experience.SelectPet(pets[2]); Click("Banho"); yield return new WaitForSecondsRealtime(.35f);
            Check(swing.Occupant == pets[0] && (!seat || seat.Occupant == pets[1]),
                "Taking a different friend to the bath keeps the swing and seating assignments.");
            Click("Brincar"); yield return new WaitForSecondsRealtime(.45f);
            Check(swing.Occupant == pets[0] && Vector3.Distance(pets[0].transform.position, swing.SeatAnchor.position) < .05f,
                "Returning to the playground resumes the same friend on the moving swing.");
            yield return DragTo(pets[0], new Vector3(-1.6f, .105f, .5f));
            Check(!swing.Occupant && !pets[0].IsBeingCarried && pets[0].transform.position.y < .2f,
                "Dragging the swinging friend out releases the seat and returns it to the ground.");
            foreach (MimoPlaygroundProp prop in props) if (prop.Occupant) prop.Release(prop.Occupant);
            yield return Capture("07-removed-from-swing.png");
        }

        IEnumerator CheckIndependentCare(PetActor[] pets)
        {
            for (int i = 0; i < pets.Length; i++) { pets[i].AddDirt(.28f + i * .14f); pets[i].ClearSoap(); }
            for (int index = 0; index < pets.Length; index++)
            {
                PetActor pet = pets[index]; Click(pet.Data.petName);
                Check(experience.Actor == pet, "The roster button selects " + pet.Data.petName + " for care.");
                if (experience.ActiveEnvironment != "Bath") Click("Banho");
                yield return new WaitForSecondsRealtime(.35f);
                Check(experience.Actor == pet && pet.gameObject.activeInHierarchy && pets.Where(p => p != pet).All(p => !p.gameObject.activeInHierarchy),
                    "The bath displays the selected creation and preserves the other friends in the group.");
                if (MimoPerformance.Lightweight) Check(experience.MobileVisibleContactShadows == 1, "The mobile bath has a contact shadow for its one visible friend.");
                float[] otherClean = pets.Select(p => p.Data.cleanliness).ToArray(); float dirty = pet.Data.cleanliness;
                yield return PaintBody(pet, true);
                float soap = pet.SoapCoverage;
                Check(soap > .15f && Mathf.Abs(pet.Data.cleanliness - dirty) < .008f, "Soap remains attached to " + pet.Data.petName + " without instantly removing dirt.");
                PetActor other = pets[(index + 1) % pets.Length];
                Click(other.Data.petName); yield return null; Click(pet.Data.petName); yield return null;
                Check(Mathf.Abs(pet.SoapCoverage - soap) < .008f, "Switching care targets preserves " + pet.Data.petName + "'s existing foam.");
                yield return Capture("08-bath-" + index + "-soaped.png");
                yield return PaintBody(pet, false);
                Check(pet.SoapCoverage < soap * .45f && pet.Data.cleanliness > dirty + .08f, "Rinsing cleans the selected creation: " + pet.Data.petName + ".");
                Check(Enumerable.Range(0, pets.Length).Where(i => i != index).All(i => Mathf.Abs(pets[i].Data.cleanliness - otherClean[i]) < .008f),
                    "Washing " + pet.Data.petName + " leaves the other two friends' cleanliness unchanged.");
            }
        }

        IEnumerator PaintBody(PetActor pet, bool soap)
        {
            for (int side = 0; side < 4; side++)
            {
                pet.transform.rotation = Quaternion.Euler(0, side * 90, 0); Physics.SyncTransforms(); yield return null;
                Bounds bounds = pet.VisibleBounds;
                Vector3 min = sceneCamera.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.center.z));
                Vector3 max = sceneCamera.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z));
                for (int row = 1; row < 10; row++) for (int col = 1; col < 10; col++)
                {
                    Vector2 point = new Vector2(Mathf.Lerp(min.x, max.x, col / 10f), Mathf.Lerp(min.y, max.y, row / 10f));
                    if (!Physics.Raycast(sceneCamera.ScreenPointToRay(point), out RaycastHit hit, 45f) || hit.collider.GetComponentInParent<PetActor>() != pet) continue;
                    if (soap) pet.SoapAt(hit, .7f); else pet.RinseAt(hit, 1f);
                }
                yield return new WaitForSecondsRealtime(.12f);
            }
            pet.transform.rotation = Quaternion.identity; yield return new WaitForSecondsRealtime(.15f);
        }

        IEnumerator DragTo(PetActor pet, Vector3 destination)
        {
            experience.SetInteractionMode("Mover"); experience.SelectPet(pet); Physics.SyncTransforms();
            bool hit = FindBodyHit(pet, out Vector2 start);
            Check(hit, "The drag begins on a real body hit for " + pet.Data.petName + "."); if (!hit) yield break;
            // Preserve the grabbed offset so the destination describes the root's floor position.
            Plane floor = new Plane(Vector3.up, new Vector3(0, .105f, 0));
            Ray ray = sceneCamera.ScreenPointToRay(start); floor.Raycast(ray, out float distance);
            Vector3 offset = ray.GetPoint(distance) - new Vector3(pet.transform.position.x, .105f, pet.transform.position.z);
            Vector2 end = sceneCamera.WorldToScreenPoint(destination + offset);
            bool began = experience.BeginPetGesture(start); Check(began, "The shared input path accepts the drag of " + pet.Data.petName + "."); if (!began) yield break;
            for (float elapsed = 0; elapsed < .95f; elapsed += Time.unscaledDeltaTime)
            { experience.MovePetGesture(Vector2.Lerp(start, end, Mathf.Clamp01(elapsed / .55f))); yield return null; }
            Check(pet.IsBeingCarried && experience.IsPetDragging, "Moving the pointer carries the selected creation above the ground.");
            experience.EndPetGesture(); yield return new WaitForSecondsRealtime(.75f);
            Check(Finite(pet.transform.position), "The released creation has a finite world position.");
        }

        bool FindBodyHit(PetActor pet, out Vector2 point)
        {
            Physics.SyncTransforms(); Bounds bounds = pet.VisibleBounds;
            Vector3 center = sceneCamera.WorldToScreenPoint(bounds.center);
            float[] offsets = { 0, -.18f, .18f, -.35f, .35f };
            float height = Mathf.Max(30, ProjectedHeight(pet));
            foreach (float y in offsets) foreach (float x in offsets)
            {
                Vector2 candidate = new Vector2(center.x + x * height, center.y + y * height);
                if (Physics.Raycast(sceneCamera.ScreenPointToRay(candidate), out RaycastHit hit, 45f) && hit.collider.GetComponentInParent<PetActor>() == pet)
                { point = candidate; return true; }
            }
            point = Vector2.zero; return false;
        }

        float ProjectedHeight(PetActor pet)
        {
            Bounds bounds = pet.VisibleBounds;
            return Mathf.Abs(sceneCamera.WorldToScreenPoint(bounds.center + Vector3.up * bounds.extents.y).y - sceneCamera.WorldToScreenPoint(bounds.center - Vector3.up * bounds.extents.y).y);
        }
        static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        void CheckVisibleRoster(string context)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(sceneCamera);
            Check(experience.ActiveEnvironment == "Play" && experience.PlaygroundPets.Count == 5, context + ": all five belong to the playground.");
            foreach (PetActor pet in experience.PlaygroundPets)
                Check(pet.gameObject.activeInHierarchy && pet.VisibleBounds.size.y > .5f && GeometryUtility.TestPlanesAABB(planes, pet.VisibleBounds), context + ": " + pet.Data.petName + " is rendered inside the camera view.");
            Check(experience.PlaygroundPets.Select(p => p.Data.petId).Distinct().Count() == 5, context + ": actors refer to five distinct persistent creations.");
            if (MimoPerformance.Lightweight) Check(experience.MobileVisibleContactShadows == 5, context + ": all five mobile friends have individual ground contact shadows.");
        }
        void Click(string label)
        {
            Button button = FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.gameObject.activeInHierarchy && b.interactable && b.GetComponentsInChildren<Text>(true).Any(t => t.text.Trim() == label));
            Check(button, "The visible " + label + " control is available."); if (button) button.onClick.Invoke();
        }
        IEnumerator Capture(string filename)
        {
            yield return new WaitForEndOfFrame(); Texture2D texture = MimoRenderCapture.Scene(sceneCamera);
            if (!texture) { errors.Add("Could not render " + filename); yield break; }
            Color32[] pixels = texture.GetPixels32(); int low = 765, high = 0;
            for (int i = 0; i < pixels.Length; i += Mathf.Max(1, pixels.Length / 4096)) { int value = pixels[i].r + pixels[i].g + pixels[i].b; low = Mathf.Min(low, value); high = Mathf.Max(high, value); }
            Check(high - low > 60 && high > 100, "The scene capture contains rendered color detail: " + filename);
            File.WriteAllBytes(Path.Combine(output, filename), texture.EncodeToPNG()); Destroy(texture); screenshots.Add(filename);
        }
        void Finish()
        {
            if (finished) return; finished = true;
            bool unchanged = SameFile(ordinaryPath, ordinaryExisted, ordinaryBytes) && SameFile(ordinaryPath + ".bak", backupExisted, backupBytes);
            Check(unchanged, "The child's ordinary save and recovery backup remain byte-for-byte untouched.");
            var report = new Report { passed = errors.Count == 0, ordinarySaveUnchanged = unchanged, mobileProfile = MimoPerformance.Lightweight,
                version = Application.version, unityVersion = Application.unityVersion, device = SystemInfo.deviceModel, gpu = SystemInfo.graphicsDeviceName,
                isolatedSavePath = isolatedPath, captureMethod = "URP SingleCameraRequest; real scene without overlay UI", width = Screen.width, height = Screen.height,
                elapsedSeconds = Time.realtimeSinceStartup - started, checks = checks.ToArray(), errors = errors.ToArray(), screenshots = screenshots.ToArray(), measurements = measurements.ToArray() };
            report.fiveFriendsAverageFps = fiveFriendsAverageFps; report.fiveFriendsMinimumWindowFps = fiveFriendsMinimumWindowFps; report.fiveFriendsP95Milliseconds = fiveFriendsP95Milliseconds;
            File.WriteAllText(Path.Combine(output, "playground-checks.json"), JsonUtility.ToJson(report, true));
            Debug.Log("MIMO_PLAYGROUND " + (report.passed ? "PASS" : "FAIL") + " checks=" + checks.Count + " errors=" + errors.Count);
            Application.Quit(report.passed ? 0 : 1);
        }
        static bool SameFile(string path, bool existed, byte[] bytes) => existed ? File.Exists(path) && bytes.SequenceEqual(File.ReadAllBytes(path)) : !File.Exists(path);
    }
}
