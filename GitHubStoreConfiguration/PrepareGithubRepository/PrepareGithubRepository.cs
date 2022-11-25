using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PrepareGithubRepo.Services;
using PrepareGithubRepository.Core;
using PrepareGithubRepository.Models;

namespace PrepareGithubRepository
{
    public static class PrepareGithubRepository
    {
        [FunctionName("PrepareGithubRepository")]
        public static async Task Run([CosmosDBTrigger(
            databaseName: "%StoreConfigurationLogs_DatabaseId%",
            collectionName: "%StoreConfigurationLogs_CollectionId%",
            ConnectionStringSetting = "CosmosDB_Connection",
            LeaseCollectionPrefix = "PrepareGithubRepository",
            LeaseCollectionName = "%leasesStoreConfigurations_CollectionId%")]IReadOnlyList<Document> documents, ILogger log)
        {
            if (documents != null && documents.Count > 0)
            {
                for (int indexDocument = 0; indexDocument < documents.Count; indexDocument++)
                {
                    log.LogInformation($"Processing document # {(indexDocument + 1)} of {documents.Count}");

                    StoreDeploymentDocument processDocument = JsonConvert.DeserializeObject<StoreDeploymentDocument>(documents[indexDocument].ToString());

                    if (processDocument == null)
                    {
                        log.LogError("Failed to retrieve Store Deployment details - unable to update GitHub !");
                    }
                    else
                    {
                        bool isSuccess;
                        
                        if (processDocument.IsUpdated())
                            isSuccess = await GithubService.UpdateDeploymentInGitHubRepos(processDocument, log);
                        else
                            isSuccess = await GithubService.UpdateGitHubRepos(processDocument, log);

                        // Send a Success/Failed message to the EventHub.
                        if (Helper.SendEvent(processDocument, isSuccess) == HttpStatusCode.Created)
                        {
                            log.LogInformation("Repository preparation status event posted to EventHub");
                        }
                        else
                        {
                            log.LogError("Failed posting Repository preparation status event to EventHub");
                        }
                    }
                }
            }
        }
    }
}
