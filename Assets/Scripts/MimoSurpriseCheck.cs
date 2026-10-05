using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MimoGarden
{
    public sealed class MimoSurpriseCheck : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed, ordinarySaveUnchanged;
            public string version, platform;
            public int width, height, randomSamples;
            public string[] checks, errors;
        }
        static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "--mimo-surprise-test") >= 0;
        static string output, ordinary;
        static byte[] before;
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        MimoExperience app;
        float started;
        bool finished;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Isolate()
        {
            if (!Enabled) return;
            string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--mimo-test-output");
            output = at >= 0 && at+1 < args.Length ? Path.GetFullPath(args[at+1]) : Path.Combine(Application.temporaryCachePath,"MimoSurpriseCheck");
            Directory.CreateDirectory(output);
            ordinary = Path.Combine(Application.persistentDataPath,"mimo-pet.json"); before = File.Exists(ordinary) ? File.ReadAllBytes(ordinary) : null;
            PetStorage.SavePathOverride = Path.Combine(output,"isolated-surprise-"+DateTime.UtcNow.Ticks+".json");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (Enabled) new GameObject("Surprise creation acceptance").AddComponent<MimoSurpriseCheck>(); }
        void Awake() { started=Time.realtimeSinceStartup;Application.runInBackground=true;Application.logMessageReceived+=Log; }
        void OnDestroy() { Application.logMessageReceived-=Log; }
        void Log(string message,string stack,LogType type) { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) errors.Add(message); }
        void Update() { if(!finished && Time.realtimeSinceStartup-started>150) {errors.Add("Surprise test timed out");Finish();} }
        void Check(bool ok,string description) {if(ok)checks.Add(description);else errors.Add(description);}
        static string Appearance(PetSave pet) => string.Join(",",Enumerable.Range(0,8).Select(c=>PetCatalog.Get(pet,c)));
        Button Find(string name) => FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b=>b.gameObject.activeInHierarchy&&b.GetComponentsInChildren<Text>().Any(t=>t.text==name));
        void Click(string name) {Button b=Find(name);Check(b&&b.interactable,"Visible usable action: "+name);if(b)b.onClick.Invoke();}
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(.9f);
            CheckGenerator();
            app=FindAnyObjectByType<MimoExperience>();Check(app,"Experience starts");if(!app){Finish();yield break;}
            Check(!app.IsSurpriseCreation,"First-time player retains the original Ateliê");
            Click("Surpresa");yield return null;
            Check(app.IsSurpriseCreation&&app.CurrentScreen=="Laboratory","Surprise button opens the separate kitchen recipe");
            Check(!app.CanFinalizeRecipe&&!app.CreationStation.LidVisible&&!app.Actor.gameObject.activeInHierarchy,"Empty surprise cannot finish and shows no lid or character");
            Check(!app.BeginIngredientDrag(6,1,app.CreationDropScreenPosition),"Surprise rejects wings and direct appearance ingredients");
            app.BeginBirth();Check(app.CurrentScreen=="Laboratory","Empty reveal is rejected");
            yield return Capture("01-surprise-empty.png");
            Vector2 source=new Vector2(Screen.width-100,Screen.height*.4f);
            app.BeginIngredientDrag(8,0,source);app.EndIngredientDrag(new Vector2(Screen.width-10,10));
            Check(app.SurpriseAmounts.Sum()==0&&app.AddedIngredientCount==0,"Dropping outside consumes no portion");
            app.BeginIngredientDrag(8,1,source);app.CancelIngredientDrag();
            Check(app.SurpriseAmounts.Sum()==0,"Canceling a drag consumes no portion");
            yield return DragActualCard(0);
            Check(app.SurpriseAmounts[0]==1&&app.PendingStir&&!app.CanFinalizeRecipe,"Real flour card adds exactly one portion and requires mixing");
            app.BeginBirth();Check(app.CurrentScreen=="Laboratory","Unmixed surprise cannot reveal");
            yield return MimoRitualCheck.Stir(app);
            Check(app.CanFinalizeRecipe,"One ingredient is enough after mixing; no mandatory recipe");
            yield return MimoRitualCheck.AddIngredient(app,8,1);
            yield return MimoRitualCheck.AddIngredient(app,8,2);
            Check(app.SurpriseAmounts.SequenceEqual(new[]{1,1,1})&&app.PendingStir,"One of each ingredient is counted independently");
            Check(!app.CreationStation.LidVisible&&!app.Actor.gameObject.activeInHierarchy,"Mixed ingredients still hide the result and lid");
            yield return MimoRitualCheck.Stir(app);yield return Capture("02-one-of-each.png");
            Click("Revelar surpresa!");
            string result=Appearance(app.DraftRecipe); app.BeginBirth();
            Check(result==Appearance(app.DraftRecipe),"Repeated finish input does not reroll the result");
            Check(app.DraftRecipe.wingsIndex==0&&string.IsNullOrEmpty(app.DraftRecipe.cosmeticKitId),"Finalized result has no wings or paid kit");
            yield return new WaitForSecondsRealtime(.65f);yield return Capture("03-surprise-steaming.png");
            yield return new WaitForSecondsRealtime(2.9f);
            Check(app.CurrentScreen=="Naming"&&app.Actor.gameObject.activeInHierarchy,"Surprise reaches the normal naming screen");
            Check(Appearance(app.Actor.Data)==result,"Revealed actor matches the single generated result");
            yield return Capture("04-surprise-born.png");
            app.ConfirmName();yield return null;
            PetSave stored=PetStorage.Load();
            Check(Appearance(stored)==result&&stored.wingsIndex==0,"Naming saves the exact random result without wings");
            Check(app.PlaygroundPets.Count==1,"Surprise joins the existing shared playground");
            foreach(string room in new[]{"Play","Food","Bath"})
            {
                app.VisitCareRoom(room);yield return null;
                foreach(string label in new[]{"Ateliê","Surpresa"})
                {
                    Button b=Find(label);Check(b&&!b.GetComponentInParent<ScrollRect>(),room+" has a fixed "+label+" entry");
                    if(b){var r=(RectTransform)b.transform;Vector3[] corners=new Vector3[4];r.GetWorldCorners(corners);Check(corners[2].x-corners[0].x>=44&&corners[2].y-corners[0].y>=44,room+" touch target fits: "+label);}
                }
            }
            app.VisitCareRoom("Play");yield return Capture("05-two-creation-entries.png");
            Click("Surpresa");yield return null;
            Check(app.SurpriseAmounts.Sum()==0&&!app.CanFinalizeRecipe,"A new surprise starts with an empty recipe");
            for(int i=0;i<MimoSurpriseRecipe.MaxPortions;i++) yield return MimoRitualCheck.AddIngredient(app,8,i%3);
            Check(app.AddedIngredientCount==12&&app.SurpriseAmounts.SequenceEqual(new[]{4,4,4}),"Repeated portions work up to the visible limit");
            Check(!app.BeginIngredientDrag(8,0,source)&&app.SurpriseAmounts.Sum()==12,"Full basket rejects another portion without changing counters");
            yield return MimoRitualCheck.Stir(app);
            Check(app.CanFinalizeRecipe,"A full basket can still mix and finalize");
            yield return Capture("06-full-surprise.png");
            Click("Ateliê");yield return null;
            Check(!app.IsSurpriseCreation&&app.AddedIngredientCount==0&&!app.CanFinalizeRecipe,"Switching modes starts a fresh ordinary recipe");
            yield return MimoRitualCheck.AddAndStir(app,0,3);
            yield return MimoRitualCheck.AddAndStir(app,6,2);
            Check(app.DraftRecipe.colorIndex==3&&app.DraftRecipe.wingsIndex==2,"Ateliê still honors exact choices including its existing wings");
            app.BeginBirth();yield return new WaitForSecondsRealtime(3.5f);app.ConfirmName();yield return null;
            Check(app.PlaygroundPets.Count==2&&PetStorage.Load().wingsIndex==2,"Both modes save into the same playground without changing prior friends");
            Check(app.PlaygroundPets.Any(p=>p.Data.petId==stored.petId&&Appearance(p.Data)==result),"The earlier surprise keeps its original appearance");
            Finish();
        }
        IEnumerator DragActualCard(int index)
        {
            Canvas.ForceUpdateCanvases();
            var handler=FindObjectsByType<MimoIngredientDrag>(FindObjectsSortMode.None).FirstOrDefault(x=>x.gameObject.name=="Kitchen ingredient "+index);
            Check(handler,"The kitchen card has a touch drag handler");if(!handler)yield break;
            var rect=(RectTransform)handler.transform;Vector2 start=rect.TransformPoint(rect.rect.center);
            var e=new PointerEventData(EventSystem.current){pointerId=91,pressPosition=start,position=start+Vector2.left*25};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=start},hits);
            Check(hits.Count>0&&ExecuteEvents.GetEventHandler<IBeginDragHandler>(hits[0].gameObject)==handler.gameObject,"The visible card receives the actual UI raycast");
            handler.OnInitializePotentialDrag(e);handler.OnBeginDrag(e);e.position=app.CreationDropScreenPosition;handler.OnDrag(e);handler.OnEndDrag(e);
            float end=Time.realtimeSinceStartup+4;while(app.CreationStation.IsPouring&&Time.realtimeSinceStartup<end)yield return null;
        }
        void CheckGenerator()
        {
            var random=new System.Random(240105);var unique=new HashSet<string>();var seen=Enumerable.Range(0,8).Select(c=>new HashSet<int>()).ToArray();
            bool valid=true;
            for(int i=0;i<12000;i++)
            {
                PetSave pet=MimoSurpriseRecipe.Roll(new[]{1,1,1},random);unique.Add(Appearance(pet));
                for(int c=0;c<8;c++){int v=PetCatalog.Get(pet,c);valid&=v>=0&&v<PetCatalog.Options(c).Length;seen[c].Add(v);}
                valid&=pet.wingsIndex==0&&string.IsNullOrEmpty(pet.cosmeticKitId);
            }
            Check(valid,"12000 equal-quantity results stay valid, free of wings and paid kits");
            Check(unique.Count>10000,"Repeating one flour, one egg, one milk keeps broad random variety");
            for(int c=0;c<8;c++)Check(seen[c].Count==(c==6?1:PetCatalog.Options(c).Length),"Random generator reaches every allowed option in category "+c);
            int neutralSoft=0,flourSoft=0,neutralGel=0,milkGel=0,neutralWarm=0,eggWarm=0;
            for(int i=0;i<4000;i++)
            {
                var n=MimoSurpriseRecipe.Roll(new[]{1,1,1},random);var f=MimoSurpriseRecipe.Roll(new[]{8,1,1},random);
                var m=MimoSurpriseRecipe.Roll(new[]{1,1,8},random);var e=MimoSurpriseRecipe.Roll(new[]{1,8,1},random);
                if(n.finishIndex<=1)neutralSoft++;if(f.finishIndex<=1)flourSoft++;
                if(n.finishIndex>=1&&n.finishIndex<=3)neutralGel++;if(m.finishIndex>=1&&m.finishIndex<=3)milkGel++;
                if(n.colorIndex==0||n.colorIndex==5||n.colorIndex==6)neutralWarm++;if(e.colorIndex==0||e.colorIndex==5||e.colorIndex==6)eggWarm++;
            }
            Check(flourSoft>neutralSoft+700,"Extra flour noticeably favors soft or pearl finishes");
            Check(milkGel>neutralGel+700,"Extra milk noticeably favors pearl and gel finishes");
            Check(eggWarm>neutralWarm+700,"Extra eggs noticeably favor warm colors");
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();Texture2D shot=MimoRenderCapture.Frame(Camera.main);
            File.WriteAllBytes(Path.Combine(output,name),shot.EncodeToPNG());Destroy(shot);
        }
        void Finish()
        {
            if(finished)return;finished=true;byte[] after=File.Exists(ordinary)?File.ReadAllBytes(ordinary):null;
            bool unchanged=before==null?after==null:after!=null&&before.SequenceEqual(after);Check(unchanged,"Normal player save remains byte-for-byte unchanged");
            var report=new Report{passed=errors.Count==0,ordinarySaveUnchanged=unchanged,version=Application.version,platform=Application.platform.ToString(),width=Screen.width,height=Screen.height,randomSamples=28000,checks=checks.ToArray(),errors=errors.ToArray()};
            File.WriteAllText(Path.Combine(output,"surprise-checks.json"),JsonUtility.ToJson(report,true));Debug.Log("MIMO_SURPRISE "+(report.passed?"PASS":"FAIL")+" checks="+checks.Count+" errors="+errors.Count);Application.Quit(report.passed?0:1);
        }
    }
}
