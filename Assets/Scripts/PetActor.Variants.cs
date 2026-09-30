using System.Collections.Generic;
using UnityEngine;

namespace MimoGarden
{
    public sealed partial class PetActor
    {
        Transform[] _earVariants, _tailVariants, _eyeVariants;
        Transform[,] _variantEyes;
        readonly List<Mesh> _variantMeshes = new List<Mesh>();
        readonly List<VariantWiggle> _variantWiggles = new List<VariantWiggle>();
        readonly List<VisibleGeometry> _allVisibleGeometry = new List<VisibleGeometry>();
        readonly List<VisibleGeometry> _activeVisibleGeometry = new List<VisibleGeometry>();
        Vector3[] _variantBodyVertices;
        int[] _variantBodyTriangles;
        Bounds _variantBodyBounds;
        struct VariantWiggle { public Transform part; public Quaternion rest; public float side, amplitude; }
        struct VisibleGeometry { public Renderer renderer; public Transform part; public Bounds localBounds; public bool hasMesh; }

        void CacheVisibleGeometry()
        {
            _allVisibleGeometry.Clear();
            foreach (Renderer renderer in _motion.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer || !renderer.enabled) continue;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
                Mesh mesh = renderer == _body && _bakedColliderMesh ? _bakedColliderMesh : filter ? filter.sharedMesh : skinned ? skinned.sharedMesh : null;
                _allVisibleGeometry.Add(new VisibleGeometry {
                    renderer = renderer, part = renderer.transform,
                    localBounds = mesh ? mesh.bounds : new Bounds(), hasMesh = mesh != null
                });
            }
            RefreshVisibleGeometry();
        }

        void RefreshVisibleGeometry()
        {
            _activeVisibleGeometry.Clear();
            foreach (VisibleGeometry geometry in _allVisibleGeometry)
                if (geometry.renderer && geometry.renderer.enabled && geometry.renderer.gameObject.activeInHierarchy)
                    _activeVisibleGeometry.Add(geometry);
        }

        public Bounds VisibleBounds
        {
            get
            {
                Bounds bounds = new Bounds(transform.position, Vector3.zero);
                if (!_motion) return bounds;
                bool found = false;
                foreach (VisibleGeometry geometry in _activeVisibleGeometry)
                {
                    Renderer renderer = geometry.renderer;
                    if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    Bounds world;
                    if (geometry.hasMesh)
                    {
                        // Mesh topology is fixed. Transform its cached box analytically rather
                        // than traversing hundreds of inactive pieces and 8 corners every drag.
                        Matrix4x4 matrix = geometry.part.localToWorldMatrix;
                        Vector3 e = geometry.localBounds.extents;
                        Vector3 extents = new Vector3(
                            Mathf.Abs(matrix.m00) * e.x + Mathf.Abs(matrix.m01) * e.y + Mathf.Abs(matrix.m02) * e.z,
                            Mathf.Abs(matrix.m10) * e.x + Mathf.Abs(matrix.m11) * e.y + Mathf.Abs(matrix.m12) * e.z,
                            Mathf.Abs(matrix.m20) * e.x + Mathf.Abs(matrix.m21) * e.y + Mathf.Abs(matrix.m22) * e.z);
                        world = new Bounds(matrix.MultiplyPoint3x4(geometry.localBounds.center), extents * 2f);
                    }
                    else world = renderer.bounds;
                    if (!found) { bounds = world; found = true; }
                    else bounds.Encapsulate(world);
                }
                return bounds;
            }
        }

        public GameObject ActiveEarsObject => Data == null || Data.earsIndex == 0 ? null : Data.earsIndex == 1
            ? (_earGroup ? _earGroup.gameObject : _leftEar ? _leftEar.gameObject : null) : ActiveVariantObject(_earVariants, Data.earsIndex);
        public GameObject ActiveTailObject => Data == null || Data.tailIndex == 0 ? null : Data.tailIndex == 1
            ? (_puff ? _puff.gameObject : null) : Data.tailIndex == 2 ? (_curl ? _curl.gameObject : null) : ActiveVariantObject(_tailVariants, Data.tailIndex);
        public GameObject ActiveEyesObject => Data == null || Data.eyeIndex == 0 ? (_leftEye ? _leftEye.gameObject : null) : ActiveVariantObject(_eyeVariants, Data.eyeIndex);
        public string[] ActiveVariantNames
        {
            get
            {
                var names = new List<string>();
                if (ActiveEarsObject && ActiveEarsObject.activeInHierarchy) names.Add(ActiveEarsObject.name);
                if (ActiveTailObject && ActiveTailObject.activeInHierarchy) names.Add(ActiveTailObject.name);
                if (ActiveEyesObject && ActiveEyesObject.activeInHierarchy) names.Add(ActiveEyesObject.name);
                return names.ToArray();
            }
        }

        static GameObject ActiveVariantObject(Transform[] choices, int index)
        {
            return choices != null && index >= 0 && index < choices.Length && choices[index] ? choices[index].gameObject : null;
        }

        Transform VariantGroup(string name, Transform parent, Vector3 position)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(parent, false); group.localPosition = position;
            return group;
        }

        Transform VariantPart(string name, Transform parent, Vector3 position, Vector3 scale, Material material, PrimitiveType primitive = PrimitiveType.Sphere)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            Collider collider = part.GetComponent<Collider>();
            if (collider) { collider.enabled = false; Destroy(collider); }
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position; part.transform.localScale = scale;
            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return part.transform;
        }

        Transform VariantTube(string name, Transform parent, Vector3[] points, float radius, Material material)
        {
            Transform root = VariantGroup(name, parent, Vector3.zero);
            if (points.Length < 2) return root;
            const int sides = 10;
            bool closed = (points[0] - points[points.Length - 1]).sqrMagnitude < .000001f;
            var vertices = new List<Vector3>(points.Length * sides + 2);
            var triangles = new List<int>();
            Vector3 previousTangent = Vector3.up, normal = Vector3.right;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 before = i > 0 ? points[i - 1] : closed ? points[points.Length - 2] : points[0];
                Vector3 after = i < points.Length - 1 ? points[i + 1] : closed ? points[1] : points[i];
                Vector3 tangent = (after - before).normalized;
                if (tangent.sqrMagnitude < .00001f) tangent = previousTangent;
                if (i == 0) normal = Vector3.Cross(tangent, Mathf.Abs(Vector3.Dot(tangent, Vector3.forward)) < .9f ? Vector3.forward : Vector3.up).normalized;
                else normal = Quaternion.FromToRotation(previousTangent, tangent) * normal;
                Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
                normal = Vector3.Cross(binormal, tangent).normalized;
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2f / sides;
                    vertices.Add(points[i] + radius * (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)));
                    if (i == 0) continue;
                    int a = (i - 1) * sides + side, b = (i - 1) * sides + (side + 1) % sides, c = i * sides + side, d = i * sides + (side + 1) % sides;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(b); triangles.Add(d); triangles.Add(c);
                }
                previousTangent = tangent;
            }
            if (!closed)
            {
                int firstCap = vertices.Count; vertices.Add(points[0]);
                int lastCap = vertices.Count; vertices.Add(points[points.Length - 1]);
                for (int side = 0; side < sides; side++)
                {
                    int next = (side + 1) % sides, last = (points.Length - 1) * sides;
                    triangles.Add(firstCap); triangles.Add(next); triangles.Add(side);
                    triangles.Add(lastCap); triangles.Add(last + side); triangles.Add(last + next);
                }
            }
            var mesh = new Mesh { name = name + " continuous tube" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); _variantMeshes.Add(mesh);
            root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return root;
        }

        // Soft bevels turn the silhouettes into actual three-dimensional, pearl-lit pieces.
        Transform VariantShape(string name, Transform parent, Vector2[] outline, float depth, Material material)
        {
            Transform part = VariantGroup(name, parent, Vector3.zero);
            int count = outline.Length;
            Vector3[] vertices = new Vector3[count * 4];
            float[] widths = { .88f, 1f, 1f, .88f };
            float[] zs = { -depth * .5f, -depth * .28f, depth * .28f, depth * .5f };
            for (int ring = 0; ring < 4; ring++)
                for (int i = 0; i < count; i++) vertices[ring * count + i] = new Vector3(outline[i].x * widths[ring], outline[i].y * widths[ring], zs[ring]);
            var triangles = new List<int>();
            List<int> face = TriangulateVariant(outline);
            for (int i = 0; i < face.Count; i += 3)
            {
                triangles.Add(face[i + 2]); triangles.Add(face[i + 1]); triangles.Add(face[i]);
                triangles.Add(count * 3 + face[i]); triangles.Add(count * 3 + face[i + 1]); triangles.Add(count * 3 + face[i + 2]);
            }
            bool ccw = VariantArea(outline) > 0f;
            for (int ring = 0; ring < 3; ring++)
                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count, a = ring * count + i, b = ring * count + next, c = (ring + 1) * count + i, d = (ring + 1) * count + next;
                    if (ccw) { triangles.Add(a); triangles.Add(b); triangles.Add(c); triangles.Add(b); triangles.Add(d); triangles.Add(c); }
                    else { triangles.Add(a); triangles.Add(c); triangles.Add(b); triangles.Add(b); triangles.Add(c); triangles.Add(d); }
                }
            var mesh = new Mesh { name = name + " beveled mesh", vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); _variantMeshes.Add(mesh);
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        static float VariantArea(Vector2[] points)
        {
            float area = 0f;
            for (int i = 0; i < points.Length; i++) { Vector2 a = points[i], b = points[(i + 1) % points.Length]; area += a.x * b.y - b.x * a.y; }
            return area * .5f;
        }

        static float VariantCross(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

        static List<int> TriangulateVariant(Vector2[] points)
        {
            var ring = new List<int>();
            bool ccw = VariantArea(points) > 0;
            for (int i = 0; i < points.Length; i++) ring.Add(ccw ? i : points.Length - 1 - i);
            var triangles = new List<int>();
            int guard = points.Length * points.Length;
            while (ring.Count > 2 && guard-- > 0)
            {
                bool cut = false;
                for (int i = 0; i < ring.Count; i++)
                {
                    int a = ring[(i + ring.Count - 1) % ring.Count], b = ring[i], c = ring[(i + 1) % ring.Count];
                    if (VariantCross(points[a], points[b], points[c]) <= .0000001f) continue;
                    bool contains = false;
                    foreach (int v in ring)
                    {
                        if (v == a || v == b || v == c) continue;
                        if (VariantCross(points[a], points[b], points[v]) >= 0 && VariantCross(points[b], points[c], points[v]) >= 0 && VariantCross(points[c], points[a], points[v]) >= 0) { contains = true; break; }
                    }
                    if (contains) continue;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c); ring.RemoveAt(i); cut = true; break;
                }
                if (!cut) break;
            }
            return triangles;
        }

        void AddVariantWiggle(Transform part, float side, float amplitude)
        {
            _variantWiggles.Add(new VariantWiggle { part = part, rest = part.localRotation, side = side, amplitude = amplitude });
        }

        void BuildBodyVariants()
        {
            BuildVariantBodySurface();
            _earVariants = new Transform[PetCatalog.EarNames.Length];
            _tailVariants = new Transform[PetCatalog.TailNames.Length];
            _eyeVariants = new Transform[PetCatalog.EyeNames.Length];
            _variantEyes = new Transform[PetCatalog.EyeNames.Length, 2];
            Material cream = Material("Mimo ivory tips", new Color(1f, .92f, .79f), .43f);
            Material berry = Material("Mimo rose velvet", new Color(.82f, .32f, .49f), .5f);
            Material aqua = Material("Mimo lagoon", new Color(.30f, .79f, .75f), .6f, .06f);
            Material gold = Material("Mimo soft sunshine", new Color(1f, .74f, .22f), .55f, .08f);
            BuildEarVariants(cream, berry);
            BuildTailVariants(cream, aqua, gold);
            BuildEyeVariants(berry, aqua, gold);
        }

        void BuildVariantBodySurface()
        {
            Mesh mesh = _bakedColliderMesh;
            if (!mesh && _body)
            {
                MeshFilter filter = _body.GetComponent<MeshFilter>();
                if (filter && filter.sharedMesh && filter.sharedMesh.isReadable) mesh = filter.sharedMesh;
            }
            if (!mesh)
            {
                _variantBodyBounds = BodyBoundsInMotion();
                return;
            }
            _variantBodyVertices = mesh.vertices;
            _variantBodyTriangles = mesh.triangles;
            bool first = true;
            for (int i = 0; i < _variantBodyVertices.Length; i++)
            {
                Vector3 point = _motion.InverseTransformPoint(_body.transform.TransformPoint(_variantBodyVertices[i]));
                _variantBodyVertices[i] = point;
                if (first) { _variantBodyBounds = new Bounds(point, Vector3.zero); first = false; }
                else _variantBodyBounds.Encapsulate(point);
            }
        }

        Vector3 VariantBodySurface(float x, float z, float inset)
        {
            float top = float.NegativeInfinity;
            if (_variantBodyTriangles != null)
                for (int i = 0; i < _variantBodyTriangles.Length; i += 3)
                {
                    Vector3 a = _variantBodyVertices[_variantBodyTriangles[i]], b = _variantBodyVertices[_variantBodyTriangles[i + 1]], c = _variantBodyVertices[_variantBodyTriangles[i + 2]];
                    float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(denominator) < .0000001f) continue;
                    float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / denominator;
                    float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / denominator;
                    float w = 1f - u - v;
                    if (u < -.0001f || v < -.0001f || w < -.0001f) continue;
                    top = Mathf.Max(top, u * a.y + v * b.y + w * c.y);
                }
            if (float.IsNegativeInfinity(top)) top = _variantBodyBounds.center.y + _variantBodyBounds.extents.y * .55f;
            return new Vector3(x, top - inset, z);
        }

        void BuildEarVariants(Material cream, Material berry)
        {
            string[] ids = { "none", "rabbit", "cat", "fox", "bear", "puppy", "axolotl" };
            float bodyScale = _variantBodyBounds.size.y / 2.4f;
            for (int type = 2; type < _earVariants.Length; type++)
            {
                Transform root = _earVariants[type] = VariantGroup("ear:" + ids[type], _motion, Vector3.zero);
                for (int side = -1; side <= 1; side += 2)
                {
                    Transform ear = VariantGroup(ids[type] + (side < 0 ? " left" : " right"), root, new Vector3(side * .82f, 2.03f, .02f));
                    if (type == 2 || type == 3)
                    {
                        float h = type == 2 ? .66f : 1.03f;
                        Vector2[] shape = { new Vector2(-.34f, -.08f), new Vector2(.34f, -.08f), new Vector2(.28f, .21f), new Vector2(.03f, h), new Vector2(-.26f, .23f) };
                        VariantShape("soft pointed ear", ear, shape, .32f, _pearl);
                        Transform inset = VariantShape("velvet ear center", ear, shape, .045f, type == 2 ? _innerEarMaterial : cream);
                        inset.localScale = new Vector3(.57f, .64f, 1f); inset.localPosition = new Vector3(0, .10f, -.182f);
                        ear.localRotation = Quaternion.Euler(-5f, side * -9f, side * -14f);
                    }
                    else if (type == 4)
                    {
                        VariantPart("round teddy ear", ear, new Vector3(0, .19f, 0), new Vector3(.69f, .68f, .35f), _pearl);
                        VariantPart("round inner velvet", ear, new Vector3(0, .19f, -.172f), new Vector3(.42f, .42f, .095f), cream);
                    }
                    else if (type == 5)
                    {
                        ear.localPosition = new Vector3(side * 1.0f, 1.99f, .015f);
                        Transform flap = VariantPart("floppy puppy ear", ear, new Vector3(side * .08f, -.35f, -.02f), new Vector3(.50f, 1.12f, .29f), _pearl);
                        flap.localRotation = Quaternion.Euler(0, 0, side * 13f);
                        Transform lining = VariantPart("puppy velvet lining", ear, new Vector3(side * .095f, -.36f, -.17f), new Vector3(.27f, .72f, .065f), _innerEarMaterial);
                        lining.localRotation = flap.localRotation;
                        ear.localRotation = Quaternion.Euler(7f, side * -12f, 0);
                    }
                    else
                    {
                        ear.localPosition = new Vector3(side * .96f, 1.86f, .03f);
                        for (int branch = 0; branch < 3; branch++)
                        {
                            float angle = (-27f + branch * 37f) * Mathf.Deg2Rad;
                            Vector3 tip = new Vector3(side * Mathf.Cos(angle) * .79f, Mathf.Sin(angle) * .79f, 0);
                            VariantTube("axolotl frill stem", ear, new[] { Vector3.zero, tip * .52f, tip }, .072f, _pearl);
                            for (int feather = 0; feather < 4; feather++)
                            {
                                Vector3 center = Vector3.Lerp(tip * .31f, tip, feather / 3f);
                                Transform fringe = VariantPart("fluffy coral gill", ear, center, new Vector3(.17f, .30f - feather * .025f, .15f), berry);
                                fringe.localRotation = Quaternion.Euler(0, 0, side * (-30f + branch * 37f));
                            }
                        }
                    }
                    float shoulder = type == 4 ? .66f : type == 5 ? .76f : type == 6 ? .85f : .57f;
                    float x = _variantBodyBounds.center.x + side * _variantBodyBounds.extents.x * shoulder;
                    float z = _variantBodyBounds.center.z + _variantBodyBounds.extents.z * .02f;
                    ear.localPosition = VariantBodySurface(x, z, bodyScale * .11f);
                    ear.localScale = Vector3.one * bodyScale;
                    AddVariantWiggle(ear, side, type == 5 ? 7f : type == 6 ? 4.5f : 2.2f);
                }
                root.gameObject.SetActive(false);
            }
        }

        void BuildTailVariants(Material cream, Material aqua, Material gold)
        {
            string[] ids = { "none", "puff", "curl", "fox", "cat", "dragon", "fish", "lightning" };
            float bodyScale = _variantBodyBounds.size.y / 2.4f;
            for (int type = 3; type < _tailVariants.Length; type++)
            {
                Transform root = _tailVariants[type] = VariantGroup("tail:" + ids[type], _motion, new Vector3(.63f, .36f, .72f));
                if (type == 3)
                {
                    for (int i = 0; i < 9; i++)
                    {
                        float t = i / 8f, angle = Mathf.Lerp(-.5f, .95f, t);
                        float radius = Mathf.Lerp(.19f, .03f, t) + Mathf.Sin(t * Mathf.PI) * .29f;
                        Vector3 point = new Vector3(Mathf.Sin(angle) * 1.43f + .60f, t * .49f, Mathf.Cos(angle) * .25f - .1f);
                        VariantPart(i > 5 ? "ivory fox tip" : "plush fox tail", root, point, new Vector3(radius * 2f, radius * 2.3f, radius * 1.7f), i > 5 ? cream : _pearl);
                    }
                }
                else if (type == 4)
                {
                    Vector3[] curve = { new Vector3(0, 0, 0), new Vector3(.55f, .03f, 0), new Vector3(.95f, .30f, -.03f), new Vector3(1.14f, .69f, -.08f), new Vector3(1.08f, 1.08f, -.11f), new Vector3(.87f, 1.22f, -.13f), new Vector3(.69f, 1.05f, -.15f) };
                    VariantTube("curious cat tail", root, curve, .12f, _pearl);
                    VariantPart("little ivory tail tip", root, curve[curve.Length - 1], Vector3.one * .265f, cream);
                }
                else if (type == 5)
                {
                    root.localPosition = new Vector3(-.56f, .32f, .76f);
                    for (int i = 0; i < 9; i++)
                    {
                        float t = i / 8f, radius = Mathf.Lerp(.34f, .055f, t);
                        Vector3 center = new Vector3(-t * 1.53f, .17f + Mathf.Sin(t * Mathf.PI) * .24f, t * -.24f);
                        VariantPart("dragon tail plush segment", root, center, new Vector3(radius * 2.5f, radius * 1.7f, radius * 2f), _pearl);
                        if (i % 2 == 0 && i < 8)
                        {
                            Transform spike = VariantShape("soft golden dragon scale", root, new[] { new Vector2(-.14f, 0), new Vector2(.13f, 0), new Vector2(0, .27f) }, .14f, gold);
                            spike.localPosition = center + Vector3.up * radius * .6f;
                        }
                    }
                }
                else if (type == 6)
                {
                    VariantPart("little mermaid tail base", root, new Vector3(.44f, .06f, 0), new Vector3(1.0f, .46f, .37f), _pearl);
                    Vector2[] fin = { new Vector2(-.13f, 0), new Vector2(.16f, -.33f), new Vector2(.73f, -.58f), new Vector2(.59f, -.08f), new Vector2(.84f, .47f), new Vector2(.22f, .36f) };
                    Transform fan = VariantShape("scalloped mermaid fin", root, fin, .19f, aqua);
                    fan.localPosition = new Vector3(.85f, .08f, -.05f);
                    VariantTube("pearl fin vein upper", fan, new[] { new Vector3(0, 0, -.11f), new Vector3(.25f, .12f, -.11f), new Vector3(.55f, .3f, -.11f) }, .018f, cream);
                    VariantTube("pearl fin vein lower", fan, new[] { new Vector3(0, 0, -.11f), new Vector3(.25f, -.12f, -.11f), new Vector3(.53f, -.38f, -.11f) }, .018f, cream);
                }
                else
                {
                    Vector2[] bolt = { new Vector2(-.20f, -.03f), new Vector2(.36f, .58f), new Vector2(.07f, .55f), new Vector2(.65f, 1.31f), new Vector2(.06f, 1.02f), new Vector2(.28f, 1.55f), new Vector2(-.43f, .59f), new Vector2(-.04f, .66f) };
                    Transform lightning = VariantShape("squishy lightning bolt", root, bolt, .23f, gold);
                    lightning.localPosition = new Vector3(.8f, -.1f, -.12f);
                    VariantPart("lightning pearl joint", root, new Vector3(.32f, .01f, 0), new Vector3(.78f, .32f, .31f), _pearl);
                }
                root.localScale = Vector3.one * bodyScale;
                root.localPosition = new Vector3(_variantBodyBounds.center.x + (type == 5 ? -.40f : .42f) * _variantBodyBounds.extents.x,
                    _variantBodyBounds.min.y + _variantBodyBounds.size.y * (type == 6 ? .29f : .23f),
                    _variantBodyBounds.center.z + _variantBodyBounds.extents.z * .40f);
                AddVariantWiggle(root, 1f, type == 4 ? 5f : 3.5f);
                root.gameObject.SetActive(false);
            }
        }

        void BuildEyeVariants(Material berry, Material aqua, Material gold)
        {
            string[] ids = { "classic", "round", "star", "heart", "cat", "robot" };
            Vector3[] centers = { EyeVariantCenter(_leftEye, -.43f), EyeVariantCenter(_rightEye, .43f) };
            Material violet = Material("Mimo starlight eyes", new Color(.22f, .12f, .34f), .79f, .10f);
            Material glow = Material("Mimo robot mint glass", new Color(.53f, 1f, .89f), .76f, .14f);
            for (int type = 1; type < _eyeVariants.Length; type++)
            {
                Transform root = _eyeVariants[type] = VariantGroup("eyes:" + ids[type], _motion, Vector3.zero);
                for (int side = 0; side < 2; side++)
                {
                    Transform eye = _variantEyes[type, side] = VariantGroup(ids[type] + " eye " + side, root, centers[side] + Vector3.back * .018f);
                    if (type == 1) VariantPart("round shiny eye", eye, Vector3.zero, new Vector3(.53f, .54f, .24f), _eyeMaterial);
                    else if (type == 2)
                    {
                        Vector2[] star = new Vector2[10];
                        for (int i = 0; i < star.Length; i++) { float a = Mathf.PI * .5f + i * Mathf.PI / 5; float r = i % 2 == 0 ? .33f : .17f; star[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r; }
                        VariantShape("shiny star eye", eye, star, .20f, violet);
                        VariantPart("golden starlight", eye, new Vector3(.025f, .028f, -.125f), new Vector3(.09f, .14f, .025f), gold);
                    }
                    else if (type == 3)
                    {
                        Vector2[] heart = new Vector2[32];
                        for (int i = 0; i < heart.Length; i++)
                        {
                            float a = i * Mathf.PI * 2f / heart.Length;
                            heart[i] = new Vector2(16f * Mathf.Pow(Mathf.Sin(a), 3f), 13f * Mathf.Cos(a) - 5f * Mathf.Cos(2f * a) - 2f * Mathf.Cos(3f * a) - Mathf.Cos(4f * a)) * .020f;
                        }
                        VariantShape("berry heart eye", eye, heart, .18f, berry);
                    }
                    else if (type == 4)
                    {
                        Vector2[] almond = { new Vector2(-.34f, 0), new Vector2(-.2f, -.19f), new Vector2(.08f, -.21f), new Vector2(.32f, 0), new Vector2(.18f, .19f), new Vector2(-.09f, .22f) };
                        VariantShape("cat almond rim", eye, almond, .19f, _eyeMaterial);
                        Transform iris = VariantShape("turquoise cat iris", eye, almond, .10f, aqua);
                        iris.localScale = new Vector3(.77f, .74f, 1f); iris.localPosition = Vector3.back * .075f;
                        VariantPart("vertical cat pupil", eye, new Vector3(0, 0, -.142f), new Vector3(.075f, .29f, .043f), _eyeMaterial);
                    }
                    else
                    {
                        VariantPart("robot rounded frame", eye, Vector3.zero, new Vector3(.58f, .47f, .22f), violet, PrimitiveType.Cube);
                        VariantPart("robot glass screen", eye, new Vector3(0, 0, -.118f), new Vector3(.46f, .34f, .048f), glow, PrimitiveType.Cube);
                        for (int pixel = 0; pixel < 3; pixel++)
                            VariantPart("friendly robot eye pixel", eye, new Vector3((pixel - 1) * .10f, .007f, -.155f), new Vector3(.057f, pixel == 1 ? .16f : .10f, .031f), _eyeMaterial, PrimitiveType.Cube);
                    }
                    // Glints belong to the assembly so every style blinks and emotes together.
                    VariantPart("eye soft reflection", eye, new Vector3(-.10f, .13f, -.17f), new Vector3(.11f, .13f, .035f), _whiteMaterial);
                    VariantPart("eye tiny sparkle", eye, new Vector3(.11f, -.09f, -.165f), new Vector3(.047f, .054f, .025f), _whiteMaterial);
                }
                root.gameObject.SetActive(false);
            }
        }

        Vector3 EyeVariantCenter(Transform eye, float fallbackX)
        {
            Renderer renderer = eye ? eye.GetComponent<Renderer>() : null;
            return renderer ? _motion.InverseTransformPoint(renderer.bounds.center) : new Vector3(fallbackX, 1.15f, -1.03f);
        }

        void ApplyBodyVariants()
        {
            for (int i = 2; i < _earVariants.Length; i++) if (_earVariants[i]) _earVariants[i].gameObject.SetActive(Data.earsIndex == i);
            for (int i = 3; i < _tailVariants.Length; i++) if (_tailVariants[i]) _tailVariants[i].gameObject.SetActive(Data.tailIndex == i);
            for (int i = 1; i < _eyeVariants.Length; i++) if (_eyeVariants[i]) _eyeVariants[i].gameObject.SetActive(Data.eyeIndex == i);
            if (_leftEye) _leftEye.gameObject.SetActive(Data.eyeIndex == 0);
            if (_rightEye) _rightEye.gameObject.SetActive(Data.eyeIndex == 0);
        }

        void AnimateBodyVariants(float t, float dt)
        {
            foreach (VariantWiggle wiggle in _variantWiggles)
                if (wiggle.part && wiggle.part.gameObject.activeInHierarchy)
                {
                    float sway = Mathf.Sin(t * 2.5f + wiggle.side) * wiggle.amplitude - _squashVelocity * 6f;
                    wiggle.part.localRotation = wiggle.rest * Quaternion.Euler(sway * .7f, sway * .6f, sway * wiggle.side);
                }
        }

        void AnimateVariantFace(float openness, float tilt, float easing)
        {
            if (Data.eyeIndex == 0 || _variantEyes == null) return;
            for (int side = 0; side < 2; side++)
            {
                Transform eye = _variantEyes[Data.eyeIndex, side];
                if (!eye) continue;
                eye.localScale = Vector3.Lerp(eye.localScale, new Vector3(1f, openness, 1f), easing);
                eye.localRotation = Quaternion.Slerp(eye.localRotation, Quaternion.Euler(0, 0, side == 0 ? tilt : -tilt), easing);
            }
        }

        void ReleaseBodyVariants()
        {
            foreach (Mesh mesh in _variantMeshes) if (mesh) Destroy(mesh);
            _variantMeshes.Clear(); _variantWiggles.Clear();
            _allVisibleGeometry.Clear(); _activeVisibleGeometry.Clear();
            _earVariants = _tailVariants = _eyeVariants = null;
            _variantEyes = null;
            _variantBodyVertices = null; _variantBodyTriangles = null;
            _variantBodyBounds = new Bounds();
        }
    }
}
