using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MimoGarden
{
    [Serializable] public sealed class MimoWallet
    {
        public int schemaVersion=1, crystals;
        public List<string> owned=new List<string>();
        public List<PaymentReceipt> transactions=new List<PaymentReceipt>();
    }
    public static class MimoShopStorage
    {
        static readonly object Gate=new object();
        // Separate simulation ledger: never migrates test currency into a real-money account.
        public static string PathName => PetStorage.SavePath+".shop-test.json";
        public static bool FailNextWriteForValidation;
        public static MimoWallet Load()
        {
            lock(Gate) {
                if(!File.Exists(PathName)) {
                    if(File.Exists(PathName+".bak") || File.Exists(PathName+".tmp"))
                        throw new IOException("Registro principal de cristais ausente. Há um registro anterior preservado; compras bloqueadas para evitar perda de saldo ou créditos duplicados.");
                    return new MimoWallet();
                }
                var w=JsonUtility.FromJson<MimoWallet>(File.ReadAllText(PathName,Encoding.UTF8));
                if(w==null || w.schemaVersion!=1 || w.crystals<0 || w.owned==null || w.transactions==null)
                    throw new IOException("Saldo de teste indisponível. O arquivo foi preservado; não repetiremos créditos.");
                var tokens=new HashSet<string>();
                foreach(var r in w.transactions)
                    if(r==null || string.IsNullOrEmpty(r.token) || !tokens.Add(r.token) || MimoShopCatalog.Pack(r.productId)==null)
                        throw new IOException("Registro de teste inválido; compras bloqueadas para preservar o saldo.");
                return w;
            }
        }
        public static bool Owns(string id)
        { if(MimoShopCatalog.Find(id)==null)return false; try{return Load().owned.Contains(id);}catch{return false;} }
        public static bool Begin(PaymentReceipt receipt,out string message)
        {
            lock(Gate) {
                message="";
                if(receipt==null || receipt.state!=PaymentState.Pending || MimoShopCatalog.Pack(receipt.productId)==null ||
                    string.IsNullOrEmpty(receipt.token) || !receipt.token.StartsWith("TEST-",StringComparison.Ordinal)) { message="Pedido de teste inválido.";return false; }
                try {
                    var w=Load(); if(w.transactions.Exists(r=>r.token==receipt.token)){message="Pedido já registrado.";return false;}
                    w.transactions.Add(new PaymentReceipt{token=receipt.token,productId=receipt.productId,state=PaymentState.Pending});
                    return Write(w,out message);
                } catch(Exception e){message=e.Message;return false;}
            }
        }
        public static bool Settle(PaymentReceipt receipt,out string message)
        {
            lock(Gate) {
                message="";
                if(receipt==null){message="Pedido ausente.";return false;}
                try {
                    var w=Load();var r=w.transactions.Find(t=>t.token==receipt.token);
                    if(r==null || r.productId!=receipt.productId){message="Pedido não reconhecido.";return false;}
                    if(r.state==PaymentState.Purchased){message="Crédito já entregue; saldo preservado.";return true;}
                    if(r.state!=PaymentState.Pending){message="Pedido encerrado sem cobrança.";return false;}
                    if(receipt.state==PaymentState.Pending){message="Pendente: nenhum cristal foi creditado.";return true;}
                    if(receipt.state!=PaymentState.Purchased && receipt.state!=PaymentState.Cancelled && receipt.state!=PaymentState.Failed){message="Resposta inválida.";return false;}
                    if(receipt.state==PaymentState.Purchased) w.crystals=checked(w.crystals+MimoShopCatalog.Pack(r.productId).crystals);
                    r.state=receipt.state;
                    if(!Write(w,out message))return false;
                    message=receipt.state==PaymentState.Purchased ? "Cristais de teste recebidos! Nenhuma cobrança." : "Compra de teste encerrada. Nenhum cristal creditado.";
                    return true;
                }catch(Exception e){message=e.Message;return false;}
            }
        }
        public static bool Unlock(string kitId,out string message)
        {
            lock(Gate) {
                message="";var kit=MimoShopCatalog.Find(kitId);if(kit==null){message="Coleção indisponível.";return false;}
                try {
                    var w=Load();if(w.owned.Contains(kitId)){message="Essa coleção já é sua.";return false;}
                    if(w.crystals<kit.crystals){message="Saldo insuficiente. Você pode voltar a brincar gratuitamente.";return false;}
                    w.crystals-=kit.crystals;w.owned.Add(kitId);
                    if(!Write(w,out message))return false;
                    message="Coleção desbloqueada!";return true;
                }catch(Exception e){message=e.Message;return false;}
            }
        }
        static bool Write(MimoWallet w,out string message)
        {
            message="";string temp=PathName+".tmp";
            try {
                if(FailNextWriteForValidation){FailNextWriteForValidation=false;throw new IOException("Interrupção simulada antes da gravação.");}
                Directory.CreateDirectory(Path.GetDirectoryName(PathName));
                byte[] bytes=new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(w,true));
                using(var s=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){s.Write(bytes,0,bytes.Length);s.Flush(true);}
                if(File.Exists(PathName))File.Replace(temp,PathName,PathName+".bak");else File.Move(temp,PathName);
                return true;
            }catch(Exception e){message="Não foi possível salvar. O saldo anterior foi preservado. "+e.Message;return false;}
        }
    }
}
