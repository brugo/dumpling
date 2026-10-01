using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MimoGarden
{
    public sealed partial class MimoExperience
    {
        public bool IsFriendDialogOpen => friendDialog && friendDialog.gameObject.activeSelf;
        public bool IsChoosingReplacement => IsFriendDialogOpen && replacementDialog && string.IsNullOrEmpty(pendingRemovalPetId);
        public string PendingRemovalPetId => pendingRemovalPetId;
        RectTransform friendDialog;
        CanvasGroup friendDialogTrayGroup;
        string pendingRemovalPetId;
        bool replacementDialog, confirmingFriendRemoval;

        public void RequestDeletePet(PetActor pet)
        {
            if (place != Place.Home || IsFriendDialogOpen || !pet || !playgroundPets.Contains(pet)) return;
            ReleaseTouch(); CancelSnack(); ResetBathTools();
            replacementDialog = false;
            pendingRemovalPetId = pet.Data.petId;
            ShowFriendRemovalConfirmation(pet);
        }

        void ShowReplacementChooser()
        {
            if (place != Place.Naming || draft == null || playgroundPets.Count < PetStorage.MaxPets) return;
            replacementDialog = true; pendingRemovalPetId = null;
            if (nameInput) nameInput.DeactivateInputField();
            RectTransform panel = CreateFriendDialog(248 + playgroundPets.Count * 72);
            FriendDialogLabel(panel, "O parquinho está cheio", 24, 48, 28, true);
            FriendDialogLabel(panel, "Escolha qual amigo substituir pelo novo.\nNinguém sai até você confirmar.", 80, 56, 19);
            for (int i = 0; i < playgroundPets.Count; i++)
            {
                PetActor friend = playgroundPets[i];
                Button option = Button("", panel, Hex("ECE5F6"), ink, () => ChooseFriendToReplace(friend));
                option.gameObject.name = "Substituir amigo " + friend.Data.petId;
                RectTransform row = (RectTransform)option.transform;
                row.anchorMin = Vector2.up; row.anchorMax = Vector2.one; row.pivot = Vector2.up;
                row.offsetMin = new Vector2(24, -148 - i * 72 - 64); row.offsetMax = new Vector2(-24, -148 - i * 72);
                DrawFriendPortrait(row, friend.Data, new Vector2(48, -32), 44);
                Text name = Label((i + 1) + " · " + friend.Data.petName, row, 22, ink, FontStyle.Bold);
                name.rectTransform.anchorMin = Vector2.up; name.rectTransform.anchorMax = Vector2.one; name.rectTransform.pivot = Vector2.up;
                name.rectTransform.offsetMin = new Vector2(91, -41); name.rectTransform.offsetMax = new Vector2(-14, -9);
                name.resizeTextForBestFit = true; name.resizeTextMinSize = 16; name.resizeTextMaxSize = 22;
                Text detail = Label(FriendDescription(friend.Data), row, 15, muted);
                detail.rectTransform.anchorMin = Vector2.up; detail.rectTransform.anchorMax = Vector2.one; detail.rectTransform.pivot = Vector2.up;
                detail.rectTransform.offsetMin = new Vector2(91, -61); detail.rectTransform.offsetMax = new Vector2(-14, -38);
            }
            Button cancel = FriendDialogButton(panel, "Voltar sem trocar", Hex("E6E9F1"), CancelFriendDialog, false, true);
            FocusDialogCancel(cancel);
        }

        public void ChooseFriendToReplace(PetActor pet)
        {
            if (!IsFriendDialogOpen || !replacementDialog || place != Place.Naming || !pet || !playgroundPets.Contains(pet)) return;
            pendingRemovalPetId = pet.Data.petId;
            ShowFriendRemovalConfirmation(pet);
        }

        void ShowFriendRemovalConfirmation(PetActor pet)
        {
            RectTransform panel = CreateFriendDialog(460);
            FriendDialogLabel(panel, replacementDialog ? "Trocar este amigo?" : "Excluir este amigo?", 24, 48, 28, true);
            RectTransform portraitRow = Rect("O amigo escolhido", panel);
            portraitRow.anchorMin = Vector2.up; portraitRow.anchorMax = Vector2.one; portraitRow.pivot = Vector2.up;
            portraitRow.offsetMin = new Vector2(26, -189); portraitRow.offsetMax = new Vector2(-26, -84);
            DrawFriendPortrait(portraitRow, pet.Data, new Vector2(59, -52), 88);
            Text name = Label(pet.Data.petName, portraitRow, 27, ink, FontStyle.Bold);
            name.rectTransform.anchorMin = Vector2.up; name.rectTransform.anchorMax = Vector2.one; name.rectTransform.pivot = Vector2.up;
            name.rectTransform.offsetMin = new Vector2(122, -62); name.rectTransform.offsetMax = new Vector2(-8, -7);
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 18; name.resizeTextMaxSize = 27;
            Text detail = Label(FriendDescription(pet.Data), portraitRow, 17, muted);
            detail.rectTransform.anchorMin = Vector2.up; detail.rectTransform.anchorMax = Vector2.one; detail.rectTransform.pivot = Vector2.up;
            detail.rectTransform.offsetMin = new Vector2(122, -101); detail.rectTransform.offsetMax = new Vector2(-8, -62);
            string message = replacementDialog
                ? "Este amigo será excluído para guardar " + draft.petName + ". Os outros amigos continuam no parquinho."
                : "Este amigo será excluído do aparelho. Os outros amigos continuam no parquinho.";
            FriendDialogLabel(panel, message, 220, 94, 21);
            FriendDialogLabel(panel, "Se mudar de ideia, toque em Cancelar.", 319, 35, 17);
            Button cancel = FriendDialogButton(panel, "Cancelar", Hex("E6E9F1"), CancelFriendDialog, false);
            FriendDialogButton(panel, replacementDialog ? "Excluir e guardar" : "Excluir", Hex("F1B7B1"), () => ConfirmFriendRemoval(), true);
            FocusDialogCancel(cancel);
        }

        public bool ConfirmFriendRemoval()
        {
            if (!IsFriendDialogOpen || confirmingFriendRemoval || string.IsNullOrEmpty(pendingRemovalPetId)) return false;
            PetActor target = playgroundPets.Find(p => p.Data.petId == pendingRemovalPetId);
            if (!target) { CancelFriendDialog(); return false; }
            confirmingFriendRemoval = true;
            try
            {
                if (replacementDialog)
                {
                    if (place != Place.Naming || draft == null) return false;
                    if (!CommitCreation(draft, pendingRemovalPetId)) { ShowFriendSaveError(); return false; }
                    CloseFriendDialog();
                    EnterHome(); actor.Pet(); Toast("Bem-vindo ao parquinho, " + saved.petName + "!", 4);
                    return true;
                }
                if (place != Place.Home) return false;
                // Build a separate complete snapshot: no live actor or collection is removed until the atomic save succeeds.
                PetCollection remaining = new PetCollection { activePetId = collection.activePetId, retiredPetIds = new List<string>(collection.retiredPetIds) };
                foreach (PetActor pet in playgroundPets) if (pet != target) remaining.pets.Add(pet.Data);
                if (remaining.activePetId == pendingRemovalPetId)
                    remaining.activePetId = remaining.pets.Count > 0 ? remaining.pets[remaining.pets.Count - 1].petId : null;
                if (!PetStorage.SaveCollection(remaining)) { ShowFriendSaveError(); return false; }
                string removedName = target.Data.petName;
                ReleaseTouch(); CancelSnack(); ResetBathTools(); RememberGardenPositions();
                AdoptStoredCollection(PetStorage.LoadCollection());
                CloseFriendDialog();
                if (playgroundPets.Count == 0)
                {
                    saved = new PetSave(); actor = creationActor; actorRoot = actor.transform;
                    EnterLaboratory(); Toast("Vamos criar um novo amigo?", 4);
                }
                else
                {
                    actor = playgroundPets.Find(p => p.Data.petId == collection.activePetId) ?? playgroundPets[playgroundPets.Count - 1];
                    actorRoot = actor.transform; saved = actor.Data;
                    if (care != Care.Play) ShowPlaygroundFriends();
                    UpdateFriendHeading(); BuildHomeUI(); SetTool(); RefreshNeeds();
                    Toast(removedName + " foi excluído.", 3);
                }
                return true;
            }
            finally { confirmingFriendRemoval = false; }
        }

        void ShowFriendSaveError()
        {
            if (!friendDialog) return;
            Text error = friendDialog.GetComponentInChildren<Text>();
            if (error) { error.text = "Não foi possível salvar. Tente de novo."; error.fontSize = 23; }
        }

        public void CancelFriendDialog() { CloseFriendDialog(); }

        void CloseFriendDialog()
        {
            if (friendDialog) { friendDialog.gameObject.SetActive(false); Destroy(friendDialog.gameObject); friendDialog = null; }
            if (friendDialogTrayGroup) friendDialogTrayGroup.interactable = true;
            pendingRemovalPetId = null; replacementDialog = false;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            ReleaseTouch();
        }

        RectTransform CreateFriendDialog(float height)
        {
            if (friendDialog) { friendDialog.gameObject.SetActive(false); Destroy(friendDialog.gameObject); }
            friendDialog = Image("Escolher e confirmar amigo", canvas.transform, new Color(.10f, .12f, .22f, .64f)).rectTransform;
            friendDialog.GetComponent<Image>().sprite = null; Stretch(friendDialog); friendDialog.SetAsLastSibling();
            friendDialogTrayGroup = tray.GetComponent<CanvasGroup>();
            if (!friendDialogTrayGroup) friendDialogTrayGroup = tray.gameObject.AddComponent<CanvasGroup>();
            friendDialogTrayGroup.interactable = false;
            RectTransform panel = Image("Confirmação", friendDialog, cream).rectTransform;
            float width = Mathf.Min(650, ((RectTransform)canvas.transform).rect.width - 40);
            SetRect(panel, Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(width, height));
            return panel;
        }

        Text FriendDialogLabel(RectTransform panel, string text, float top, float height, int size, bool bold = false)
        {
            Text label = Label(text, panel, size, bold ? ink : muted, bold ? FontStyle.Bold : FontStyle.Normal);
            label.alignment = TextAnchor.MiddleCenter;
            RectTransform r = label.rectTransform; r.anchorMin = Vector2.up; r.anchorMax = Vector2.one; r.pivot = Vector2.up;
            r.offsetMin = new Vector2(28, -top - height); r.offsetMax = new Vector2(-28, -top);
            return label;
        }

        Button FriendDialogButton(RectTransform panel, string text, Color color, System.Action action, bool right, bool full = false)
        {
            Button button = Button(text, panel, color, ink, action, 20);
            RectTransform r = (RectTransform)button.transform;
            r.anchorMin = new Vector2(full || !right ? 0 : .5f, 0); r.anchorMax = new Vector2(full || right ? 1 : .5f, 0); r.pivot = Vector2.zero;
            r.offsetMin = new Vector2(full || !right ? 24 : 7, 22); r.offsetMax = new Vector2(full || right ? -24 : -7, 78);
            return button;
        }

        void FocusDialogCancel(Button cancel)
        {
            if (EventSystem.current) { EventSystem.current.SetSelectedGameObject(null); EventSystem.current.SetSelectedGameObject(cancel.gameObject); }
        }

        string FriendDescription(PetSave pet) => PetCatalog.ColorNames[pet.colorIndex] + " · " + PetCatalog.FinishNames[pet.finishIndex];

        void DrawFriendPortrait(Transform parent, PetSave pet, Vector2 position, float size)
        {
            Image portrait = Image("Retrato de " + pet.petName, parent, colors[pet.colorIndex], true); portrait.raycastTarget = false;
            SetRect(portrait.rectTransform, Vector2.up, Vector2.up, Vector2.one * .5f, position, new Vector2(size, size * .85f));
            for (int side = -1; side <= 1; side += 2)
            {
                Image eye = Image("Olhinho", portrait.transform, ink, true); eye.raycastTarget = false;
                SetRect(eye.rectTransform, Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f, new Vector2(side * size * .17f, -size * .04f), new Vector2(size * .085f, size * .12f));
            }
        }
    }
}
