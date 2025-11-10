using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IMongoClient>(sp => new MongoClient("// Your MongoDB connection string here //"));

var app = builder.Build();

app.MapPost("/api/bulk-validator", async (IMongoClient client, BulkValidationRequest request) =>
{
    try
    {
        var sourceDb = client.GetDatabase(request.SourceDatabase);
        var sourceCollections = await sourceDb.ListCollectionsAsync();
        var sourceList = await sourceCollections.ToListAsync();

        var sourceCollection = sourceList
            .FirstOrDefault(c => c["name"] == request.SourceCollection);

        if (sourceCollection == null)
            return Results.NotFound($"Source collection '{request.SourceCollection}' not found.");

        var options = sourceCollection["options"]?.AsBsonDocument;
        var validator = options?["validator"]?.AsBsonDocument;

        if (validator == null)
            return Results.BadRequest("No validator found in source collection.");

        var appliedTo = new List<string>();

        foreach (var target in request.Targets)
        {
            var targetDb = client.GetDatabase(target.Database);

            var cmd = new BsonDocument
            {
                { "collMod", target.Collection },
                { "validator", validator }
            };

            await targetDb.RunCommandAsync<BsonDocument>(cmd);
            appliedTo.Add($"{target.Database}.{target.Collection}");
        }

        return Results.Ok(new
        {
            message = "Schema successfully applied to all target collections.",
            appliedTo
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.Run();

record BulkValidationRequest(
    string SourceDatabase,
    string SourceCollection,
    List<TargetCollection> Targets
);

record TargetCollection(string Database, string Collection);
