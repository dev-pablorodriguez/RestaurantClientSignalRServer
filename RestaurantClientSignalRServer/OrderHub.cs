using Azure.Storage.Blobs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.Cosmos;
using RestaurantClientSignalRServer.Models;
using System.Text;
using System.Text.Json;

namespace RestaurantClientSignalRServer
{
    public class OrderHub : Hub
    {
        private readonly Container _cosmosContainer;
        private readonly BlobContainerClient _storageContainer;

        public OrderHub(IConfiguration configuration) {
            var cosmosConnString = configuration.GetConnectionString("CosmosDB");
            var storageConnString = configuration.GetConnectionString("StorageAccount");

            // validate connection strings
            if (string.IsNullOrWhiteSpace(cosmosConnString)) throw new ArgumentNullException(nameof(cosmosConnString));
            if (string.IsNullOrWhiteSpace(storageConnString)) throw new ArgumentNullException(nameof(storageConnString));


            // init cosmos container
            CosmosClient cosmosClient = new(cosmosConnString, new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
                }
            });
            _cosmosContainer = cosmosClient.GetContainer("prmlearning", "orders");

            // init storage container
            BlobServiceClient blobServiceClient = new(storageConnString);
            _storageContainer = blobServiceClient.GetBlobContainerClient("restaurant");

            // make sure the container exists
            _storageContainer.CreateIfNotExists();
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
                Order order = new()
                {
                    Title = title,
                    Description = description,
                    Quantity = quantity,
                };

                await UpsertDocAsync(order);

                

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
            var query = _cosmosContainer.GetItemQueryIterator<Order>("SELECT * FROM c");
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
                var response = await _cosmosContainer.ReadItemAsync<Order>(order.Id, new PartitionKey(order.PartitionKey));
                var orderFromDB = response.Resource;

                // If found, update it
                orderFromDB.Status = "COMPLETED";

                await _cosmosContainer.ReplaceItemAsync(orderFromDB, order.Id, new PartitionKey(order.PartitionKey));

                // If the order was completed, generate receipt and save it in blob storage
                await UploadFile(order);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // If not found, create it
                await _cosmosContainer.CreateItemAsync(order, new PartitionKey(order.PartitionKey));
            }
        }

        private async Task UploadFile(Order order)
        {
            // create blob client
            BlobClient blobClient = _storageContainer.GetBlobClient($"{order.Id}.txt");

            if (await blobClient.ExistsAsync()) {
                throw new Exception($"The file {order.Id} exists already.");
            }

            StringBuilder sb = new();
            sb.AppendLine("Order Receipt:");
            sb.AppendLine("==============");
            sb.AppendLine($"ID: {order.Id}");
            sb.AppendLine($"Title: {order.Title}");
            sb.AppendLine($"Description: {order.Description}");
            sb.AppendLine($"Quantity: {order.Quantity}");
            sb.AppendLine($"Status: {order.Status}");

            var byteArray = Encoding.UTF8.GetBytes(sb.ToString());
            using var stream = new MemoryStream(byteArray);

            await blobClient.UploadAsync(stream);
        }

        #endregion
    }
}
