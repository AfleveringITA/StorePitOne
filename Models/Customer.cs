namespace StorePitOne.Models
{
    public class Customer
    {
        public int Id { get; set; }

        public string CompanyName { get; set; } = string.Empty;

        public string OwnerName { get; set; } = string.Empty;

        public string CvrNumber { get; set; } = string.Empty;

        public string ContactPerson { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public string City { get; set; } = string.Empty;

        public string PostalCode { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public string PeakWmsOwner { get; set; } = string.Empty;

        public string PeakWmsApiKey { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}