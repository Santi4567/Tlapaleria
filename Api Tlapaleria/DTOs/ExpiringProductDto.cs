namespace Api_Tlapaleria.DTOs
{
    public class ExpiringProductDto
    {
        public int Id { get; set; }
        public string InternalCode { get; set; }
        public string Name { get; set; }
        public DateTime? NextExpirationDate { get; set; }
        public int DaysRemaining { get; set; }
    }
}
