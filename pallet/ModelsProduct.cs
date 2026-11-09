namespace PalletApp.Models
{
    public class Product
    {
        public int ProductID { get; set; }
        public string SKU { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Manufacturer { get; set; }
        public string Composition { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string ImagePath { get; set; }

        /// <summary>
        /// Есть ли товар на складе (Quantity > 0).
        /// </summary>
        public bool IsAvailable { get; set; } = true;

        /// <summary>
        /// Для UI: низкий остаток (< 20).
        /// </summary>
        public bool IsLowStock => Quantity > 0 && Quantity < 20;
    }
}