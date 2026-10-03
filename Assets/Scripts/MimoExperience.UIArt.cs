using System;
using UnityEngine;
using UnityEngine.UI;

namespace MimoGarden
{
    public sealed partial class MimoExperience
    {
        Image Artwork(string name, Transform parent, Sprite sprite)
        {
            Image image = Image(name, parent, Color.white);
            image.type = UnityEngine.UI.Image.Type.Simple; image.sprite = sprite;
            image.preserveAspect = true; image.raycastTarget = false;
            return image;
        }

        void PictureButton(Button button, string text, bool tile, float size = 68f)
        {
            Sprite sprite = MimoUIArt.Action(text);
            if (!sprite) return;
            Text label = button.GetComponentInChildren<Text>();
            Image art = Artwork("Illustration · " + text, button.transform, sprite);
            if (tile)
            {
                SetRect(art.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -1), new Vector2(size, size));
                label.fontSize = 14; label.resizeTextForBestFit = false;
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = new Vector2(1, 0);
                label.rectTransform.offsetMin = new Vector2(4, 4); label.rectTransform.offsetMax = new Vector2(-4, 48);
            }
            else
            {
                SetRect(art.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(5, -1), new Vector2(size, size));
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(size + 10, 5); label.rectTransform.offsetMax = new Vector2(-8, -5);
                label.fontSize = 18;
            }
            button.gameObject.AddComponent<MimoUIButtonFeedback>().artwork = art.rectTransform;
        }

        Button PictureCard(string text, int column, int columns, float top, Color color, Action action, bool selected = false, float height = 116f)
        {
            Button button = Button(text, railContent, color, ink, action);
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(column / (float)columns, 1); rect.anchorMax = new Vector2((column + 1) / (float)columns, 1);
            rect.pivot = Vector2.up; rect.offsetMin = new Vector2(4, -top - height); rect.offsetMax = new Vector2(-4, -top);
            PictureButton(button, text, true, height - 44);
            if (selected)
            {
                Outline outline = button.gameObject.AddComponent<Outline>(); outline.effectColor = purple; outline.effectDistance = new Vector2(2, -2);
                Image dot = Image("Selected action", button.transform, purple, true); dot.raycastTarget = false;
                SetRect(dot.rectTransform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-6, -6), new Vector2(9, 9));
            }
            return button;
        }

        void PictureNavigation()
        {
            string[] names = { "Comidinha", "Banho", "Brincar", "Ateliê" };
            Color[] colors = { Hex("FFF0DC"), Hex("E2F6F7"), Hex("E4EEE3"), Hex("FFF3D5") };
            RectTransform nav = Rect("Always visible destinations", tray);
            nav.anchorMin = Vector2.zero; nav.anchorMax = Vector2.right;
            nav.offsetMin = new Vector2(8, 12); nav.offsetMax = new Vector2(-8, 94);
            for (int i = 0; i < names.Length; i++)
            {
                int destination = i;
                Button button = Button(names[i], nav, colors[i], ink, () => { if (destination == 3) EnterLaboratory(); else SelectCare((Care)destination); });
                RectTransform cell = (RectTransform)button.transform;
                cell.anchorMin = new Vector2(i * .25f, 0); cell.anchorMax = new Vector2((i + 1) * .25f, 1);
                cell.offsetMin = new Vector2(3, 0); cell.offsetMax = new Vector2(-3, 0);
                PictureButton(button, names[i], true, 54);
                Text label = button.GetComponentInChildren<Text>(); label.fontSize = 12;
                label.rectTransform.offsetMax = new Vector2(-2, 27); label.rectTransform.offsetMin = new Vector2(2, 3);
                if ((int)care == i)
                {
                    Outline outline = button.gameObject.AddComponent<Outline>(); outline.effectColor = purple; outline.effectDistance = new Vector2(1.6f, -1.6f);
                }
            }
        }

        void PictureHands(float top)
        {
            ScrollRailLabel("Com as mãos", top, 27, 16, true);
            string[] modes = { "Mover", "Amassar", "Girar" };
            for (int i = 0; i < modes.Length; i++)
            {
                string mode = modes[i];
                PictureCard(mode, i, 3, top + 36, Hex("EAF1E7"), () => { SetInteractionMode(mode); BuildHomeUI(); }, InteractionMode == mode, 106);
            }
        }
    }
}
