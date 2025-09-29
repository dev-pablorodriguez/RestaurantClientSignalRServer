
namespace RestaurantClientSignalRServer.Models
{
    public class Order : OrderDto
    {
        public string PartitionKey { get; set; } = "order";
    }
}
