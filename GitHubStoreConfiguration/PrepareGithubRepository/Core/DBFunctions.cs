using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PrepareGithubRepo.Core
{
    /// <summary>
    /// This Class will be used to:
    /// Read the templates for creating Repositories/Branches/Configuration files.
    /// </summary>
    public static class DBFunctions
    {
        private static string _connectionString = Environment.GetEnvironmentVariable("CosmosDB_Connection");
        private static CosmosClient _cosmosClient;
        private static Database _cosmosDatabase;
        private static Container _cosmosContainer;

        // Connect to a DB.
        /// <summary>
        /// Initialize CosmosClient for DB operations.
        /// </summary>
        /// <param name="log"></param>
        /// <returns></returns>
        public static bool InitializeDBClient(ILogger log)
        {
            bool isInitialized = false;

            try
            {
                _cosmosClient = new CosmosClient(_connectionString);
                isInitialized = true;
            }
            catch (Exception ex)
            {
                log.LogError($"Unable to access the Database: {ex.Message}, Stacktrace: {ex.StackTrace}");
            }

            return isInitialized;
        }

        /// <summary>
        /// Create a new document in the Container.
        /// </summary>
        /// <param name="database"></param>
        /// <param name="container"></param>
        /// <param name="partition"></param>
        /// <param name="document"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<ResponseMessage> AddDocument(string database, string container, string partition, string document, ILogger log)
        {
            ResponseMessage response = null;

            try
            {
                _cosmosDatabase = _cosmosClient.GetDatabase(database);
                _cosmosContainer = _cosmosDatabase.GetContainer(container);

                Stream payload = new MemoryStream(Encoding.UTF8.GetBytes(document));
                response = await _cosmosContainer.CreateItemStreamAsync(payload, new PartitionKey(partition), new ItemRequestOptions { EnableContentResponseOnWrite = true });

                if (response.IsSuccessStatusCode)
                {
                    log.LogInformation("Document successfully added");
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Error adding a Document: {ex.Message}, Stacktrace: {ex.StackTrace}");
                response = new ResponseMessage(System.Net.HttpStatusCode.BadRequest);
            }

            return response;
        }

        /// <summary>
        /// Get document(s) using a Query.
        /// </summary>
        /// <param name="database"></param>
        /// <param name="container"></param>
        /// <param name="query"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<Stream> GetDocument(string database, string container, string query, ILogger log)
        {
            try
            {
                _cosmosDatabase = _cosmosClient.GetDatabase(database);
                _cosmosContainer = _cosmosDatabase.GetContainer(container);

                var iterator = _cosmosContainer.GetItemQueryStreamIterator(new QueryDefinition(query));

                if (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        return response.Content;
                    }
                    else
                    {
                        throw new Exception($"Unable to read document from Database: {database}, Container: {container}, StatusCode: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Error reading Document: {ex.Message}, Stacktrace: {ex.StackTrace}");
            }

            return null;
        }
    }
}
