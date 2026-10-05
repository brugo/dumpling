using System.Collections.Generic;
using UnityEngine;

namespace MimoGarden
{
    /// <summary>ImageGen artwork: five authored atlases, sliced without changing their pixels.</summary>
    public static class MimoUIArt
    {
        static readonly Dictionary<string, Sprite[]> atlases = new Dictionary<string, Sprite[]>();
        static Sprite Get(string name, int index)
        {
            if (!atlases.TryGetValue(name, out Sprite[] sprites))
            {
                Texture2D texture = Resources.Load<Texture2D>("UI/" + name);
                if (!texture) { Debug.LogError("Missing Mimo UI artwork: " + name); return null; }
                sprites = new Sprite[16];
                // The generated sheets have intentionally generous gutters, but their row
                // baselines vary. Authored slice boundaries keep neighboring art out of tiles.
                float[] rows = name == "MimoUIAtlas" ? new[] { 0f, .275f, .49f, .735f, 1f }
                    : name == "MimoEyesAtlas" ? new[] { 0f, .265f, .505f, .69f, 1f }
                    : name == "MimoBodyAtlas" ? new[] { 0f, .272f, .502f, .74f, 1f }
                    : name == "MimoDressAtlas" ? new[] { 0f, .266f, .503f, .727f, 1f }
                    : new[] { 0f, .25f, .5f, .735f, 1f };
                float[] columns = name == "MimoBodyAtlas" || name == "MimoDressAtlas" || name == "MimoUIAtlas"
                    ? new[] { 0f, .267f, .5f, .75f, 1f } : new[] { 0f, .25f, .5f, .75f, 1f };
                for (int i = 0; i < 16; i++)
                {
                    int c = i % 4, r = i / 4;
                    float[] rowColumns = name == "MimoDressAtlas" ? new[] { 0f, .255f, .5f, .75f, 1f }
                        : name == "MimoUIAtlas" && r >= 2 ? new[] { 0f, r == 2 ? .253f : .247f, .5f, .75f, 1f } : columns;
                    Rect region = new Rect(rowColumns[c] * texture.width, (1f - rows[r + 1]) * texture.height,
                        (rowColumns[c + 1] - rowColumns[c]) * texture.width, (rows[r + 1] - rows[r]) * texture.height);
                    sprites[i] = Sprite.Create(texture, region, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
                    sprites[i].name = name + ":" + i;
                }
                atlases.Add(name, sprites);
            }
            return sprites[Mathf.Clamp(index, 0, 15)];
        }
        public static Sprite Category(int category) => Get("MimoUIAtlas", category);
        public static Sprite Ingredient(int category, int index)
        {
            switch (category)
            {
                case 0: return Get("MimoColorsAtlas", index);
                case 1: return Get("MimoBodyAtlas", index);
                case 2: return Get("MimoBodyAtlas", 7 + index);
                case 3: return Get("MimoEyesAtlas", index);
                case 4: return Get("MimoEyesAtlas", 6 + index);
                case 5: return Get("MimoDressAtlas", index);
                case 6: return Get("MimoDressAtlas", 6 + index);
                case 7: return Get("MimoColorsAtlas", 8 + index);
                default: return null;
            }
        }
        public static Sprite Action(string label)
        {
            switch (label)
            {
                case "Comidinha": case "Dar frutinha": return Get("MimoUIAtlas", 8);
                case "Banho": return Get("MimoUIAtlas", 9);
                case "Brincar": return Get("MimoUIAtlas", 10);
                case "Surpresa": case "Revelar surpresa!": case "Ateliê": case "Criar meu Mimo!": case "Criar outro amigo": return Get("MimoUIAtlas", 11);
                case "Pular sem parar": case "Um pulinho!": return Get("MimoUIAtlas", 12);
                case "Pega-pega": return Get("MimoUIAtlas", 13);
                case "Descansar": return Get("MimoUIAtlas", 14);
                case "Ensaboar": return Get("MimoUIAtlas", 15);
                case "Enxaguar": return Get("MimoColorsAtlas", 14);
                case "Girar": case "Virar o Mimo": return Get("MimoColorsAtlas", 15);
                case "Mover": return Get("MimoEyesAtlas", 12);
                case "Amassar": return Get("MimoEyesAtlas", 13);
                case "Brincar na lama": return Get("MimoEyesAtlas", 14);
                case "Levar para casa": return Get("MimoDressAtlas", 13);
                case "Voltar": return Get("MimoDressAtlas", 12);
                default: return null;
            }
        }
    }
}
