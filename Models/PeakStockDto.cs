namespace StorePitOne.Models
{
    public class PeakStockDto
    {
        public int Id { get; set; }
        public string? ItemNumber { get; set; }
        public string? Ean { get; set; }
        public decimal Quantity { get; set; }
        public decimal AvailableQuantity { get; set; }
        public decimal ReservedQuantity { get; set; }
        public string? LocationCode { get; set; }
    }
}