namespace StorePitOne.Models
{
    public class PeakStockActionDto
    {
        public int Id { get; set; }
        public DateTime AdjustmentTime { get; set; }
        public int AdjustmentType { get; set; }
        public string? AdjustmentReasonName { get; set; }
        public string? ItemNumber { get; set; }
        public decimal QuantityAdjusted { get; set; }
        public decimal TotalQuantity { get; set; }
        public string? WarehouseName { get; set; }
        public string? LotNumber { get; set; }
        public DateTime? BestBeforeDate { get; set; }
    }
}