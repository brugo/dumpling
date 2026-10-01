using System;

namespace MimoGarden
{
    public enum PaymentState { Pending, Purchased, Cancelled, Failed }
    [Serializable] public sealed class PaymentReceipt
    {
        public string token, productId;
        public PaymentState state;
    }
    public interface IMimoPayments
    {
        bool IsSimulation { get; }
        PaymentReceipt Begin(string productId);
        PaymentReceipt Resolve(PaymentReceipt receipt, PaymentState outcome);
    }
    // This provider never contacts a payment service. Production must use a verified server ledger.
    public sealed class MimoTestPayments : IMimoPayments
    {
        public bool IsSimulation => true;
        public PaymentReceipt Begin(string id) => MimoShopCatalog.Pack(id)==null ? null :
            new PaymentReceipt { token="TEST-"+Guid.NewGuid().ToString("N"),productId=id,state=PaymentState.Pending };
        public PaymentReceipt Resolve(PaymentReceipt receipt,PaymentState outcome) => receipt==null ? null :
            new PaymentReceipt { token=receipt.token,productId=receipt.productId,state=outcome };
    }
}
