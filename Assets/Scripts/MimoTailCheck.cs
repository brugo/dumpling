using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MimoGarden {
    public sealed class MimoTailCheck : MonoBehaviour {
        [Serializable] sealed class Shape { public string kit,tail;public Vector3 root,min,max;public Vector3 rotation; }
        [Serializable] sealed class Report { public string version,mode;public bool passed,ordinarySaveUnchanged;public string[] checks,errors;public Shape[] shapes; }
        static bool Baseline=>Environment.GetCommandLineArgs().Contains("--mimo-tail-baseline");
        static bool Enabled=>Baseline || Environment.GetCommandLineArgs().Contains("--mimo-tail-test");
        static string output,ordinary;static byte[] original;static bool existed;
        readonly List<string> checks=new List<string>(),errors=new List<string>();readonly List<Shape> shapes=new List<Shape>();
        readonly BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        bool done;float began;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Isolate(){
            var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"--mimo-test-output");
            if(a.Contains("--mimo-tail-preview") && i>=0 && i+1<a.Length){output=Path.GetFullPath(a[i+1]);Directory.CreateDirectory(output);PetStorage.SavePathOverride=Path.Combine(output,"interactive-tail-save.json");return;}
            if(!Enabled)return;
            output=Path.GetFullPath(a[i+1]);Directory.CreateDirectory(output);ordinary=Path.Combine(Application.persistentDataPath,"mimo-pet.json");existed=File.Exists(ordinary);original=existed?File.ReadAllBytes(ordinary):null;
            PetStorage.SavePathOverride=Path.Combine(output,"isolated-tail-save.json");PetStorage.Save(new PetSave{created=true,petName="Caudas de teste",tailIndex=1});
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot(){if(Enabled)new GameObject("Tail validation").AddComponent<MimoTailCheck>();}
        void Awake(){began=Time.realtimeSinceStartup;Application.runInBackground=true;Application.logMessageReceived+=Log;}
        void OnDestroy(){Application.logMessageReceived-=Log;}
        void Log(string m,string s,LogType t){if(t==LogType.Error || t==LogType.Exception || t==LogType.Assert)errors.Add(m);}
        void Check(bool ok,string label){if(ok)checks.Add(label);else errors.Add(label);}
        void Update(){if(!done && Time.realtimeSinceStartup-began>300){errors.Add("Tail validation timed out");Finish();}}
        IEnumerator Start(){
            yield return new WaitForSecondsRealtime(1);var game=FindFirstObjectByType<MimoExperience>();Check(game,"Game starts with isolated save");
            var root=new GameObject("Actual tail character inspection");root.transform.position=new Vector3(80,0,0);var pet=root.AddComponent<PetActor>();pet.AllowCosmeticPreview=true;
            var cameraObject=new GameObject("Actual three dimensional capture");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<29;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.88f,.91f,.95f);camera.orthographic=true;camera.orthographicSize=2.35f;camera.nearClipPlane=.1f;camera.farClipPlane=20;cameraObject.AddComponent<UniversalAdditionalCameraData>();
            var target=new RenderTexture(640,640,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
            pet.Initialize(new PetSave{tailIndex=0});Check(!pet.ActiveTailObject,"The no-tail choice remains genuinely tail-free");
            foreach(string kit in new[]{"","aurora","luna","mar"})for(int tail=1;tail<PetCatalog.TailNames.Length;tail++){
                pet.Initialize(new PetSave{created=false,tailIndex=tail,earsIndex=tail%7,hatIndex=kit==""?tail%6:0,wingsIndex=kit==""?tail%6:0,cosmeticKitId=kit,cleanliness=1});
                foreach(Transform t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=29;
                var motion=(Transform)typeof(PetActor).GetField("_motion",flags).GetValue(pet);
                Vector3 min=Vector3.one*float.PositiveInfinity,max=Vector3.one*float.NegativeInfinity;
                foreach(var mf in pet.ActiveTailObject.GetComponentsInChildren<MeshFilter>(true))foreach(var v in mf.sharedMesh.vertices){var p=motion.InverseTransformPoint(mf.transform.TransformPoint(v));min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
                shapes.Add(new Shape{kit=kit,tail=PetCatalog.TailNames[tail],root=motion.InverseTransformPoint(pet.ActiveTailObject.transform.position),min=min,max=max,rotation=pet.ActiveTailObject.transform.eulerAngles});
                if(!Baseline){
                    var attachment=motion.InverseTransformPoint(pet.ActiveTailObject.transform.position);var body=(Renderer)typeof(PetActor).GetField("_body",flags).GetValue(pet);
                    var collider=(Collider)typeof(PetActor).GetField("_bodyCollider",flags).GetValue(pet);
                    var ray=new Ray(motion.TransformPoint(new Vector3(0,attachment.y,8)),motion.TransformDirection(Vector3.back));
                    bool hit=collider.Raycast(ray,out RaycastHit h,20);Vector3 surface=hit?motion.InverseTransformPoint(h.point):Vector3.zero;
                    Check(hit && Mathf.Abs(attachment.x-surface.x)<.015f && attachment.z>surface.z-.2f && attachment.z<=surface.z+.01f,"Rear-surface root (independent collider ray): "+kit+" tail "+tail);
                    Check(max.z>attachment.z+.15f && min.y>-.02f,"Tail extends behind the rump and stays above the floor: "+kit+" tail "+tail);
                    foreach(float scale in new[]{.65f,1.4f}){root.transform.localScale=Vector3.one*scale;Physics.SyncTransforms();Check(RearAttached(pet),"Rear anatomy follows body scale "+scale+": "+kit+" tail "+tail);}root.transform.localScale=Vector3.one;
                }
                foreach(int angle in new[]{0,90,180}){
                    root.transform.rotation=Quaternion.Euler(0,angle,0);yield return null;
                    if(!Baseline){Physics.SyncTransforms();Check(RearAttached(pet),"Independent rear collider test follows body rotation "+angle+": "+kit+" tail "+tail);}
                    Bounds frame=pet.VisibleBounds;camera.orthographicSize=Mathf.Max(frame.size.y,frame.size.x)*.61f;
                    var center=frame.center;camera.transform.position=center+new Vector3(0,.10f,-8);camera.transform.LookAt(center);
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                    var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(640,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,640,640),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,(kit==""?"free":kit)+"-tail-"+tail+"-"+angle+".png"),image.EncodeToPNG());Destroy(image);RenderTexture.active=previous;
                }
                root.transform.rotation=Quaternion.identity;
            }
            Destroy(root);Destroy(cameraObject);target.Release();Destroy(target);
            if(!Baseline)yield return Integration(game);
            Finish();
        }
        bool RearAttached(PetActor pet){var motion=(Transform)typeof(PetActor).GetField("_motion",flags).GetValue(pet);var attachment=motion.InverseTransformPoint(pet.ActiveTailObject.transform.position);var collider=(Collider)typeof(PetActor).GetField("_bodyCollider",flags).GetValue(pet);var ray=new Ray(motion.TransformPoint(new Vector3(0,attachment.y,8)),motion.TransformDirection(Vector3.back));if(!collider.Raycast(ray,out RaycastHit h,20))return false;var surface=motion.InverseTransformPoint(h.point);return Mathf.Abs(attachment.x-surface.x)<.015f && attachment.z>surface.z-.2f && attachment.z<=surface.z+.01f;}
        IEnumerator Integration(MimoExperience game){
            var provider=new MimoTestPayments();var pending=provider.Begin(MimoShopCatalog.Packs[2].id);Check(MimoShopStorage.Begin(pending,out _) && MimoShopStorage.Settle(provider.Resolve(pending,PaymentState.Purchased),out _),"Simulated isolated wallet grants once for premium-tail checks");
            foreach(var k in MimoShopCatalog.Kits)Check(MimoShopStorage.Unlock(k.id,out _),"Existing collection unlock remains functional: "+k.id);
            int walletBefore=MimoShopStorage.Load().crystals;var originalFriend=game.Actor;
            game.EnterLaboratory();yield return null;yield return MimoRitualCheck.AddAndStir(game,0,1);
            for(int tail=1;tail<PetCatalog.TailNames.Length;tail++){
                yield return MimoRitualCheck.AddAndStir(game,2,tail);Check(game.DraftRecipe.tailIndex==tail,"Creation ingredients preserve tail index "+tail);
                game.OpenShop();yield return null;var previews=(Dictionary<string,PetActor>)typeof(MimoExperience).GetField("shopPreviewActors",flags).GetValue(game);
                Check(previews.Values.All(p=>p.Data.tailIndex==tail && Mathf.Abs(p.ActiveTailObject.transform.localPosition.x)<.015f),"All real shop previews retain the rear tail from draft "+tail);
                for(int turn=0;turn<3;turn++) {
                    if(turn>0)foreach(var kit in MimoShopCatalog.Kits){game.RotateKitPreview(kit.id);game.RotateKitPreview(kit.id);}
                    yield return new WaitForEndOfFrame();
                    var cameras=(Dictionary<string,Camera>)typeof(MimoExperience).GetField("shopPreviewCameras",flags).GetValue(game);
                    foreach(var k in MimoShopCatalog.Kits){var c=cameras[k.id];Bounds b=previews[k.id].VisibleBounds;bool inside=true;
                        for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){var v=c.WorldToViewportPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));inside &= v.x>=0 && v.x<=1 && v.y>=0 && v.y<=1;}
                        Check(inside,"Rotating actual shop preview contains the complete tail: "+k.id+" tail "+tail+" angle "+turn*90);
                    }
                    yield return Capture("creation-shop-tail-"+tail+"-"+turn*90+".png");
                }
                game.CloseShop();
            }
            game.EquipKit("aurora");game.BeginBirth();float until=Time.realtimeSinceStartup+8;while(game.CurrentScreen=="Birth" && Time.realtimeSinceStartup<until)yield return null;
            Check(game.CurrentScreen=="Naming" && game.Actor.Data.tailIndex==7 && game.Actor.VisibleCosmetic=="aurora","Actual birth reveals the selected tail together with the premium collection");
            yield return Capture("actual-creation-reveal.png");
            game.ConfirmName();yield return null;Check(game.Actor.Data.created && game.Actor.Data.tailIndex==7,"Naming commits the selected tail without changing its stored ID");
            foreach(int tail in Enumerable.Range(1,7)){
                game.Actor.ApplyRecipe(0,0,tail);game.EquipKit(MimoShopCatalog.Kits[(tail-1)%3].id);yield return null;
                Check(game.Actor.ActiveTailObject && game.Actor.ActiveTailObject.activeInHierarchy && Mathf.Abs(game.Actor.ActiveTailObject.transform.localPosition.x)<.015f,"Equipped tail stays rear-centered in the actual park: "+tail);
                Check(PetStorage.Load().tailIndex==tail,"Equipped park tail persists under unchanged tail ID: "+tail);
                foreach(int angle in new[]{0,90,180}){game.Actor.transform.rotation=Quaternion.Euler(0,angle,0);yield return null;yield return Capture("park-equipped-tail-"+tail+"-"+angle+".png");}
                game.Actor.transform.rotation=Quaternion.identity;
            }
            game.RemoveKit();Check(game.Actor.Data.tailIndex==7 && game.Actor.ActiveTailObject.activeInHierarchy,"Removing premium kit preserves selected free tail");
            Check(MimoShopStorage.Load().crystals==walletBefore && MimoShopStorage.Load().owned.Count==3,"Tail creation/equip/remove never debit the shop economy");
            Check(originalFriend.Data.tailIndex==1,"New tail creation does not change the previous friend");
        }
        IEnumerator Capture(string name){yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());Destroy(image);}
        void Finish(){if(done)return;done=true;bool untouched=existed?File.Exists(ordinary)&&original.SequenceEqual(File.ReadAllBytes(ordinary)):!File.Exists(ordinary);Check(untouched,"Ordinary child save unchanged");var r=new Report{version=Application.version,mode=Baseline?"baseline-captures":"rear-tail-regression",passed=errors.Count==0,ordinarySaveUnchanged=untouched,checks=checks.ToArray(),errors=errors.ToArray(),shapes=shapes.ToArray()};File.WriteAllText(Path.Combine(output,"tail-checks.json"),JsonUtility.ToJson(r,true));Debug.Log("MIMO_TAIL_CHECK "+(r.passed?"PASS":"FAIL")+" checks="+checks.Count+" errors="+errors.Count);Application.Quit(r.passed?0:1);}
    }
}
