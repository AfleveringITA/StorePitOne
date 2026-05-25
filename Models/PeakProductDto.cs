namespace StorePitOne.Models
{
    public class PeakProductDto
    {
        public int Id { get; set; }
        public string? ProductId { get; set; }
        public string? VariantId { get; set; }
        public string? ItemNumber { get; set; }
        public string? Description { get; set; }
        public bool LotNumberControlled { get; set; }
    }
}