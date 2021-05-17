namespace Shared.Models
{
    public class Product
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public decimal Value { get; set; }

        public int Quantity { get; set; }

        public bool Reserved { get; set; }
    }
}
