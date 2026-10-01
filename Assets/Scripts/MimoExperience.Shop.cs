using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

namespace MimoGarden
{
    public sealed partial class MimoExperience
    {
        RectTransform shopRoot,shopDialog;
        readonly List<GameObject> shopPreviewObjects=new List<GameObject>();
        readonly List<RenderTexture> shopPreviewTextures=new List<RenderTexture>();
        readonly Dictionary<string,PetActor> shopPreviewActors=new Dictionary<string,PetActor>();
        readonly Dictionary<string,Camera> shopPreviewCameras=new Dictionary<string,Camera>();
        readonly IMimoPayments payments=new MimoTestPayments();
        float parentAccessUntil;
        PaymentReceipt pendingPayment;
        public bool IsShopOpen => shopRoot;
        public bool IsParentAreaAuthorized => Time.unscaledTime<parentAccessUntil;
        public string ShopDialogName => shopDialog ? shopDialog.gameObject.name : "";
        public int ParentChallengeAnswer {get;private set;}

        void BuildShopEntry()
        {
            var b=Button("Coleções",header,cream,ink,OpenShop,16);
            SetRect((RectTransform)b.transform,Vector2.up,Vector2.up,Vector2.up,new Vector2(410,4),new Vector2(150,48));
        }
        public void OpenShop()
        {
            if(IsFriendDialogOpen || place==Place.Birth || place==Place.Naming)return;
            ReleaseTouch();ResetBathTools();CancelSnack();
            CloseShop(false);
            shopRoot=Image("Coleções - teste sem cobrança",safeArea,Hex("FBF8F3")).rectTransform;Stretch(shopRoot);
            shopRoot.SetAsLastSibling(); toast.SetAsLastSibling(); toast.gameObject.SetActive(false);
            var heading=Label("Pequenos tesouros",shopRoot,32,ink,FontStyle.Bold);
            SetRect(heading.rectTransform,Vector2.up,Vector2.up,Vector2.up,new Vector2(28,-18),new Vector2(460,50));
            var note=Label("COLEÇÕES COMPLETAS  •  TESTE SEM COBRANÇA REAL",shopRoot,14,purple,FontStyle.Bold);
            SetRect(note.rectTransform,Vector2.up,Vector2.up,Vector2.up,new Vector2(30,-70),new Vector2(680,32));
            var close=Button("Voltar a brincar",shopRoot,palePurple,ink,()=>CloseShop(),18);
            SetRect((RectTransform)close.transform,Vector2.one,Vector2.one,Vector2.one,new Vector2(-28,-26),new Vector2(215,58));
            string balance;
            try{balance=MimoShopStorage.Load().crystals+" cristais de teste";}catch{balance="Saldo indisponível";}
            var wallet=Label(balance,shopRoot,20,ink,FontStyle.Bold);
            SetRect(wallet.rectTransform,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(92,68),new Vector2(400,36));
            var gem=Rect("Cristal criado no ChatGPT",shopRoot).gameObject.AddComponent<Image>();gem.sprite=Resources.Load<Sprite>("Cosmetics/crystal");gem.preserveAspect=true;gem.raycastTarget=false;
            SetRect(gem.rectTransform,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(32,54),new Vector2(50,58));
            var free=Label("Criar, cuidar e brincar continuam gratuitos. As opções anteriores estão disponíveis.",shopRoot,15,muted);
            SetRect(free.rectTransform,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(32,22),new Vector2(760,38));
            var parent=Button("Cristais • responsáveis",shopRoot,purple,Color.white,()=>RequireParent(ShowCrystalPacks),18);
            SetRect((RectTransform)parent.transform,Vector2.right,Vector2.right,Vector2.right,new Vector2(-28,28),new Vector2(280,62));
            Canvas.ForceUpdateCanvases();
            float width=(shopRoot.rect.width-96)/3;
            float height=shopRoot.rect.height-234;
            for(int i=0;i<3;i++) BuildKitCard(MimoShopCatalog.Kits[i],28+i*(width+20),width,height);
            StartCoroutine(RenderShopPreviews());
        }
        void Toast(string text) { Toast(text,5f); }
        void BuildKitCard(MimoKit kit,float x,float width,float height)
        {
            var card=Image(kit.title,shopRoot,kit.accent).rectTransform;
            SetRect(card,Vector2.up,Vector2.up,Vector2.up,new Vector2(x,-116),new Vector2(width,height));
            var name=Label(kit.title,card,23,ink,FontStyle.Bold);name.alignment=TextAnchor.MiddleCenter;
            SetRect(name.rectTransform,Vector2.up,Vector2.up,Vector2.up,new Vector2(12,-12),new Vector2(width-24,48));
            var preview=Rect("Prévia real 3D - "+kit.id,card).gameObject.AddComponent<RawImage>();preview.raycastTarget=false;
            float portrait=Mathf.Min(width-32,height-340);
            SetRect(preview.rectTransform,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-66),new Vector2(portrait,portrait));
            preview.texture=CreateShopPreview(kit);
            var description=Label(kit.contents,card,16,ink);description.alignment=TextAnchor.MiddleCenter;
            SetRect(description.rectTransform,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(14,180),new Vector2(width-28,72));
            bool owned=MimoShopStorage.Owns(kit.id);
            bool equipped=(place==Place.Home && actor && actor.Data!=null && actor.Data.cosmeticKitId==kit.id) || (place==Place.Laboratory && draft!=null && draft.cosmeticKitId==kit.id);
            var cost=Label(owned ? "Coleção adquirida" : kit.crystals+" cristais • conteúdo completo",card,16,ink,FontStyle.Bold);cost.alignment=TextAnchor.MiddleCenter;
            SetRect(cost.rectTransform,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(12,140),new Vector2(width-24,32));
            var action=Button(owned ? (equipped ? "Retirar coleção" : "Usar coleção") : "Desbloquear…",card,purple,Color.white,
                ()=> {if(owned){if(equipped)RemoveKit();else EquipKit(kit.id);}else RequireParent(()=>ConfirmKit(kit));},18);
            SetRect((RectTransform)action.transform,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(14,76),new Vector2(width-28,58));
            var rotate=Button("Girar prévia",card,Color.white,muted,()=>RotateKitPreview(kit.id),14);
            SetRect((RectTransform)rotate.transform,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(14,10),new Vector2(width-28,56));
            // A second large transparent touch target contains the actual preview.
            var rotateTarget=preview.gameObject.AddComponent<Button>();preview.raycastTarget=true;rotateTarget.targetGraphic=preview;
            rotateTarget.onClick.AddListener(()=>RotateKitPreview(kit.id));
        }
        RenderTexture CreateShopPreview(MimoKit kit)
        {
            int i=shopPreviewObjects.Count;
            var root=new GameObject("Coleção preview "+kit.id);root.transform.position=new Vector3(80+i*12,0,0);
            var p=root.AddComponent<PetActor>();p.AllowCosmeticPreview=true;
            var d=place==Place.Laboratory && draft!=null ? draft.Copy() : actor && actor.Data!=null ? actor.Data.Copy() : new PetSave();
            d.created=false;d.glassesIndex=d.hatIndex=d.wingsIndex=0;d.finishIndex=1;d.cleanliness=1;d.cosmeticKitId=kit.id;
            p.Initialize(d);
            foreach(Transform t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
            sceneCamera.cullingMask &= ~(1<<30);
            Bounds b=p.VisibleBounds;
            var camObject=new GameObject("Coleção camera "+kit.id);var c=camObject.AddComponent<Camera>();c.enabled=false;
            c.cullingMask=1<<30;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=kit.accent;
            c.orthographic=true;c.orthographicSize=Mathf.Max(b.size.y*.61f,b.size.x*.61f);c.nearClipPlane=.1f;c.farClipPlane=20;
            c.transform.position=b.center+new Vector3(0,.10f,-7);c.transform.LookAt(b.center);
            var data=c.gameObject.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=false;
            var rt=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32){name="Prévia fiel "+kit.id,antiAliasing=1};rt.Create();c.targetTexture=rt;
            shopPreviewObjects.Add(root);shopPreviewObjects.Add(camObject);shopPreviewTextures.Add(rt);shopPreviewActors[kit.id]=p;shopPreviewCameras[kit.id]=c;
            return rt;
        }
        IEnumerator RenderShopPreviews()
        {
            yield return null;
            foreach(var c in shopPreviewCameras.Values)if(c) UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=c.targetTexture});
        }
        public void RotateKitPreview(string id)
        {
            if(shopPreviewActors.TryGetValue(id,out var p) && p){p.transform.Rotate(0,45,0);var c=shopPreviewCameras[id];UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=c.targetTexture});}
        }
        public void CloseShop(bool resetAuthorization=true)
        {
            if(shopRoot){shopRoot.gameObject.SetActive(false);Destroy(shopRoot.gameObject);}shopRoot=null;shopDialog=null;
            foreach(var o in shopPreviewObjects)if(o){o.SetActive(false);Destroy(o);}shopPreviewObjects.Clear();
            foreach(var t in shopPreviewTextures)if(t){t.Release();Destroy(t);}shopPreviewTextures.Clear();shopPreviewActors.Clear();shopPreviewCameras.Clear();
            if(resetAuthorization)parentAccessUntil=0;
        }
        void Dialog(string name,string heading,string body)
        {
            if(shopDialog){shopDialog.gameObject.SetActive(false);Destroy(shopDialog.gameObject);}
            shopDialog=Image(name,shopRoot,new Color(.13f,.12f,.24f,.75f)).rectTransform;shopDialog.GetComponent<Image>().sprite=null;Stretch(shopDialog);
            var panel=Image("Painel",shopDialog,cream).rectTransform;
            SetRect(panel,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(Mathf.Min(830,shopRoot.rect.width-60),480));
            var h=Label(heading,panel,28,ink,FontStyle.Bold);SetRect(h.rectTransform,Vector2.up,Vector2.up,Vector2.up,new Vector2(30,-20),new Vector2(panel.sizeDelta.x-60,52));
            var b=Label(body,panel,18,muted);SetRect(b.rectTransform,Vector2.up,Vector2.up,Vector2.up,new Vector2(30,-85),new Vector2(panel.sizeDelta.x-60,128));
            var close=Button("Voltar",panel,palePurple,ink,()=>{ parentAccessUntil=0; CloseShopDialog(); },18);SetRect((RectTransform)close.transform,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(30,20),new Vector2(180,56));
        }
        Transform DialogPanel => shopDialog.GetChild(0);
        void CloseShopDialog(){if(shopDialog){shopDialog.gameObject.SetActive(false);Destroy(shopDialog.gameObject);}shopDialog=null;}
        Button DialogButton(string text,float x,float y,float width,Action action,Color? background=null)
        {var b=Button(text,DialogPanel,background??purple,Color.white,action,18);SetRect((RectTransform)b.transform,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(x,y),new Vector2(width,56));return b;}
        void RequireParent(Action next)
        {
            if(IsParentAreaAuthorized){next();return;}
            int a=UnityEngine.Random.Range(13,30),b=UnityEngine.Random.Range(7,19);ParentChallengeAnswer=a+b;
            Dialog("Área dos responsáveis","Vamos chamar um responsável?","A loja é opcional. Peça a um adulto para resolver:\n"+a+" + "+b+" = ?\nNenhuma cobrança real nesta versão de teste.");
            var field=Image("Resposta do responsável",DialogPanel,Color.white).gameObject.AddComponent<InputField>();
            SetRect((RectTransform)field.transform,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(30,175),new Vector2(210,60));
            var text=Label("",field.transform,24,ink);Stretch(text.rectTransform,14,8);field.textComponent=text;field.contentType=InputField.ContentType.IntegerNumber;field.characterLimit=3;
            var placeholder=Label("Resposta",field.transform,20,muted);Stretch(placeholder.rectTransform,14,8);field.placeholder=placeholder;
            DialogButton("Continuar",260,175,210,()=>{if(int.TryParse(field.text,out int answer)&&answer==ParentChallengeAnswer){parentAccessUntil=Time.unscaledTime+120;CloseShopDialog();next();}else{field.text="";Toast("Vamos tentar novamente com um responsável.");}});
        }
        void ConfirmKit(MimoKit kit)
        {
            int balance;try{balance=MimoShopStorage.Load().crystals;}catch(Exception e){Dialog("Saldo indisponível","Saldo preservado",e.Message);return;}
            if(MimoShopStorage.Owns(kit.id)){Dialog("Coleção adquirida","Essa coleção já é sua","Não gastaremos cristais novamente.");return;}
            if(balance<kit.crystals){Dialog("Saldo insuficiente","Faltam "+(kit.crystals-balance)+" cristais","Seu saldo: "+balance+" cristais de teste.\nVocê pode fechar e continuar brincando gratuitamente.");return;}
            Dialog("Confirmar gasto","Desbloquear "+kit.title+"?",kit.contents+"\nGasto: "+kit.crystals+" cristais de teste. Saldo depois: "+(balance-kit.crystals)+".\nSem dinheiro real. Valores provisórios.");
            DialogButton("Confirmar "+kit.crystals+" cristais",245,20,420,()=>{if(!IsParentAreaAuthorized){CloseShopDialog();RequireParent(()=>ConfirmKit(kit));return;}bool ok=MimoShopStorage.Unlock(kit.id,out string message);if(ok){OpenShop();Toast(message);}else Dialog("Gasto não concluído","Saldo preservado",message);});
        }
        public void EquipKit(string id)
        {
            if(!MimoShopStorage.Owns(id)){Toast("Coleção ainda não adquirida.");return;}
            if(place==Place.Laboratory && draft!=null){draft.cosmeticKitId=id;CloseShop();Toast("A coleção aparecerá no Mimo ao concluir a criação.");return;}
            if(place==Place.Home && actor && actor.Data!=null && actor.Data.created) {
                string before=actor.Data.cosmeticKitId;actor.Data.cosmeticKitId=id;
                if(!PetStorage.Save(actor.Data)){actor.Data.cosmeticKitId=before;Toast("Não conseguimos salvar. Aparência anterior preservada.");return;}
                actor.Initialize(actor.Data);saved=actor.Data;CloseShop();BuildHomeUI();Toast("Coleção equipada! Suas escolhas gratuitas foram preservadas.");
            }
        }
        public void RemoveKit()
        {
            if(place==Place.Laboratory && draft!=null){draft.cosmeticKitId="";CloseShop();Toast("A nova receita usará a aparência gratuita.");return;}
            if(place==Place.Home && actor && actor.Data!=null && actor.Data.created){string before=actor.Data.cosmeticKitId;actor.Data.cosmeticKitId="";if(!PetStorage.Save(actor.Data)){actor.Data.cosmeticKitId=before;Toast("Não conseguimos salvar. Aparência anterior preservada.");return;}actor.Initialize(actor.Data);saved=actor.Data;CloseShop();BuildHomeUI();Toast("Voltou à aparência gratuita.");}
        }
        void ShowCrystalPacks()
        {
            MimoWallet wallet;try{wallet=MimoShopStorage.Load();}catch(Exception e){Dialog("Saldo indisponível","Saldo preservado",e.Message);return;}
            Dialog("Cristais de teste","Cristais • área dos responsáveis","SIMULAÇÃO SEM COBRANÇA REAL\nPreços em dinheiro apenas ilustrativos. Os cristais servem para desbloquear coleções completas. Sem anúncios, sorteios ou assinaturas.");
            for(int i=0;i<MimoShopCatalog.Packs.Length;i++){
                var p=MimoShopCatalog.Packs[i];var b=DialogButton(p.crystals+" cristais\n"+p.testPrice+" • teste",30+i*245,170,225,()=>BeginTestPayment(p));
                ((RectTransform)b.transform).sizeDelta=new Vector2(225,80);b.GetComponentInChildren<Text>().fontSize=16;
            }
            DialogButton("Minhas coleções",245,20,220,()=>{OpenShop();},purple);
            var waiting=wallet.transactions.FindLast(r=>r.state==PaymentState.Pending);
            if(waiting!=null)DialogButton("Retomar pedido pendente",30,100,420,()=>{pendingPayment=waiting;ShowPaymentResultControls(MimoShopCatalog.Pack(waiting.productId));});
            if((place==Place.Laboratory && draft!=null && !string.IsNullOrEmpty(draft.cosmeticKitId)) || (place==Place.Home && actor && actor.Data!=null && !string.IsNullOrEmpty(actor.Data.cosmeticKitId)))DialogButton("Tirar coleção",480,20,260,RemoveKit,purple);
        }
        void BeginTestPayment(CrystalPack pack)
        {
            if(!IsParentAreaAuthorized){RequireParent(()=>BeginTestPayment(pack));return;}
            pendingPayment=payments.Begin(pack.id);
            if(!MimoShopStorage.Begin(pendingPayment,out string message)){Dialog("Compra indisponível","Saldo preservado",message);return;}
            ShowPaymentResultControls(pack);
        }
        void ShowPaymentResultControls(CrystalPack pack)
        {
            Dialog("Pagamento simulado","SIMULAÇÃO • nenhuma cobrança",pack.crystals+" cristais de teste por "+pack.testPrice+" fictícios.\nNada será debitado. Escolha um resultado para testar.\nApenas aprovação acrescenta cristais; pendência, falha e cancelamento não acrescentam.");
            DialogButton("Aprovar teste",30,170,220,()=>ResolvePayment(PaymentState.Purchased));
            DialogButton("Deixar pendente",265,170,220,()=>ResolvePayment(PaymentState.Pending));
            DialogButton("Simular falha",500,170,220,()=>ResolvePayment(PaymentState.Failed));
            DialogButton("Cancelar compra",245,20,220,()=>ResolvePayment(PaymentState.Cancelled));
        }
        void ResolvePayment(PaymentState state)
        {
            if(!IsParentAreaAuthorized){RequireParent(()=>ShowPaymentResultControls(MimoShopCatalog.Pack(pendingPayment.productId)));return;}
            var resolved=payments.Resolve(pendingPayment,state);
            bool ok=MimoShopStorage.Settle(resolved,out string message);
            if(state==PaymentState.Pending && ok){Dialog("Compra pendente","Pedido de teste pendente",message+"\nEle pode ser retomado na área dos responsáveis.");return;}
            if(ok){OpenShop();Toast(message);}else Dialog("Resultado não concluído","Saldo preservado",message);
        }
    }
}
