using UnityEngine;

namespace MimoGarden
{
    public sealed partial class MimoExperience
    {
        void BuildPlaygroundWorld()
        {
            Material lawn = Mat("Park clover lawn", Hex("82B873"), .08f);
            Material sky = Mat("Park powder blue sky", Hex("A4DBED"), .05f);
            Material cream = Mat("Park vanilla paint", Hex("FFF1D4"), .28f);
            Material oak = Mat("Park warm oak", Hex("B88655"), .22f);
            Material lightOak = Mat("Park maple edges", Hex("DBB680"), .25f);
            Material grassLight = Mat("Park soft meadow", Hex("9EC67F"), .06f);
            Material path = Mat("Park warm sand", Hex("E7CDA1"), .05f);
            Material teal = Mat("Park sage teal", Hex("4F9991"), .2f);
            Material coral = Mat("Park soft coral", Hex("EB9783"), .16f);
            Material gold = Mat("Park butter yellow", Hex("F3CE75"), .25f);

            BuildThemeParkBackdrop();

            BuildPlaygroundSwing(oak, lightOak, cream, teal);
            BuildPlaygroundSofa(oak, cream, coral);
            BuildPlaygroundBench(oak, lightOak, teal);
            BuildPlaygroundTrampoline(cream, teal, gold);

            // Layered planted edges make the space feel cared for while keeping the central play area clear.
            for (int side = -1; side <= 1; side += 2)
            {
                BuildPlaygroundPlanter(new Vector3(side * 6.25f, 0, 1.0f), lightOak, cream, coral, gold);
                BuildPlaygroundPlanter(new Vector3(side * 6.05f, 0, -1.55f), lightOak, cream, coral, gold);
                for (int i = 0; i < 3; i++)
                    Pill("Round border pebble", gardenRoom, new Vector3(side * (5.9f + i * .23f), .045f, 2.4f + i * .3f), new Vector3(.38f, .21f, .29f), cream);
            }
            BuildPlaygroundPicnic(oak, cream, teal, coral);

        }

        Transform themeParkBackdrop;
        public bool HasThemeParkBackdrop => themeParkBackdrop;
        void BuildThemeParkBackdrop()
        {
            Texture2D picture = Resources.Load<Texture2D>("Environment/MimoThemePark");
            if (!picture) { Debug.LogError("Theme park image is missing."); return; }
            Material material = Mat("ImageGen theme park panorama",Color.white);
            material.shader = Shader.Find("Universal Render Pipeline/Unlit");
            material.SetColor("_BaseColor",Color.white); material.SetTexture("_BaseMap",picture);
            themeParkBackdrop = Shape("The illustrated theme park panorama",PrimitiveType.Quad,gardenRoom,Vector3.zero,Vector3.one,material);
            LayoutThemeParkBackdrop();
        }
        void LayoutThemeParkBackdrop()
        {
            if (!themeParkBackdrop || !sceneCamera) return;
            const float distance = 45f;
            float height = 2 * Mathf.Tan(sceneCamera.fieldOfView*Mathf.Deg2Rad*.5f)*distance;
            float width = Mathf.Max(height*16f/9f,height*sceneCamera.aspect);
            themeParkBackdrop.position = sceneCamera.transform.position + sceneCamera.transform.forward*distance;
            themeParkBackdrop.rotation = sceneCamera.transform.rotation;
            themeParkBackdrop.localScale = new Vector3(width,height,1);
        }

        void BuildPlaygroundFence(Material cream, Material oak, Material teal, Material coral, Material gold)
        {
            for (int i = -12; i <= 12; i++)
            {
                float x = i * .66f;
                if (Mathf.Abs(x) < 1.12f) continue;
                Box("Painted garden picket", gardenRoom, new Vector3(x, .62f, 6.6f), new Vector3(.38f, 1.1f, .15f), cream);
                Pill("Rounded picket crown", gardenRoom, new Vector3(x, 1.16f, 6.6f), new Vector3(.38f, .28f, .15f), cream);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                for (int h = 0; h < 2; h++)
                    Box("Long fence rail", gardenRoom, new Vector3(side * 4.65f, .33f + h * .52f, 6.69f), new Vector3(6.65f, .115f, .13f), oak);
                PlayBeam("Welcome arch upright", gardenRoom, new Vector3(side * 1.31f, .05f, 6.64f), new Vector3(side * 1.31f, 3.12f, 6.64f), .17f, oak);
                Pill("Welcome post finial", gardenRoom, new Vector3(side * 1.31f, 3.15f, 6.64f), Vector3.one * .3f, cream);
            }
            Box("The welcoming garden arch", gardenRoom, new Vector3(0, 2.83f, 6.64f), new Vector3(2.96f, .2f, .24f), oak);
            Pill("Little welcome sign", gardenRoom, new Vector3(0, 2.7f, 6.47f), new Vector3(1.43f, .65f, .13f), teal);
            Star("Welcome sign star", gardenRoom, new Vector3(0, 2.72f, 6.38f), .2f, gold);
            for (int i = 0; i < 9; i++)
            {
                float x = -5.3f + i * 1.325f;
                float nextX = x + 1.325f;
                float y = 3.2f + Mathf.Abs(x) * .12f;
                float nextY = 3.2f + Mathf.Abs(nextX) * .12f;
                if (i < 8) PlayBeam("Festival garland cord", gardenRoom, new Vector3(x, y, 6.52f), new Vector3(nextX, nextY, 6.52f), .025f, cream);
                PlayPennant(gardenRoom, new Vector3(x, y - .07f, 6.5f), i % 3 == 0 ? coral : i % 3 == 1 ? gold : teal);
            }
        }

        void BuildPlaygroundTree(Vector3 position, float scale, Material oak, Material cream)
        {
            Transform tree = Group("A shady orchard tree", gardenRoom);
            tree.localPosition = position;
            tree.localScale = Vector3.one * scale;
            Material leaf = Mat("Park canopy sage", Hex("659C77"), .06f);
            Material leafLight = Mat("Park canopy lime", Hex("91B97B"), .06f);
            Disk("Tree soft grounding", tree, new Vector3(0, -.003f, 0), new Vector3(2.8f, .018f, 1.75f), Mat("Park ground shade", Hex("74A36C"), .05f));
            PlayBeam("Smooth tree trunk", tree, new Vector3(0, 0, 0), new Vector3(.1f, 3.1f, .05f), .39f, oak);
            PlayBeam("Branch fork left", tree, new Vector3(.05f, 1.8f, 0), new Vector3(-.82f, 3.05f, -.03f), .24f, oak);
            PlayBeam("Branch fork right", tree, new Vector3(.05f, 2.0f, 0), new Vector3(.81f, 3.2f, .06f), .22f, oak);
            Pill("Leafy orchard crown", tree, new Vector3(0, 3.5f, .08f), new Vector3(2.5f, 2.25f, 1.87f), leaf);
            Pill("Round sunlit foliage", tree, new Vector3(-.91f, 3.0f, -.12f), new Vector3(1.9f, 1.85f, 1.64f), leafLight);
            Pill("Round shady foliage", tree, new Vector3(.92f, 3.2f, .05f), new Vector3(1.82f, 1.94f, 1.7f), leaf);
            Pill("Soft foliage top", tree, new Vector3(.23f, 4.29f, .1f), new Vector3(1.72f, 1.42f, 1.53f), leafLight);
            for (int i = 0; i < 4; i++)
                Pill("A tiny orchard blossom", tree, new Vector3(-.8f + i * .55f, 3.32f + Mathf.Sin(i * 2f) * .55f, -.84f), Vector3.one * .16f, cream);
        }

        void BuildPlaygroundSwing(Material oak, Material lightOak, Material cream, Material teal)
        {
            Transform swing = Group("Balanço — pode sentar", gardenRoom);
            swing.localPosition = new Vector3(-3.5f, 0, 3.65f);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int front = -1; front <= 1; front += 2)
                {
                    PlayBeam("Rounded A frame leg", swing, new Vector3(side * 1.47f, .1f, front * .98f), new Vector3(side * 1.22f, 3.99f, 0), .2f, oak);
                    Pill("Swing foot cap", swing, new Vector3(side * 1.47f, .12f, front * .98f), new Vector3(.29f, .24f, .3f), teal);
                }
                Box("A frame cross brace", swing, new Vector3(side * 1.4f, 1.14f, 0), new Vector3(.12f, .13f, 1.36f), lightOak);
                Pill("Swing metal joint", swing, new Vector3(side * 1.22f, 3.93f, -.13f), Vector3.one * .15f, cream);
            }
            Box("Solid maple top beam", swing, new Vector3(0, 4.0f, 0), new Vector3(3.13f, .24f, .26f), lightOak);
            Transform pivot = Group("The moving swing assembly", swing);
            pivot.localPosition = new Vector3(0, 3.91f, 0);
            pivot.gameObject.AddComponent<MimoPlaygroundMovingPart>();
            Material rope = Mat("Park braided swing rope", Hex("E3D6B7"), .12f);
            for (int side = -1; side <= 1; side += 2)
            {
                PlayBeam("Front swing rope", pivot, new Vector3(side * 1.11f, 0, 0), new Vector3(side * 1.11f, -3.21f, -.51f), .045f, rope);
                PlayBeam("Back swing rope", pivot, new Vector3(side * 1.11f, 0, 0), new Vector3(side * 1.11f, -3.21f, .49f), .045f, rope);
                Pill("Swing rope knot", pivot, new Vector3(side * 1.11f, -3.12f, -.51f), Vector3.one * .11f, rope);
            }
            Pill("Cushioned swing seat", pivot, new Vector3(0, -3.25f, 0), new Vector3(2.45f, .2f, 1.4f), teal);
            Pill("Swing cream seat piping", pivot, new Vector3(0, -3.19f, 0), new Vector3(2.23f, .11f, 1.22f), cream);
            for (int side = -1; side <= 1; side += 2)
                Pill("A comfortable swing arm", pivot, new Vector3(side * 1.09f, -3.09f, .02f), new Vector3(.13f, .26f, 1.12f), teal);
            Transform anchor = Group("Swing visitor feet", pivot);
            anchor.localPosition = new Vector3(0, -3.13f, -.02f);
            MimoPlaygroundProp prop = swing.gameObject.AddComponent<MimoPlaygroundProp>();
            prop.Configure(MimoPlaygroundProp.PropKind.Swing, "Balanço", anchor, new Vector2(1.32f, 1.2f), pivot);
        }

        void BuildPlaygroundSofa(Material oak, Material cream, Material coral)
        {
            Transform sofa = Group("Sofá — um cantinho para descansar", gardenRoom);
            sofa.localPosition = new Vector3(3.45f, 0, 3.95f);
            sofa.localRotation = Quaternion.Euler(0, -12, 0);
            Material cushion = Mat("Park peach upholstery", Hex("E8AD9F"), .08f);
            Material piping = Mat("Park peach piping", Hex("F8D5BD"), .1f);
            for (int side = -1; side <= 1; side += 2)
                for (int end = -1; end <= 1; end += 2)
                    Box("Short sofa oak foot", sofa, new Vector3(side * 1.05f, .18f, end * .43f), new Vector3(.15f, .35f, .17f), oak);
            Pill("Upholstered sofa base", sofa, new Vector3(0, .48f, 0), new Vector3(2.95f, .56f, 1.63f), coral);
            Pill("Soft high sofa back", sofa, new Vector3(0, 1.08f, .52f), new Vector3(2.8f, 1.13f, .41f), cushion);
            Pill("Sofa back stitching", sofa, new Vector3(0, 1.11f, .294f), new Vector3(2.32f, .73f, .045f), piping);
            Pill("Sofa back cushion", sofa, new Vector3(0, 1.13f, .26f), new Vector3(2.2f, .65f, .12f), cushion);
            for (int side = -1; side <= 1; side += 2)
                Pill("Rounded sofa arm", sofa, new Vector3(side * 1.25f, .86f, -.015f), new Vector3(.38f, .57f, 1.4f), cushion);
            Pill("Sofa seat piping", sofa, new Vector3(0, .74f, -.14f), new Vector3(2.34f, .22f, 1.26f), piping);
            Pill("Inviting sofa cushion", sofa, new Vector3(0, .8f, -.16f), new Vector3(2.21f, .16f, 1.15f), cream);
            Transform anchor = Group("Sofa visitor feet", sofa);
            anchor.localPosition = new Vector3(0, .87f, -.2f);
            sofa.gameObject.AddComponent<MimoPlaygroundProp>().Configure(MimoPlaygroundProp.PropKind.Seat, "Sofá", anchor, new Vector2(1.42f, 1.06f));
            // A little striped outdoor canopy frames the reading corner.
            for (int side = -1; side <= 1; side += 2)
                PlayBeam("Reading canopy post", sofa, new Vector3(side * 1.56f, .08f, .76f), new Vector3(side * 1.56f, 3.93f, .76f), .12f, oak);
            for (int i = 0; i < 6; i++)
                Box("Fabric canopy stripe", sofa, new Vector3(-1.375f + i * .55f, 3.93f, .18f), new Vector3(.55f, .055f, 1.74f), (i & 1) == 0 ? cream : coral).localRotation = Quaternion.Euler(-8, 0, 0);
            for (int i = 0; i < 6; i++)
                Pill("Canopy scalloped edge", sofa, new Vector3(-1.375f + i * .55f, 3.7f, -.68f), new Vector3(.55f, .33f, .07f), (i & 1) == 0 ? cream : coral);
        }

        void BuildPlaygroundBench(Material oak, Material lightOak, Material teal)
        {
            Transform bench = Group("Banco — uma pausa no jardim", gardenRoom);
            bench.localPosition = new Vector3(4.65f, 0, .1f);
            bench.localRotation = Quaternion.Euler(0, -18, 0);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int end = -1; end <= 1; end += 2)
                    PlayBeam("Bench painted leg", bench, new Vector3(side * .97f, .08f, end * .45f), new Vector3(side * .89f, .71f, end * .39f), .13f, teal);
                PlayBeam("Bench back upright", bench, new Vector3(side * 1.02f, .58f, .45f), new Vector3(side * 1.02f, 1.47f, .56f), .12f, teal);
            }
            for (int i = 0; i < 4; i++)
                Box("Rounded maple seat slat", bench, new Vector3(0, .69f, -.43f + i * .28f), new Vector3(2.67f, .12f, .245f), (i & 1) == 0 ? lightOak : oak);
            for (int i = 0; i < 3; i++)
                Box("Bench back slat", bench, new Vector3(0, .96f + i * .18f, .51f + i * .025f), new Vector3(2.65f, .145f, .1f), lightOak);
            Transform anchor = Group("Bench visitor feet", bench);
            anchor.localPosition = new Vector3(0, .77f, -.13f);
            bench.gameObject.AddComponent<MimoPlaygroundProp>().Configure(MimoPlaygroundProp.PropKind.Seat, "Banquinho", anchor, new Vector2(1.3f, .95f));
        }

        void BuildPlaygroundTrampoline(Material cream, Material teal, Material gold)
        {
            Transform trampoline = Group("Pula-pula — pulinhos sem parar", gardenRoom);
            trampoline.localPosition = new Vector3(-4.35f, 0, -.12f);
            Material metal = Mat("Park trampoline silver", Hex("AAB6AD"), .5f, .25f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3;
                Pill("Trampoline short leg", trampoline, new Vector3(Mathf.Cos(a) * 1.03f, .18f, Mathf.Sin(a) * 1.03f), new Vector3(.14f, .35f, .14f), metal);
                Pill("Rubber trampoline foot", trampoline, new Vector3(Mathf.Cos(a) * 1.03f, .065f, Mathf.Sin(a) * 1.03f), new Vector3(.22f, .12f, .22f), teal);
            }
            Disk("Padded trampoline rim", trampoline, new Vector3(0, .34f, 0), new Vector3(2.83f, .115f, 2.83f), teal);
            Disk("Trampoline warm piping", trampoline, new Vector3(0, .445f, 0), new Vector3(2.55f, .035f, 2.55f), gold);
            Transform mat = Group("Flexible trampoline centre", trampoline);
            mat.localPosition = new Vector3(0, .471f, 0);
            mat.gameObject.AddComponent<MimoPlaygroundMovingPart>();
            Disk("Soft woven jumping mat", mat, Vector3.zero, new Vector3(2.31f, .025f, 2.31f), Mat("Park trampoline woven mint", Hex("90BDB0"), .05f));
            Transform star = Star("A star to jump on", mat, new Vector3(0, .033f, 0), .39f, cream);
            star.localRotation = Quaternion.Euler(90, 0, 0);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6;
                Pill("Trampoline padded seam", trampoline, new Vector3(Mathf.Cos(a) * 1.34f, .427f, Mathf.Sin(a) * 1.34f), new Vector3(.045f, .022f, .15f), cream).localRotation = Quaternion.Euler(0, 90 - a * Mathf.Rad2Deg, 0);
            }
            Transform anchor = Group("Trampoline visitor feet", trampoline);
            anchor.localPosition = new Vector3(0, .51f, 0);
            trampoline.gameObject.AddComponent<MimoPlaygroundProp>().Configure(MimoPlaygroundProp.PropKind.Trampoline, "Pula-pula", anchor, new Vector2(1.39f, 1.39f), mat);
        }

        void BuildPlaygroundPlanter(Vector3 position, Material oak, Material cream, Material coral, Material gold)
        {
            Transform planter = Group("Flower border box", gardenRoom);
            planter.localPosition = position;
            Box("Raised cedar planter", planter, new Vector3(0, .23f, 0), new Vector3(.72f, .43f, 1.54f), oak);
            Box("Planter dark soil", planter, new Vector3(0, .46f, 0), new Vector3(.58f, .025f, 1.37f), Mat("Park rich planter soil", Hex("786D53"), .05f));
            for (int side = -1; side <= 1; side += 2)
                Box("Raised bed trim", planter, new Vector3(side * .35f, .45f, 0), new Vector3(.065f, .09f, 1.61f), cream);
            Material green = Mat("Park flower foliage", Hex("528D63"), .08f);
            for (int i = 0; i < 3; i++)
            {
                Vector3 flower = new Vector3((i & 1) * .13f - .08f, .82f + (i & 1) * .13f, -.48f + i * .46f);
                PlayBeam("Flower green stem", planter, new Vector3(flower.x, .46f, flower.z), flower, .035f, green);
                Pill("Flower soft leaf", planter, flower + new Vector3(.09f, -.19f, .02f), new Vector3(.25f, .075f, .11f), green).localRotation = Quaternion.Euler(0, 0, 28);
                for (int j = 0; j < 5; j++)
                {
                    float a = j * Mathf.PI * .4f;
                    Pill("Bright border flower petal", planter, flower + new Vector3(Mathf.Cos(a) * .13f, Mathf.Sin(a) * .13f, -.015f), new Vector3(.2f, .2f, .07f), (i & 1) == 0 ? coral : cream);
                }
                Pill("Flower honey centre", planter, flower + new Vector3(0, 0, -.066f), new Vector3(.15f, .15f, .07f), gold);
            }
        }

        void BuildPlaygroundPicnic(Material oak, Material cream, Material teal, Material coral)
        {
            Transform picnic = Group("Picnic basket and picture books", gardenRoom);
            picnic.localPosition = new Vector3(6.25f, 0, 3.7f);
            Disk("Round side table top", picnic, new Vector3(0, .68f, 0), new Vector3(1.19f, .065f, 1.03f), oak);
            PlayBeam("Table centre leg", picnic, new Vector3(0, .07f, 0), new Vector3(0, .69f, 0), .16f, oak);
            Disk("Table weighted foot", picnic, new Vector3(0, .07f, 0), new Vector3(.65f, .05f, .62f), oak);
            Pill("Little picnic cup", picnic, new Vector3(.25f, .91f, -.04f), new Vector3(.29f, .37f, .28f), cream);
            Box("An illustrated picture book", picnic, new Vector3(-.19f, .8f, 0), new Vector3(.56f, .075f, .49f), coral).localRotation = Quaternion.Euler(0, -14, 0);
            Box("Book creamy page block", picnic, new Vector3(-.19f, .843f, 0), new Vector3(.51f, .025f, .44f), cream).localRotation = Quaternion.Euler(0, -14, 0);
            Pill("Wicker picnic basket", picnic, new Vector3(.06f, .25f, -.83f), new Vector3(.85f, .45f, .57f), oak);
            Pill("Basket striped blanket", picnic, new Vector3(.11f, .47f, -.85f), new Vector3(.58f, .09f, .49f), teal);
        }

        Transform PlayBeam(string label, Transform parent, Vector3 from, Vector3 to, float width, Material material)
        {
            Transform beam = Box(label, parent, (from + to) * .5f, new Vector3(width, Vector3.Distance(from, to), width), material);
            beam.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
            return beam;
        }

        void PlayPennant(Transform parent, Vector3 position, Material material)
        {
            var mesh = new Mesh { name = "A cloth garden pennant" };
            mesh.vertices = new[] { new Vector3(-.24f, 0, 0), new Vector3(.24f, 0, 0), new Vector3(0, -.43f, -.015f), new Vector3(-.24f, 0, 0), new Vector3(.24f, 0, 0), new Vector3(0, -.43f, -.015f) };
            mesh.triangles = new[] { 0, 1, 2, 3, 5, 4 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            mobileWorldMeshes.Add(mesh);
            Transform flag = Group("A cheerful cloth pennant", parent);
            flag.localPosition = position;
            flag.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            flag.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
