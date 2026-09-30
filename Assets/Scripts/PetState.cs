using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MimoGarden
{
    [Serializable]
    public sealed class PetSave
    {
        public int schemaVersion = 3;
        public string petId = Guid.NewGuid().ToString("N");
        public string petName = "Mimo";
        public int colorIndex;
        public int earsIndex;
        public int tailIndex;
        public int eyeIndex;
        public int glassesIndex;
        public int hatIndex;
        public int wingsIndex;
        public int finishIndex;
        public bool created;
        public float hunger = 0.75f;
        public float cleanliness = 0.80f;
        public float joy = 0.80f;
        public long savedUtcTicks;
        public int recipeSeed;

        public PetSave Copy()
        {
            return (PetSave)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class PetCollection
    {
        public int schemaVersion = 1;
        // Creation order is independent of which friend was last selected or cared for.
        public List<PetSave> pets = new List<PetSave>();
        public string activePetId;
        // A delayed autosave from an evicted actor must never add it back to the playground.
        public List<string> retiredPetIds = new List<string>();
    }

    /// <summary>Up to three local creations, with explicit removal and migration from the original single-pet save.</summary>
    public static class PetStorage
    {
        public const int MaxPets = 3;
        /// <summary>Allows isolated validation without touching the child's existing creation.</summary>
        public static string SavePathOverride { get; set; }
        public static string SavePath => !string.IsNullOrWhiteSpace(SavePathOverride) ? SavePathOverride :
            Path.Combine(Application.persistentDataPath, Array.Exists(Environment.GetCommandLineArgs(), a => a == "--mimo-qa")
                ? "mimo-qa-save.json" : "mimo-pet.json");
        private static string BackupPath => SavePath + ".bak";
        private static readonly object Gate = new object();

        public static PetSave Load()
        {
            PetCollection collection = LoadCollection();
            return collection.pets.Find(pet => pet.petId == collection.activePetId) ?? new PetSave();
        }

        public static PetCollection LoadCollection()
        {
            lock (Gate)
            {
                bool migrated;
                PetCollection collection = ReadCollection(out migrated);
                SanitizeCollection(collection);
                long now = DateTime.UtcNow.Ticks;
                foreach (PetSave pet in collection.pets) ApplyOfflineTime(pet, now);
                // Persist the assigned ID during migration so separate loads agree on identity.
                if (migrated) WriteCollection(collection);
                return collection;
            }
        }

        /// <summary>Save one friend without replacing another. A full playground rejects a new ID.</summary>
        public static bool Save(PetSave pet) => Save(pet, null);

        /// <summary>Atomically replace only the explicitly chosen friend with a distinct new creation.</summary>
        public static bool Save(PetSave pet, string replacePetId)
        {
            if (pet == null) return false;
            lock (Gate)
            {
                PetSave candidate = pet.Copy();
                Sanitize(candidate);
                bool migrated;
                PetCollection collection = ReadCollection(out migrated);
                SanitizeCollection(collection);
                if (collection.retiredPetIds.Contains(candidate.petId)) return false;
                int existing = collection.pets.FindIndex(friend => friend.petId == candidate.petId);
                if (!string.IsNullOrWhiteSpace(replacePetId))
                {
                    int replacement = collection.pets.FindIndex(friend => friend.petId == replacePetId);
                    if (replacement < 0 || existing >= 0) return false;
                    collection.pets.RemoveAt(replacement);
                    collection.retiredPetIds.Add(replacePetId);
                }
                else if (existing < 0 && collection.pets.Count >= MaxPets) return false;

                candidate.savedUtcTicks = DateTime.UtcNow.Ticks;
                if (existing >= 0) collection.pets[existing] = candidate;
                else collection.pets.Add(candidate);
                collection.activePetId = candidate.petId;
                SanitizeCollection(collection);
                if (!WriteCollection(collection)) return false;
                ApplySavedPet(pet, candidate);
                return true;
            }
        }

        /// <summary>Remove one known friend by identity. Unknown IDs leave the saved playground untouched.</summary>
        public static bool DeletePet(string petId)
        {
            if (string.IsNullOrWhiteSpace(petId)) return false;
            lock (Gate)
            {
                bool migrated;
                PetCollection collection = ReadCollection(out migrated);
                SanitizeCollection(collection);
                int index = collection.pets.FindIndex(pet => pet.petId == petId);
                if (index < 0) return false;
                collection.pets.RemoveAt(index);
                collection.retiredPetIds.Add(petId);
                SanitizeCollection(collection);
                // An empty pets array is a valid authoritative save, even when the backup has a friend.
                return WriteCollection(collection);
            }
        }

        /// <summary>Save the complete live playground and its selection. Do not include an unfinished draft.</summary>
        public static bool SaveCollection(PetCollection collection)
        {
            if (collection == null) return false;
            lock (Gate)
            {
                PetCollection candidate = CopyCollection(collection);
                // Keep the copy-to-live correspondence even after sanitization removes duplicate entries.
                List<PetSave> copiedPets = candidate.pets;
                bool migrated;
                PetCollection previous = ReadCollection(out migrated);
                SanitizeCollection(previous);
                foreach (string id in previous.retiredPetIds)
                    if (!candidate.retiredPetIds.Contains(id)) candidate.retiredPetIds.Add(id);
                var distinct = new HashSet<string>();
                foreach (PetSave pet in candidate.pets)
                {
                    if (pet == null) continue;
                    Sanitize(pet);
                    if (!candidate.retiredPetIds.Contains(pet.petId)) distinct.Add(pet.petId);
                }
                // Saving must not silently choose a victim; oversized legacy files may still be repaired on load.
                if (distinct.Count > MaxPets) return false;
                SanitizeCollection(candidate);
                foreach (PetSave previousPet in previous.pets)
                {
                    if (!candidate.pets.Exists(pet => pet.petId == previousPet.petId) &&
                        !candidate.retiredPetIds.Contains(previousPet.petId))
                        candidate.retiredPetIds.Add(previousPet.petId);
                }
                long now = DateTime.UtcNow.Ticks;
                foreach (PetSave pet in candidate.pets) pet.savedUtcTicks = now;
                if (!WriteCollection(candidate)) return false;

                var committedPets = new List<PetSave>(candidate.pets.Count);
                foreach (PetSave pet in candidate.pets)
                {
                    int sourceIndex = copiedPets.IndexOf(pet);
                    PetSave live = collection.pets[sourceIndex];
                    ApplySavedPet(live, pet);
                    committedPets.Add(live);
                }
                collection.schemaVersion = candidate.schemaVersion;
                collection.pets = committedPets;
                collection.activePetId = candidate.activePetId;
                collection.retiredPetIds = candidate.retiredPetIds;
                return true;
            }
        }

        private static PetCollection CopyCollection(PetCollection source)
        {
            var copy = new PetCollection {
                schemaVersion = source.schemaVersion, activePetId = source.activePetId,
                retiredPetIds = source.retiredPetIds == null ? new List<string>() : new List<string>(source.retiredPetIds)
            };
            if (source.pets != null) foreach (PetSave pet in source.pets) copy.pets.Add(pet == null ? null : pet.Copy());
            return copy;
        }

        private static void ApplySavedPet(PetSave target, PetSave saved)
        {
            target.schemaVersion = saved.schemaVersion; target.petId = saved.petId; target.petName = saved.petName;
            target.colorIndex = saved.colorIndex; target.earsIndex = saved.earsIndex; target.tailIndex = saved.tailIndex;
            target.eyeIndex = saved.eyeIndex; target.glassesIndex = saved.glassesIndex; target.hatIndex = saved.hatIndex;
            target.wingsIndex = saved.wingsIndex; target.finishIndex = saved.finishIndex; target.created = saved.created;
            target.hunger = saved.hunger; target.cleanliness = saved.cleanliness; target.joy = saved.joy;
            target.savedUtcTicks = saved.savedUtcTicks; target.recipeSeed = saved.recipeSeed;
        }

        public static void SanitizeCollection(PetCollection collection)
        {
            if (collection == null) return;
            if (collection.pets == null) collection.pets = new List<PetSave>();
            if (collection.retiredPetIds == null) collection.retiredPetIds = new List<string>();
            var retired = new HashSet<string>(collection.retiredPetIds);
            retired.Remove(null); retired.Remove("");
            var seen = new HashSet<string>();
            var unique = new List<PetSave>();
            foreach (PetSave pet in collection.pets)
            {
                if (pet == null) continue;
                Sanitize(pet);
                if (!retired.Contains(pet.petId) && seen.Add(pet.petId)) unique.Add(pet);
            }
            while (unique.Count > MaxPets)
            {
                retired.Add(unique[0].petId);
                unique.RemoveAt(0);
            }
            collection.pets = unique;
            collection.retiredPetIds = new List<string>(retired);
            if (!unique.Exists(pet => pet.petId == collection.activePetId))
                collection.activePetId = unique.Count > 0 ? unique[unique.Count - 1].petId : null;
            collection.schemaVersion = 1;
        }

        private static bool WriteCollection(PetCollection collection)
        {
            string temporary = SavePath + ".tmp";
            try
            {
                string directory = Path.GetDirectoryName(Path.GetFullPath(SavePath));
                Directory.CreateDirectory(directory);
                byte[] json = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(collection, true));
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(json, 0, json.Length);
                    stream.Flush(true);
                }

                if (File.Exists(SavePath))
                {
                    // Replace on the same volume is atomic; the previous complete save is retained.
                    // If the filesystem cannot commit it, fail without deleting the current roster.
                    File.Replace(temporary, SavePath, BackupPath);
                }
                else File.Move(temporary, SavePath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Mimo não conseguiu salvar esta vez. O arquivo anterior foi preservado: " + exception.Message);
                return false;
            }
        }

        private static PetCollection ReadCollection(out bool migrated)
        {
            return Read(SavePath, out migrated) ?? Read(BackupPath, out migrated) ?? new PetCollection();
        }

        private static PetCollection Read(string path, out bool migrated)
        {
            migrated = false;
            if (!File.Exists(path)) return null;
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json) || !json.Contains("\"schemaVersion\"")) return null;
                if (Regex.IsMatch(json, "\"pets\"\\s*:"))
                {
                    PetCollection collection = JsonUtility.FromJson<PetCollection>(json);
                    if (collection == null || collection.schemaVersion != 1 || collection.pets == null) return null;
                    migrated = collection.pets.Exists(pet => pet != null && string.IsNullOrWhiteSpace(pet.petId));
                    return collection;
                }
                PetSave pet = JsonUtility.FromJson<PetSave>(json);
                if (pet == null || pet.schemaVersion < 1 || pet.schemaVersion > 3) return null;
                Sanitize(pet);
                migrated = true;
                return new PetCollection { pets = new List<PetSave> { pet }, activePetId = pet.petId };
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Mimo tentará recuperar o último cuidado salvo: " + exception.Message);
                return null;
            }
        }

        public static void ApplyOfflineTime(PetSave pet, long nowUtcTicks)
        {
            if (pet == null) return;
            if (pet.created && pet.savedUtcTicks > 0 && nowUtcTicks > pet.savedUtcTicks)
            {
                // An absence never creates a distressed or unrecognizable pet.
                double hours = Math.Min(6d, (nowUtcTicks - pet.savedUtcTicks) / (double)TimeSpan.TicksPerHour);
                pet.hunger = GentleDecay(pet.hunger, (float)hours * 0.025f, 0.35f);
                pet.cleanliness = GentleDecay(pet.cleanliness, (float)hours * 0.018f, 0.40f);
                pet.joy = GentleDecay(pet.joy, (float)hours * 0.020f, 0.45f);
            }
            pet.savedUtcTicks = nowUtcTicks;
        }

        private static float GentleDecay(float value, float amount, float floor)
        {
            return Mathf.Max(Mathf.Min(value, floor), value - amount);
        }

        private static float ValidUnit(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value);
        }

        public static void Sanitize(PetSave pet)
        {
            if (pet == null) return;
            if (string.IsNullOrWhiteSpace(pet.petId)) pet.petId = Guid.NewGuid().ToString("N");
            if (pet.schemaVersion < 3)
            {
                // The first eight color families keep their indices, now in softer tones.
                // Retired colors map to the nearest retained family before the ordinary clamp.
                switch (pet.colorIndex)
                {
                    case 8: pet.colorIndex = 7; break;  // Turquoise -> mint.
                    case 9: pet.colorIndex = 1; break;  // Violet -> lavender.
                    case 10: pet.colorIndex = 4; break; // Cherry -> bubblegum pink.
                    case 11: pet.colorIndex = 3; break; // Galaxy blue -> cloud blue.
                }
                pet.finishIndex = 0;
            }
            pet.petName = string.IsNullOrWhiteSpace(pet.petName) ? "Mimo" : pet.petName.Trim();
            if (pet.petName.Length > 24) pet.petName = pet.petName.Substring(0, 24);
            for (int category = 0; category < PetCatalog.CategoryCount; category++)
                PetCatalog.Set(pet, category, PetCatalog.Get(pet, category));
            pet.hunger = ValidUnit(pet.hunger, 0.75f);
            pet.cleanliness = ValidUnit(pet.cleanliness, 0.80f);
            pet.joy = ValidUnit(pet.joy, 0.80f);
            if (pet.savedUtcTicks < 0 || pet.savedUtcTicks > DateTime.MaxValue.Ticks) pet.savedUtcTicks = 0;
            pet.schemaVersion = 3;
        }
    }
}
