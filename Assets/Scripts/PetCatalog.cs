using UnityEngine;

namespace MimoGarden
{
    /// <summary>Pastel ingredients with stable part indices and a finish stored with each creation.</summary>
    public static class PetCatalog
    {
        public static readonly Color[] Colors = {
            C("F7C6DA"), C("CEC4F2"), C("DBEAB9"), C("B9DDF4"),
            C("EFB9E6"), C("F8CEB7"), C("F7E7AB"), C("B6E5D1")
        };
        public static readonly string[] ColorNames = { "Rosa marshmallow", "Lavanda", "Pistache", "Azul nuvem", "Rosa chiclete", "Pêssego", "Baunilha", "Menta" };
        public static readonly string[] EarNames = { "Só fofura", "Toque de coelhinho", "Gato", "Raposa", "Urso", "Cachorrinho", "Axolote" };
        public static readonly string[] TailNames = { "Sem rabinho", "Pompom de nuvem", "Rabinho enrolado", "Raposa", "Gato", "Dragão", "Sereia", "Raio" };
        public static readonly string[] EyeNames = { "Clássicos", "Redondinhos", "Estrelas", "Corações", "Felinos", "Robô" };
        public static readonly string[] GlassesNames = { "Sem óculos", "Redondos", "Estrelas", "Corações", "Visor", "Explorador" };
        public static readonly string[] HatNames = { "Sem chapéu", "Boné", "Coroa", "Mago", "Flores", "Espacial" };
        public static readonly string[] WingNames = { "Sem asas", "Fada", "Borboleta", "Dragão", "Anjo", "Cósmicas" };
        public static readonly string[] FinishNames = { "Macio", "Pérola", "Gelatina", "Gel com glitter", "Cristal de açúcar", "Brilho mágico" };
        public static readonly string[] CategoryNames = { "Cores", "Orelhas", "Rabos", "Olhos", "Óculos", "Chapéus", "Asas", "Texturas" };
        public const int CategoryCount = 8;
        public static string[] Options(int category)
        {
            switch (category)
            {
                case 0: return ColorNames; case 1: return EarNames; case 2: return TailNames;
                case 3: return EyeNames; case 4: return GlassesNames; case 5: return HatNames;
                case 6: return WingNames; case 7: return FinishNames;
                default: return System.Array.Empty<string>();
            }
        }
        public static int Get(PetSave pet, int category)
        {
            if (pet == null) return 0;
            switch (category)
            {
                case 0: return pet.colorIndex; case 1: return pet.earsIndex; case 2: return pet.tailIndex;
                case 3: return pet.eyeIndex; case 4: return pet.glassesIndex; case 5: return pet.hatIndex;
                case 6: return pet.wingsIndex; case 7: return pet.finishIndex;
                default: return 0;
            }
        }
        public static void Set(PetSave pet, int category, int value)
        {
            int count = Options(category).Length;
            if (pet == null || count == 0) return;
            value = Mathf.Clamp(value, 0, count - 1);
            switch (category)
            {
                case 0: pet.colorIndex = value; break; case 1: pet.earsIndex = value; break;
                case 2: pet.tailIndex = value; break; case 3: pet.eyeIndex = value; break;
                case 4: pet.glassesIndex = value; break; case 5: pet.hatIndex = value; break;
                case 6: pet.wingsIndex = value; break;
                case 7: pet.finishIndex = value; break;
            }
        }
        static Color C(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out Color result); return result; }
    }
}
