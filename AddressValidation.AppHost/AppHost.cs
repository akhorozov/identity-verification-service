using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// ==================== Redis Cache ====================
var cache = builder
    .AddRedis("cache");

// ==================== Azure Cosmos DB (Emulator) ====================
var cosmosDb = builder
    .AddAzureCosmosDB("cosmosdb");

// ==================== Address Validation API ====================
var api = builder
    .AddProject<Projects.AddressValidation_Api>("api")
    .WithReference(cache)
    .WithReference(cosmosDb)
    .WithHttpEndpoint(port: 5000, targetPort: 5000, name: "http");

// ==================== YARP Reverse Proxy Gateway ====================
var gateway = builder
    .AddProject<Projects.AddressValidation_Gateway>("gateway")
    .WithReference(api)
    .WithHttpEndpoint(port: 5001, targetPort: 5001, name: "http");

// ==================== Build and Run ====================
builder.Build().Run();


