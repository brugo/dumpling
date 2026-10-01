using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MimoGarden
{
    public sealed partial class PetActor
    {
        Transform cosmeticRoot;
        string visibleCosmetic;
        public bool AllowCosmeticPreview { get; set; }
        public bool HasCosmeticGeometry => cosmeticRoot && cosmeticRoot.gameObject.activeSelf;
        public string VisibleCosmetic => visibleCosmetic;
        readonly List<Material> cosmeticMaterials=new List<Material>();
        void ApplyCosmetic()
        {
            var kit=MimoShopCatalog.Find(Data.cosmeticKitId);
            if(kit!=null && !AllowCosmeticPreview && !MimoShopStorage.Owns(kit.id))kit=null;
            string id=kit==null ? "" : kit.id;
            if(visibleCosmetic!=id) {
                if(cosmeticRoot){cosmeticRoot.gameObject.SetActive(false);Destroy(cosmeticRoot.gameObject);}
                foreach(var m in cosmeticMaterials)if(m)Destroy(m);cosmeticMaterials.Clear();
                cosmeticRoot=null;visibleCosmetic=id;
                if(kit!=null) {
                    var prefab=Resources.Load<GameObject>("Cosmetics/"+id);
                    if(!prefab)throw new InvalidOperationException("Geometria da coleção ausente: "+id);
                    cosmeticRoot=Instantiate(prefab,_motion,false).transform;
                    cosmeticRoot.name="Coleção completa - "+kit.title;
                    float h=_variantBodyBounds.size.y;
                    cosmeticRoot.localPosition=new Vector3(0,_variantBodyBounds.min.y,0);
                    cosmeticRoot.localScale=Vector3.one*h;
                    cosmeticRoot.localRotation=Quaternion.Euler(0,180,0)*cosmeticRoot.localRotation;
                    var shared=new Dictionary<string,Material>();
                    foreach(var r in cosmeticRoot.GetComponentsInChildren<Renderer>(true)) {
                        var mats=r.sharedMaterials;
                        for(int i=0;i<mats.Length;i++) {
                            string key=mats[i] ? mats[i].name : "pearl";
                            if(!shared.TryGetValue(key,out var m)) {
                                m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Mimo premium "+id+" "+key};
                                m.SetColor("_BaseColor",key.Contains("gold") ? new Color(.96f,.78f,.42f) : key.Contains("pearl") ? new Color(1,.96f,.91f) : Color.white);
                                m.SetFloat("_Metallic",key.Contains("gold") ? .65f : .12f);
                                m.SetFloat("_Smoothness",key.Contains("gold") ? .68f : .52f);
                                if(key.Contains("fabric"))m.SetTexture("_BaseMap",Resources.Load<Texture2D>("Cosmetics/"+id));
                                shared[key]=m;cosmeticMaterials.Add(m);
                            }
                            mats[i]=m;
                        }
                        r.sharedMaterials=mats;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
                    }
                }
                CacheVisibleGeometry();
            }
            if(_body && _body.sharedMaterial) {
                var m=_body.sharedMaterial;
                // Reset every finish parameter so the kit never inherits the free recipe's sugar, gel or glow.
                if(kit!=null)ApplyKitMaterial(m,kit,true);
                m.SetFloat("_KitEnabled",kit==null ? 0 : 1);
                m.SetTexture("_KitMap",kit==null ? Texture2D.whiteTexture : Resources.Load<Texture2D>("Cosmetics/"+id));
                if(kit!=null){if(_glitterRenderer)_glitterRenderer.enabled=false;if(_auraRenderer)_auraRenderer.enabled=false;}
            }
            if(_accessoryRoot)_accessoryRoot.gameObject.SetActive(kit==null);
            if(kit!=null && _pearl)ApplyKitMaterial(_pearl,kit,false);
            if(_eyeMaterial)_eyeMaterial.renderQueue=IsTranslucentFinish ? 2995 : -1;
            if(_whiteMaterial)_whiteMaterial.renderQueue=IsTranslucentFinish ? 2996 : -1;
            if(_blushMaterial)_blushMaterial.renderQueue=IsTranslucentFinish ? 2997 : 3000;
        }
        void ApplyKitMaterial(Material m,MimoKit kit,bool body)
        {
            ApplyFinishMaterial(m,FinishStyles[1],body);
            m.SetFloat("_FinishKind",1);m.SetFloat("_Grain",0);m.SetFloat("_DetailScale",72);
            m.SetFloat("_RimStrength",.35f);m.SetFloat("_Subsurface",.09f);
            m.SetFloat("_Smoothness",.65f);m.SetFloat("_Iridescence",kit.id=="mar" ? .65f : .25f);
            m.SetFloat("_Transmission",0);m.SetFloat("_Opacity",1);m.SetFloat("_Thickness",body ? 1 : .55f);
            m.SetFloat("_RefractionStrength",0);m.SetFloat("_SceneRefraction",0);
            m.SetFloat("_MicroNormalStrength",.025f);m.SetFloat("_SpecularStrength",.3f);
            m.SetFloat("_EnvironmentStrength",.8f);m.SetFloat("_CoatStrength",.3f);
            m.SetFloat("_KitEnabled",body ? 1 : 0);m.SetFloat("_ZWrite",1);m.SetFloat("_SrcBlend",1);m.SetFloat("_DstBlend",0);
            var accent=Color.Lerp(kit.accent,Color.white,.12f);
            m.SetColor("_RimColor",Color.Lerp(accent,Color.white,.78f));
            if(!body){m.SetColor("_BaseColor",accent);m.SetColor("_Color",accent);}
            m.SetOverrideTag("RenderType","Opaque");m.renderQueue=2000;
        }
        void ReleaseCosmetic()
        {
            foreach(var m in cosmeticMaterials)if(m)Destroy(m);cosmeticMaterials.Clear();
            if(cosmeticRoot)Destroy(cosmeticRoot.gameObject);cosmeticRoot=null;visibleCosmetic="";
        }
    }
}
