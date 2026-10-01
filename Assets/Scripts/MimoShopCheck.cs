using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;

namespace MimoGarden
{
    public sealed class MimoShopCheck : MonoBehaviour
    {
        [Serializable] sealed class Report {
            public string version,mode;public bool passed,ordinarySaveUnchanged;
            public int width,height,checksPassed;public long peakAllocatedBytes;
            public float fiveFriendsAverageFps,minOneSecondFps;
            public string[] checks,errors,screenshots;
        }
        static string output,ordinary;static byte[] original;static bool existed;
        static bool Restart => Array.IndexOf(Environment.GetCommandLineArgs(),"--mimo-shop-restart-test")>=0;
        static bool Enabled => Restart || Array.IndexOf(Environment.GetCommandLineArgs(),"--mimo-shop-test")>=0;
        readonly List<string> checks=new List<string>(),errors=new List<string>(),screenshots=new List<string>();
        MimoExperience game;bool done;float started;long peak;float fps,minFps;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Isolate()
        {
            if(!Enabled)return;
            var args=Environment.GetCommandLineArgs();int idx=Array.IndexOf(args,"--mimo-test-output");
            output=idx>=0 && idx+1<args.Length ? Path.GetFullPath(args[idx+1]) : Path.Combine(Application.temporaryCachePath,"MimoShopValidation");
            Directory.CreateDirectory(output);ordinary=Path.Combine(Application.persistentDataPath,"mimo-pet.json");existed=File.Exists(ordinary);original=existed ? File.ReadAllBytes(ordinary):null;
            PetStorage.SavePathOverride=Path.Combine(output,"isolated-shop-save.json");
            if(!Restart){
                foreach(string suffix in new[]{"",".bak",".shop-test.json",".shop-test.json.bak",".shop-test.json.tmp"})if(File.Exists(PetStorage.SavePath+suffix))File.Delete(PetStorage.SavePath+suffix);
                PetStorage.Save(new PetSave{created=true,petName="Amigo de teste",colorIndex=7,earsIndex=1,tailIndex=2,hatIndex=2,wingsIndex=1});
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot(){if(Enabled)new GameObject("Mimo shop acceptance").AddComponent<MimoShopCheck>();}
        void Awake(){started=Time.realtimeSinceStartup;Application.runInBackground=true;Application.logMessageReceived+=Log;}
        void OnDestroy(){Application.logMessageReceived-=Log;}
        void Log(string m,string s,LogType t){if(t==LogType.Error || t==LogType.Exception || t==LogType.Assert)errors.Add(m);}
        void Check(bool ok,string text){if(ok)checks.Add(text);else errors.Add(text);}
        void Update(){peak=Math.Max(peak,Profiler.GetTotalAllocatedMemoryLong());if(!done && Time.realtimeSinceStartup-started>180){errors.Add("Timeout");Finish();}}
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(1);
            game=FindFirstObjectByType<MimoExperience>();Check(game,"Game started");if(!game){Finish();yield break;}
            if(Restart){
                var w=MimoShopStorage.Load();Check(w.crystals==650,"Balance survives a real process restart");
                Check(w.owned.Count==3,"All three entitlements survive process restart");
                Check(game.Actor.Data.cosmeticKitId=="mar" && game.Actor.HasCosmeticGeometry,"Equipped cosmetic survives process restart");
                foreach(var r in w.transactions.Where(r=>r.state==PaymentState.Purchased).ToArray()){int before=MimoShopStorage.Load().crystals;MimoShopStorage.Settle(r,out _);Check(MimoShopStorage.Load().crystals==before,"Replayed callback after restart never grants twice: "+r.token);}
                game.OpenShop();yield return new WaitForSecondsRealtime(.6f);yield return Capture("restart-store.png");
                game.CloseShop();game.RemoveKit();Check(string.IsNullOrEmpty(game.Actor.Data.cosmeticKitId),"Unequip survives after restart");
                Check(game.Actor.Data.hatIndex==2 && game.Actor.Data.wingsIndex==1 && game.Actor.Data.colorIndex==7,"Original free appearance is retained beneath the full kit");
                game.EquipKit("mar");Finish();yield break;
            }
            Check(MimoShopStorage.Load().crystals==0,"No unsolicited currency or purchase required for free play");
            Check(PetStorage.Load().hatIndex==2 && PetStorage.Load().wingsIndex==1,"Existing free recipe migrates without alteration");
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var collectionField=typeof(MimoExperience).GetField("collection",flags);
            var saveCare=typeof(MimoExperience).GetMethod("SaveCare",flags);
            object liveCollection=collectionField.GetValue(game);string validSavePath=PetStorage.SavePathOverride;
            string invalidSavePath=Path.Combine(output,"directory-not-a-save-file");Directory.CreateDirectory(invalidSavePath);
            try {
                PetStorage.SavePathOverride=invalidSavePath;
                Check(!(bool)saveCare.Invoke(game,null),"SaveCare reports an actual filesystem commit failure");
                Check(ReferenceEquals(liveCollection,collectionField.GetValue(game)),"Failed save does not replace or clear the live collection");
                var notice=(Text)typeof(MimoExperience).GetField("toastText",flags).GetValue(game);
                Check(notice.text.Contains("Não foi possível salvar"),"Failed save has visible feedback instead of silent success");
            } finally {PetStorage.SavePathOverride=validSavePath;}
            Check((bool)saveCare.Invoke(game,null),"Care save retries successfully after filesystem recovery");
            Check(!MimoShopStorage.Unlock("aurora",out _),"Insufficient balance blocks unlock");
            game.OpenShop();yield return new WaitForSecondsRealtime(.7f);
            Check(game.IsShopOpen,"Catalog opens");yield return Capture("01-catalog.png");
            foreach(var kit in MimoShopCatalog.Kits){
                Check(Resources.Load<Texture2D>("Cosmetics/"+kit.id),"Generated albedo present: "+kit.id);
                Check(Resources.Load<GameObject>("Cosmetics/"+kit.id),"Authored mesh present: "+kit.id);
                for(int i=0;i<3;i++){game.RotateKitPreview(kit.id);yield return new WaitForEndOfFrame();}
            }
            yield return Capture("02-catalog-back.png");
            foreach(var kit in MimoShopCatalog.Kits)for(int i=0;i<5;i++)game.RotateKitPreview(kit.id);
            yield return Click("Desbloquear…");yield return null;Check(game.ShopDialogName=="Área dos responsáveis","Spending requires responsible access");
            yield return Capture("03-parent-gate.png");
            yield return Answer(false);Check(game.ShopDialogName=="Área dos responsáveis","Wrong answer keeps gate closed");
            yield return Answer(true);yield return null;Check(game.ShopDialogName=="Saldo insuficiente","Insufficient balance shown clearly");yield return Capture("04-insufficient.png");
            yield return Click("Voltar");yield return null;
            yield return Click("Cristais • responsáveis");yield return Answer(true);yield return null;Check(game.ShopDialogName=="Cristais de teste","Parent area shows simulation packs");yield return Capture("05-crystal-packs.png");
            yield return Click("200 cristais\nR$ 4,90 • teste");yield return null;yield return Click("Cancelar compra");yield return null;
            Check(MimoShopStorage.Load().crystals==0,"Cancellation gives zero crystals");
            yield return Click("Cristais • responsáveis");yield return null;yield return Click("200 cristais\nR$ 4,90 • teste");yield return Click("Simular falha");yield return null;
            Check(MimoShopStorage.Load().crystals==0,"Failure gives zero crystals");
            yield return Click("Cristais • responsáveis");yield return null;yield return Click("200 cristais\nR$ 4,90 • teste");yield return null;yield return Capture("06-payment-simulation.png");yield return Click("Deixar pendente");yield return null;
            Check(MimoShopStorage.Load().crystals==0,"Pending payment gives zero crystals");
            game.CloseShop();game.OpenShop();yield return null;yield return Click("Cristais • responsáveis");yield return Answer(true);yield return null;
            yield return Click("Retomar pedido pendente");yield return null;yield return Click("Aprovar teste");yield return null;
            Check(MimoShopStorage.Load().crystals==200,"Resumed approval grants exactly 200 crystals");
            var provider=new MimoTestPayments();var receipt=MimoShopStorage.Load().transactions.Last(r=>r.state==PaymentState.Purchased);
            Check(MimoShopStorage.Settle(receipt,out _) && MimoShopStorage.Load().crystals==200,"Duplicate callback is idempotent");
            var bad=new PaymentReceipt{token=receipt.token,productId=MimoShopCatalog.Packs[2].id,state=PaymentState.Purchased};
            Check(!MimoShopStorage.Settle(bad,out _) && MimoShopStorage.Load().crystals==200,"Product mismatch cannot multiply grant");
            Check(!MimoShopStorage.Settle(new PaymentReceipt{token="TEST-unregistered",productId=receipt.productId,state=PaymentState.Purchased},out _),"Unknown token rejected");
            yield return Click("Desbloquear…");yield return null;yield return Capture("07-confirm-kit.png");yield return Click("Voltar");yield return null;
            Check(MimoShopStorage.Load().crystals==200 && !MimoShopStorage.Owns("aurora"),"Cancelling spend preserves balance and entitlement");
            yield return Click("Desbloquear…");yield return Answer(true);yield return null;yield return Click("Confirmar 150 cristais");yield return null;
            Check(MimoShopStorage.Load().crystals==50 && MimoShopStorage.Owns("aurora"),"Debit and entitlement committed together");
            Check(!MimoShopStorage.Unlock("aurora",out _) && MimoShopStorage.Load().crystals==50,"Already owned kit never debits again");
            var interrupted=provider.Begin(MimoShopCatalog.Packs[2].id);Check(MimoShopStorage.Begin(interrupted,out _),"Pending transaction persisted before callback");
            var approved=provider.Resolve(interrupted,PaymentState.Purchased);MimoShopStorage.FailNextWriteForValidation=true;
            Check(!MimoShopStorage.Settle(approved,out _) && MimoShopStorage.Load().crystals==50,"Interrupted credit preserves previous ledger");
            Check(MimoShopStorage.Settle(approved,out _) && MimoShopStorage.Load().crystals==1050,"Retry after write failure grants once");
            MimoShopStorage.FailNextWriteForValidation=true;
            Check(!MimoShopStorage.Unlock("luna",out _) && MimoShopStorage.Load().crystals==1050 && !MimoShopStorage.Owns("luna"),"Interrupted spend changes neither balance nor entitlement");
            Check(MimoShopStorage.Unlock("luna",out _) && MimoShopStorage.Unlock("mar",out _) && MimoShopStorage.Load().crystals==650,"All three full kits unlock at exact prices");
            byte[] ledger=File.ReadAllBytes(MimoShopStorage.PathName);File.WriteAllText(MimoShopStorage.PathName,"broken file");
            Check(!MimoShopStorage.Settle(approved,out _),"Corrupted primary fails closed rather than rolling back duplicate-credit ledger");File.WriteAllBytes(MimoShopStorage.PathName,ledger);
            yield return ReviewedEdges();
            foreach(var kit in MimoShopCatalog.Kits){
                game.EquipKit(kit.id);yield return new WaitForSecondsRealtime(.45f);
                Check(game.Actor.VisibleCosmetic==kit.id && game.Actor.HasCosmeticGeometry,"Real character wears complete "+kit.id);
                Check(PetStorage.Load().cosmeticKitId==kit.id,"Equipped kit saved: "+kit.id);
                yield return Capture("kit-"+kit.id+"-front.png");
                game.Actor.transform.Rotate(0,135,0);yield return new WaitForSecondsRealtime(.2f);yield return Capture("kit-"+kit.id+"-back.png");game.Actor.transform.Rotate(0,-135,0);
                game.RemoveKit();Check(string.IsNullOrEmpty(game.Actor.VisibleCosmetic) && !game.Actor.HasCosmeticGeometry,"Unequip removes premium geometry: "+kit.id);
            }
            game.OpenShop();yield return new WaitForSecondsRealtime(.5f);yield return Capture("08-owned-catalog.png");
            CheckTouches();game.CloseShop();Check(!game.IsParentAreaAuthorized,"Leaving shop revokes parent session");
            // Five distinct premium friends exercise the complete geometry/material budget.
            var collection=PetStorage.LoadCollection();
            for(int i=collection.pets.Count;i<5;i++){var p=new PetSave{created=true,petName="Teste "+i,cosmeticKitId=MimoShopCatalog.Kits[i%3].id};collection.pets.Add(p);}
            Check(PetStorage.SaveCollection(collection),"Five premium friends persisted without replacing an existing friend");
            typeof(MimoExperience).GetField("collection",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(game,PetStorage.LoadCollection());
            game.SendMessage("SynchronizePlayground",SendMessageOptions.RequireReceiver);game.EnterHome();
            var friends=FindObjectsByType<PetActor>(FindObjectsSortMode.None).Where(p=>p.gameObject.activeInHierarchy && p.Data.created).ToArray();
            Check(friends.Length==5,"Five actual friends loaded into the game roster");
            foreach(var friend in friends){game.SelectPet(friend);yield return Click("Pega-pega");}
            var originalFriend=friends.First(p=>p.Data.petName=="Amigo de teste");game.SelectPet(originalFriend);game.EquipKit("mar");
            yield return new WaitForSecondsRealtime(3);float start=Time.realtimeSinceStartup,win=start;int frames=0,wframes=0;minFps=float.MaxValue;
            while(Time.realtimeSinceStartup-start<20){yield return null;frames++;wframes++;float now=Time.realtimeSinceStartup;if(now-win>=1){minFps=Mathf.Min(minFps,wframes/(now-win));win=now;wframes=0;}}
            fps=frames/(Time.realtimeSinceStartup-start);Check(fps>=30,"Five premium friend desktop mobile-profile average meets 30 FPS");yield return Capture("09-five-premium-friends.png");
            Finish();
        }
        IEnumerator ReviewedEdges()
        {
            string walletPath=MimoShopStorage.PathName,held=walletPath+".validation-held";
            byte[] backup=File.ReadAllBytes(walletPath+".bak");
            File.Move(walletPath,held);
            try {
                bool blocked=false;try{MimoShopStorage.Load();}catch(IOException){blocked=true;}
                Check(blocked,"Missing primary with an existing backup is explicitly blocked, never shown as a fresh zero wallet");
                Check(!MimoShopStorage.Begin(new MimoTestPayments().Begin(MimoShopCatalog.Packs[0].id),out _) && !File.Exists(walletPath),"Missing primary cannot silently create a replacement ledger");
                Check(backup.SequenceEqual(File.ReadAllBytes(walletPath+".bak")),"Missing-primary handling preserves the backup exactly");
            }finally{File.Move(held,walletPath);}
            Check(MimoShopStorage.Load().crystals==650 && MimoShopStorage.Load().owned.Count==3,"Restored primary retains the exact balance and all entitlements");

            PetActor originalFriend=game.Actor;string originalId=originalFriend.Data.petId;
            game.EnterLaboratory();yield return null;
            Check(game.Actor.Data==null,"First laboratory shop is exercised before the creation actor is initialized");
            game.OpenShop();yield return new WaitForSecondsRealtime(.3f);yield return Click("Cristais • responsáveis");yield return Answer(true);
            Check(game.ShopDialogName=="Cristais de teste","Parent packs open safely in the first laboratory with null actor data");
            game.CloseShop();byte[] savedBefore=File.ReadAllBytes(PetStorage.SavePath);
            game.EquipKit("aurora");Check(game.CurrentScreen=="Laboratory" && game.DraftRecipe.cosmeticKitId=="aurora","First laboratory equips only the uncreated draft");
            game.RemoveKit();Check(game.CurrentScreen=="Laboratory" && game.DraftRecipe.cosmeticKitId=="" && savedBefore.SequenceEqual(File.ReadAllBytes(PetStorage.SavePath)),"First laboratory unequip leaves the saved friend and creation screen intact");
            game.EquipKit("luna");yield return MimoRitualCheck.AddAndStir(game,0,1);game.BeginBirth();
            float until=Time.realtimeSinceStartup+8;while(game.CurrentScreen=="Birth" && Time.realtimeSinceStartup<until)yield return null;
            Check(game.CurrentScreen=="Naming","A draft wearing a full collection completes the actual birth reveal");
            game.ConfirmName();yield return null;PetActor born=game.Actor;
            Check(born.Data.created && born.Data.cosmeticKitId=="luna" && born.Data.petId!=originalId,"New collection-wearing friend is committed with a distinct identity");
            game.EnterLaboratory();yield return null;
            Check(game.Actor.Data!=null && game.Actor.Data.created,"Second recipe exercises the retained created actor that triggered the review bug");
            string previousKit=game.Actor.Data.cosmeticKitId;string previousId=game.Actor.Data.petId;
            savedBefore=File.ReadAllBytes(PetStorage.SavePath);
            game.EquipKit("mar");game.OpenShop();yield return new WaitForSecondsRealtime(.3f);
            var previews=(Dictionary<string,PetActor>)typeof(MimoExperience).GetField("shopPreviewActors",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(game);
            Check(previews["mar"].Data.colorIndex==game.DraftRecipe.colorIndex,"Laboratory previews use the new recipe rather than the previous friend");
            yield return Click("Retirar coleção");
            Check(game.CurrentScreen=="Laboratory" && game.DraftRecipe.cosmeticKitId=="","Second-recipe card unequip remains in the laboratory and edits only the draft");
            Check(game.Actor.Data.cosmeticKitId==previousKit && game.Actor.Data.petId==previousId && savedBefore.SequenceEqual(File.ReadAllBytes(PetStorage.SavePath)),"Second-recipe unequip changes neither the previous actor nor any saved friend");
            game.EnterHome();game.SelectPet(originalFriend);game.EquipKit("mar");
            string goodPath=PetStorage.SavePathOverride,badPath=Path.Combine(output,"unequip-directory-not-a-file");Directory.CreateDirectory(badPath);
            File.Copy(MimoShopStorage.PathName,badPath+".shop-test.json",true);
            try {
                PetStorage.SavePathOverride=badPath;game.RemoveKit();
                Check(game.Actor.Data.cosmeticKitId=="mar" && game.Actor.VisibleCosmetic=="mar","A real filesystem failure during unequip preserves the equipped appearance");
                var notice=(Text)typeof(MimoExperience).GetField("toastText",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(game);
                Check(notice.text.Contains("Não conseguimos salvar"),"Unequip save failure gives visible feedback");
            }finally{PetStorage.SavePathOverride=goodPath;}
            Check(PetStorage.Load().cosmeticKitId=="mar","Failed unequip does not change the persisted collection choice");
            game.RemoveKit();Check(string.IsNullOrEmpty(game.Actor.Data.cosmeticKitId),"Unequip succeeds after filesystem recovery");

            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var bodyField=typeof(PetActor).GetField("_body",flags);var pearlField=typeof(PetActor).GetField("_pearl",flags);
            string[] properties={"_Grain","_DetailScale","_RimStrength","_Subsurface","_FinishKind","_Smoothness","_Iridescence","_Transmission","_Opacity","_Thickness","_RefractionStrength","_SceneRefraction","_MicroNormalStrength","_SpecularStrength","_EnvironmentStrength","_CoatStrength","_ZWrite","_SrcBlend","_DstBlend","_KitEnabled"};
            PetSave free=game.Actor.Data.Copy();game.OpenShop();yield return new WaitForSecondsRealtime(.3f);
            previews=(Dictionary<string,PetActor>)typeof(MimoExperience).GetField("shopPreviewActors",flags).GetValue(game);
            foreach(var kit in MimoShopCatalog.Kits)for(int finish=0;finish<PetCatalog.FinishNames.Length;finish++) {
                var d=free.Copy();d.finishIndex=finish;d.cosmeticKitId=kit.id;game.Actor.Initialize(d);
                var real=((Renderer)bodyField.GetValue(game.Actor)).sharedMaterial;var preview=((Renderer)bodyField.GetValue(previews[kit.id])).sharedMaterial;
                var realPearl=(Material)pearlField.GetValue(game.Actor);var previewPearl=(Material)pearlField.GetValue(previews[kit.id]);
                bool same=properties.All(p=>Mathf.Approximately(real.GetFloat(p),preview.GetFloat(p)) && Mathf.Approximately(realPearl.GetFloat(p),previewPearl.GetFloat(p)));
                same &= real.GetColor("_RimColor")==preview.GetColor("_RimColor") && realPearl.GetColor("_BaseColor")==previewPearl.GetColor("_BaseColor") && real.renderQueue==preview.renderQueue;
                Check(same,"Complete preview and equipped finish match for "+kit.id+" over free finish "+finish);
                game.Actor.SetFinish(finish);Check(game.Actor.Data.finishIndex==finish,"Premium appearance preserves the underlying free finish "+finish);
            }
            game.CloseShop();game.Actor.Initialize(free);game.RemoveKit();
            Check(game.Actor.FinishIndex==free.finishIndex && game.Actor.Data.colorIndex==free.colorIndex,"Removing the collection restores the original free recipe after every finish variation");
        }
        Button FindButton(string text)=>FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b=>b.interactable && b.gameObject.activeInHierarchy && b.GetComponentsInChildren<Text>().Any(t=>t.text==text) && (text!="Desbloquear…" || b.transform.parent.name=="Jardim de Aurora"));
        IEnumerator Click(string text)
        {
            yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases();var b=FindButton(text);Check(b,"Visible action: "+text);if(!b)yield break;
            var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,((RectTransform)b.transform).TransformPoint(((RectTransform)b.transform).rect.center))};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Any(h=>h.gameObject==b.gameObject || h.gameObject.transform.IsChildOf(b.transform)),"Action is reached through actual UI raycast: "+text);
            ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);
        }
        IEnumerator Answer(bool correct)
        {
            if(game.ShopDialogName!="Área dos responsáveis")yield break;
            yield return null;var f=FindObjectsByType<InputField>(FindObjectsSortMode.None).FirstOrDefault(t=>t.gameObject.activeInHierarchy);Check(f,"Parent input visible");if(!f)yield break;
            f.text=(correct ? game.ParentChallengeAnswer : -1).ToString();yield return Click("Continuar");
        }
        void CheckTouches()
        {
            foreach(var b in FindObjectsByType<Button>(FindObjectsSortMode.None).Where(x=>x.gameObject.activeInHierarchy && x.GetComponentsInParent<Transform>().Any(t=>t.name=="Coleções - teste sem cobrança"))) {
                var r=(RectTransform)b.transform;Vector3[] corners=new Vector3[4];r.GetWorldCorners(corners);
                Check(corners[0].x>=-.5f && corners[2].x<=Screen.width+.5f && corners[0].y>=-.5f && corners[2].y<=Screen.height+.5f,"Touch target fully visible: "+b.name);
            }
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();var t=ScreenCapture.CaptureScreenshotAsTexture();if(t){File.WriteAllBytes(Path.Combine(output,name),t.EncodeToPNG());Destroy(t);screenshots.Add(name);}else errors.Add("Screenshot unavailable: "+name);
            if(name=="01-catalog.png") {
                var cameras=(Dictionary<string,Camera>)typeof(MimoExperience).GetField("shopPreviewCameras",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(game);
                var previous=RenderTexture.active;
                foreach(var entry in cameras){var target=entry.Value.targetTexture;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();string file="preview-"+entry.Key+".png";File.WriteAllBytes(Path.Combine(output,file),image.EncodeToPNG());Destroy(image);screenshots.Add(file);}
                RenderTexture.active=previous;
            }
        }
        void Finish()
        {
            if(done)return;done=true;
            bool untouched=existed ? File.Exists(ordinary) && original.SequenceEqual(File.ReadAllBytes(ordinary)) : !File.Exists(ordinary);Check(untouched,"Ordinary child save unchanged");
            var r=new Report{version=Application.version,mode=Restart ? "real-process-restart":"purchase-ui-cosmetic-performance",passed=errors.Count==0,ordinarySaveUnchanged=untouched,width=Screen.width,height=Screen.height,checksPassed=checks.Count,peakAllocatedBytes=peak,fiveFriendsAverageFps=fps,minOneSecondFps=minFps,checks=checks.ToArray(),errors=errors.ToArray(),screenshots=screenshots.ToArray()};
            File.WriteAllText(Path.Combine(output,Restart ? "shop-restart-checks.json":"shop-checks.json"),JsonUtility.ToJson(r,true));Debug.Log("MIMO_SHOP_CHECK "+(r.passed ? "PASS":"FAIL")+" checks="+checks.Count+" errors="+errors.Count);Application.Quit(r.passed ? 0:1);
        }
    }
}
