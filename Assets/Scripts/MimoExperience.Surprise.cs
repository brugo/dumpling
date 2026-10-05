using System;
using UnityEngine;
using UnityEngine.UI;

namespace MimoGarden
{
    public sealed partial class MimoExperience
    {
        bool surpriseCreation, surpriseRolled;
        readonly int[] surpriseAmounts = new int[3];
        readonly System.Random surpriseRandom = new System.Random(Guid.NewGuid().GetHashCode());
        Text[] surpriseCountLabels;
        Text surpriseSummary;
        public bool IsSurpriseCreation => surpriseCreation;
        public int[] SurpriseAmounts => (int[])surpriseAmounts.Clone();

        public void EnterSurpriseLaboratory()
        {
            surpriseCreation = true;
            ShowLaboratory(false);
        }
        void ResetSurpriseRecipe()
        {
            Array.Clear(surpriseAmounts, 0, surpriseAmounts.Length);
            surpriseRolled = false;
        }
        void BuildCreationModeButtons()
        {
            RectTransform modes = Rect("Choose creation mode", tray);
            modes.anchorMin = Vector2.up; modes.anchorMax = Vector2.one; modes.pivot = Vector2.up;
            modes.offsetMin = new Vector2(12, -114); modes.offsetMax = new Vector2(-12, -62);
            for (int i = 0; i < 2; i++)
            {
                bool surprise = i == 1, selected = surpriseCreation == surprise;
                Button button = Button(surprise ? "Surpresa" : "Ateliê", modes, selected ? purple : Hex("E8EEE3"), selected ? Color.white : ink,
                    () => { if (surpriseCreation == surprise) return; if (surprise) EnterSurpriseLaboratory(); else EnterLaboratory(); }, 18);
                button.gameObject.name = surprise ? "Creation mode Surprise" : "Creation mode Atelier";
                RectTransform cell = (RectTransform)button.transform;
                cell.anchorMin = new Vector2(i * .5f, 0); cell.anchorMax = new Vector2((i + 1) * .5f, 1);
                cell.offsetMin = new Vector2(i == 0 ? 0 : 4, 0); cell.offsetMax = new Vector2(i == 0 ? -4 : 0, 0);
            }
        }
        void BuildSurpriseUI()
        {
            SidebarStart("Creation");
            Text heading = RailLabel("CRIAÇÃO SURPRESA", 12, 38, 16, true);
            if (saved.created)
            {
                heading.rectTransform.sizeDelta = new Vector2(220, 38);
                Button back = Button("Voltar", tray, Hex("E5EEE4"), ink, EnterHome, 18);
                SetRect((RectTransform)back.transform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-12, -8), new Vector2(86, 48));
            }
            BuildCreationModeButtons();
            ritualHint = RailLabel("Arraste para a vaporeira.\nPode repetir os ingredientes!", 124, 56, 17, true);
            rememberedRailOffset = 0;
            CreateRailScroll(194, 150, 390);
            surpriseCountLabels = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                int ingredientIndex = i;
                Button card = RailButton("", i * 118, Hex("F0F3EA"), () => Toast("Puxe " + MimoSurpriseRecipe.Names[ingredientIndex].ToLowerInvariant() + " para a vaporeira.", 3), 108);
                card.gameObject.name = "Kitchen ingredient " + i;
                Destroy(card.GetComponentInChildren<Text>().gameObject);
                MimoKitchenIcon art = Rect("Kitchen illustration", card.transform).gameObject.AddComponent<MimoKitchenIcon>();
                art.Ingredient = i; art.raycastTarget = false;
                SetRect(art.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(8, -8), new Vector2(90, 90));
                Text label = Label(MimoSurpriseRecipe.Names[i], card.transform, 22, ink, FontStyle.Bold);
                SetRect(label.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(108, -12), new Vector2(170, 32));
                Text quantity = surpriseCountLabels[i] = Label("", card.transform, 17, muted);
                SetRect(quantity.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(108, -46), new Vector2(195, 28));
                Text pull = Label("Puxe para adicionar", card.transform, 14, muted);
                SetRect(pull.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(108, -76), new Vector2(195, 25));
                card.gameObject.AddComponent<MimoUIButtonFeedback>().artwork = art.rectTransform;
                card.gameObject.AddComponent<MimoIngredientDrag>().Configure(this, railScroll, MimoSurpriseRecipe.Category, i, canvas);
            }
            ScrollRailLabel("Trocar de modo começa outra receita.", 354, 34, 14);
            surpriseSummary = RailLabel("", 0, 30, 16, true);
            SetRect(surpriseSummary.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(14, 115), new Vector2(RailWidth - 28, 30));
            ritualFooter = Label("", tray, 14, muted);
            SetRect(ritualFooter.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(14, 78), new Vector2(RailWidth - 28, 38));
            ritualFooter.alignment = TextAnchor.MiddleCenter;
            finishRecipeButton = RailAction("Revelar surpresa!", BeginBirth);
            RefreshSurpriseCounts(); lastRitualStatus = -1; RefreshRitualStatus(); FinishSidebarScroll();
        }
        void RefreshSurpriseCounts()
        {
            if (!surpriseCreation) return;
            for (int i = 0; i < 3; i++)
                if (surpriseCountLabels != null && surpriseCountLabels[i]) surpriseCountLabels[i].text = surpriseAmounts[i] + " na mistura";
            if (surpriseSummary) surpriseSummary.text = "Sua receita: " + MimoSurpriseRecipe.Total(surpriseAmounts) + " / " + MimoSurpriseRecipe.MaxPortions + " porções";
        }
    }
}
