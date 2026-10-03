using UnityEngine;
using UnityEngine.UI;

namespace MimoGarden
{
    public sealed partial class MimoExperience
    {
        float RailWidth => railScreen == "Creation" ? 360f : 320f;
        const float RailTouchHeight = 56f;
        RectTransform sidebarBacking, railContent;
        ScrollRect railScroll;
        string railScreen;
        float rememberedRailOffset;
        float lastCanvasScale;
        Rect lastSafeArea;

        void BuildSidebarChrome()
        {
            sidebarBacking = Image("Quiet sidebar background", safeArea, Hex("D7E2D6")).rectTransform;
            sidebarBacking.GetComponent<Image>().sprite = null;
            sidebarBacking.SetAsFirstSibling();
            tray.GetComponent<Image>().color = new Color(1, .985f, .95f, .98f);
            tray.GetComponent<Shadow>().enabled = false;
            SetRect(header, Vector2.up, Vector2.up, Vector2.up, new Vector2(28, -22), new Vector2(570, 100));
            Image headingPlate = Image("Readable ivory heading", header, new Color(1f, .976f, .91f, 1f));
            headingPlate.raycastTarget = false;
            SetRect(headingPlate.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(-13, 10), new Vector2(580, 113));
            headingPlate.transform.SetAsFirstSibling();
            title.fontSize = 29; title.resizeTextMaxSize = 29;
            SetRect(title.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(0, -28), new Vector2(590, 42));
            subtitle.fontSize = 14;
            SetRect(subtitle.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(1, -68), new Vector2(560, 27));
        }

        void LayoutSidebar()
        {
            if (!sidebarBacking) return;
            lastCanvasScale = canvas.scaleFactor; lastSafeArea = Screen.safeArea;
            sidebarBacking.anchorMin = new Vector2(1, 0); sidebarBacking.anchorMax = Vector2.one;
            sidebarBacking.pivot = new Vector2(1, .5f);
            sidebarBacking.offsetMin = new Vector2(-RailWidth - 16, 0); sidebarBacking.offsetMax = Vector2.zero;
            tray.anchorMin = new Vector2(1, 0); tray.anchorMax = Vector2.one; tray.pivot = new Vector2(1, .5f);
            tray.offsetMin = new Vector2(-RailWidth - 8, 12); tray.offsetMax = new Vector2(-8, -12);
            float railPixels = (RailWidth + 16) * canvas.scaleFactor;
            Rect area = Screen.safeArea;
            float gameWidth = Mathf.Max(Screen.width * .6f, area.xMax - railPixels);
            sceneCamera.rect = new Rect(0, 0, gameWidth / Screen.width, 1);
            LayoutThemeParkBackdrop();
            toast.anchorMin = toast.anchorMax = new Vector2(gameWidth / Screen.width * .5f, 0);
            toast.pivot = new Vector2(.5f, 0); toast.anchoredPosition = new Vector2(0, 22);
            toast.sizeDelta = new Vector2(450, 42); toastText.fontSize = 14;
        }

        void SidebarStart(string screen)
        {
            rememberedRailOffset = railScreen == screen && railContent ? Mathf.Max(0, railContent.anchoredPosition.y) : 0f;
            railScreen = screen;
            railScroll = null; railContent = null;
            tray.gameObject.SetActive(true); Clear(tray); Clear(topRight); needBars.Clear();
            LayoutSidebar();
        }

        // Categories and choices have one scroll owner; the fixed footer is outside its clip.
        RectTransform CreateRailScroll(float top, float bottom, float contentHeight)
        {
            RectTransform region = Rect("Sidebar scroll", tray);
            region.anchorMin = Vector2.zero; region.anchorMax = Vector2.one;
            region.offsetMin = new Vector2(10, bottom); region.offsetMax = new Vector2(-10, -top);
            RectTransform viewport = Image("Sidebar scroll viewport", region, Color.clear).rectTransform;
            Stretch(viewport); viewport.offsetMax = new Vector2(-10, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            railContent = Rect("Sidebar scroll content", viewport);
            railContent.anchorMin = new Vector2(0, 1); railContent.anchorMax = Vector2.one;
            railContent.pivot = Vector2.up; railContent.anchoredPosition = Vector2.zero;
            railContent.sizeDelta = new Vector2(0, contentHeight);
            railScroll = region.gameObject.AddComponent<ScrollRect>();
            railScroll.viewport = viewport; railScroll.content = railContent;
            railScroll.horizontal = false; railScroll.vertical = true;
            railScroll.movementType = ScrollRect.MovementType.Clamped;
            railScroll.inertia = true; railScroll.decelerationRate = .12f; railScroll.scrollSensitivity = 42f;

            RectTransform bar = Image("Sidebar scroll track", region, Hex("E2E8DF")).rectTransform;
            bar.anchorMin = new Vector2(1, 0); bar.anchorMax = Vector2.one;
            bar.offsetMin = new Vector2(-5, 0); bar.offsetMax = Vector2.zero;
            RectTransform thumb = Image("Sidebar scroll thumb", bar, Hex("719E8C"), true).rectTransform;
            Stretch(thumb);
            Scrollbar scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop; scrollbar.handleRect = thumb;
            scrollbar.targetGraphic = thumb.GetComponent<Image>();
            railScroll.verticalScrollbar = scrollbar;
            railScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return railContent;
        }

        void FinishSidebarScroll()
        {
            Canvas.ForceUpdateCanvases();
            if (!railScroll || !railContent) return;
            float range = Mathf.Max(0, railContent.rect.height - railScroll.viewport.rect.height);
            railScroll.StopMovement();
            railScroll.verticalNormalizedPosition = range > .01f ? 1f - Mathf.Clamp(rememberedRailOffset, 0, range) / range : 1f;
        }

        void PlaceRailContent(RectTransform rect, float top, float height, float left = 0, float right = 0)
        {
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = Vector2.up;
            rect.offsetMin = new Vector2(left, -top - height); rect.offsetMax = new Vector2(-right, -top);
        }

        Text RailLabel(string text, float y, float height = 28, int size = 18, bool bold = false)
        {
            Text label = Label(text, tray, size, bold ? ink : muted, bold ? FontStyle.Bold : FontStyle.Normal);
            SetRect(label.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(14, -y), new Vector2(RailWidth - 28, height));
            return label;
        }

        Text ScrollRailLabel(string text, float y, float height = 28, int size = 18, bool bold = false)
        {
            Text label = Label(text, railContent, size, bold ? ink : muted, bold ?FontStyle.Bold : FontStyle.Normal);
            PlaceRailContent(label.rectTransform, y, height, 2, 2);
            return label;
        }

        Button RailButton(string text, float y, Color color, System.Action action, float height = RailTouchHeight, bool selected = false)
        {
            Button button = Button(text, railContent, color, ink, action, 20);
            PlaceRailContent((RectTransform)button.transform, y, height);
            if (selected)
            {
                Outline outline = button.gameObject.AddComponent<Outline>(); outline.effectColor = purple; outline.effectDistance = new Vector2(1.3f, -1.3f);
            }
            return button;
        }

        Button RailAction(string text, System.Action action)
        {
            Button button = Button(text, tray, purple, Color.white, action, 19);
            SetRect((RectTransform)button.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(12, railScreen == "Creation" || railScreen == "Naming" ? 14 : 105), new Vector2(RailWidth - 24, 62));
            PictureButton(button, text, false, 58);
            return button;
        }

        void BuildNamingUI()
        {
            place = Place.Naming; SidebarStart("Naming");
            title.text = "Olha quem nasceu!"; subtitle.text = "Um pequeno amigo. Com um pouquinho de você.";
            RailLabel("SEU NOVO AMIGO", 15, 32, 17, true);
            CreateRailScroll(62, 94, 352);
            ScrollRailLabel("Como vamos chamar essa fofura?", 16, 108, 25, true);
            Image inputBG = Image("Name your little friend", railContent, Hex("EDE5F1"));
            PlaceRailContent(inputBG.rectTransform, 144, 70);
            nameInput = inputBG.gameObject.AddComponent<InputField>();
            Text text = Label("", inputBG.transform, 24, ink, FontStyle.Bold); Stretch(text.rectTransform, 10, 8);
            Text placeholder = Label("Nome", inputBG.transform, 22, muted); Stretch(placeholder.rectTransform, 10, 8);
            nameInput.textComponent = text; nameInput.placeholder = placeholder; nameInput.characterLimit = 20;
            nameInput.lineType = InputField.LineType.SingleLine; nameInput.text = string.IsNullOrWhiteSpace(draft.petName) ? "Mimo" : draft.petName;
            nameInput.selectionColor = new Color(.66f, .54f, .78f, .28f); nameInput.caretColor = ink;
            ScrollRailLabel(playgroundPets.Count >= PetStorage.MaxPets ? "O parquinho tem 5 amigos. Você vai escolher quem substituir." : "Mais um amigo para brincar no parquinho.", 242, 84, 18);
            RailAction("Levar para casa", ConfirmName);
            FinishSidebarScroll();
            Toast("Ele abriu os olhinhos para conhecer você.", 4f);
        }

        void BuildHomeUI()
        {
            if (care == Care.Play) { BuildPlaygroundHomeUI(); return; }
            if (care == Care.Bath) { BuildBathHomeUI(); return; }
            SidebarStart("Home"); BuildFriendPicker();
            CreateRailScroll(159, 184, 374);
            string[] needNames = { "Barriguinha", "Limpeza", "Alegria" };
            Color[] barColors = { Hex("EDB377"), Hex("73C9D1"), Hex("BB9DDD") };
            for (int i = 0; i < 3; i++)
            {
                ScrollRailLabel(needNames[i], 4 + i * 42, 22, 16);
                Image bg = Image("Need background", railContent, Hex("EEE5ED"));
                PlaceRailContent(bg.rectTransform, 28 + i * 42, 8, 2, 2); bg.raycastTarget = false;
                Image fill = Image("Need fill", bg.transform, barColors[i]); Stretch(fill.rectTransform); fill.raycastTarget = false; needBars.Add(fill);
            }
            ScrollRailLabel("Uma frutinha para seu amigo", 144, 46, 18, true);
            PictureHands(212);
            RailAction("Dar frutinha", MainCareAction); PictureNavigation();
            FinishSidebarScroll(); RefreshNeeds();
        }
    }
}
