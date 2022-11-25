using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MCD.FN.ManageGit
{
    public static class CoreFunction
    {
        public const string RCT = "RCT";
        public const string RTP = "RTP";
        public const string RCTS = "RCTS";
        public const string DST = "DST";
/*
        private static string key = TelemetryConfiguration.Active.InstrumentationKey = Helper.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY", EnvironmentVariableTarget.Process);

        private static TelemetryClient telemetry = new TelemetryClient()
        {
            InstrumentationKey = key
        };
*/
        [FunctionName("CoreFunction")]
        public static async System.Threading.Tasks.Task RunAsync([CosmosDBTrigger(
            databaseName: "%ApplicationSettings_DatabaseId%",
            collectionName: "%RestaurantConfiguration_CollectionId%",
            ConnectionStringSetting = "CosmosDBConnection",
            CreateLeaseCollectionIfNotExists=true,
            LeaseCollectionName = "%RestaurantConfigurationLeases_CollectionId%")]IReadOnlyList<Document> documents, ExecutionContext context, ILogger log)
          {
            string currentStoreId = string.Empty;
            HttpClient httpClient = new HttpClient();

            try
            {
               /* telemetry.Context.Operation.Id = context.InvocationId.ToString();
                telemetry.Context.Operation.Name = "GetHashKey";*/

                if (documents != null && documents.Count > 0)
                {
                    log.LogInformation("In the Function " + documents.Count);
                    for (int singleDoc = 0; singleDoc < documents.Count; singleDoc++)
                    {
                        log.LogInformation("Processing document # " + singleDoc + " of " + documents.Count);

                        //Deserialize Store Object
                        var storeObject = JsonConvert.DeserializeObject<Store>(documents[singleDoc].ToString());
                        currentStoreId = storeObject.storeId;

                        var storeResults = new StoreResults();
                        TriggerMessage triggerMessage = new TriggerMessage();

                        ValidateStoreObject validateStoreObject = new ValidateStoreObject();

                        if (validateStoreObject.validate(storeObject))
                        {
                            //Scheduling Capability
                            if (storeObject.scheduledDate=="" || storeObject.scheduledDate==null)
                            {
                                log.LogInformation("Validation succesfully completed.");
                                storeResults = Process(storeObject, storeResults, log);
                            }
                            else
                            {
                                log.LogWarning("Validation Succesful, but skipped processing as not scheduled for now.");
                                storeResults.errorList = "Validation Succesful, but skipped processing as it is scheduled.";
                                //int docCount = singleDoc + 1;
                                //await Helper.SendEmailAsync("Interim Deployment Console Error For Store " + storeObject.storeId, "An error occured while Processing document # " + docCount + " of " + documents.Count + ". The error occured during processing of Store:" + storeObject.storeId + ". Please refer to lookup tab for more details");
                            }
                        }
                        else
                        {
                            log.LogWarning("Validation Not Succesful. Moving to write error and exit.");
                            log.LogError(storeResults.errorList);
                            storeResults.errorList = Helper.ErrorToWrite(validateStoreObject.ErrorList());
                        }

                        triggerMessage.message = $"id:{storeObject.id},market:{storeObject.market},storeId:{storeObject.market}{storeObject.storeId},deploymentId:{storeObject.deploymentId},workflowTemplate:{storeObject.workflowTemplate},workflowName:{storeObject.workflowName}";
                        
                        if (storeResults.RTPBranchRemoteUrl != null)
                        {
                            triggerMessage.status = Environment.GetEnvironmentVariable("SuccessResponse");
                        }
                        else
                        {
                            triggerMessage.status = Environment.GetEnvironmentVariable("FailedResponse");
                        }

                        await SendEvent(triggerMessage, httpClient, log);
                        log.LogInformation("Finished processing and now logging into the logs collections.");
                        await Helper.AddtoRTPHistory(storeObject, storeResults);
                    }

                }
            }
            catch (Exception e)
            {
                await Helper.SendEmailAsync("Interim Deployment Console Error For Store " + currentStoreId, "Message: " + e.Message + "  Inner Exception :" + e.InnerException + "----Please refer to lookup tab for more details or reach out to support team.");
                log.LogInformation($"Caught Exception in Run: {e.Message}, Source:{e.Source},InnerException {e.InnerException}");
            }
        }

        public static StoreResults Process(Store store, StoreResults storeResults, ILogger log)
        {
            try
            {
                var _gitClient = GitFunctions.Authenticate(log);
                log.LogInformation("Succesfully Logged for Store:" + store.storeId);
                GitRef _gitRepoRCTStateBranch = null;
                GitRef _gitBranchRCT = null;
                GitRef _gitNewBranchRCTState = null;
                GitRef _gitNewBranchRCTMarket = null;
                var _processedMessage = string.Empty;

                Boolean blnDCT = false;
                var RepositoryName = store.rtpType;

                #region RCT State Repository
                log.LogInformation("Starting Clone Branch for  RCT State Branch for Store: " + store.storeId);
                var _gitRepoRCTState = GitFunctions.FindRepository(_gitClient, Helper.GetEnvironmentVariable("DevOpsProjectName"), Helper.RepositoryName(store, RCTS), log);
                if (_gitRepoRCTState != null)
                {
                    _gitRepoRCTStateBranch = GitFunctions.FindBranch(_gitClient, _gitRepoRCTState, store.profile);
                    if (_gitRepoRCTStateBranch != null)
                    {
                        if (GitFunctions.FindBranch(_gitClient, _gitRepoRCTState, Helper.CloneBranchName(store)) == null)
                        {
                            GitFunctions.Clone(_gitClient, _gitRepoRCTState, _gitRepoRCTStateBranch, store, log);
                            var newBranch = GitFunctions.FindBranch(_gitClient, _gitRepoRCTState, Helper.CloneBranchName(store));
                            AddSecurityFile(_gitClient, _gitRepoRCTState, newBranch, string.Empty, store, true, log);
                            newBranch = GitFunctions.FindBranch(_gitClient, _gitRepoRCTState, Helper.CloneBranchName(store));
                            GitFunctions.RemoveReadMe(_gitClient, _gitRepoRCTState, newBranch, log);
                            log.LogInformation("Completed Processing RCT State Branch. Repository Name:" + Helper.RepositoryName(store, RCTS) + " , Branch Name:" + Helper.CloneBranchName(store) + ", Store:" + store.storeId);
                        }
                        else
                        {
                            _processedMessage = "RCT State Branch to clone already exists. No Action. Repository Name:" + Helper.RepositoryName(store, RCTS) + " , Branch Name:" + Helper.CloneBranchName(store) + ", Store:" + store.storeId;
                            log.LogWarning(_processedMessage);
                        }
                        _gitNewBranchRCTState = GitFunctions.FindBranch(_gitClient, _gitRepoRCTState, Helper.CloneBranchName(store));
                    }
                    else
                    {
                        _processedMessage = "RCT State Source Branch does not exists. No Action. Repository Name:" + Helper.RepositoryName(store, RCTS) + ", Store:" + store.storeId;
                        log.LogWarning(_processedMessage);
                    }
                }
                else
                {
                    _processedMessage = "RCT State Source Repository does not exists. No Action. Repository Name:" + Helper.RepositoryName(store, RCTS) + ", Store:" + store.storeId;
                    log.LogWarning(_processedMessage);
                }
                #endregion



                #region RCT Market
                //Check for the Repository 2
                log.LogInformation("Starting Provisioning RCT Repository for Store:" + store.storeId);
                var _gitRepoRCT = GitFunctions.FindRepository(_gitClient, Helper.GetEnvironmentVariable("DevOpsProjectName"), Helper.RepositoryName(store, RCT), log);
                //If not found, then need to create a Repository
                if (_gitRepoRCT != null)
                {
                    //Check for the Branch with above repository
                    _gitBranchRCT = GitFunctions.FindBranch(_gitClient, _gitRepoRCT, store.profile);
                    //If not found, then create a new branch.
                    if (_gitBranchRCT != null)
                    {
                        // Only Create Branch if not exists already.
                        if (GitFunctions.FindBranch(_gitClient, _gitRepoRCT, Helper.CloneBranchName(store)) == null)
                        {
                            GitFunctions.Clone(_gitClient, _gitRepoRCT, _gitBranchRCT, store, log);
                            var newBranch = GitFunctions.FindBranch(_gitClient, _gitRepoRCT, Helper.CloneBranchName(store));
                            AddSecurityFile(_gitClient, _gitRepoRCT, newBranch, "security/", store, true, log);
                            newBranch = GitFunctions.FindBranch(_gitClient, _gitRepoRCT, Helper.CloneBranchName(store));
                            GitFunctions.RemoveReadMe(_gitClient, _gitRepoRCT, newBranch, log);
                            log.LogInformation("Completed Processing RCT Market Branch. Repository Name:" + Helper.RepositoryName(store, RCT) + " , Branch Name:" + Helper.CloneBranchName(store) + ", Store:" + store.storeId);
                        }
                        else
                        {
                            _processedMessage = "RCT Market Branch to clone already exists. No Action. Repository Name:" + Helper.RepositoryName(store, RCT) + " , Branch Name" + Helper.CloneBranchName(store) + ", Store:" + store.storeId;
                            log.LogWarning(_processedMessage);
                        }
                        _gitNewBranchRCTMarket = GitFunctions.FindBranch(_gitClient, _gitRepoRCT, Helper.CloneBranchName(store));
                    }
                    else
                    {
                        _processedMessage = "RCT Market Source Branch does not exists. No Action. Repository Name:" + Helper.RepositoryName(store, RCTS) + ", Store:" + store.storeId;
                        log.LogInformation(_processedMessage);
                    }
                }
                else
                {
                    _processedMessage = "RCT Market Source Repository does not exists. No Action. Repository Name:" + Helper.RepositoryName(store, RCT) + ", Store:" + store.storeId;
                    log.LogInformation(_processedMessage);
                }

                //Copy Files From Internal to the branch.
                //_gitBranchRCT = GitFunctions.FindBranch(_gitClient, _gitRepoRCT, Helper.BranchName(store, RCT));

                #endregion

                if (store.rtpType == DST)
                {
                    blnDCT = true;
                    RepositoryName = DST;
                }
                //else
                //{
                //    RepositoryName = "RTP";
                //}

                #region RTP Repository
                //Check for the Repository 1
                log.LogInformation("Start Provisioning " + RepositoryName + " Repository for Store: " + store.storeId);
                var _gitRepoRTP = GitFunctions.FindRepository(_gitClient, Helper.GetEnvironmentVariable("DevOpsProjectName"), Helper.RepositoryName(store, RTP), log);

                //If not found, then need to create a Repository
                if (_gitRepoRTP == null)
                {
                    _processedMessage = RepositoryName + " Repository does not exists. Creating new Repository. Repository Name:" + Helper.RepositoryName(store, RTP) + ", Store:" + store.storeId;
                    log.LogInformation(_processedMessage);
                    _gitRepoRTP = GitFunctions.CreateRepository(_gitClient, Helper.RepositoryName(store, RTP), Helper.GetEnvironmentVariable("DevOpsProjectName"),
                        Helper.GetEnvironmentVariable("DevOpsGitPath"));
                }

                //Check for the Branch with above repository
                var _gitBranchRTP = GitFunctions.FindBranch(_gitClient, _gitRepoRTP, Helper.BranchName(store, RTP));

                if (_gitBranchRTP == null)
                {
                    _processedMessage = RepositoryName + " Branch does not exists. Creating New Branch . Repository Name:" + Helper.RepositoryName(store, RTP) + " , Branch Name:" + Helper.BranchName(store, RTP) + ", Store:" + store.storeId;
                    log.LogInformation(_processedMessage);
                    GitFunctions.CreateBranch(_gitClient, _gitRepoRTP, Helper.BranchName(store, RTP));
                    _gitBranchRTP = GitFunctions.FindBranch(_gitClient, _gitRepoRTP, Helper.BranchName(store, RTP));
                    GitFunctions.RemoveReadMe(_gitClient, _gitRepoRTP, _gitBranchRTP, log);

                }
                else
                {
                    _processedMessage = RepositoryName + " Market Branch already exists. No Action. Repository Name:" + Helper.RepositoryName(store, RTP) + " , Branch Name:" + Helper.BranchName(store, RTP) + ", Store:" + store.storeId;
                    log.LogInformation(_processedMessage);
                }

                //Remove Files
                //GitFunctions.RemoveFilesNotInSource(_gitClient, _gitRepoRTP, store, log);

                //return null;

                //Update Plugin Information
                _gitBranchRTP = GitFunctions.FindBranch(_gitClient, _gitRepoRTP, Helper.BranchName(store, RTP));
                AddPluginFileInfo(_gitClient, _gitRepoRTP, _gitBranchRTP, store, log);
                _gitBranchRTP = GitFunctions.FindBranch(_gitClient, _gitRepoRTP, Helper.BranchName(store, RTP));


                _processedMessage = RepositoryName + " Repository and Branch is ready. Starting Copying Files From Source. Repository Name:" + Helper.RepositoryName(store, RTP) + " , Branch Name:" + Helper.BranchName(store, RTP) + ", Store:" + store.storeId;
                log.LogInformation(_processedMessage);
                //Copy Files From Internal to the branch.
                GitFunctions.Copy(_gitClient, _gitRepoRTP, _gitBranchRTP, store, log);

                //if (!blnDCT)
                //{
                    List<AppComponent> appComponents = Helper.ReadApplicationSettings();
                    if (appComponents != null && appComponents.Count > 0)
                    {
                        log.LogInformation("Read all components and now starting to update WildCard files");
                        _gitBranchRTP = GitFunctions.FindBranch(_gitClient, _gitRepoRTP, Helper.BranchName(store, RTP));
                        GitFunctions.UpdateFiles(appComponents, _gitClient, _gitRepoRTP, _gitBranchRTP, store, log);
                    }
                    else
                    {
                        log.LogWarning("Issue reading Application Settings. Aborting wild card updates.");
                    }
                //}
                #endregion

                ProcessLicense(store, log);

                if (_gitRepoRCTState != null && _gitNewBranchRCTState != null)
                {
                    //Convert URLs to friendly URLs viewable by browser.
                    //storeResults.RCTStateBranchFriendlyURL = "https://" + _gitRepoRCTState.RemoteUrl.Substring(_gitRepoRCTState.RemoteUrl.IndexOf('@') + 1) + "?version=GB" + _gitNewBranchRCTState.Name.Substring(11);
                    storeResults.RCTStateBranchRemoteURL = _gitRepoRCTState.RemoteUrl + "?version=GB" + _gitNewBranchRCTState.Name.Substring(11);
                }
                if (_gitRepoRCT != null && _gitNewBranchRCTMarket != null)
                {
                    //storeResults.RCTBranchFriendlyURL = "https://" + _gitRepoRCT.RemoteUrl.Substring(_gitRepoRCT.RemoteUrl.IndexOf('@') + 1) + "?version=GB" + _gitNewBranchRCTMarket.Name.Substring(11);
                    storeResults.RCTBranchRemoteUrl = _gitRepoRCT.RemoteUrl + "?version=GB" + _gitNewBranchRCTMarket.Name.Substring(11);
                }

                if (_gitRepoRTP != null && _gitBranchRTP != null)
                {
                    //storeResults.RTPBranchFriendlyURL = "https://" + _gitRepoRTP.RemoteUrl.Substring(_gitRepoRTP.RemoteUrl.IndexOf('@') + 1) + "?version=GB" + _gitBranchRTP.Name.Substring(11);
                    storeResults.RTPBranchRemoteUrl = _gitRepoRTP.RemoteUrl + "?version=GB" + _gitBranchRTP.Name.Substring(11);
                }
                //Replace WildCards

                return storeResults;
            }
            catch (Exception e)
            {
                log.LogError($"Caught Exception in Process: {e.Message}, Source:{e.Source},InnerException {e.InnerException}, StackTrace {e.StackTrace} ");
                if (storeResults != null)
                {
                    storeResults.errorList = $"Caught Exception in Process: {e.Message}, Source:{e.Source},InnerException {e.InnerException}, StackTrace {e.StackTrace}";
                    return storeResults;
                }
                return null;
            }
        }

        private static void AddSecurityFile(GitHttpClient _gitClient, GitRepository _gitRepo, GitRef _gitDestinationBranch, String path, Store store, bool _base64Encoded, ILogger log)
        {
            string FileName = path + "security.data";
            if (!string.IsNullOrEmpty(store.securityData))
            {
                GitFunctions.AddSecurityFile(_gitClient, _gitRepo, _gitDestinationBranch, FileName, store.securityData, true, log);

            }
            else
            {
                log.LogInformation("Skipping Adding Security.Data File to as property is empty");
            }
        }

        private static void ProcessLicense(Store store, ILogger log)
        {
            try
            {
                JObject jObject = JObject.Parse("{}");
                jObject.Add("market", store.market);
                jObject.Add("storeId", store.storeId);
                var _url = System.Environment.GetEnvironmentVariable("LicenseServer_URL");
                HttpWebRequest rqst = (HttpWebRequest)HttpWebRequest.Create(_url);
                rqst.Method = "POST";
                rqst.ContentType = "application/json";
                byte[] byteData = Encoding.UTF8.GetBytes(jObject.ToString());
                rqst.ContentLength = byteData.Length;
                using (Stream postStream = rqst.GetRequestStream())
                {
                    postStream.Write(byteData, 0, byteData.Length);
                    postStream.Close();
                }
                log.LogInformation("Pushed data to License Server");
                StreamReader rsps = new StreamReader(rqst.GetResponse().GetResponseStream());
                string strRsps = rsps.ReadToEnd();
                if (strRsps == "")
                {
                    strRsps = "EMPTY";
                }
                log.LogInformation("Response from the License Server Request-" + strRsps);
            }
            catch (Exception e)
            {
                log.LogError($"Exception occured processing license: {e.Message}, Source:{e.Source},InnerException {e.InnerException}, StackTrace {e.StackTrace} ");

            }
        }

        private static void AddPluginFileInfo(GitHttpClient _gitClient, GitRepository _gitRepo, GitRef _gitDestinationBranch, Store store, ILogger log)
        {
            string FileName = "rtpVersion.json";
            GitFunctions.AddPlugInFileInfo(_gitClient, _gitRepo, _gitDestinationBranch, FileName, store, log);
        }


        private static string CreateToken(string resourceUri, string keyName, string key)
        {
            TimeSpan sinceEpoch = DateTime.UtcNow - new DateTime(1970, 1, 1);
            var week = 60 * 60 * 24 * 7;
            var expiry = Convert.ToString((int)sinceEpoch.TotalSeconds + week);
            string stringToSign = System.Web.HttpUtility.UrlEncode(resourceUri) + "\n" + expiry;
            HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
            var sasToken = String.Format(CultureInfo.InvariantCulture, "SharedAccessSignature sr={0}&sig={1}&se={2}&skn={3}", HttpUtility.UrlEncode(resourceUri), HttpUtility.UrlEncode(signature), expiry, keyName);
            return sasToken;
        }

        //Sends Event To Event Hub
        public async static Task SendEvent(TriggerMessage triggerMessage, HttpClient httpClient, ILogger log)
        {
            httpClient.Dispose();
            httpClient = new HttpClient();
            string keyValue = Environment.GetEnvironmentVariable("EventHubNamespaceKeyValue");
            string sasToken = CreateToken(Environment.GetEnvironmentVariable("EventHubNamespaceUrl") + Environment.GetEnvironmentVariable("ResourceUri"), Environment.GetEnvironmentVariable("EventHubNamespaceKeyName"), keyValue);

            HttpRequestMessage requestPost = new HttpRequestMessage(new HttpMethod(Environment.GetEnvironmentVariable("UseHttpMethod")), "");
            httpClient.BaseAddress = new Uri(Environment.GetEnvironmentVariable("EventHubNamespaceUrl"));
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(Environment.GetEnvironmentVariable("ContentTypeKey"), Environment.GetEnvironmentVariable("ContentType"));
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(Environment.GetEnvironmentVariable("HostKey"), Environment.GetEnvironmentVariable("HostValue"));
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(Environment.GetEnvironmentVariable("AuthKey"), sasToken);

            var contentEventSend = new StringContent(JsonConvert.SerializeObject(triggerMessage));
            var responseFromEventHub = await httpClient.PostAsync(Environment.GetEnvironmentVariable("ResourceUri"), contentEventSend);

            log.LogInformation($"Store Configuration Change feed function posted response to EventHub : {responseFromEventHub.ToString()}");

            // Added a delay of 3 seconds.
            await Task.Delay(3000);
        }
    }
}
