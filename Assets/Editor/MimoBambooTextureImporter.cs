using UnityEditor;
using UnityEngine;

namespace MimoGarden.EditorTools
{
    // Applies only to this single material asset, leaving UI/environment import policy intact.
    public sealed class MimoBambooTextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (assetPath != "Assets/Resources/Textures/BambooGrain.png") return;
            var texture = (TextureImporter)assetImporter;
            texture.textureType = TextureImporterType.Default;
            texture.sRGBTexture = true;
            texture.alphaSource = TextureImporterAlphaSource.None;
            texture.mipmapEnabled = true;
            texture.isReadable = false;
            texture.maxTextureSize = 512;
            texture.textureCompression = TextureImporterCompression.CompressedHQ;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 2;
        }
    }
}
