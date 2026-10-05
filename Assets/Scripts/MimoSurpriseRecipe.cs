using System;
using UnityEngine;

namespace MimoGarden
{
    /// <summary>Kitchen quantities bias a fresh roll, never a deterministic recipe or a reward tier.</summary>
    public static class MimoSurpriseRecipe
    {
        public const int Category = 8, MaxPortions = 12;
        public static readonly string[] Names = { "Farinha", "Ovo", "Leite" };
        public static int Total(int[] amounts)
        {
            int total = 0;
            for (int i = 0; i < 3; i++) total += Mathf.Clamp(amounts[i], 0, MaxPortions);
            return total;
        }

        // Equal amounts are neutral. An excess adds at most 3 to a base weight of 1,
        // so every ordinary catalog option always remains possible.
        public static float Weight(int category, int option, int[] amounts)
        {
            float average = Total(amounts) / 3f;
            float flour = Mathf.Clamp((amounts[0] - average) * .6f, 0, 3);
            float egg = Mathf.Clamp((amounts[1] - average) * .6f, 0, 3);
            float milk = Mathf.Clamp((amounts[2] - average) * .6f, 0, 3);
            float weight = 1;
            if (category == 7)
            {
                if (option == 0 || option == 1) weight += flour;
                if (option == 1 || option == 2 || option == 3) weight += milk;
            }
            if (category == 0 && (option == 0 || option == 5 || option == 6)) weight += egg;
            if (category == 5 && option > 0) weight += egg * .5f;
            return weight;
        }

        public static PetSave Roll(int[] amounts, System.Random random)
        {
            if (amounts == null || amounts.Length != 3 || Total(amounts) == 0) throw new ArgumentException("Add a kitchen ingredient first.");
            var pet = new PetSave { created = false, cleanliness = 1, hunger = .9f, joy = 1, recipeSeed = random.Next() };
            for (int category = 0; category < PetCatalog.CategoryCount; category++)
            {
                if (category == 6) { pet.wingsIndex = 0; continue; }
                int count = PetCatalog.Options(category).Length;
                float total = 0;
                for (int i = 0; i < count; i++) total += Weight(category, i, amounts);
                double pick = random.NextDouble() * total;
                int selected = count - 1;
                for (int i = 0; i < count; i++) { pick -= Weight(category, i, amounts); if (pick < 0) { selected = i; break; } }
                PetCatalog.Set(pet, category, selected);
            }
            return pet;
        }
    }
}
