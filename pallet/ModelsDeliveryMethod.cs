namespace PalletApp.Models
{
    public class DeliveryMethod
    {
        public int DeliveryMethodID { get; set; }
        public string Name { get; set; }
        public decimal BaseCost { get; set; }
        public decimal Capacity { get; set; }
    }
}