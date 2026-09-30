using UnityEngine;
using UnityEngine.UI;

namespace MimoGarden
{
    public sealed partial class MimoExperience
    {
        Button[] recipeOptionButtons;
        Text[] recipeOptionLabels;
        Outline[] recipeSelectionOutlines;
        Button finishRecipeButton;
        Text ritualHint, ritualFooter;
        RectTransform mixingMeter;
        Image mixingMeterFill;
        Text mixingMeterLabel;
        MimoCreationStation creationStation;
        bool mainIngredientAdded, creationSyntheticStir;
        int creationFinger = -1, lastRitualStatus = -1;
        public PetSave DraftRecipe => draft == null ? null : draft.Copy();
        public PetSave SavedRecipe => saved == null ? null : saved.Copy();
        public MimoCreationStation CreationStation => creationStation;
        public bool PendingStir => creationStation && creationStation.PendingStir;
        public float StirProgress => creationStation ? creationStation.StirProgress : 0f;
        public int AddedIngredientCount => creationStation ? creationStation.AddedIngredientCount : 0;
        public RectTransform MixingMeter => mixingMeter;
        public float MixingMeterProgress => mixingMeterFill ? mixingMeterFill.rectTransform.anchorMax.x : 0f;
        public bool CanFinalizeRecipe => place == Place.Laboratory && mainIngredientAdded && creationStation && !creationStation.Busy && !creationStation.PendingStir;
        public Vector2 CreationDropScreenPosition => creationStation ? creationStation.CenterScreen : Vector2.zero;
        public Vector2 CreationStirScreenPoint(float radians) => creationStation ? creationStation.StirScreenPoint(radians) : Vector2.zero;

        public void ResetCreationRitual(bool keepRecipe)
        {
            if (!creationStation)
            {
                creationStation = new GameObject("A surprise inside the bamboo steamer").AddComponent<MimoCreationStation>();
                creationStation.Initialize(sceneCamera);
            }
            CancelCreationRitual();
            if (!keepRecipe) { mainIngredientAdded = false; creationStation.ResetStation(); }
            else creationStation.RestoreForCreation();
            creationStation.gameObject.SetActive(true);
            EnsureMixingMeter(); mixingMeter.gameObject.SetActive(true);
            actorRoot.gameObject.SetActive(false);
            bowl.gameObject.SetActive(false); brew.gameObject.SetActive(false); brewParts.gameObject.SetActive(false);
            lastRitualStatus = -1;
        }
        public void UpdateCreationRitual()
        {
            if (!creationStation || !creationStation.gameObject.activeSelf) return;
            creationStation.Tick(Time.unscaledDeltaTime);
            if (place != Place.Laboratory) return;
            if (actorRoot.gameObject.activeSelf) actorRoot.gameObject.SetActive(false);
            RefreshRitualStatus();
        }
        public void CancelCreationRitual(bool hideStation = false)
        {
            creationFinger = -1; creationSyntheticStir = false;
            if (mixingMeter) mixingMeter.gameObject.SetActive(false);
            if (!creationStation) return;
            creationStation.CancelPointer();
            if (hideStation) creationStation.gameObject.SetActive(false);
        }
        public void SuspendCreationRitual() { CancelCreationRitual(true); }
        public void ReleaseCreationRitual()
        {
            if (!creationStation) return;
            creationStation.ReleaseResources(); Destroy(creationStation.gameObject); creationStation = null;
        }
        public void BeginCreationReveal() { CancelCreationRitual(); if (creationStation) creationStation.BeginReveal(); }
        public void SetCreationRevealProgress(float progress) { if (creationStation) creationStation.SetRevealProgress(progress); }
        public void CompleteCreationReveal() { if (creationStation) creationStation.CompleteReveal(); }

        public void SelectRecipeCategory(int category)
        {
            if (place != Place.Laboratory) return;
            step = Mathf.Clamp(category, 0, PetCatalog.CategoryCount - 1); BuildRecipeUI();
        }
        static bool NoPhysicalIngredient(int category, int index)
        { return index == 0 && (category == 1 || category == 2 || category == 4 || category == 5 || category == 6); }

        // A tap can omit a piece. Actual ingredients travel into the steamer and get mixed.
        public void SelectIngredient(int index)
        {
            if (place != Place.Laboratory || !creationStation) return;
            index = Mathf.Clamp(index, 0, PetCatalog.Options(step).Length - 1);
            if (creationStation.Busy) return;
            if (NoPhysicalIngredient(step, index))
            {
                PetCatalog.Set(draft, step, index); RefreshRecipeSelection();
                Toast("Combinado: " + PetCatalog.Options(step)[index] + ".", 2.2f);
            }
            else Toast("Puxe este ingrediente para a esquerda, até a vaporeira.", 3.5f);
        }
        public bool BeginIngredientDrag(int category, int index, Vector2 screenPosition)
        {
            if (place != Place.Laboratory || !creationStation || category < 0 || category >= PetCatalog.CategoryCount) return false;
            if (index < 0 || index >= PetCatalog.Options(category).Length || NoPhysicalIngredient(category, index)) return false;
            if (creationStation.Busy) return false;
            EndStir();
            return creationStation.BeginIngredientDrag(category, index, screenPosition);
        }
        public void MoveIngredientDrag(Vector2 position) { if (creationStation) creationStation.MoveIngredientDrag(position); }
        public bool EndIngredientDrag(Vector2 position)
        {
            if (!creationStation) return false;
            int category = creationStation.DragCategory, index = creationStation.DragIndex;
            bool dropped = creationStation.EndIngredientDrag(position);
            if (dropped)
            {
                PetCatalog.Set(draft, category, index);
                if (category == 0) mainIngredientAdded = true;
                RefreshRecipeSelection(); lastRitualStatus = -1; RefreshRitualStatus();
            }
            return dropped;
        }
        public void CancelIngredientDrag() { if (creationStation) creationStation.CancelDrag(); }
        public bool BeginStir(Vector2 position)
        {
            if (place != Place.Laboratory || !creationStation || IsGestureOverUI(position)) return false;
            bool began = creationStation.BeginStir(position); creationSyntheticStir = began; return began;
        }
        public void MoveStir(Vector2 position) { if (creationStation) creationStation.MoveStir(position); }
        public void EndStir()
        {
            if (creationStation) creationStation.EndStir();
            creationSyntheticStir = false; creationFinger = -1; RefreshRitualStatus();
        }
        void RefreshBrew()
        {
            // Never initialize the draft character before the reveal.
            actorRoot.gameObject.SetActive(false);
            bowl.gameObject.SetActive(false); brew.gameObject.SetActive(false); brewParts.gameObject.SetActive(false);
            RefreshRecipeSelection();
        }
        // Compatibility for old callers, without an automatic shortcut around the ritual.
        public void SurpriseRecipe() { Toast("Arraste uma cor e mova o dedo na mistura!", 4f); }

        void EnsureMixingMeter()
        {
            if (mixingMeter) return;
            Image panel = Image("Mixing progress near steamer", safeArea, Hex("FFFAF4")); panel.raycastTarget = false;
            mixingMeter = panel.rectTransform;
            SetRect(mixingMeter, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(286, 72));
            mixingMeterLabel = Label("Mova o dedo para misturar", mixingMeter, 16, ink, FontStyle.Bold);
            mixingMeterLabel.alignment = TextAnchor.MiddleCenter;
            SetRect(mixingMeterLabel.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -5), new Vector2(270, 30));
            Image track = Image("Mixing progress track", mixingMeter, Hex("E6DCF2")); track.raycastTarget = false;
            SetRect(track.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 12), new Vector2(258, 22));
            mixingMeterFill = Image("Mixing progress fill", track.transform, purple); mixingMeterFill.raycastTarget = false;
            Stretch(mixingMeterFill.rectTransform);
        }

        void RefreshMixingMeter()
        {
            EnsureMixingMeter();
            mixingMeter.gameObject.SetActive(true);
            // Follow the front of the vessel as resolution, camera and safe area change.
            Vector2 screen = sceneCamera.WorldToScreenPoint(creationStation.transform.TransformPoint(new Vector3(0, .05f, -1.4f)));
            float scale = canvas.scaleFactor;
            screen.y -= 46f * scale;
            screen.x = Mathf.Clamp(screen.x, Screen.safeArea.xMin + 150f * scale, sceneCamera.pixelRect.xMax - 150f * scale);
            screen.y = Mathf.Clamp(screen.y, Screen.safeArea.yMin + 100f * scale, Screen.safeArea.yMax - 50f * scale);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safeArea, screen, null, out Vector2 position);
            mixingMeter.anchoredPosition = position;
            RectTransform fillRect = mixingMeterFill.rectTransform;
            fillRect.anchorMax = new Vector2(StirProgress, 1);
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            mixingMeterFill.gameObject.SetActive(StirProgress > 0);
            mixingMeterFill.color = CanFinalizeRecipe ? Hex("60BFA4") : purple;
            mixingMeterLabel.text = creationStation.IsPouring ? "Ingrediente chegando..." : CanFinalizeRecipe ? "Tudo misturado!" : "Mova o dedo para misturar";
        }

        void BuildRecipeUI()
        {
            SidebarStart("Creation");
            Text ingredientsTitle = RailLabel("INGREDIENTES", 12, 30, 16, true);
            if (saved.created)
            {
                ingredientsTitle.rectTransform.sizeDelta = new Vector2(126, 38);
                Button back = Button("Voltar", tray, Hex("DEE7F5"), ink, EnterHome, 18);
                SetRect((RectTransform)back.transform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-12, -8), new Vector2(86, 56));
            }
            ritualHint = RailLabel("Arraste para a vaporeira", 62, 56, 17, true);
            const int categoryColumns = 4;
            const float categoryHeight = 96f, categoryStride = 104f;
            float categoriesHeight = Mathf.CeilToInt(PetCatalog.CategoryCount / (float)categoryColumns) * categoryStride;
            RectTransform categories = Rect("Ingredient categories", tray);
            categories.anchorMin = Vector2.up; categories.anchorMax = Vector2.one; categories.pivot = Vector2.up;
            categories.offsetMin = new Vector2(12, -122 - categoriesHeight); categories.offsetMax = new Vector2(-12, -122);
            string[] names = PetCatalog.Options(step);
            int rows = Mathf.CeilToInt(names.Length / 2f);
            const float optionHeight = 152f, optionStride = 160f;
            RailLabel(PetCatalog.CategoryNames[step] + " · " + names.Length, 122 + categoriesHeight, 32, 22, true);
            // Category pictures stay within reach while only the ingredients scroll.
            rememberedRailOffset = 0;
            CreateRailScroll(160 + categoriesHeight, 132, rows * optionStride + 8f);
            for (int category = 0; category < PetCatalog.CategoryCount; category++)
            {
                int chosen = category;
                bool selected = step == category;
                Button tab = Button("", categories, selected ? Hex("E4D7FF") : Hex("FFF9F2"), ink, () => SelectRecipeCategory(chosen));
                tab.gameObject.name = "Category " + category + " " + PetCatalog.CategoryNames[category];
                Destroy(tab.GetComponentInChildren<Text>().gameObject);
                RectTransform cell = (RectTransform)tab.transform;
                int column = category % categoryColumns;
                float top = category / categoryColumns * categoryStride;
                cell.anchorMin = new Vector2(column / (float)categoryColumns, 1);
                cell.anchorMax = new Vector2((column + 1) / (float)categoryColumns, 1); cell.pivot = Vector2.up;
                cell.offsetMin = new Vector2(3, -top - categoryHeight); cell.offsetMax = new Vector2(-3, -top);
                MimoIngredientIcon picture = Rect("Category picture", tab.transform).gameObject.AddComponent<MimoIngredientIcon>();
                picture.raycastTarget = false; picture.Category = category; picture.Index = category == 2 ? 2 : 1;
                picture.CategoryPreview = true; picture.IngredientColor = Hex("8760D8");
                SetRect(picture.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 9), new Vector2(76, 76));
                Text categoryName = Label(PetCatalog.CategoryNames[category], tab.transform, 12, ink, FontStyle.Bold);
                categoryName.alignment = TextAnchor.MiddleCenter;
                SetRect(categoryName.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 8), new Vector2(77, 22));
                tab.gameObject.AddComponent<MimoUIButtonFeedback>().artwork = picture.rectTransform;
                if (selected)
                {
                    Outline outline = tab.gameObject.AddComponent<Outline>(); outline.effectColor = purple; outline.effectDistance = new Vector2(2, -2);
                    Image marker = Image("Selected category", tab.transform, purple, true); marker.raycastTarget = false;
                    SetRect(marker.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 5), new Vector2(7, 7));
                }
            }
            RectTransform options = Rect("Creation options", railContent); PlaceRailContent(options, 0, rows * optionStride);
            recipeOptionButtons = new Button[names.Length]; recipeOptionLabels = new Text[names.Length]; recipeSelectionOutlines = new Outline[names.Length];
            for (int option = 0; option < names.Length; option++)
            {
                int chosen = option;
                Button choice = Button(names[option], options, Hex("FFFAF4"), ink, () => SelectIngredient(chosen), 17);
                choice.gameObject.name = "Option " + step + "." + option + " " + names[option];
                PlaceRecipeCell((RectTransform)choice.transform, option % 2, option / 2 * optionStride, optionHeight);
                recipeOptionButtons[option] = choice;
                Text label = recipeOptionLabels[option] = choice.GetComponentInChildren<Text>();
                label.resizeTextForBestFit = false; label.fontSize = 16;
                foreach (string word in names[option].Split(' ')) if (word.Length > 10) label.fontSize = 15;
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(5, 5); label.rectTransform.offsetMax = new Vector2(-5, -102);
                MimoIngredientIcon icon = Rect("Ingredient icon", choice.transform).gameObject.AddComponent<MimoIngredientIcon>();
                icon.raycastTarget = false; icon.Category = step; icon.Index = option;
                icon.IngredientColor = step == 0 ? colors[option] : Hex("8760D8");
                SetRect(icon.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -2), new Vector2(112, 100));
                choice.gameObject.AddComponent<MimoUIButtonFeedback>().artwork = icon.rectTransform;
                MimoIngredientDrag handler = choice.gameObject.AddComponent<MimoIngredientDrag>(); handler.Configure(this, railScroll, step, option, canvas);
                Outline outline = choice.gameObject.AddComponent<Outline>(); outline.effectColor = purple; outline.effectDistance = new Vector2(2, -2); recipeSelectionOutlines[option] = outline;
            }
            ritualFooter = Label("Deslize para ver os ingredientes", tray, 15, muted);
            SetRect(ritualFooter.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(14, 84), new Vector2(RailWidth - 28, 45));
            ritualFooter.alignment = TextAnchor.MiddleCenter;
            finishRecipeButton = RailAction("Criar meu Mimo!", BeginBirth);
            RefreshRecipeSelection(); lastRitualStatus = -1; RefreshRitualStatus(); FinishSidebarScroll();
        }
        void PlaceRecipeCell(RectTransform cell, int column, float top, float height)
        {
            cell.anchorMin = new Vector2(column * .5f, 1); cell.anchorMax = new Vector2((column + 1) * .5f, 1); cell.pivot = Vector2.up;
            cell.offsetMin = new Vector2(column == 0 ? 0 : 4, -top - height); cell.offsetMax = new Vector2(column == 0 ? -4 : 0, -top);
        }
        void RefreshRecipeSelection()
        {
            if (recipeOptionButtons == null || draft == null) return;
            int selected = PetCatalog.Get(draft, step);
            for (int option = 0; option < recipeOptionButtons.Length; option++)
            {
                if (!recipeOptionButtons[option]) continue;
                bool chosen = option == selected && (step != 0 || mainIngredientAdded);
                recipeOptionButtons[option].GetComponent<Image>().color = chosen ? Hex("DDD1FF") : Hex("F3F5FF");
                recipeOptionLabels[option].color = ink; recipeSelectionOutlines[option].enabled = chosen;
            }
        }
        void RefreshRitualStatus()
        {
            if (!creationStation || !ritualHint || place != Place.Laboratory) return;
            RefreshMixingMeter();
            int status = creationStation.IsPouring ? 1000 : PendingStir ? 2000 : CanFinalizeRecipe ? 3000 : 4000;
            if (lastRitualStatus == status) return; lastRitualStatus = status;
            ritualHint.text = "Arraste ingredientes\npara a vaporeira.";
            ritualFooter.text = PendingStir ? "Pode acrescentar ingredientes\ne misturar quando quiser." : CanFinalizeRecipe ? "Crie agora ou acrescente\noutro ingrediente." : "Puxe para a esquerda.\nDeslize para ver mais.";
            finishRecipeButton.interactable = CanFinalizeRecipe;
        }
        void HandleCreationTouch()
        {
            if (!creationStation || creationSyntheticStir || creationStation.IsDraggingIngredient) return;
            if (Input.touchCount > 0)
            {
                if (creationFinger < 0)
                {
                    for (int i = 0; i < Input.touchCount; i++)
                    {
                        Touch t = Input.GetTouch(i);
                        if (t.phase == TouchPhase.Began && BeginStir(t.position)) { creationFinger = t.fingerId; creationSyntheticStir = false; break; }
                    }
                }
                else
                {
                    bool found = false;
                    for (int i = 0; i < Input.touchCount; i++)
                    {
                        Touch t = Input.GetTouch(i); if (t.fingerId != creationFinger) continue; found = true;
                        if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) EndStir(); else MoveStir(t.position);
                        break;
                    }
                    if (!found) EndStir();
                }
                return;
            }
            if (creationFinger >= 0) { EndStir(); return; }
            if (Input.GetMouseButtonDown(0)) { BeginStir(Input.mousePosition); creationSyntheticStir = false; }
            if (Input.GetMouseButton(0)) MoveStir(Input.mousePosition);
            if (Input.GetMouseButtonUp(0) || (!Input.GetMouseButton(0) && creationStation.IsStirring)) EndStir();
        }
    }
}
