using UnityEngine;

namespace MimoGarden
{
    public sealed partial class PetActor
    {
        // OrientFace establishes front=-Z. Every tail grows from the midline of the rump toward +Z.
        Vector3 TailRearAnchor()
        {
            float x=_variantBodyBounds.center.x;
            float y=_variantBodyBounds.min.y+_variantBodyBounds.size.y*.36f;
            float rear=float.NegativeInfinity;
            if(_variantBodyTriangles!=null)for(int i=0;i<_variantBodyTriangles.Length;i+=3) {
                Vector3 a=_variantBodyVertices[_variantBodyTriangles[i]],b=_variantBodyVertices[_variantBodyTriangles[i+1]],c=_variantBodyVertices[_variantBodyTriangles[i+2]];
                float denominator=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);
                if(Mathf.Abs(denominator)<.0000001f)continue;
                float u=((b.y-c.y)*(x-c.x)+(c.x-b.x)*(y-c.y))/denominator;
                float v=((c.y-a.y)*(x-c.x)+(a.x-c.x)*(y-c.y))/denominator;
                float w=1-u-v;
                if(u<-.0001f || v<-.0001f || w<-.0001f)continue;
                rear=Mathf.Max(rear,u*a.z+v*b.z+w*c.z);
            }
            if(float.IsNegativeInfinity(rear))rear=_variantBodyBounds.max.z;
            // Small overlap joins the solid meshes, instead of leaving the tail floating above the skin.
            return new Vector3(x,y,rear-_variantBodyBounds.size.y/2.4f*.08f);
        }

        Transform AttachImportedTail(Transform sculpture,bool curled)
        {
            if(!sculpture)return null;
            Vector3 authoredJoint=_motion.InverseTransformPoint(sculpture.position);
            var root=VariantGroup(curled ? "tail:curl" : "tail:puff",_motion,Vector3.zero);
            // Keep the sculpted mesh and its normalized imported scale; do not reinterpret Blender axes.
            sculpture.SetParent(root,true);sculpture.gameObject.SetActive(true);
            if(curled) {
                // Its authored pivot is the beginning of the curled tube, not the coil's center.
                // Keep that real joint at the body surface and turn XY length into rearward YZ depth.
                sculpture.localPosition-=authoredJoint;
                root.localRotation=Quaternion.Euler(0,-90,0);
            } else {
                Bounds bounds=ImportedTailBounds(sculpture);
                sculpture.localPosition-=bounds.center;
                sculpture.localPosition+=Vector3.forward*(bounds.extents.z-.06f);
            }
            root.localPosition=TailRearAnchor();
            AddVariantWiggle(root,1,curled ? 3f : 2f);
            root.gameObject.SetActive(false);
            return root;
        }

        Bounds ImportedTailBounds(Transform sculpture)
        {
            bool first=true;Bounds result=new Bounds();
            foreach(var renderer in sculpture.GetComponentsInChildren<Renderer>(true)) {
                var filter=renderer.GetComponent<MeshFilter>();var skin=renderer as SkinnedMeshRenderer;
                var mesh=filter ? filter.sharedMesh : skin ? skin.sharedMesh : null;
                if(!mesh)continue;
                Bounds b=mesh.bounds;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2) {
                    Vector3 p=_motion.InverseTransformPoint(renderer.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));
                    if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);
                }
            }
            return result;
        }
    }
}
