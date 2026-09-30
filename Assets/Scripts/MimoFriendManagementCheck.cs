using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MimoGarden
{
    /// <summary>Destructive friend-management acceptance confined to a disposable three-friend save.</summary>
    public sealed class MimoFriendManagementCheck : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed, ordinarySaveUnchanged, mobileProfile;
            public string version, device, gpu, isolatedSavePath;
            public int width, height;
            public float elapsedSeconds;
            public string[] checks, errors, screenshots, dialogText;
        }
        static string output, isolated, ordinary;
        static byte[] ordinaryBytes, backupBytes;
        static bool ordinaryExisted, backupExisted;
        static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "--mimo-friend-management-test") >= 0;
        readonly List<string> checks = new List<string>(), errors = new List<string>(), screenshots = new List<string>(), dialogText = new List<string>();
        MimoExperience app;
        Camera sceneCamera;
        bool finished;
        float started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Isolate()
        {
            if (!Enabled) return;
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--mimo-test-output");
            output = index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1]) : Path.Combine(Application.temporaryCachePath, "MimoFriendManagementValidation");
            Directory.CreateDirectory(output);
            ordinary = Path.Combine(Application.persistentDataPath, "mimo-pet.json");
            ordinaryExisted = File.Exists(ordinary); ordinaryBytes = ordinaryExisted ? File.ReadAllBytes(ordinary) : null;
            backupExisted = File.Exists(ordinary + ".bak"); backupBytes = backupExisted ? File.ReadAllBytes(ordinary + ".bak") : null;
            isolated = Path.Combine(output, "isolated-friend-management-" + DateTime.UtcNow.Ticks + ".json");
            PetStorage.SavePathOverride = isolated;
            var collection = new PetCollection { activePetId = "qa-friend-middle" };
            collection.pets.Add(new PetSave { petId = "qa-friend-first", petName = "Mim", created = true, colorIndex = 1, finishIndex = 0, hunger = .61f, cleanliness = .72f, joy = .83f, recipeSeed = 7101 });
            collection.pets.Add(new PetSave { petId = "qa-friend-middle", petName = "Mim", created = true, colorIndex = 3, finishIndex = 2, hunger = .51f, cleanliness = .62f, joy = .73f, recipeSeed = 7102 });
            collection.pets.Add(new PetSave { petId = "qa-friend-last", petName = "Pingo", created = true, colorIndex = 7, finishIndex = 3, hunger = .41f, cleanliness = .52f, joy = .63f, recipeSeed = 7103 });
            PetStorage.SaveCollection(collection);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (Enabled) new GameObject("Mimo friend management acceptance").AddComponent<MimoFriendManagementCheck>(); }
        void Awake() { started = Time.realtimeSinceStartup; Application.runInBackground = true; Application.logMessageReceived += Log; }
        void OnDestroy() { Application.logMessageReceived -= Log; }
        void Log(string message, string stack, LogType type)
        { if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !errors.Contains(message)) errors.Add(message); }
        void Check(bool pass, string description) { if (pass) checks.Add(description); else errors.Add(description); }
        void Update() { if (!finished && Time.realtimeSinceStartup - started > 180f) { errors.Add("Friend management check timed out."); Finish(); } }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(1.2f);
            app = FindAnyObjectByType<MimoExperience>(); sceneCamera = Camera.main;
            Check(app && sceneCamera && app.CurrentScreen == "Home" && app.PlaygroundPets.Count == 3, "The isolated group opens with three playable creations.");
            Check(PetStorage.SavePath == isolated, "Friend-management checks use only their disposable save.");
            if (!app || !sceneCamera || app.PlaygroundPets.Count != 3) { Finish(); yield break; }
            PetActor first = app.PlaygroundPets[0], middle = app.PlaygroundPets[1], last = app.PlaygroundPets[2];
            string firstId = first.Data.petId, middleId = middle.Data.petId, lastId = last.Data.petId;
            Check(first.Data.petName == middle.Data.petName && firstId != middleId && first.Data.colorIndex != middle.Data.colorIndex,
                "Two creations deliberately share a name while retaining distinct IDs and appearance.");
            PaintPatch(first); PaintPatch(last);
            float firstSoap = first.SoapCoverage, lastSoap = last.SoapCoverage;
            PetSave firstData = first.Data, lastData = last.Data;
            Texture2D firstMask = first.SoapMaskTexture, lastMask = last.SoapMaskTexture;
            Check(firstSoap > 0 && lastSoap > 0, "Remaining friends carry real independent foam patches before deletion.");
            MimoPlaygroundProp swing = FindObjectsByType<MimoPlaygroundProp>(FindObjectsSortMode.None).FirstOrDefault(p => p.Kind == MimoPlaygroundProp.PropKind.Swing);
            Check(swing && app.TryPlaceOnProp(middle, swing), "The middle creation is on the real swing before its deletion dialog.");
            app.SelectPet(middle); app.SendMessage("SaveCare");
            byte[] beforeCancel = File.ReadAllBytes(isolated);
            app.RequestDeletePet(middle); yield return null;
            Check(app.IsFriendDialogOpen && app.PendingRemovalPetId == middleId, "Delete requests target the selected persistent ID even when names are duplicated.");
            Check(Ids().SequenceEqual(new[] { firstId, middleId, lastId }) && SameBytes(beforeCancel), "Opening deletion confirmation changes neither the group nor its saved file.");
            Check(!app.BeginPetGesture(sceneCamera.WorldToScreenPoint(first.VisibleBounds.center)), "The confirmation dialog blocks touches on friends behind it.");
            RecordDialog("delete-middle");
            yield return CaptureNativeDialog("android-delete-confirmation.png");
            app.CancelFriendDialog(); yield return null;
            Check(!app.IsFriendDialogOpen && Ids().SequenceEqual(new[] { firstId, middleId, lastId }) && SameBytes(beforeCancel), "Cancelling deletion preserves the complete group and byte-identical save.");
            Check(swing && swing.Occupant == middle, "Cancelling deletion leaves the creation on its swing.");
            app.RequestDeletePet(middle); app.ConfirmFriendRemoval(); yield return new WaitForSecondsRealtime(.25f);
            Check(!app.IsFriendDialogOpen && Ids().SequenceEqual(new[] { firstId, lastId }), "Confirming middle deletion removes only the chosen ID, keeping the equally named first friend.");
            Check(PetStorage.LoadCollection().pets.Select(p => p.petId).SequenceEqual(new[] { firstId, lastId }), "The exact deletion survives a disk reload.");
            byte[] afterDelete = File.ReadAllBytes(isolated); app.ConfirmFriendRemoval();
            Check(Ids().SequenceEqual(new[] { firstId, lastId }) && SameBytes(afterDelete), "A repeated confirmation after the dialog closes cannot remove another friend.");
            Check(swing && !swing.Occupant, "Deleting a seated creation frees its swing immediately.");
            Check(app.PlaygroundPets.Contains(first) && app.PlaygroundPets.Contains(last) && first.Data == firstData && last.Data == lastData,
                "Deletion preserves the two remaining live actors and their independent care data.");
            Check(first.SoapMaskTexture == firstMask && last.SoapMaskTexture == lastMask && Mathf.Abs(first.SoapCoverage - firstSoap) < .001f && Mathf.Abs(last.SoapCoverage - lastSoap) < .001f,
                "Deletion does not rebuild or erase the remaining friends' painted foam.");
            yield return Capture("01-middle-deleted.png");

            app.SelectPet(first); app.VisitCareRoom("Bath"); yield return new WaitForSecondsRealtime(.3f);
            app.SelectBathMode("Rinse"); yield return null;
            Vector2 tool = sceneCamera.WorldToScreenPoint(app.BathToolWorldPosition);
            Check(app.BeginBathToolGesture(tool), "The selected friend's real shower can be held before deletion.");
            app.DragBathToolGesture(sceneCamera.WorldToScreenPoint(first.VisibleBounds.center), .05f); yield return null;
            Check(app.IsWaterRunning, "Water is actively running when deletion is requested.");
            app.RequestDeletePet(first); yield return null;
            Check(!app.IsWaterRunning && !app.IsBathToolHeld, "Opening deletion safely ends the held bath tool and running water.");
            app.ConfirmFriendRemoval(); yield return new WaitForSecondsRealtime(.25f);
            Check(Ids().SequenceEqual(new[] { lastId }) && app.Actor == last && app.ActiveEnvironment == "Bath", "Deleting the bath target selects the surviving friend in the same care room.");
            Check(!app.IsWaterRunning && !app.IsBathToolHeld, "The surviving friend does not inherit the deleted friend's held shower.");
            Check(PetStorage.Load().petId == lastId, "The fallback active selection is saved.");
            yield return Capture("02-bath-fallback.png");

            app.VisitCareRoom("Food"); yield return new WaitForSecondsRealtime(.2f);
            PetSave eatingData = last.Data; float hungerBefore = eatingData.hunger;
            app.MainCareAction(); yield return null;
            app.RequestDeletePet(last); app.ConfirmFriendRemoval(); yield return new WaitForSecondsRealtime(.9f);
            Check(app.PlaygroundPets.Count == 0 && PetStorage.LoadCollection().pets.Count == 0, "Deleting the final creation persists an empty group.");
            Check(app.CurrentScreen == "Laboratory" && !app.CanFinalizeRecipe && !app.Actor.gameObject.activeInHierarchy,
                "Deleting the last friend returns to an empty, usable creation table.");
            Check(Mathf.Abs(eatingData.hunger - hungerBefore) < .002f, "Deleting a friend cancels its in-flight food animation without feeding a replacement actor.");
            Check(!app.IsFriendDialogOpen && !app.IsWaterRunning && !app.IsBathToolHeld, "Removing the last friend leaves no modal or held care tool behind.");
            yield return Capture("03-empty-after-last-delete.png");

            yield return PrepareBirth("Lumi", 0); app.ConfirmName(); yield return new WaitForSecondsRealtime(.25f);
            Check(app.CurrentScreen == "Home" && app.PlaygroundPets.Count == 1 && app.Actor.Data.petName == "Lumi", "A fresh creation completes normally after deleting the entire previous group.");
            PetActor retainedFirst = app.Actor;
            yield return PrepareBirth("Mim", 1); app.ConfirmName(); yield return new WaitForSecondsRealtime(.25f);
            PetActor replacedMiddle = app.Actor;
            yield return PrepareBirth("Mim", 4); app.ConfirmName(); yield return new WaitForSecondsRealtime(.25f);
            PetActor retainedLast = app.Actor;
            Check(app.PlaygroundPets.Count == 3, "Three newly created friends fill the group again.");
            string[] fullIds = Ids(); string replaceId = replacedMiddle.Data.petId;
            yield return PrepareBirth("Nuvem", 3);
            PetSave draft = app.DraftRecipe;
            byte[] fullFile = File.ReadAllBytes(isolated);
            app.ConfirmName(); yield return null;
            Check(app.IsFriendDialogOpen && app.IsChoosingReplacement && app.CurrentScreen == "Naming", "Naming a fourth creation opens the replacement chooser before committing it.");
            Check(Ids().SequenceEqual(fullIds) && SameBytes(fullFile), "Opening the replacement chooser leaves all three saved creations untouched.");
            RecordDialog("choose-replacement");
            yield return CaptureNativeDialog("android-replacement-chooser.png");
            app.ChooseFriendToReplace(replacedMiddle); yield return null;
            Check(app.IsFriendDialogOpen && app.PendingRemovalPetId == replaceId, "Replacement confirmation names the explicitly chosen non-oldest creation's ID.");
            Check(Ids().SequenceEqual(fullIds) && SameBytes(fullFile), "Selecting a replacement does not delete or write before confirmation.");
            yield return CaptureNativeDialog("android-replacement-confirmation.png");
            app.CancelFriendDialog(); yield return null;
            Check(Ids().SequenceEqual(fullIds) && SameBytes(fullFile) && SameRecipe(draft, app.DraftRecipe) && app.DraftRecipe.petName == "Nuvem", "Cancelling replacement preserves the entire group, saved bytes and unfinished new recipe and name.");
            if (app.IsFriendDialogOpen) app.CancelFriendDialog();
            app.ConfirmName(); yield return null;
            Check(app.IsChoosingReplacement, "The same pending fourth recipe can reopen the replacement chooser.");
            app.ChooseFriendToReplace(replacedMiddle); app.ConfirmFriendRemoval(); yield return new WaitForSecondsRealtime(.35f);
            PetActor newFriend = app.Actor;
            Check(app.CurrentScreen == "Home" && !app.IsFriendDialogOpen && app.PlaygroundPets.Count == 3 && newFriend.Data.petName == "Nuvem",
                "Confirming replacement commits the new friend and returns to the playable group.");
            Check(Ids().SequenceEqual(new[] { retainedFirst.Data.petId, retainedLast.Data.petId, newFriend.Data.petId }) && !Ids().Contains(replaceId),
                "Only the explicitly chosen non-oldest friend is replaced; the other two keep their creation order.");
            Check(app.PlaygroundPets.Contains(retainedFirst) && app.PlaygroundPets.Contains(retainedLast), "Replacement preserves the remaining live actor instances.");
            PetCollection final = PetStorage.LoadCollection();
            Check(final.pets.Select(p => p.petId).SequenceEqual(Ids()) && final.activePetId == newFriend.Data.petId && final.retiredPetIds.Contains(replaceId),
                "The chosen replacement, retirement and new selection survive a complete disk reload.");
            Check(final.retiredPetIds.Contains(firstId) && final.retiredPetIds.Contains(middleId) && final.retiredPetIds.Contains(lastId),
                "Previously deleted identities remain retired after starting a fresh group.");
            yield return Capture("04-chosen-replacement-complete.png");
            Finish();
        }

        IEnumerator PrepareBirth(string name, int color)
        {
            app.EnterLaboratory(); yield return null;
            yield return MimoRitualCheck.AddAndStir(app, 0, color);
            Check(app.CanFinalizeRecipe, "The real mixed recipe is ready for " + name + ".");
            app.BeginBirth(); float deadline = Time.realtimeSinceStartup + 6;
            while (app.CurrentScreen == "Birth" && Time.realtimeSinceStartup < deadline) yield return null;
            Check(app.CurrentScreen == "Naming", "The real reveal reaches naming for " + name + ".");
            InputField nameField = FindObjectsByType<InputField>(FindObjectsSortMode.None).FirstOrDefault(f => f.gameObject.activeInHierarchy);
            Check(nameField, "The new friend's visible name field is available.");
            if (nameField) nameField.text = name;
        }
        void PaintPatch(PetActor pet)
        {
            Physics.SyncTransforms(); Bounds bounds = pet.VisibleBounds;
            foreach (float horizontal in new[] { 0f, -.2f, .2f })
            {
                Vector3 world = bounds.center + Vector3.right * bounds.extents.x * horizontal;
                if (Physics.Raycast(sceneCamera.ScreenPointToRay(sceneCamera.WorldToScreenPoint(world)), out RaycastHit hit, 45f) && hit.collider.GetComponentInParent<PetActor>() == pet)
                    pet.SoapAt(hit, .8f);
            }
        }
        string[] Ids() => app.PlaygroundPets.Select(p => p.Data.petId).ToArray();
        bool SameBytes(byte[] before) => File.Exists(isolated) && before.SequenceEqual(File.ReadAllBytes(isolated));
        static bool SameRecipe(PetSave first, PetSave second) => first != null && second != null && first.petId == second.petId && Enumerable.Range(0, PetCatalog.CategoryCount).All(c => PetCatalog.Get(first, c) == PetCatalog.Get(second, c));
        void RecordDialog(string kind)
        {
            dialogText.Add(kind + ": " + string.Join(" | ", FindObjectsByType<Text>(FindObjectsSortMode.None).Where(t => t.gameObject.activeInHierarchy && t.enabled && !string.IsNullOrWhiteSpace(t.text)).Select(t => t.text.Replace("\n", " "))));
        }
        IEnumerator CaptureNativeDialog(string filename)
        {
            if (!Application.isMobilePlatform) yield break;
            yield return new WaitForEndOfFrame();
            Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
            Check(image, "The Android display provides a native screenshot for " + filename + ".");
            if (!image) yield break;
            Color32[] pixels = image.GetPixels32(); int low = 765, high = 0, different = 0;
            Color32 first = pixels[0];
            for (int i = 0; i < pixels.Length; i += Mathf.Max(1, pixels.Length / 8192))
            {
                Color32 pixel = pixels[i]; int light = pixel.r + pixel.g + pixel.b;
                low = Mathf.Min(low, light); high = Mathf.Max(high, light);
                if (Mathf.Abs(pixel.r - first.r) + Mathf.Abs(pixel.g - first.g) + Mathf.Abs(pixel.b - first.b) > 30) different++;
            }
            Check(image.width == Screen.width && image.height == Screen.height && high > 100 && high - low > 60 && different > 500,
                "The native Android modal capture contains a complete, nonblank display: " + filename);
            Check(app.IsFriendDialogOpen, "The friend dialog remains open in the native capture: " + filename);
            File.WriteAllBytes(Path.Combine(output, filename), image.EncodeToPNG()); Destroy(image); screenshots.Add(filename);
        }
        IEnumerator Capture(string filename)
        {
            yield return new WaitForEndOfFrame(); Texture2D image = MimoRenderCapture.Scene(sceneCamera);
            if (!image) { errors.Add("Scene capture failed: " + filename); yield break; }
            Color32[] pixels = image.GetPixels32(); int low = 765, high = 0;
            for (int i = 0; i < pixels.Length; i += Mathf.Max(1, pixels.Length / 4096)) { int light = pixels[i].r + pixels[i].g + pixels[i].b; low = Mathf.Min(low, light); high = Mathf.Max(high, light); }
            Check(high > 100 && high - low > 60, "The scene screenshot contains nonblank rendered detail: " + filename);
            File.WriteAllBytes(Path.Combine(output, filename), image.EncodeToPNG()); Destroy(image); screenshots.Add(filename);
        }
        void Finish()
        {
            if (finished) return; finished = true;
            bool unchanged = SameFile(ordinary, ordinaryExisted, ordinaryBytes) && SameFile(ordinary + ".bak", backupExisted, backupBytes);
            Check(unchanged, "The child's ordinary save and backup remain byte-for-byte untouched.");
            var report = new Report { passed = errors.Count == 0, ordinarySaveUnchanged = unchanged, mobileProfile = MimoPerformance.Lightweight, version = Application.version,
                device = SystemInfo.deviceModel, gpu = SystemInfo.graphicsDeviceName, isolatedSavePath = isolated, width = Screen.width, height = Screen.height,
                elapsedSeconds = Time.realtimeSinceStartup - started, checks = checks.ToArray(), errors = errors.ToArray(), screenshots = screenshots.ToArray(), dialogText = dialogText.ToArray() };
            File.WriteAllText(Path.Combine(output, "friend-management-checks.json"), JsonUtility.ToJson(report, true));
            Debug.Log("MIMO_FRIEND_MANAGEMENT " + (report.passed ? "PASS" : "FAIL") + " checks=" + checks.Count + " errors=" + errors.Count);
            Application.Quit(report.passed ? 0 : 1);
        }
        static bool SameFile(string path, bool existed, byte[] bytes) => existed ? File.Exists(path) && bytes.SequenceEqual(File.ReadAllBytes(path)) : !File.Exists(path);
    }
}
