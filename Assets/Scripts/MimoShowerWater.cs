using UnityEngine;
using UnityEngine.Rendering;

namespace MimoGarden
{
    public sealed class MimoShowerWater : MonoBehaviour
    {
        const int Jets = 18, Segments = 6, Drops = 160;
        const int Quads = Jets * Segments + Drops;
        Mesh mesh;
        Material material;
        Vector3[] vertices;
        Vector2[] uv, style;
        public void Initialize()
        {
            vertices = new Vector3[Quads * 4]; uv = new Vector2[vertices.Length]; style = new Vector2[vertices.Length];
            var triangles = new int[Quads * 6];
            for (int i = 0; i < Quads; i++)
            {
                int v = i * 4, t = i * 6;
                bool drop = i >= Jets * Segments;
                uv[v] = new Vector2(-1, drop ? -1 : 0); uv[v+1] = new Vector2(-1,1); uv[v+2] = new Vector2(1,1); uv[v+3] = new Vector2(1,drop ? -1 : 0);
                for (int q = 0; q < 4; q++) style[v+q] = new Vector2(drop ? 1 : 0, Mathf.Repeat(i * .618f, 1));
                triangles[t] = v; triangles[t+1] = v+1; triangles[t+2] = v+2;
                triangles[t+3] = v; triangles[t+4] = v+2; triangles[t+5] = v+3;
            }
            mesh = new Mesh { name = "Abundant clear shower streams and 160 droplets", vertices = vertices, uv = uv, uv2 = style, triangles = triangles }; mesh.MarkDynamic();
            material = new Material(Shader.Find("MimoGarden/ShowerWater")) { name = "Clear flowing water with glints" };
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
        Vector3 Stream(Vector3 start, Vector3 end, Vector3 right, int jet, float p, float time)
        {
            float spread = (jet - (Jets-1)*.5f) / (Jets-1);
            Vector3 bend = right * (Mathf.Sin(p*8 + time*8 + jet*1.6f) * .015f * Mathf.Sin(p*Mathf.PI));
            return Vector3.Lerp(start, end, p) + right * spread * Mathf.Lerp(.65f, 1.32f, p) + bend;
        }
        public void Animate(Vector3 start, Vector3 end, Camera camera)
        {
            if (!mesh) return;
            Vector3 right = camera.transform.right, up = camera.transform.up;
            float time = Time.unscaledTime;
            for (int jet = 0; jet < Jets; jet++) for (int segment = 0; segment < Segments; segment++)
            {
                float a = segment/(float)Segments, b = (segment+1)/(float)Segments;
                Vector3 p = Stream(start,end,right,jet,a,time), q = Stream(start,end,right,jet,b,time);
                Vector3 width = right * (.025f + .010f * Mathf.Sin(time*6 + jet*2));
                int v = (jet*Segments+segment)*4;
                vertices[v] = p-width; vertices[v+1] = q-width; vertices[v+2] = q+width; vertices[v+3] = p+width;
            }
            for (int drop = 0; drop < Drops; drop++)
            {
                float seed = Mathf.Repeat(drop * .618034f, 1), p = Mathf.Repeat(time*(1.0f+seed*.7f) + drop*.173f, 1);
                Vector3 center;
                if (drop < 112)
                    center = Vector3.Lerp(start,end,p*p) + right * Mathf.Sin(drop*5.2f) * Mathf.Lerp(.33f,.80f,p) + camera.transform.forward * Mathf.Cos(drop*2.4f)*.10f;
                else
                    center = end + right * Mathf.Sin(drop*3.2f)*p*.75f + Vector3.up * (Mathf.Sin(p*Mathf.PI)*.23f - p*.16f);
                float size = .019f + seed*.028f;
                Vector3 x = right*size, y = up*size*(drop<112 ? 1.65f : 1);
                int v = (Jets*Segments+drop)*4;
                vertices[v] = center-x-y; vertices[v+1] = center-x+y; vertices[v+2] = center+x+y; vertices[v+3] = center+x-y;
            }
            mesh.vertices = vertices;
            Bounds bounds = new Bounds(start,Vector3.one*2); bounds.Encapsulate(end); bounds.Expand(1.5f); mesh.bounds = bounds;
        }
        void OnDestroy() { if (mesh) Destroy(mesh); if (material) Destroy(material); }
    }
}
