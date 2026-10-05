using UnityEngine;

namespace MimoGarden
{
    public sealed partial class MimoCreationStation
    {
        Transform BuildKitchenIngredient(int index)
        {
            Transform root = Group("Kitchen " + MimoSurpriseRecipe.Names[index], transform);
            Material ivory = Mat("Kitchen ivory", C("FFF2DC"), .3f);
            Material milk = Mat("Kitchen milk", C("FFFEF4"), .45f);
            Material jade = Mat("Kitchen jade label", C("8FC8B9"), .4f);
            Material kraft = Mat("Kitchen flour bag", C("D7B17E"), .15f);
            Material wheat = Mat("Kitchen wheat mark", C("A7804E"), .2f);
            if (index == 0)
            {
                Part("Rounded flour sack", root, sphereMesh, Vector3.zero, new Vector3(.60f,.69f,.36f), kraft);
                Part("Folded paper opening", root, cubeMesh, new Vector3(0,.32f,0), new Vector3(.47f,.09f,.18f), kraft);
                Part("Flour sack label", root, sphereMesh, new Vector3(0,0,-.18f), new Vector3(.31f,.36f,.025f), ivory);
                Part("Wheat stalk", root, cylinderMesh, new Vector3(0,0,-.20f), new Vector3(.018f,.24f,.018f), wheat);
                for (int i=0;i<3;i++) for(int side=-1;side<=1;side+=2)
                {
                    Transform seed=Part("Wheat seed",root,sphereMesh,new Vector3(side*.045f,-.06f+i*.066f,-.208f),new Vector3(.09f,.037f,.022f),wheat);
                    seed.localRotation=Quaternion.Euler(0,0,side*25);
                }
            }
            else if (index == 1)
            {
                Mesh egg = Lathe("Soft egg",new[]{new Vector2(0,-.29f),new Vector2(.16f,-.25f),new Vector2(.23f,-.12f),new Vector2(.24f,.02f),new Vector2(.19f,.17f),new Vector2(.10f,.29f),new Vector2(0,.33f)},24);
                Part("A kitchen egg",root,egg,Vector3.zero,Vector3.one,ivory);
                Part("Egg highlight",root,sphereMesh,new Vector3(-.08f,.12f,-.177f),new Vector3(.06f,.12f,.018f),milk);
            }
            else
            {
                Mesh bottle = Lathe("Milk bottle",new[]{new Vector2(0,-.30f),new Vector2(.21f,-.30f),new Vector2(.24f,-.23f),new Vector2(.24f,.18f),new Vector2(.13f,.30f),new Vector2(.13f,.40f),new Vector2(0,.40f)},24);
                Part("Cream milk bottle",root,bottle,Vector3.zero,Vector3.one,milk);
                Part("Mint bottle cap",root,cylinderMesh,new Vector3(0,.415f,0),new Vector3(.29f,.07f,.29f),jade);
                Part("Milk label",root,sphereMesh,new Vector3(0,-.01f,-.24f),new Vector3(.34f,.30f,.028f),jade);
                Part("Milk drop mark",root,sphereMesh,new Vector3(0,-.01f,-.26f),new Vector3(.105f,.14f,.02f),milk);
            }
            BakeParts(root); return root;
        }
    }
}
