using UnityEngine;
using UnityEngine.UI;

namespace MimoGarden
{
    /// <summary>Resolution-independent pantry illustrations for the experimental kitchen.</summary>
    public sealed class MimoKitchenIcon : MaskableGraphic
    {
        public int Ingredient;
        VertexHelper mesh;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            mesh = vh; mesh.Clear();
            Ellipse(51, 13, 34, 6, "D7DFCF");
            if (Ingredient == 0)
            {
                Polygon("C99763", 22,18, 78,18, 74,65, 66,79, 33,79, 26,65);
                Polygon("E3BF8F", 25,21, 73,21, 69,65, 64,75, 35,75, 30,64);
                Polygon("F0D6AD", 28,25, 41,25, 39,70, 33,68);
                Polygon("AD7E53", 30,78, 70,78, 67,85, 33,85);
                Ellipse(50, 47, 19, 23, "FFF8E8");
                Polygon("B68D53", 49,31, 52,31, 52,62, 49,62);
                for (int i = 0; i < 3; i++)
                { Ellipse(44, 42+i*7, 6, 3, "CEA969"); Ellipse(57, 45+i*7, 6, 3, "CEA969"); }
            }
            else if (Ingredient == 1)
            {
                Ellipse(51, 47, 28, 33, "DEC7A6");
                Egg("F9EAD2", 49, 48, 27, 36);
                Egg("FFF7E8", 45, 51, 21, 30);
                Ellipse(38, 64, 5, 10, "FFFFFF");
                Ellipse(64, 32, 2, 2, "DFBF91"); Ellipse(60, 27, 1.5f, 1.5f, "DFBF91");
            }
            else
            {
                Polygon("BADBD8", 30,18, 72,18, 72,66, 63,76, 63,85, 39,85, 39,76, 30,66);
                Polygon("FFFDF3", 33,21, 66,21, 66,64, 59,73, 59,84, 42,84, 42,73, 33,63);
                Polygon("6BADA6", 39,81, 63,81, 63,89, 39,89);
                Polygon("A5D7CF", 33,30, 66,30, 66,59, 33,59);
                Ellipse(49, 43, 9, 9, "FFFDF3");
                Polygon("FFFDF3", 40,45, 58,45, 49,57);
                Polygon("FFFFFF", 36,64, 39,64, 39,72, 36,70);
            }
        }
        void Egg(string color, float x, float y, float rx, float ry)
        {
            var p = new float[64];
            for (int i=0;i<32;i++) { float a=i*Mathf.PI/16, s=Mathf.Sin(a); p[i*2]=x+Mathf.Cos(a)*rx*(1-s*.18f);p[i*2+1]=y+s*ry; }
            Polygon(color,p);
        }
        void Ellipse(float x,float y,float rx,float ry,string color)
        {
            var p=new float[64]; for(int i=0;i<32;i++){float a=i*Mathf.PI/16;p[i*2]=x+Mathf.Cos(a)*rx;p[i*2+1]=y+Mathf.Sin(a)*ry;} Polygon(color,p);
        }
        void Polygon(string hex, params float[] points)
        {
            ColorUtility.TryParseHtmlString("#"+hex,out Color tint);
            Rect rect=GetPixelAdjustedRect(); int start=mesh.currentVertCount;
            for(int i=0;i<points.Length;i+=2) mesh.AddVert(new Vector3(rect.x+rect.width*points[i]/100,rect.y+rect.height*points[i+1]/100),tint,Vector2.zero);
            for(int i=1;i<points.Length/2-1;i++) mesh.AddTriangle(start,start+i,start+i+1);
        }
    }
}
