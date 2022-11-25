using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MCD.FN.ManageGit.Models;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace MCD.FN.ManageGit
{
    public static class StoreVersionFunction
    {
        [FunctionName("StoreVersionFunction")]
        public static async System.Threading.Tasks.Task RunAsync([CosmosDBTrigger(
            databaseName: "%ApplicationSettings_DatabaseId%",
            collectionName: "%StoreVersionConfiguration_CollectionId%",
            ConnectionStringSetting = "CosmosDBConnection",
            CreateLeaseCollectionIfNotExists=true,
            LeaseCollectionPrefix = "StoreVersionFunction",
            LeaseCollectionName = "%StoreVersionConfigurationLeases_CollectionId%")]IReadOnlyList<Document> documents, ExecutionContext context, ILogger log)
        {
            string currentStoreId = string.Empty;

            try
            {
                if (documents != null && documents.Count > 0)
                {
                    log.LogInformation("In the StoreVersionFunction " + documents.Count);
                    for (int singleDoc = 0; singleDoc < documents.Count; singleDoc++)
                    {
                        log.LogInformation("Processing document # " + singleDoc + " of " + documents.Count);

                        //Deserialize Store Object
                        var storeDocumentObject = JsonConvert.DeserializeObject<StoreDocument>(documents[singleDoc].ToString());

                        var storeVersionObject = storeDocumentObject.StoreVersion;
                        currentStoreId = storeDocumentObject.StoreId;

                        var tempDirPath = BlobUploadService.GetStoreVersionFolder(currentStoreId);

                        //Saving file locally
                        using (FileStream fs = File.Create(Path.Combine(tempDirPath, BlobUploadService.storeVersionFileName)))
                        {
                            var content = JsonConvert.SerializeObject(storeVersionObject, Formatting.Indented);
                            byte[] info = new UTF8Encoding(true).GetBytes(content);
                            // Adding content to the file.
                            fs.Write(info, 0, info.Length);
                        }

                        //Uploading files to blob
                        BlobUploadService.UploadStoreVersionFile(currentStoreId, log);
                    }
                }
            }
            catch (Exception e)
            {
                await Helper.SendEmailAsync("Interim Deployment Console Error For Store " + currentStoreId, "Message: " + e.Message + "  Inner Exception :" + e.InnerException + "----Please refer to lookup tab for more details or reach out to support team.");
                log.LogInformation($"Caught Exception in StoreVersionFunction: {e.Message}, Source:{e.Source},InnerException {e.InnerException}");
            }            
        }     
    }
}
