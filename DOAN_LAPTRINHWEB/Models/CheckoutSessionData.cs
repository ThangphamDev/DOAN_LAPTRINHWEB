namespace DOAN_LAPTRINHWEB.Models
{
    public class CheckoutSessionData
    {
        public string UserId { get; set; }
        public string PaymentMethod { get; set; }
        public int SelectedAddressId { get; set; }
        public bool SaveAddressToAccount { get; set; }
        public Address Address { get; set; }
    }
}