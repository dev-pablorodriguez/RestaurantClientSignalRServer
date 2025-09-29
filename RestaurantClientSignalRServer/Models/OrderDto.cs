namespace RestaurantClientSignalRServer.Models
{
    public class OrderDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        public string? Title { get; set; }

        public string? Description { get; set; }

        public int Quantity { get; set; }

        public string Status { get; set; } = "CREATED";
    }
}
