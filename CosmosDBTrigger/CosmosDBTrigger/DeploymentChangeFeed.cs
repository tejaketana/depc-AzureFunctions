using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace CosmosDBTrigger
{
    public static class DeploymentChangeFeed
    {
        [FunctionName("DeploymentMigration")]
        public static async Task Run([CosmosDBTrigger(
        databaseName: "%databaseName%",
        collectionName: "%sourceContainer%",
        LeaseCollectionName = "%leaseCollection%",
        ConnectionStringSetting = "deploymentconsole_DOCUMENTDB",
        StartFromBeginning = true,
        CreateLeaseCollectionIfNotExists = true
    )] IReadOnlyList<Document> source,
    [CosmosDB(
        databaseName: "%databaseName%",
        collectionName: "%destinationContainer%",
        ConnectionStringSetting = "deploymentconsole_DOCUMENTDB"
    )] IAsyncCollector<Document> destination,
    ILogger log)
        {
            if (source != null && source.Count > 0)
            {
                log.LogInformation($"Documents modified: {source.Count}");
                log.LogInformation($"First document Id: {source[0].Id}");

                foreach (var doc in source)
                {
                    if (doc.GetPropertyValue<string>("isRecordCreated") == "true")
                    {
                        await destination.AddAsync(doc);
                        log.LogInformation($"document created in destination: {doc.Id}");
                    }
                }
            }
        }
    }
}
