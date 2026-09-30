using System.Collections.Generic;
using UnityEngine;

namespace MimoGarden
{
    public sealed partial class PetActor
    {
        Transform _accessoryRoot;
        readonly Transform[] _glassesAccessories = new Transform[6];
        readonly Transform[] _hatAccessories = new Transform[6];
        readonly Transform[] _wingAccessories = new Transform[6];
        readonly Transform[,] _wingPivots = new Transform[6, 2];
        readonly Dictionary<string, Material> _accessoryMaterials = new Dictionary<string, Material>();
        public int ActiveAccessoryCount { get { return ActiveAccessoryNames.Length; } }
        public string[] ActiveAccessoryNames
        {
            get
            {
                var names = new List<string>();
                foreach (Transform item in _glassesAccessories) if (item && item.gameObject.activeInHierarchy) names.Add(item.name);
                foreach (Transform item in _hatAccessories) if (item && item.gameObject.activeInHierarchy) names.Add(item.name);
                foreach (Transform item in _wingAccessories) if (item && item.gameObject.activeInHierarchy) names.Add(item.name);
                return names.ToArray();
            }
        }

        void BuildAccessories()
        {
            if (_accessoryRoot) return;
            _accessoryRoot = VariantGroup("Mimo colorful accessories", _motion, Vector3.zero);
            BuildGlassesAccessories(); BuildHatAccessories(); BuildWingAccessories();
            foreach (Transform item in _glassesAccessories) if (item) item.gameObject.SetActive(false);
            foreach (Transform item in _hatAccessories) if (item) item.gameObject.SetActive(false);
            foreach (Transform item in _wingAccessories) if (item) item.gameObject.SetActive(false);
        }

        void ApplyAccessories()
        {
            if (Data == null || !_motion) return;
            if (!_accessoryRoot) BuildAccessories();
            int glasses = Mathf.Clamp(Data.glassesIndex, 0, 5), hat = Mathf.Clamp(Data.hatIndex, 0, 5), wings = Mathf.Clamp(Data.wingsIndex, 0, 5);
            for (int i = 1; i < 6; i++)
            {
                if (_glassesAccessories[i]) _glassesAccessories[i].gameObject.SetActive(i == glasses);
                if (_hatAccessories[i]) _hatAccessories[i].gameObject.SetActive(i == hat);
                if (_wingAccessories[i]) _wingAccessories[i].gameObject.SetActive(i == wings);
            }
        }

        void AnimateAccessories(float t, float dt)
        {
            if (Data == null) return;
            int index = Mathf.Clamp(Data.wingsIndex, 0, 5);
            if (index == 0) return;
            float flutter = Mathf.Sin(t * (index == 1 ? 5.8f : 3.4f)) * (index == 1 ? 9f : 6f);
            if (_carried || _pressed) flutter += Mathf.Sin(t * 13f) * 9f;
            for (int i = 0; i < 2; i++)
            {
                Transform wing = _wingPivots[index, i]; if (!wing) continue;
                float side = i == 0 ? -1f : 1f;
                Quaternion target = Quaternion.Euler(0, side * (14f + flutter), side * Mathf.Sin(t * 2.4f) * 2.2f);
                wing.localRotation = Quaternion.Slerp(wing.localRotation, target, 1f - Mathf.Exp(-16f * dt));
            }
        }

        void ReleaseAccessories()
        {
            _accessoryRoot = null;
            for (int i = 0; i < 6; i++)
            {
                _glassesAccessories[i] = _hatAccessories[i] = _wingAccessories[i] = null;
                _wingPivots[i, 0] = _wingPivots[i, 1] = null;
            }
            // Material() and VariantShape/Tube register their resources with the character owner.
            _accessoryMaterials.Clear();
        }

        Material AccessoryMaterial(string name, string hex, float metallic = .08f)
        {
            Material result; if (_accessoryMaterials.TryGetValue(name, out result)) return result;
            Color color; ColorUtility.TryParseHtmlString("#" + hex, out color);
            result = Material("Accessory · " + name, color, .48f, metallic);
            _accessoryMaterials.Add(name, result); return result;
        }

        Transform AccessoryPart(string name, Transform parent, Vector3 p, Vector3 scale, Material material)
        { return VariantPart(name, parent, p, scale, material); }

        Vector2[] AccessoryStar(int points, float outer, float inner)
        {
            var outline = new Vector2[points * 2];
            for (int i = 0; i < outline.Length; i++)
            {
                float a = Mathf.PI * .5f + i * Mathf.PI / points, r = i % 2 == 0 ? outer : inner;
                outline[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            return outline;
        }

        void AccessoryLoop(string name, Transform parent, Vector2[] contour, Vector3 center, float radius, Material material)
        {
            // A single low-poly tube keeps an open frame light enough for a tablet.
            const int ringSides = 8; int count = contour.Length;
            var vertices = new Vector3[count * ringSides]; var triangles = new int[count * ringSides * 6];
            for (int i = 0; i < count; i++)
            {
                Vector2 tangent = (contour[(i + 1) % count] - contour[(i + count - 1) % count]).normalized;
                Vector3 normal = new Vector3(-tangent.y, tangent.x, 0);
                for (int j = 0; j < ringSides; j++)
                {
                    float a = j * Mathf.PI * 2f / ringSides;
                    vertices[i * ringSides + j] = center + new Vector3(contour[i].x, contour[i].y, 0) + radius * (normal * Mathf.Cos(a) + Vector3.forward * Mathf.Sin(a));
                    int current = i * ringSides + j, section = i * ringSides + (j + 1) % ringSides;
                    int next = ((i + 1) % count) * ringSides + j, nextSection = ((i + 1) % count) * ringSides + (j + 1) % ringSides, k = current * 6;
                    triangles[k] = current; triangles[k + 1] = section; triangles[k + 2] = next;
                    triangles[k + 3] = section; triangles[k + 4] = nextSection; triangles[k + 5] = next;
                }
            }
            Mesh mesh = new Mesh { name = name, vertices = vertices, triangles = triangles }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); _variantMeshes.Add(mesh);
            Transform obj = VariantGroup(name, parent, Vector3.zero); obj.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh; obj.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        Vector2[] AccessoryEllipse(float x, float y, int count = 36)
        {
            var points = new Vector2[count];
            for (int i = 0; i < count; i++) { float a = i * Mathf.PI * 2f / count; points[i] = new Vector2(Mathf.Cos(a) * x, Mathf.Sin(a) * y); }
            return points;
        }

        Bounds AccessoryBodyGeometryBounds()
        {
            // The FBX skin's bind-pose bounds extend above the rendered knot. Anchor to the
            // already baked collider vertices, which follow the visible neutral body exactly.
            Mesh source = _bakedColliderMesh;
            if (!source && _body)
            {
                MeshFilter filter = _body.GetComponent<MeshFilter>();
                if (filter) source = filter.sharedMesh;
            }
            if (!source || !_body) return BodyBoundsInMotion();
            Vector3[] vertices = source.vertices;
            if (vertices.Length == 0) return BodyBoundsInMotion();
            Bounds bounds = new Bounds(_motion.InverseTransformPoint(_body.transform.TransformPoint(vertices[0])), Vector3.zero);
            for (int i = 1; i < vertices.Length; i++) bounds.Encapsulate(_motion.InverseTransformPoint(_body.transform.TransformPoint(vertices[i])));
            return bounds;
        }

        void BuildGlassesAccessories()
        {
            Vector3 eyeLeft = new Vector3(-.44f, 1.13f, -1.1f), eyeRight = new Vector3(.44f, 1.13f, -1.1f);
            Renderer leftRenderer = _leftEye ? _leftEye.GetComponent<Renderer>() : null;
            Renderer rightRenderer = _rightEye ? _rightEye.GetComponent<Renderer>() : null;
            if (leftRenderer) eyeLeft = _motion.InverseTransformPoint(leftRenderer.bounds.center);
            if (rightRenderer) eyeRight = _motion.InverseTransformPoint(rightRenderer.bounds.center);
            if (eyeLeft.x > eyeRight.x) { Vector3 temporary = eyeLeft; eyeLeft = eyeRight; eyeRight = temporary; }
            float halfGap = Mathf.Max(.31f, (eyeRight.x - eyeLeft.x) * .5f), centerX = (eyeLeft.x + eyeRight.x) * .5f;
            float eyeY = (eyeLeft.y + eyeRight.y) * .5f, front = Mathf.Min(eyeLeft.z, eyeRight.z) - .14f;
            float width = Mathf.Clamp(halfGap * .76f, .25f, .38f), height = width * 1.23f;
            string[] names = { "", "Round", "Stars", "Hearts", "Visor", "Explorer" };
            Material[] frames = { null, AccessoryMaterial("Sapphire frames", "245DE8", .22f), AccessoryMaterial("Sunshine frames", "F2AD19", .3f), AccessoryMaterial("Cherry frames", "E92C72"), AccessoryMaterial("Aqua visor", "00BBAE", .25f), AccessoryMaterial("Explorer tangerine", "F47827") };
            for (int index = 1; index < 6; index++)
            {
                Transform root = VariantGroup("Glasses." + index + "." + names[index], _accessoryRoot, Vector3.zero); _glassesAccessories[index] = root;
                if (index == 4)
                {
                    AccessoryLoop("Open panoramic visor", root, AccessoryEllipse(halfGap + width + .08f, height * 1.04f, 48), new Vector3(centerX, eyeY, front), .035f, frames[index]);
                    VariantTube("Visor brow accent", root, new[] { new Vector3(centerX - halfGap - width * .76f, eyeY + height * .66f, front - .027f), new Vector3(centerX, eyeY + height * 1.05f, front - .027f), new Vector3(centerX + halfGap + width * .76f, eyeY + height * .66f, front - .027f) }, .018f, AccessoryMaterial("Visor yellow", "F5D424"));
                }
                else
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 center = new Vector3(centerX + side * halfGap, eyeY, front); Vector2[] outline;
                        if (index == 2)
                        {
                            outline = AccessoryStar(5, width * 1.51f, width * 1.04f);
                            for (int p = 0; p < outline.Length; p++) outline[p].y *= 1.08f;
                        }
                        else if (index == 3)
                        {
                            outline = new Vector2[44];
                            for (int p = 0; p < outline.Length; p++)
                            {
                                float a = p * Mathf.PI * 2f / outline.Length;
                                outline[p] = new Vector2(Mathf.Pow(Mathf.Sin(a), 3f) * width * 1.24f,
                                    (13f * Mathf.Cos(a) - 5f * Mathf.Cos(2f * a) - 2f * Mathf.Cos(3f * a) - Mathf.Cos(4f * a) + 2f) / 17f * height * 1.15f);
                            }
                        }
                        else outline = AccessoryEllipse(width, height);
                        AccessoryLoop("Open " + names[index] + " frame " + side, root, outline, center, index == 5 ? .055f : .029f, frames[index]);
                        if (index == 5)
                        {
                            AccessoryLoop("Explorer metal inner rim " + side, root, AccessoryEllipse(width - .035f, height - .035f), center + Vector3.back * .035f, .014f, AccessoryMaterial("Explorer copper", "8D4629", .28f));
                            AccessoryPart("Explorer side buckle " + side, root, center + new Vector3(side * (width + .075f), 0, .1f), new Vector3(.12f, .26f, .12f), AccessoryMaterial("Explorer gold", "F5C62F", .28f));
                        }
                    }
                    VariantTube("Glasses bridge", root, new[] { new Vector3(centerX - halfGap + width * .94f, eyeY + .06f, front), new Vector3(centerX, eyeY + .12f, front - .02f), new Vector3(centerX + halfGap - width * .94f, eyeY + .06f, front) }, .028f, frames[index]);
                }
                for (int side = -1; side <= 1; side += 2)
                    VariantTube("Glasses arm " + side, root, new[] { new Vector3(centerX + side * (halfGap + width), eyeY + .035f, front), new Vector3(centerX + side * (halfGap + width + .18f), eyeY + .03f, front + .32f), new Vector3(centerX + side * (halfGap + width + .15f), eyeY - .04f, front + .5f) }, .025f, frames[index]);
            }
        }

        void BuildHatAccessories()
        {
            Bounds body = AccessoryBodyGeometryBounds();
            float hatTop = body.max.y, centerZ = body.center.z;
            Material blue = AccessoryMaterial("Cap cobalt", "2862DF"), gold = AccessoryMaterial("Crown gold", "F6B816", .42f);
            Material pink = AccessoryMaterial("Flower pink", "ED3C87"), lime = AccessoryMaterial("Flower leaf", "53B841");
            Transform cap = VariantGroup("Hat.1.Cap", _accessoryRoot, new Vector3(-.12f, hatTop - .15f, centerZ - .06f)); _hatAccessories[1] = cap;
            cap.localRotation = Quaternion.Euler(0, -14, -10);
            AccessoryPart("Cap round crown", cap, new Vector3(0, .12f, 0), new Vector3(1.13f, .62f, .92f), blue);
            AccessoryPart("Cap forward brim", cap, new Vector3(0, -.035f, -.55f), new Vector3(1.17f, .11f, .85f), blue);
            AccessoryPart("Cap top button", cap, new Vector3(0, .45f, 0), Vector3.one * .12f, gold);
            Transform capStar = VariantShape("Cap golden badge", cap, AccessoryStar(5, .14f, .07f), .04f, gold); capStar.localPosition = new Vector3(0, .16f, -.45f);
            Transform crown = VariantGroup("Hat.2.Crown", _accessoryRoot, new Vector3(0, hatTop - .18f, centerZ)); _hatAccessories[2] = crown;
            var crownBand = new Vector3[33];
            for (int i = 0; i < crownBand.Length; i++) { float a = i * Mathf.PI * 2f / 32; crownBand[i] = new Vector3(Mathf.Cos(a) * .52f, .06f, Mathf.Sin(a) * .44f); }
            VariantTube("Crown circlet", crown, crownBand, .09f, gold);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                Transform point = VariantShape("Crown royal point " + i, crown, new[] { new Vector2(-.22f, 0), new Vector2(.22f, 0), new Vector2(.16f, .18f), new Vector2(0, .43f), new Vector2(-.16f, .18f) }, .075f, gold);
                point.localPosition = new Vector3(Mathf.Sin(a) * .48f, .09f, -Mathf.Cos(a) * .41f); point.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
                AccessoryPart("Crown jewel " + i, point, new Vector3(0, .18f, -.055f), new Vector3(.14f, .2f, .07f), AccessoryMaterial("Crown berry jewels", "C52DB2", .2f));
            }
            Transform wizard = VariantGroup("Hat.3.Wizard", _accessoryRoot, new Vector3(0, hatTop - .12f, centerZ + .03f)); _hatAccessories[3] = wizard;
            AccessoryPart("Wizard brim", wizard, Vector3.zero, new Vector3(1.47f, .11f, 1.1f), AccessoryMaterial("Wizard purple", "7133C5"));
            AccessoryCone("Bent wizard cone", wizard, .5f, 1.03f, AccessoryMaterial("Wizard purple", "7133C5"));
            for (int i = 0; i < 3; i++)
            {
                Transform star = VariantShape("Wizard embroidered star " + i, wizard, AccessoryStar(5, .105f - i * .014f, .05f - i * .005f), .025f, gold);
                star.localPosition = new Vector3((i % 2 == 0 ? -.1f : .12f), .25f + i * .24f, -.4f + i * .11f);
            }
            Transform flower = VariantGroup("Hat.4.Flower", _accessoryRoot, new Vector3(-.54f, hatTop - .2f, centerZ - .35f)); _hatAccessories[4] = flower;
            flower.localRotation = Quaternion.Euler(-17, -12, 12);
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f;
                Transform petal = AccessoryPart("Flower plump petal " + i, flower, new Vector3(Mathf.Cos(a) * .25f, Mathf.Sin(a) * .25f, 0), new Vector3(.36f, .2f, .14f), pink);
                petal.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg);
            }
            AccessoryPart("Flower golden center", flower, new Vector3(0, 0, -.09f), new Vector3(.3f, .3f, .15f), gold);
            AccessoryPart("Flower leaf left", flower, new Vector3(-.34f, -.25f, .08f), new Vector3(.44f, .18f, .09f), lime).localRotation = Quaternion.Euler(0, 0, 29);
            Transform space = VariantGroup("Hat.5.Space", _accessoryRoot, new Vector3(0, hatTop - .25f, centerZ - .14f)); _hatAccessories[5] = space;
            Material teal = AccessoryMaterial("Space turquoise", "05B6C5", .27f), violet = AccessoryMaterial("Space violet", "9030D6", .23f);
            var band = new Vector3[19];
            for (int i = 0; i < band.Length; i++) { float a = Mathf.PI * (.12f + .76f * i / 18f); band[i] = new Vector3(Mathf.Cos(a) * .82f, Mathf.Sin(a) * .27f, -.1f); }
            VariantTube("Space curved headband", space, band, .055f, violet);
            for (int side = -1; side <= 1; side += 2)
            {
                VariantTube("Space bouncy antenna " + side, space, new[] { new Vector3(side * .59f, .19f, -.11f), new Vector3(side * .75f, .46f, -.12f), new Vector3(side * .76f, .65f, -.1f) }, .035f, teal);
                Transform star = VariantShape("Space antenna star " + side, space, AccessoryStar(5, .23f, .115f), .09f, side < 0 ? gold : pink); star.localPosition = new Vector3(side * .76f, .76f, -.1f);
            }
        }

        void AccessoryCone(string name, Transform parent, float radius, float height, Material material)
        {
            const int segments = 32; var vertices = new Vector3[segments + 2]; var triangles = new int[segments * 6];
            vertices[segments] = new Vector3(-.17f, height, .035f); vertices[segments + 1] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments; vertices[i] = new Vector3(Mathf.Cos(a) * radius, .02f, Mathf.Sin(a) * radius * .85f);
                int next = (i + 1) % segments, k = i * 6; triangles[k] = i; triangles[k + 1] = segments; triangles[k + 2] = next; triangles[k + 3] = i; triangles[k + 4] = next; triangles[k + 5] = segments + 1;
            }
            Mesh mesh = new Mesh { name = name, vertices = vertices, triangles = triangles }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); _variantMeshes.Add(mesh);
            Transform obj = VariantGroup(name, parent, Vector3.zero); obj.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh; obj.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        void BuildWingAccessories()
        {
            Bounds body = AccessoryBodyGeometryBounds();
            float shoulder = body.min.y + body.size.y * .52f;
            float wingHeight = Mathf.Clamp(body.size.y / 2.4f, .65f, 1.05f);
            string[] names = { "", "Fairy", "Butterfly", "Dragon", "Angel", "Cosmic" };
            for (int index = 1; index < 6; index++)
            {
                Transform pair = VariantGroup("Wings." + index + "." + names[index], _accessoryRoot, Vector3.zero); _wingAccessories[index] = pair;
                for (int side = 0; side < 2; side++)
                {
                    float direction = side == 0 ? -1 : 1;
                    Transform wing = VariantGroup(names[index] + " flapping wing " + side, pair, new Vector3(body.center.x + direction * body.size.x * .24f, shoulder, body.center.z + body.size.z * .18f));
                    wing.localScale = new Vector3(direction, wingHeight, 1); _wingPivots[index, side] = wing;
                    if (index == 1) BuildFairyWing(wing);
                    else if (index == 2) BuildButterflyWing(wing);
                    else if (index == 3) BuildDragonWing(wing);
                    else if (index == 4) BuildAngelWing(wing);
                    else BuildCosmicWing(wing);
                }
            }
        }

        void AccessoryWingPanel(string name, Transform parent, Vector2[] outline, Material material)
        { VariantShape(name, parent, outline, .065f, material); }

        void AccessoryVein(string name, Transform wing, Material material, params Vector2[] path)
        {
            var points = new Vector3[path.Length]; for (int i = 0; i < path.Length; i++) points[i] = new Vector3(path[i].x, path[i].y, -.048f);
            VariantTube(name, wing, points, .017f, material);
        }

        void BuildFairyWing(Transform wing)
        {
            Material aqua = AccessoryMaterial("Fairy aqua", "00BDBA", .18f), lime = AccessoryMaterial("Fairy lime", "96D532", .15f), vein = AccessoryMaterial("Fairy gold veins", "F2DC3E", .22f);
            AccessoryWingPanel("Fairy pointed upper leaf", wing, new[] { new Vector2(0, 0), new Vector2(.29f, .64f), new Vector2(.93f, 1.22f), new Vector2(1.43f, 1.35f), new Vector2(1.51f, .99f), new Vector2(1.16f, .39f), new Vector2(.63f, .1f) }, aqua);
            AccessoryWingPanel("Fairy slender lower leaf", wing, new[] { new Vector2(0, 0), new Vector2(.36f, -.27f), new Vector2(.96f, -1f), new Vector2(1.28f, -.94f), new Vector2(1.2f, -.43f), new Vector2(.71f, -.04f) }, lime);
            AccessoryVein("Fairy upper central vein", wing, vein, Vector2.zero, new Vector2(.69f, .56f), new Vector2(1.37f, 1.23f));
            AccessoryVein("Fairy upper vein branch", wing, vein, new Vector2(.69f, .56f), new Vector2(1.22f, .5f));
            AccessoryVein("Fairy lower central vein", wing, vein, Vector2.zero, new Vector2(.66f, -.42f), new Vector2(1.15f, -.86f));
            for (int i = 0; i < 3; i++) AccessoryPart("Fairy pearly dew " + i, wing, new Vector3(.76f + i * .19f, .8f + i * .13f, -.055f), Vector3.one * .085f, AccessoryMaterial("Fairy dew", "F4F3A7"));
        }

        void BuildButterflyWing(Transform wing)
        {
            Material pink = AccessoryMaterial("Butterfly fuchsia", "DC287E"), purple = AccessoryMaterial("Butterfly violet", "8233CB"), gold = AccessoryMaterial("Butterfly yellow", "F2C622");
            AccessoryWingPanel("Butterfly broad upper lobe", wing, new[] { new Vector2(0, 0), new Vector2(.23f, .88f), new Vector2(.76f, 1.37f), new Vector2(1.3f, 1.45f), new Vector2(1.57f, 1.13f), new Vector2(1.53f, .72f), new Vector2(1.33f, .41f), new Vector2(1.11f, .31f), new Vector2(1.3f, .11f), new Vector2(.7f, -.06f) }, pink);
            AccessoryWingPanel("Butterfly rounded lower lobe", wing, new[] { new Vector2(0, 0), new Vector2(.45f, -.08f), new Vector2(.91f, -.18f), new Vector2(1.32f, -.47f), new Vector2(1.23f, -.87f), new Vector2(.83f, -1.03f), new Vector2(.5f, -.77f), new Vector2(.18f, -.3f) }, purple);
            AccessoryVein("Butterfly arched vein", wing, purple, Vector2.zero, new Vector2(.55f, .53f), new Vector2(.95f, 1.25f));
            AccessoryVein("Butterfly outward vein", wing, purple, Vector2.zero, new Vector2(.72f, .26f), new Vector2(1.43f, .84f));
            AccessoryVein("Butterfly lower vein", wing, gold, Vector2.zero, new Vector2(.52f, -.38f), new Vector2(1.14f, -.71f));
            AccessoryPart("Butterfly upper eye spot", wing, new Vector3(1.04f, .89f, -.06f), new Vector3(.42f, .48f, .065f), gold);
            AccessoryPart("Butterfly upper eye spot heart", wing, new Vector3(1.04f, .89f, -.105f), new Vector3(.2f, .24f, .045f), purple);
            AccessoryPart("Butterfly lower eye spot", wing, new Vector3(.85f, -.59f, -.065f), new Vector3(.31f, .34f, .065f), gold);
            for (int i = 0; i < 4; i++) AccessoryPart("Butterfly edge dot " + i, wing, new Vector3(1.32f - i * .14f, 1.2f + Mathf.Sin(i) * .1f, -.06f), Vector3.one * .085f, AccessoryMaterial("Butterfly cream dots", "FFF0A8"));
        }

        void BuildDragonWing(Transform wing)
        {
            Material red = AccessoryMaterial("Dragon coral", "EF623C"), gold = AccessoryMaterial("Dragon golden bones", "F0C125", .15f);
            AccessoryWingPanel("Dragon scalloped membrane", wing, new[] { new Vector2(0, 0), new Vector2(.3f, .91f), new Vector2(.75f, 1.42f), new Vector2(.71f, .78f), new Vector2(1.65f, 1.09f), new Vector2(1.35f, .36f), new Vector2(1.66f, .07f), new Vector2(1.12f, -.08f), new Vector2(1.14f, -.62f), new Vector2(.66f, -.35f), new Vector2(.15f, -.26f) }, red);
            AccessoryVein("Dragon high wing bone", wing, gold, Vector2.zero, new Vector2(.38f, .61f), new Vector2(.75f, 1.42f));
            AccessoryVein("Dragon long wing bone", wing, gold, Vector2.zero, new Vector2(.57f, .48f), new Vector2(1.65f, 1.09f));
            AccessoryVein("Dragon middle wing bone", wing, gold, Vector2.zero, new Vector2(.77f, .1f), new Vector2(1.66f, .07f));
            AccessoryVein("Dragon lower wing bone", wing, gold, Vector2.zero, new Vector2(.69f, -.2f), new Vector2(1.14f, -.62f));
        }

        void BuildAngelWing(Transform wing)
        {
            Material ivory = AccessoryMaterial("Angel warm ivory", "FFF0BB"), cream = AccessoryMaterial("Angel white feathers", "FFF9EF"), gold = AccessoryMaterial("Angel gold shafts", "EFC756", .15f);
            AccessoryPart("Angel soft shoulder", wing, new Vector3(.49f, .26f, 0), new Vector3(1.11f, .66f, .2f), ivory).localRotation = Quaternion.Euler(0, 0, 34);
            for (int i = 0; i < 7; i++)
            {
                float angle = 34f + i * 9f;
                Transform feather = AccessoryPart("Angel long feather " + i, wing, new Vector3(.68f + i * .12f, .67f - i * .18f, -.018f - i * .012f), new Vector3(.28f, 1.28f - i * .055f, .17f), i % 2 == 0 ? cream : ivory);
                feather.localRotation = Quaternion.Euler(0, 0, -angle);
                AccessoryPart("Angel feather gold shaft " + i, feather, new Vector3(0, 0, -.45f), new Vector3(.08f, .73f, .17f), gold);
            }
            for (int i = 0; i < 4; i++) AccessoryPart("Angel shoulder down " + i, wing, new Vector3(.17f + i * .18f, .21f + i * .1f, -.13f), new Vector3(.34f, .25f, .14f), cream);
        }

        void BuildCosmicWing(Transform wing)
        {
            Material blue = AccessoryMaterial("Cosmic indigo", "4C42D3", .2f), aqua = AccessoryMaterial("Cosmic neon mint", "18CDA9", .18f), yellow = AccessoryMaterial("Cosmic starlight", "F9CC26", .25f);
            Transform star = VariantShape("Cosmic large star wing", wing, AccessoryStar(5, .87f, .4f), .1f, blue); star.localPosition = new Vector3(.87f, .3f, 0); star.localRotation = Quaternion.Euler(0, 0, -20);
            Transform inner = VariantShape("Cosmic mint star inlay", wing, AccessoryStar(5, .58f, .27f), .035f, aqua); inner.localPosition = new Vector3(.87f, .3f, -.073f); inner.localRotation = Quaternion.Euler(0, 0, -20);
            AccessoryVein("Cosmic constellation trail", wing, yellow, Vector2.zero, new Vector2(.39f, -.3f), new Vector2(.83f, -.71f), new Vector2(1.41f, -.35f));
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = i == 0 ? new Vector3(.39f, -.3f, -.08f) : i == 1 ? new Vector3(.83f, -.71f, -.08f) : new Vector3(1.41f, -.35f, -.08f);
                Transform little = VariantShape("Cosmic trailing star " + i, wing, AccessoryStar(5, .12f + i * .025f, .057f + i * .012f), .055f, yellow); little.localPosition = p;
            }
            AccessoryPart("Cosmic golden center", wing, new Vector3(.87f, .3f, -.11f), new Vector3(.22f, .22f, .07f), yellow);
        }
    }
}
