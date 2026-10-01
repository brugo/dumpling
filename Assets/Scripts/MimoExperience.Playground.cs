using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MimoGarden
{
    public sealed partial class MimoExperience
    {
        public enum PlaygroundAction { Idle, Jump, Tag }
        public IReadOnlyList<PetActor> PlaygroundPets => playgroundPets;
        const float PlaygroundScale = .72f;
        const float PetGround = .105f;
        readonly List<PetActor> playgroundPets = new List<PetActor>();
        readonly Dictionary<PetActor, PlayMotion> playMotions = new Dictionary<PetActor, PlayMotion>();
        PetCollection collection;
        PetActor creationActor;
        MimoPlaygroundProp[] playgroundProps;
        Transform selectionHalo;

        sealed class PlayMotion
        {
            public PlaygroundAction action;
            public Vector3 gardenPosition, destination, hopStart, hopEnd;
            public Quaternion gardenRotation = Quaternion.identity;
            public float nextHop, hopAt, chooseAt;
            public bool hopping;
        }

        void LoadPlayground()
        {
            creationActor = actor;
            collection = PetStorage.LoadCollection();
            playgroundProps = gardenRoom.GetComponentsInChildren<MimoPlaygroundProp>(true);
            SynchronizePlayground();
            if (playgroundPets.Count > 0)
            {
                PetActor active = playgroundPets.Find(p => p.Data.petId == collection.activePetId) ?? playgroundPets[playgroundPets.Count - 1];
                actor = active; actorRoot = active.transform; saved = active.Data;
            }
            else saved = new PetSave();
            BuildSelectionHalo();
        }

        void SynchronizePlayground()
        {
            for (int i = playgroundPets.Count - 1; i >= 0; i--)
            {
                PetActor pet = playgroundPets[i];
                if (collection.pets.Exists(p => p.petId == pet.Data.petId)) continue;
                StopPetAction(pet);
                pet.StateChanged -= RefreshNeeds;
                playMotions.Remove(pet); playgroundPets.RemoveAt(i);
                pet.gameObject.SetActive(false); Destroy(pet.gameObject);
            }
            foreach (PetSave data in collection.pets)
            {
                if (playgroundPets.Exists(p => p.Data.petId == data.petId)) continue;
                PetActor pet = new GameObject("Amigo — " + data.petName).AddComponent<PetActor>();
                pet.Initialize(data); pet.StateChanged += RefreshNeeds;
                Vector3[] spots = { new Vector3(-1.8f, PetGround, -.45f), new Vector3(1.8f, PetGround, -.45f), new Vector3(0, PetGround, 2f), new Vector3(-3f, PetGround, 1f), new Vector3(3f, PetGround, 1f) };
                Vector3 position = spots[0];
                foreach (Vector3 spot in spots)
                {
                    position = spot;
                    if (!playgroundPets.Exists(p => (playMotions[p].gardenPosition - spot).sqrMagnitude < 2.5f)) break;
                }
                pet.transform.position = position; pet.transform.localScale = Vector3.one * PlaygroundScale;
                playgroundPets.Add(pet); playMotions.Add(pet, new PlayMotion { gardenPosition = position, destination = position });
                pet.gameObject.SetActive(false);
            }
        }

        void PrepareCreationActor()
        {
            RememberGardenPositions();
            foreach (PetActor pet in playgroundPets) pet.gameObject.SetActive(false);
            actor = creationActor; actorRoot = actor.transform;
            if (selectionHalo) selectionHalo.gameObject.SetActive(false);
        }

        bool CommitCreation(PetSave newPet, string replacePetId = null)
        {
            // Storage only appends at the naming confirmation, never while the recipe is being edited.
            if (!PetStorage.Save(newPet, replacePetId)) { Toast("Não conseguimos guardar agora. Tente mais uma vez.", 5); return false; }
            AdoptStoredCollection(PetStorage.LoadCollection());
            actor = playgroundPets.Find(p => p.Data.petId == newPet.petId);
            actorRoot = actor.transform; saved = actor.Data;
            return true;
        }

        void AdoptStoredCollection(PetCollection persisted)
        {
            // Keep the existing live data and painted bath masks rather than rebuilding their actors.
            foreach (PetActor pet in playgroundPets)
            {
                int index = persisted.pets.FindIndex(p => p.petId == pet.Data.petId);
                if (index >= 0) persisted.pets[index] = pet.Data;
            }
            collection = persisted;
            SynchronizePlayground();
        }

        void ShowPlaygroundFriends()
        {
            creationActor.gameObject.SetActive(false);
            foreach (PetActor pet in playgroundPets)
            {
                PlayMotion state = playMotions[pet];
                bool visible = care == Care.Play || pet == actor;
                pet.gameObject.SetActive(visible);
                pet.transform.localScale = Vector3.one * (care == Care.Play ? PlaygroundScale : 1f);
                if (care == Care.Play)
                {
                    pet.transform.position = state.gardenPosition;
                    pet.transform.rotation = state.gardenRotation;
                }
                else if (pet == actor)
                {
                    pet.transform.position = new Vector3(0, PetGround, -.03f);
                    pet.transform.rotation = Quaternion.identity;
                }
            }
            FrameCareRoom();
        }

        void RememberGardenPositions()
        {
            if (place != Place.Home || care != Care.Play) return;
            foreach (PetActor pet in playgroundPets)
            {
                PlayMotion state = playMotions[pet];
                state.gardenPosition = pet.transform.position;
                state.gardenPosition.y = PetGround;
                state.gardenRotation = pet.transform.rotation;
                state.hopping = false;
            }
        }

        void FrameCareRoom()
        {
            if (care == Care.Play)
            {
                sceneCamera.transform.position = new Vector3(0, 8f, -14f);
                sceneCamera.transform.LookAt(new Vector3(0, .75f, 1.3f));
                sceneCamera.fieldOfView = 47f;
            }
            else if (care == Care.Bath)
            {
                sceneCamera.transform.position = new Vector3(0,4.4f,-8.8f);
                sceneCamera.transform.LookAt(new Vector3(0,1.2f,.4f));
                sceneCamera.fieldOfView = 40f;
            }
            else
            {
                sceneCamera.transform.position = new Vector3(0, 5.5f, -11.5f);
                sceneCamera.transform.LookAt(new Vector3(0, 1f, .6f));
                sceneCamera.fieldOfView = 43f;
            }
        }

        public bool SelectPet(PetActor pet)
        {
            if (IsFriendDialogOpen || place != Place.Home || !pet || !playgroundPets.Contains(pet)) return false;
            if (actor == pet) return true;
            ReleaseTouch(); CancelSnack(); ResetBathTools();
            actor = pet; actorRoot = pet.transform; saved = pet.Data;
            collection.activePetId = saved.petId;
            if (care != Care.Play) { StopPetAction(pet); ShowPlaygroundFriends(); SetTool(); }
            UpdateFriendHeading(); BuildHomeUI(); RefreshNeeds(); SaveCare();
            return true;
        }

        public void VisitCareRoom(string room)
        {
            if (place != Place.Home) return;
            SelectCare(room == "Bath" ? Care.Bath : room == "Food" ? Care.Food : Care.Play);
        }

        public PlaygroundAction GetPetAction(PetActor pet) => pet && playMotions.TryGetValue(pet, out PlayMotion state) ? state.action : PlaygroundAction.Idle;

        MimoPlaygroundProp OccupiedProp(PetActor pet)
        {
            if (playgroundProps != null) foreach (MimoPlaygroundProp prop in playgroundProps) if (prop && prop.Occupant == pet) return prop;
            return null;
        }

        public void StopPetAction(PetActor pet)
        {
            if (!pet || !playMotions.TryGetValue(pet, out PlayMotion state)) return;
            MimoPlaygroundProp prop = OccupiedProp(pet);
            if (prop) prop.Release(pet);
            state.action = PlaygroundAction.Idle; state.hopping = false;
            pet.CancelTouchMotion();
            if (place == Place.Home && care == Care.Play) state.gardenPosition = ClampPlaygroundPosition(pet.transform.position);
        }

        public bool SetPetAction(PetActor pet, PlaygroundAction action)
        {
            if (IsFriendDialogOpen || place != Place.Home || care != Care.Play || !pet || !playMotions.ContainsKey(pet)) return false;
            StopPetAction(pet);
            PlayMotion state = playMotions[pet]; state.action = action;
            state.nextHop = Time.time + .1f; state.chooseAt = 0;
            if (pet == actor) { UpdateFriendHeading(); BuildHomeUI(); }
            return true;
        }

        public bool TryPlaceOnProp(PetActor pet, MimoPlaygroundProp prop)
        {
            if (place != Place.Home || care != Care.Play || !pet || !prop || !playMotions.ContainsKey(pet)) return false;
            if (prop.Occupant && prop.Occupant != pet) return false;
            StopPetAction(pet);
            if (!prop.TryOccupy(pet)) return false;
            playMotions[pet].gardenPosition = pet.transform.position;
            if (pet == actor) { UpdateFriendHeading(); BuildHomeUI(); }
            return true;
        }

        void FinishPlaygroundDrop(Vector2 screenPoint)
        {
            if (care != Care.Play) return;
            MimoPlaygroundProp chosen = null;
            float best = float.MaxValue;
            foreach (MimoPlaygroundProp prop in playgroundProps)
            {
                if (!prop || (prop.Occupant && prop.Occupant != actor)) continue;
                // The drop is evaluated at the dumpling's feet, matching the ground marker while carrying.
                if (!prop.ContainsDrop(actorRoot.position)) continue;
                float distance = (prop.SeatAnchor.position - actorRoot.position).sqrMagnitude;
                if (distance < best) { best = distance; chosen = prop; }
            }
            if (chosen && TryPlaceOnProp(actor, chosen)) Toast("No " + chosen.DisplayName.ToLowerInvariant() + "! Arraste para sair.", 3.5f);
            else
            {
                actorRoot.position = SeparatePetPosition(actor, ClampPlaygroundPosition(actorRoot.position));
                playMotions[actor].gardenPosition = actorRoot.position;
            }
        }

        public Vector3 ClampPlaygroundPosition(Vector3 point)
        {
            point = new Vector3(Mathf.Clamp(point.x, -5.5f, 5.5f), PetGround, Mathf.Clamp(point.z, -5f, 5f));
            if (sceneCamera && place == Place.Home && care == Care.Play)
            {
                Vector3 middle = point + Vector3.up;
                Vector3 viewport = sceneCamera.WorldToViewportPoint(middle);
                float reach = Mathf.Abs(sceneCamera.WorldToViewportPoint(middle + sceneCamera.transform.right * 1.1f).x - viewport.x) + .015f;
                viewport.x = Mathf.Clamp(viewport.x, reach, 1f - reach);
                point.x = sceneCamera.ViewportToWorldPoint(viewport).x;
            }
            return point;
        }

        Vector3 SeparatePetPosition(PetActor pet, Vector3 point)
        {
            foreach (PetActor other in playgroundPets)
            {
                if (pet == other || !other.gameObject.activeInHierarchy || OccupiedProp(other)) continue;
                Vector3 away = point - other.transform.position; away.y = 0;
                if (away.sqrMagnitude >= 2.4f) continue;
                if (away.sqrMagnitude < .001f) away = playgroundPets.IndexOf(pet) > playgroundPets.IndexOf(other) ? Vector3.right : Vector3.left;
                point = ClampPlaygroundPosition(other.transform.position + away.normalized * 1.56f);
            }
            return point;
        }

        Vector3 PickPlayDestination(PetActor pet)
        {
            Vector3 point = pet.transform.position;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                point = new Vector3(UnityEngine.Random.Range(-4.9f, 4.9f), PetGround, UnityEngine.Random.Range(-4.4f, 4.6f));
                // Friends take turns darting toward each other and changing direction.
                if (attempt == 0 && playgroundPets.Count > 1 && UnityEngine.Random.value < .6f)
                {
                    PetActor friend = playgroundPets[UnityEngine.Random.Range(0, playgroundPets.Count)];
                    if (friend != pet && !OccupiedProp(friend)) point = friend.transform.position + UnityEngine.Random.insideUnitSphere * 1.6f;
                }
                point = ClampPlaygroundPosition(point);
                bool blocked = false;
                foreach (MimoPlaygroundProp prop in playgroundProps) if (prop.ContainsDrop(point)) { blocked = true; break; }
                if (!blocked && (point - pet.transform.position).sqrMagnitude > 3f) break;
            }
            return point;
        }

        void TickPlayground()
        {
            LayoutThemeParkBackdrop();
            if (IsFriendDialogOpen || place != Place.Home || care != Care.Play) return;
            float now = Time.time;
            foreach (PetActor pet in playgroundPets)
            {
                PlayMotion state = playMotions[pet];
                if (!pet.gameObject.activeInHierarchy || OccupiedProp(pet) || (pet == actor && petGesture)) continue;
                if (state.action == PlaygroundAction.Jump)
                {
                    if (now >= state.nextHop && pet.LiftHeight < .03f) { pet.Jump(); state.nextHop = now + .8f; }
                }
                else if (state.action == PlaygroundAction.Tag)
                {
                    if (state.hopping)
                    {
                        float progress = Mathf.Clamp01((now - state.hopAt) / .37f);
                        pet.transform.position = Vector3.Lerp(state.hopStart, state.hopEnd, progress);
                        if (progress >= 1) { state.hopping = false; state.nextHop = now + .075f; }
                    }
                    else if (now >= state.nextHop && pet.LiftHeight < .03f)
                    {
                        if (now >= state.chooseAt || Vector3.Distance(pet.transform.position, state.destination) < .7f)
                        { state.destination = PickPlayDestination(pet); state.chooseAt = now + UnityEngine.Random.Range(2.3f, 4.4f); }
                        Vector3 direction = state.destination - pet.transform.position; direction.y = 0;
                        Vector3 next = SeparatePetPosition(pet, ClampPlaygroundPosition(pet.transform.position + direction.normalized * .62f));
                        bool blocked = false;
                        foreach (MimoPlaygroundProp prop in playgroundProps) if (prop.ContainsDrop(next)) { blocked = true; break; }
                        if (blocked) { state.chooseAt = 0; state.nextHop = now + .15f; continue; }
                        state.hopStart = pet.transform.position; state.hopEnd = next; state.hopAt = now; state.hopping = true;
                        if (direction.sqrMagnitude > .01f) pet.transform.rotation = Quaternion.LookRotation(-direction.normalized, Vector3.up);
                        pet.PlayHop();
                    }
                }
                state.gardenPosition = pet.transform.position; state.gardenRotation = pet.transform.rotation;
            }
        }

        void UpdateFriendHeading()
        {
            if (place != Place.Home || !actor || actor.Data == null) return;
            title.text = care == Care.Play ? "Parquinho dos amigos" : care == Care.Bath ? "Um banho de carinho." : "Hora do nhac!";
            MimoPlaygroundProp prop = OccupiedProp(actor);
            string activity = prop ? prop.DisplayName : GetPetAction(actor) == PlaygroundAction.Jump ? "Pulando!" : GetPetAction(actor) == PlaygroundAction.Tag ? "Pega-pega!" : "Toque e arraste para brincar";
            subtitle.text = saved.petName + "  •  " + (care == Care.Play ? activity : care == Care.Bath ? "Esponja, espuma e água fresquinha" : "Uma frutinha para este amigo");
        }

        void BuildFriendPicker()
        {
            RailLabel("AMIGOS  " + playgroundPets.Count + "/" + PetStorage.MaxPets, 10, 26, 15, true);
            float width = (RailWidth - 24 - (PetStorage.MaxPets - 1) * 4) / PetStorage.MaxPets;
            for (int i = 0; i < playgroundPets.Count; i++)
            {
                PetActor friend = playgroundPets[i];
                Button button = Button(friend.Data.petName, tray, friend == actor ? purple : Hex("E4DEF4"), friend == actor ? Color.white : ink, () => SelectPet(friend), 15);
                SetRect((RectTransform)button.transform, Vector2.up, Vector2.up, Vector2.up, new Vector2(12 + i * (width + 4), -43), new Vector2(width, 54));
                Text text = button.GetComponentInChildren<Text>(); text.resizeTextForBestFit = true; text.resizeTextMinSize = 10; text.resizeTextMaxSize = 15;
                SetRect(text.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -13), new Vector2(width - 5, 23));
                Image portrait = Image("Cor deste amigo", button.transform, colors[friend.Data.colorIndex], true);
                SetRect(portrait.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 11), new Vector2(27, 22));
                portrait.raycastTarget = false;
                Image friendArt = Artwork("Friend illustration", portrait.transform, MimoUIArt.Action("Pular sem parar"));
                SetRect(friendArt.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(36, 36));
            }
            Button remove = Button("Excluir amigo...", tray, Hex("F3E0DE"), Hex("885858"), () => RequestDeletePet(actor), 16);
            SetRect((RectTransform)remove.transform, Vector2.up, Vector2.up, Vector2.up, new Vector2(12, -105), new Vector2(RailWidth - 24, 40));
        }

        void BuildPlaygroundHomeUI()
        {
            SidebarStart("Playground"); BuildFriendPicker();
            CreateRailScroll(159, 184, 454);
            ScrollRailLabel(saved.petName + " quer brincar!", 0, 32, 20, true);
            PictureCard("Pular sem parar", 0, 3, 42, Hex("FFF0CF"), () => SetPetAction(actor, PlaygroundAction.Jump), GetPetAction(actor) == PlaygroundAction.Jump);
            PictureCard("Pega-pega", 1, 3, 42, Hex("E7F4E7"), () => SetPetAction(actor, PlaygroundAction.Tag), GetPetAction(actor) == PlaygroundAction.Tag);
            PictureCard("Descansar", 2, 3, 42, Hex("EEE5FC"), () => { StopPetAction(actor); UpdateFriendHeading(); BuildHomeUI(); });
            ScrollRailLabel("Leve os amigos aos brinquedos do jardim.", 169, 54, 16);
            PictureHands(240);
            ScrollRailLabel("Até 5 amigos para inventar histórias.", 397, 42, 15);
            RailAction("Criar outro amigo", EnterLaboratory); PictureNavigation();
            FinishSidebarScroll();
        }

        void BuildSelectionHalo()
        {
            selectionHalo = Group("Este amigo está selecionado");
            LineRenderer ring = selectionHalo.gameObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 56;
            ring.widthMultiplier = .035f; ring.sharedMaterial = Mat("Friend selection gold", Hex("FFEBA1"), .1f);
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < 56; i++) { float a = i * Mathf.PI * 2 / 56; ring.SetPosition(i, new Vector3(Mathf.Cos(a) * 1.08f, 0, Mathf.Sin(a) * .8f)); }
            selectionHalo.gameObject.SetActive(false);
        }

        void UpdateSelectionHalo()
        {
            if (!selectionHalo) return;
            bool show = place == Place.Home && actor && actor.gameObject.activeInHierarchy;
            selectionHalo.gameObject.SetActive(show);
            if (show) { selectionHalo.position = new Vector3(actorRoot.position.x, actorRoot.position.y + .015f, actorRoot.position.z); selectionHalo.localScale = Vector3.one * (care == Care.Play ? 1 : 1.35f); }
        }
    }
}
