using System;
using UnityEngine;

namespace MimoGarden
{
    [Serializable] public sealed class MimoKit
    {
        public string id, title, description, contents;
        public int crystals;
        public Color accent;
        public MimoKit(string id, string title, string description, string contents, int crystals, string accent)
        { this.id=id; this.title=title; this.description=description; this.contents=contents; this.crystals=crystals; ColorUtility.TryParseHtmlString("#"+accent,out this.accent); }
    }
    public static class MimoShopCatalog
    {
        // Provisional test values. All existing creation options remain free.
        public static readonly MimoKit[] Kits = {
            new MimoKit("aurora","Jardim de Aurora","Um jardim de seda e pétalas.","Pele floral perolada • coroa de margaridas • asas de folhas",150,"DCEEDF"),
            new MimoKit("luna","Guardião da Lua","Um abraço feito de estrelas.","Pele de constelações • chapéu lunar • capa com estrelas",180,"E2DDF9"),
            new MimoKit("mar","Pérola do Mar","Tesouros suaves do oceano.","Pele de conchas nacaradas • boina-concha • colar de pérolas • nadadeiras",220,"D4EDF1")
        };
        public static MimoKit Find(string id) => Array.Find(Kits,k=>k.id==id);
        public static readonly CrystalPack[] Packs = {
            new CrystalPack("mimo.test.crystals.200",200,"R$ 4,90"),
            new CrystalPack("mimo.test.crystals.500",500,"R$ 9,90"),
            new CrystalPack("mimo.test.crystals.1000",1000,"R$ 17,90")
        };
        public static CrystalPack Pack(string id) => Array.Find(Packs,p=>p.id==id);
    }
    [Serializable] public sealed class CrystalPack
    {
        public string id, testPrice; public int crystals;
        public CrystalPack(string id,int crystals,string price){this.id=id;this.crystals=crystals;testPrice=price;}
    }
}
