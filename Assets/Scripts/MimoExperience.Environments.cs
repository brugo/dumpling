using UnityEngine;

namespace MimoGarden
{
    public sealed partial class MimoExperience
    {
        Transform kitchenRoom, bathRoom, gardenRoom;
        public string ActiveEnvironment { get; private set; } = "Laboratory";
        public int ActiveCareEnvironmentCount
        {
            get
            {
                return (kitchenRoom && kitchenRoom.gameObject.activeSelf ? 1 : 0)
                    + (bathRoom && bathRoom.gameObject.activeSelf ? 1 : 0)
                    + (gardenRoom && gardenRoom.gameObject.activeSelf ? 1 : 0);
            }
        }

        void BuildCareEnvironments()
        {
            if (kitchenRoom) return;
            kitchenRoom = Group("03 — the sunshine kitchen");
            bathRoom = Group("04 — the aqua splash room");
            gardenRoom = Group("05 — the bright garden");
            BuildKitchenRoom(); BuildBathRoom(); BuildGardenRoom();
            kitchenRoom.gameObject.SetActive(false);
            bathRoom.gameObject.SetActive(false);
            gardenRoom.gameObject.SetActive(false);
        }

        void ShowCareEnvironment(string destination)
        {
            bool food = destination == "Food", bath = destination == "Bath", play = destination == "Play";
            ActiveEnvironment = food ? "Food" : bath ? "Bath" : play ? "Play" : "Laboratory";
            if (studioShell) studioShell.gameObject.SetActive(!food && !bath && !play);
            if (home) home.gameObject.SetActive(false);
            if (kitchenRoom) kitchenRoom.gameObject.SetActive(food);
            if (bathRoom) bathRoom.gameObject.SetActive(bath);
            if (gardenRoom) gardenRoom.gameObject.SetActive(play);
            sceneCamera.backgroundColor = play ? Hex("A4DBED") : bath ? Hex("23B8D1") : Hex("FFE0A1");
        }

        void CareRoomShell(Transform parent, string prefix, Color floorColor, Color wallColor)
        {
            Box(prefix + " floor", parent, new Vector3(0, -.13f, .8f), new Vector3(22, .2f, 20), Mat(prefix + " floor", floorColor, .2f));
            Box(prefix + " wall", parent, new Vector3(0, 3.5f, 4.55f), new Vector3(18, 9, .25f), Mat(prefix + " wall", wallColor, .18f));
            Box(prefix + " skirting", parent, new Vector3(0, .16f, 4.36f), new Vector3(18, .28f, .16f), Mat("Care ivory trim", Hex("FFF9EA")));
        }

        void BuildKitchenRoom()
        {
            CareRoomShell(kitchenRoom, "Kitchen", Hex("F1E1BE"), Hex("FFD263"));
            Material ivory = Mat("Kitchen vanilla", Hex("FFFAE8"), .5f);
            Material peach = Mat("Kitchen peach", Hex("FF5C45"), .35f);
            Material sage = Mat("Kitchen sage", Hex("00ADA5"), .4f);
            Material wood = Mat("Kitchen maple", Hex("B97936"));
            Arch("Tall pantry alcove", kitchenRoom, new Vector3(0, .05f, 4.38f), 5.5f, 5.1f, Mat("Kitchen alcove", Hex("FFAF50")));
            Arch("Arched kitchen window", kitchenRoom, new Vector3(0, 2.2f, 4.25f), 1.8f, 2.15f, ivory);
            Arch("Mint morning outside", kitchenRoom, new Vector3(0, 2.31f, 4.21f), 1.58f, 1.89f, Mat("Kitchen window blue", Hex("14CBE4")));
            Box("Window crosspiece", kitchenRoom, new Vector3(0, 3.21f, 4.15f), new Vector3(1.6f, .08f, .08f), ivory);
            Box("Window upright", kitchenRoom, new Vector3(0, 3.21f, 4.14f), new Vector3(.08f, 1.85f, .08f), ivory);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 3.65f;
                Box("Rounded cabinet core", kitchenRoom, new Vector3(x, .64f, 3.0f), new Vector3(2.5f, 1.18f, 1.15f), side < 0 ? sage : peach);
                Pill("Soft stone countertop", kitchenRoom, new Vector3(x, 1.26f, 2.94f), new Vector3(2.73f, .22f, 1.34f), ivory);
                for (int door = -1; door <= 1; door += 2)
                {
                    Box("Cabinet inset door", kitchenRoom, new Vector3(x + door * .59f, .64f, 2.405f), new Vector3(1.02f, .91f, .035f), Mat("Kitchen inset " + side, side < 0 ? Hex("20CABC") : Hex("FF7854")));
                    Pill("Wooden cabinet knob", kitchenRoom, new Vector3(x + door * .2f, .78f, 2.35f), Vector3.one * .12f, wood);
                }
                Box("Kitchen floating shelf", kitchenRoom, new Vector3(x, 2.75f, 4.09f), new Vector3(2.45f, .13f, .63f), wood);
                for (int i = 0; i < 3; i++)
                {
                    float jarX = x - .66f + i * .66f;
                    Pill("Little pantry jar", kitchenRoom, new Vector3(jarX, 3.04f + i * .045f, 4.02f), new Vector3(.42f, .46f + i * .09f, .36f), Mat("Pantry jar " + i, i == 0 ? Hex("09BFA6") : i == 1 ? Hex("F65374") : Hex("7C4ADD"), .5f));
                    Disk("Pantry cork", kitchenRoom, new Vector3(jarX, 3.3f + i * .09f, 4.02f), new Vector3(.31f, .035f, .28f), wood);
                }
            }
            Disk("Fruit plate", kitchenRoom, new Vector3(-3.53f, 1.41f, 2.84f), new Vector3(1.21f, .055f, .76f), ivory);
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = new Vector3(-3.86f + i * .34f, 1.59f + (i % 2) * .09f, 2.83f);
                Pill("Juicy bright fruit", kitchenRoom, p, Vector3.one * .37f, Mat("Fruit " + i, i == 0 ? Hex("F83B52") : i == 1 ? Hex("FFC51B") : Hex("63C82E"), .45f));
                Pill("Fruit leaf", kitchenRoom, p + new Vector3(.045f, .2f, 0), new Vector3(.18f, .05f, .09f), sage).localRotation = Quaternion.Euler(0, 0, 24);
            }
            Pill("Little oven body", kitchenRoom, new Vector3(3.72f, 1.72f, 3.06f), new Vector3(1.3f, .85f, .8f), ivory);
            Pill("Oven window", kitchenRoom, new Vector3(3.72f, 1.69f, 2.65f), new Vector3(.84f, .5f, .055f), Mat("Oven warm glass", Hex("654653"), .65f));
            Pill("Oven handle", kitchenRoom, new Vector3(3.72f, 1.99f, 2.57f), new Vector3(.63f, .065f, .08f), wood);
            Disk("Kitchen scalloped mat edge", kitchenRoom, new Vector3(0, .001f, -.05f), new Vector3(5.6f, .024f, 3.85f), Mat("Kitchen rug border", Hex("EF7848")));
            Disk("Kitchen biscuit mat", kitchenRoom, new Vector3(0, .038f, -.05f), new Vector3(5.37f, .017f, 3.62f), Mat("Kitchen rug cream", Hex("FFF0C3")));
            MakePlant(kitchenRoom, new Vector3(-4.97f, 0, 2.45f), .75f);
            BuildCareFloorInlays(kitchenRoom, Mat("Kitchen stone tile joints", Hex("DECFAD"), .08f));
            for (int i = 0; i < 7; i++)
                Box("Woven kitchen mat stitch", kitchenRoom, new Vector3(-1.7f + i * .56f, .062f, -1.35f), new Vector3(.29f, .006f, .025f), peach);
            Pill("Kitchen wall plate rim", kitchenRoom, new Vector3(-5.13f, 3.27f, 4.37f), new Vector3(1.04f, 1.04f, .08f), ivory);
            Pill("Kitchen wall plate centre", kitchenRoom, new Vector3(-5.13f, 3.27f, 4.32f), new Vector3(.81f, .81f, .035f), sage);
            Star("Sun on the kitchen plate", kitchenRoom, new Vector3(-5.13f, 3.27f, 4.28f), .23f, ivory);
        }

        void BuildBathRoom()
        {
            CareRoomShell(bathRoom, "Bath", Hex("D6E7DF"), Hex("22B8D1"));
            Material grout = Mat("Bath grout", Hex("B7F4EA"), .2f);
            Material pearl = Mat("Bath pearl", Hex("F5FDFF"), .65f);
            Material gold = Mat("Bath brass", Hex("EAA629"), .6f, .2f);
            for (int i = -10; i <= 10; i++)
                Box("Vertical tile grout", bathRoom, new Vector3(i * .74f, 1.3f, 4.405f), new Vector3(.014f, 2.6f, .018f), grout);
            for (int i = 0; i < 5; i++)
                Box("Horizontal tile grout", bathRoom, new Vector3(0, .06f + i * .64f, 4.40f), new Vector3(18, .014f, .018f), grout);
            Box("Aqua tile cap", bathRoom, new Vector3(0, 2.68f, 4.33f), new Vector3(18, .09f, .11f), pearl);
            Arch("Tall mirror frame", bathRoom, new Vector3(-2.75f, 1.95f, 4.26f), 1.73f, 2.25f, gold);
            Arch("Misty blue mirror", bathRoom, new Vector3(-2.75f, 2.06f, 4.22f), 1.5f, 2.03f, Mat("Bath mirror", Hex("8AE6F3"), .82f, .15f));
            Pill("Mirror glimmer", bathRoom, new Vector3(-3.15f, 3.2f, 4.16f), new Vector3(.065f, 1.18f, .025f), pearl).localRotation = Quaternion.Euler(0, 0, -25);
            Pill("Round washstand", bathRoom, new Vector3(-3.62f, .58f, 3f), new Vector3(1.72f, 1.13f, 1.02f), Mat("Bath cabinet", Hex("2456CF"), .35f));
            Pill("Washstand basin", bathRoom, new Vector3(-3.62f, 1.18f, 2.95f), new Vector3(1.84f, .28f, 1.17f), pearl);
            Pill("Basin hollow", bathRoom, new Vector3(-3.62f, 1.31f, 2.85f), new Vector3(1.28f, .065f, .72f), Mat("Bath basin interior", Hex("B7F1EF"), .75f));
            Pill("Basin faucet", bathRoom, new Vector3(-3.62f, 1.47f, 3.3f), new Vector3(.1f, .42f, .1f), gold);
            Pill("Basin spout", bathRoom, new Vector3(-3.62f, 1.65f, 3.17f), new Vector3(.1f, .11f, .31f), gold);
            Transform tub = Group("A little bubble bathtub", bathRoom); tub.localPosition = new Vector3(3.65f, 0, 2.83f);
            Pill("Tub feet left", tub, new Vector3(-.65f, .12f, -.1f), new Vector3(.3f, .26f, .35f), gold);
            Pill("Tub feet right", tub, new Vector3(.65f, .12f, -.1f), new Vector3(.3f, .26f, .35f), gold);
            Pill("Bathtub curved shell", tub, new Vector3(0, .56f, 0), new Vector3(2.5f, 1.01f, 1.5f), pearl);
            Pill("Bathtub inner rim", tub, new Vector3(0, .91f, -.02f), new Vector3(2.14f, .13f, 1.15f), Mat("Bath tub inner", Hex("149BB6"), .65f));
            Pill("Calm bath water", tub, new Vector3(0, .95f, -.02f), new Vector3(1.98f, .09f, 1.02f), Mat("Bath water aqua", Hex("08CBEC"), .85f, .07f));
            for (int i = 0; i < 8; i++)
                Pill("Soft bath foam", tub, new Vector3(-.79f + i * .22f, .99f, .18f + Mathf.Sin(i * 2f) * .2f), Vector3.one * (.19f + (i % 3) * .055f), pearl);
            Pill("Tub faucet rise", tub, new Vector3(.85f, 1.18f, .57f), new Vector3(.1f, .92f, .1f), gold);
            Pill("Tub faucet spout", tub, new Vector3(.69f, 1.59f, .56f), new Vector3(.4f, .1f, .1f), gold);
            Transform duck = Group("The little bath duck", tub); duck.localPosition = new Vector3(-.48f, 1.1f, -.35f);
            Material yellow = Mat("Bath duck yellow", Hex("FFD023"), .55f);
            Pill("Duck body", duck, Vector3.zero, new Vector3(.4f, .24f, .3f), yellow);
            Pill("Duck head", duck, new Vector3(-.11f, .16f, -.02f), Vector3.one * .23f, yellow);
            Pill("Duck beak", duck, new Vector3(-.23f, .14f, -.04f), new Vector3(.14f, .07f, .1f), Mat("Duck beak coral", Hex("FF8125")));
            Pill("Duck eye", duck, new Vector3(-.16f, .2f, -.117f), Vector3.one * .037f, Mat("Duck eye", Hex("293552")));
            Box("Towel rail", bathRoom, new Vector3(.9f, 2.01f, 4.08f), new Vector3(1.52f, .065f, .08f), gold);
            Box("Hanging peach towel", bathRoom, new Vector3(.87f, 1.53f, 4.04f), new Vector3(.86f, .96f, .065f), Mat("Bath peach towel", Hex("FF6C48"), .1f));
            Box("Towel ivory stripe", bathRoom, new Vector3(.87f, 1.16f, 3.997f), new Vector3(.86f, .065f, .018f), pearl);
            Disk("Cloud bath mat", bathRoom, new Vector3(0, .005f, -.05f), new Vector3(5.5f, .035f, 3.8f), Mat("Bath mat teal", Hex("245BCD"), .05f));
            Disk("Bath mat soft center", bathRoom, new Vector3(0, .047f, -.05f), new Vector3(5.16f, .012f, 3.48f), Mat("Bath mat pale", Hex("A6E1DE"), .05f));
            MakePlant(bathRoom, new Vector3(-5.08f, 0, 2.12f), .9f);
            BuildCareFloorInlays(bathRoom, Mat("Bath floor stone joints", Hex("BED6D0"), .08f));
            for (int i = 0; i < 8; i++)
                Box("Woven bath mat border", bathRoom, new Vector3(-1.83f + i * .52f, .067f, -1.34f), new Vector3(.26f, .008f, .035f), pearl);
            for (int i = 0; i < 3; i++)
            {
                Pill("Rolled fresh hand towel", bathRoom, new Vector3(-3.95f + i * .27f, 1.49f, 3.01f), new Vector3(.24f, .25f, .43f), i == 1 ? Mat("Bath rolled towel peach", Hex("FFB99C"), .05f) : pearl);
                Pill("Hand towel rolled seam", bathRoom, new Vector3(-3.95f + i * .27f, 1.49f, 2.79f), new Vector3(.13f, .13f, .018f), grout);
            }
        }

        void BuildCareFloorInlays(Transform room, Material grout)
        {
            for (int x = -6; x <= 6; x++)
                Box("Fine floor tile joint", room, new Vector3(x * 1.15f, -.028f, .55f), new Vector3(.012f, .004f, 8.0f), grout);
            for (int z = -3; z <= 3; z++)
                Box("Fine cross floor tile joint", room, new Vector3(0, -.027f, z * 1.15f), new Vector3(16, .004f, .012f), grout);
        }

        void BuildGardenRoom()
        {
            BuildPlaygroundWorld();
        }
    }
}
