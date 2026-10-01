using System.IO;
using UnityEditor;
namespace MimoGarden.EditorTools
{
    public static class MimoShopBuild
    {
        public static void PrepareArt()
        {
            foreach(var id in new[]{"aurora","luna","mar"}) {
                var texture=AssetImporter.GetAtPath("Assets/Resources/Cosmetics/"+id+".png") as TextureImporter;
                if(texture){texture.textureType=TextureImporterType.Default;texture.maxTextureSize=1024;texture.sRGBTexture=true;texture.mipmapEnabled=true;texture.isReadable=false;texture.wrapMode=UnityEngine.TextureWrapMode.Repeat;texture.textureCompression=TextureImporterCompression.CompressedHQ;texture.SaveAndReimport();}
                var model=AssetImporter.GetAtPath("Assets/Resources/Cosmetics/"+id+".fbx") as ModelImporter;
                if(model){model.importAnimation=false;model.isReadable=true;model.SaveAndReimport();}
            }
            var crystal=AssetImporter.GetAtPath("Assets/Resources/Cosmetics/crystal.png") as TextureImporter;
            if(crystal){crystal.textureType=TextureImporterType.Sprite;crystal.spriteImportMode=SpriteImportMode.Single;crystal.alphaIsTransparency=true;crystal.mipmapEnabled=false;crystal.maxTextureSize=256;crystal.textureCompression=TextureImporterCompression.CompressedHQ;crystal.SaveAndReimport();}
        }
        public static void Windows(){PrepareArt();MimoBuild.BuildWindows();}
        public static void Android(){PrepareArt();MimoBuild.BuildAndroid();}
    }
}
