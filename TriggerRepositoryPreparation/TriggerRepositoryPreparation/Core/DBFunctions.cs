using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace TriggerRepositoryPreparation.Core
{
    /// <summary>
    /// This Class will be used to:
    /// 1. Read DB containers for Mappings,
    /// 2. Write the mapped configuration to the DB.
    /// </summary>
    public static class DBFunctions
    {
        private static Lazy<CosmosClient> lazyCosmosClient = new Lazy<CosmosClient>(InitializeCosmosClient);
        private static CosmosClient _cosmosClient => lazyCosmosClient.Value;
        private static Container _cosmosContainer;
        private static CosmosClient InitializeCosmosClient()
        {
            return new CosmosClient(Environment.GetEnvironmentVariable("CosmosDB_Connection"));
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
            try
            {
                _cosmosContainer = _cosmosClient.GetDatabase(database).GetContainer(container);

                Stream payload = new MemoryStream(Encoding.UTF8.GetBytes(document));
                var response = await _cosmosContainer.CreateItemStreamAsync(payload, new PartitionKey(partition), new ItemRequestOptions { EnableContentResponseOnWrite = true });

                if (response.IsSuccessStatusCode)
                {
                    log.LogInformation($"Document successfully added to {database}/{container}");
                }
                return response;
            }
            catch (Exception ex)
            {
                log.LogError($"Error adding Document: {ex.Message}, Stacktrace: {ex.StackTrace}");
                throw ex;
            }
        }

        public static async Task<ResponseMessage> UpdateDocument(string database, string container, string id, string partition, string document, ILogger log)
        {
            try
            {
                _cosmosContainer = _cosmosClient.GetDatabase(database).GetContainer(container);

                Stream payload = new MemoryStream(Encoding.UTF8.GetBytes(document));
                var response = await _cosmosContainer.ReplaceItemStreamAsync(payload, id, new PartitionKey(partition), new ItemRequestOptions { EnableContentResponseOnWrite = true });

                if (response.IsSuccessStatusCode)
                {
                    log.LogInformation($"Document '{id}' successfully updated in {database}/{container}");
                }
                return response;
            }
            catch (Exception ex)
            {
                log.LogError($"Error updating Document: {ex.Message}, Stacktrace: {ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// Update a document based on the filter and patchOperations.
        /// </summary>
        /// <param name="database"></param>
        /// <param name="container"></param>
        /// <param name="filterQuery"></param>
        /// <param name="patchOperations"></param>
        /// <param name="id"></param>
        /// <param name="partition"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<ResponseMessage> UpdateDocument(string database, string container, string filterQuery, List<PatchOperation> patchOperations, string id, string partition, ILogger log)
        {
            try
            {
                PatchItemRequestOptions patchItemRequestOptions = new PatchItemRequestOptions
                {
                    FilterPredicate = filterQuery
                };

                Container _targetContainer = _cosmosClient.GetDatabase(database).GetContainer(container);

                var response = await _targetContainer.PatchItemStreamAsync(id, new PartitionKey(partition), patchOperations, patchItemRequestOptions);

                if (response.IsSuccessStatusCode)
                {
                    log.LogInformation($"Document update successful in {database}/{container} for query '{filterQuery}'");
                }
                return response;
            }
            catch (Exception ex)
            {
                log.LogError($"Error updating Document: {ex.Message}, Stacktrace: {ex.StackTrace}");
                throw ex;
            }
        }

        public static async Task<ResponseMessage> UpsertDocument(string database, string container, string partition, string document, ILogger log)
        {
            try
            {
                _cosmosContainer = _cosmosClient.GetDatabase(database).GetContainer(container);

                Stream payload = new MemoryStream(Encoding.UTF8.GetBytes(document));
                var response = await _cosmosContainer.UpsertItemStreamAsync(payload, new PartitionKey(partition), new ItemRequestOptions { EnableContentResponseOnWrite = true });

                if (response.IsSuccessStatusCode)
                {
                    log.LogInformation($"Document upsert successful in {database}/{container}");
                }
                return response;
            }
            catch (Exception ex)
            {
                log.LogError($"Error adding a Document: {ex.Message}, Stacktrace: {ex.StackTrace}");
                throw ex;
            }
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
                _cosmosContainer = _cosmosClient.GetDatabase(database).GetContainer(container);

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

                return null;
            }
            catch (Exception ex)
            {
                log.LogError($"Error reading Document: {ex.Message}, Stacktrace: {ex.StackTrace}");
                throw ex;
            }
        }
    }
}
