using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.Cosmos;
using RestaurantClientSignalRServer.Models;
using System.Reflection.Metadata;
using System.Text.Json;

namespace RestaurantClientSignalRServer
{
    public class OrderHub : Hub
    {
        private readonly CosmosClient _client;
        private readonly Container _container;

        public OrderHub(IConfiguration configuration) {
            var connString = configuration.GetConnectionString("CosmosDB");

            if (string.IsNullOrWhiteSpace(connString)) throw new ArgumentNullException(nameof(connString));

            _client = new CosmosClient(connString, new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
                }
            });
            _container = _client.GetContainer("prmlearning", "orders");
        }

        #region ENDPOINTS

        // Show orders on load
        public async Task OnLoad()
        {
            try
            {
                // return updated orders
                await ReturnOrdersToClients();
            }
            catch (Exception e)
            {
                await Clients.Caller.SendAsync("Error", e.Message);
            }
        }

        // Send order to all connected clients
        public async Task CreateOrder(string title, string description, int quantity)
        {
            try
            {
                // create order
                await UpsertDocAsync(new Order
                {
                    Title = title,
                    Description = description,
                    Quantity = quantity,
                });

                // return updated orders
                await ReturnOrdersToClients();
            }
            catch (Exception e)
            {
                await Clients.Caller.SendAsync("Error", e.Message);
            }
        }

        // Send order to all connected clients
        public async Task CompleteOrder(string orderId)
        {
            try
            {
                // complete order
                await UpsertDocAsync(new Order
                {
                    Id = orderId,
                });

                // return updated orders
                await ReturnOrdersToClients();
            }
            catch (Exception e)
            {
                await Clients.Caller.SendAsync("Error", e.Message);
            }
        }

        #endregion

        #region PRIVATE MEMBERS

        private async Task ReturnOrdersToClients()
        {
            var orders = await GetOrders();
            var ordersJson = JsonSerializer.Serialize(orders.Select(i =>
            new OrderDto
            {
                Id = i.Id,
                Title = i.Title,
                Description = i.Description,
                Status = i.Status,
                Quantity = i.Quantity,
            }).ToList());
            await Clients.All.SendAsync("ReceiveOrders", ordersJson);
        }

        private async Task<List<Order>> GetOrders()
        {
            var query = _container.GetItemQueryIterator<Order>("SELECT * FROM c");
            var results = new List<Order>();

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                results.AddRange([.. response]);
            }

            return results;
        }

        private async Task UpsertDocAsync(Order order)
        {
            try
            {
                var response = await _container.ReadItemAsync<Order>(order.Id, new PartitionKey(order.PartitionKey));
                var orderFromDB = response.Resource;

                // If found, update it
                orderFromDB.Status = "COMPLETED";

                await _container.ReplaceItemAsync(orderFromDB, order.Id, new PartitionKey(order.PartitionKey));
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // If not found, create it
                await _container.CreateItemAsync(order, new PartitionKey(order.PartitionKey));
            }
        }

        #endregion
    }
}
