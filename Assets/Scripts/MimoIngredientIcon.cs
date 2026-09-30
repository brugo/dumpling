using UnityEngine;
using UnityEngine.UI;

namespace MimoGarden
{
    /// <summary>Authored ImageGen artwork, shared by category tabs and draggable ingredient cards.</summary>
    public sealed class MimoIngredientIcon : Image
    {
        int category, index;
        bool preview;
        public int Category { get => category; set { category = value; RefreshArt(); } }
        public int Index { get => index; set { index = value; RefreshArt(); } }
        public bool CategoryPreview { get => preview; set { preview = value; RefreshArt(); } }
        // Source compatibility: colors are baked into the generated bottle artwork.
        public Color IngredientColor { get; set; } = Color.white;
        protected override void OnEnable() { base.OnEnable(); RefreshArt(); }
        void RefreshArt()
        {
            sprite = preview ? MimoUIArt.Category(category) : MimoUIArt.Ingredient(category, index);
            type = Type.Simple; preserveAspect = true; color = Color.white; raycastTarget = false;
        }
    }
}
