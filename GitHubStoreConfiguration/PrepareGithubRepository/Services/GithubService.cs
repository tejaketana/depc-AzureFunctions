using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PrepareGithubRepo.Core;
using PrepareGithubRepository.Core;
using PrepareGithubRepository.Models;
using System;
using System.Threading.Tasks;

namespace PrepareGithubRepo.Services
{
    /// <summary>
    /// Contains service related methods for GitHub.
    /// </summary>
    public static class GithubService
    {
        #region Private Methods
        /// <summary>
        /// Create a new Github Repository - CURRENTLY BLOCKED.
        /// </summary>
        /// <param name="market"></param>
        /// <param name="repository"></param>
        /// <param name="marketStoreId"></param>
        /// <param name="deploymentModel"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static async Task<bool> CreateNewRepository(string repository, string marketStoreId, ILogger log)
        {
            bool repositoryCreated = false;

            // Call the GitHub Create method.
            if (await GithubFunctions.CreateRepository(repository, marketStoreId, log) != null)
            {
                repositoryCreated = true;
            }

            return repositoryCreated;
        }

        /// <summary>
        /// Create a new Branch.
        /// </summary>
        /// <param name="market"></param>
        /// <param name="repository"></param>
        /// <param name="marketStoreId"></param>
        /// <param name="deploymentModel"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static async Task<bool> CreateNewBranch(string market, string repository, string branch, string rtpType, string path, ILogger log)
        {
            Octokit.Reference newBranch = null;
            bool branchCreated = false;
            JObject emptyConfig = JObject.Parse("{}");

            // Get the Repository Id.
            Octokit.Repository targetRepository = await GithubFunctions.FindRepository(repository, log);

            if (rtpType == GlobalConstants.RtpType.DST.ToString())
                newBranch = await GithubFunctions.CreateBranch(repository, targetRepository.Id, branch, path, emptyConfig, log);
            else if (rtpType == GlobalConstants.RtpType.SC.ToString())
            {
                // Create new Branch for SC.
                newBranch = await GithubFunctions.CreateBranch(repository, targetRepository.Id, branch, string.Empty, null, log);
            }

            if (newBranch != null)
            {
                branchCreated = true;
                log.LogInformation($"New Branch created: {branch}; Ref: {newBranch.Ref}; Url: {newBranch.Url}");
            }

            return branchCreated;
        }

        /// <summary>
        /// Update the rtpVersion for the store.
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="storeDeploymentDocument"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static async Task<bool> UpdateRtpVersionConfig(string repository, StoreDeploymentDocument storeDeploymentDocument, ILogger log)
        {
            try
            {
                string commitMessage = string.Format(Environment.GetEnvironmentVariable("CommitMessage"), GlobalConstants.RTP_VERSION_JSON);

                JObject rtpVersionObject = new JObject();
                rtpVersionObject.Add("rtpVersion", string.Format(Environment.GetEnvironmentVariable("RTPS_Source_Branch_Template"), storeDeploymentDocument.rtpVersion));

                string contents = JsonConvert.SerializeObject(rtpVersionObject);

                var updatedConfig = await GithubFunctions.CreateOrUpdateFile(repository, storeDeploymentDocument.storeId,
                                    Environment.GetEnvironmentVariable("RTPVersion_Json_File_Path"),
                                    storeDeploymentDocument.createdBy, storeDeploymentDocument.createdByUserId, commitMessage,
                                    contents, log);

                return updatedConfig;
            }
            catch (Exception ex)
            {
                log.LogError($"Error in GithubService.UpdateRtpVersionConfig: {ex.Message}, Stacktrace: {ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// Updates the store configurations for Distributed or Server Centric.
        /// Store configurations processed --> storeversion.json (DST, SC), localcontainerversion.json (SC).
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="storeDeploymentDocument"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static async Task<bool> UpdateStoreConfigs(string repository, StoreDeploymentDocument storeDeploymentDocument, ILogger log)
        {
            try
            {
                int updated = 0;

                // How many Store Configs are to be updated?
                if (storeDeploymentDocument.storeConfigs.Count > 0)
                {
                    string location = string.Empty;
                    string configFile = string.Empty;
                    JObject storeConfig = null;

                    for (int i = 0; i < storeDeploymentDocument.storeConfigs.Count; i++)
                    {
                        location = storeDeploymentDocument.storeConfigs[i].GetValue("location").ToString();
                        configFile = storeDeploymentDocument.storeConfigs[i].GetValue("configFile").ToString();

                        storeConfig = JObject.Parse(storeDeploymentDocument.storeConfigs[i].GetValue(configFile).ToString());

                        if (storeConfig != null)
                        {
                            // Update the Config file(s).
                            string currentConfigContents = string.Empty;
                            JObject updatedConfig = null;

                            if (await GithubFunctions.IsBlobExists(repository, storeDeploymentDocument.storeId, location, log))
                            {
                                currentConfigContents = await GithubFunctions.GetFileContents(repository, storeDeploymentDocument.storeId, location, log);
                                updatedConfig = ReplaceStoreConfigTags(JObject.Parse(currentConfigContents), storeConfig, log);
                            }
                            else
                            {
                                updatedConfig = storeConfig;
                            }

                            string commitMessage = string.Format(Environment.GetEnvironmentVariable("CommitMessage"), 
                                GlobalConstants.STORE_VERSION_JSON.StartsWith(configFile) ? GlobalConstants.STORE_VERSION_JSON :
                                GlobalConstants.LOCAL_CONTAINER_VERSION_JSON.StartsWith(configFile) ? GlobalConstants.LOCAL_CONTAINER_VERSION_JSON : "");

                            currentConfigContents = JsonConvert.SerializeObject(updatedConfig, Formatting.Indented);
                            if (await GithubFunctions.CreateOrUpdateFile(repository, storeDeploymentDocument.storeId, location, storeDeploymentDocument.createdBy, storeDeploymentDocument.createdByUserId, commitMessage, currentConfigContents, log))
                            {
                                updated++;
                                log.LogInformation($"Successfully updated Repos: {repository}, Branch: {storeDeploymentDocument.storeId}, Store config: {location}");
                            }
                            else
                            {
                                log.LogError($"Failed to update Repos: {repository}, Branch: {storeDeploymentDocument.storeId}, Store config: {location}");
                            }
                        }
                    }
                }
                return (storeDeploymentDocument.storeConfigs.Count == updated) ? true : false;
            }
            catch (Exception ex)
            {
                log.LogError($"Error in GithubService.UpdateStoreConfigs: {ex.Message}, Stacktrace: {ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// Replace tag values in a store configuration.
        /// </summary>
        /// <param name="storeConfig"></param>
        /// <param name="inputDocumentConfig"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static JObject ReplaceStoreConfigTags(JObject storeConfig, JObject inputDocumentConfig, ILogger log)
        {
            try
            {
                foreach (var property in inputDocumentConfig.Properties())
                {
                    switch (inputDocumentConfig[property.Name].Type)
                    {
                        case JTokenType.Object:
                            break;
                        case JTokenType.Array:
                            bool matchFound = false;

                            for (int index = 0; index < ((JArray)inputDocumentConfig[property.Name]).Count; index++, matchFound = false)
                            {
                                JObject eachConfig = ((JArray)inputDocumentConfig[property.Name])[index].ToObject<JObject>();

                                foreach (var configItem in storeConfig[property.Name].Children())
                                {
                                    if (configItem["packageType"].Value<string>() == eachConfig["packageType"].Value<string>()
                                        && configItem["target"].Value<string>() == eachConfig["target"].Value<string>())
                                    {
                                        matchFound = true;

                                        if (DoesTemplateHaveNewTags((JObject)eachConfig, (JObject)configItem))
                                        {
                                            ((JObject)configItem).ReplaceAll(eachConfig.Properties());
                                        }
                                        else
                                        {
                                            foreach (var configProperty in eachConfig.Properties())
                                            {
                                                configItem[configProperty.Name].Replace(configProperty.Value.ToString());
                                            }
                                        }
                                    }
                                }

                                if (!matchFound)
                                {
                                    ((JArray)storeConfig[property.Name]).Add(eachConfig);
                                }
                            }
                            break;
                        default:
                            if (!string.IsNullOrEmpty(inputDocumentConfig[property.Name].Value<string>()))
                                storeConfig[property.Name] = inputDocumentConfig[property.Name].Value<string>();
                            break;
                    }
                }
                return storeConfig;
            }
            catch (Exception ex)
            {
                log.LogError($"Error in GithubService.ReplaceStoreConfigTags: {ex.Message}, Stacktrace: {ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// Check if there are new tags in the GitHub template mapping definition.
        /// </summary>
        /// <param name="template"></param>
        /// <param name="storeConfig"></param>
        /// <returns></returns>
        private static bool DoesTemplateHaveNewTags(JObject template, JObject storeConfig)
        {
            foreach (var configProperty in template.Properties())
            {
                if (storeConfig[configProperty.Name] == null)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Update properties in "storePackages" after deploymentId is confirmed.
        /// </summary>
        /// <param name="storeConfig"></param>
        /// <param name="inputDocumentConfig"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static JObject UpdateTagsInStorePackages(string storeId, JObject storeConfig, JObject inputDocumentConfig, ILogger log)
        {
            try
            {
                foreach (var property in inputDocumentConfig.Properties())
                {
                    switch (inputDocumentConfig[property.Name].Type)
                    {
                        case JTokenType.Array:
                            for (int index = 0; index < ((JArray)inputDocumentConfig[property.Name]).Count; index++)
                            {
                                JObject eachConfig = ((JArray)inputDocumentConfig[property.Name])[index].ToObject<JObject>();

                                foreach (var configItem in storeConfig[property.Name].Children())
                                {
                                    if (configItem["packageType"].Value<string>() == eachConfig["packageType"].Value<string>()
                                        && configItem["target"].Value<string>() == eachConfig["target"].Value<string>())
                                    {
                                        // Last check with the deploymentId...
                                        if (configItem["deploymentId"].Value<string>() == eachConfig["deploymentId"].Value<string>())
                                        {
                                            if (DoesTemplateHaveNewTags((JObject)eachConfig, (JObject)configItem))
                                            {
                                                ((JObject)configItem).ReplaceAll(eachConfig.Properties());
                                            }
                                            else
                                            {
                                                foreach (var configProperty in eachConfig.Properties())
                                                {
                                                    configItem[configProperty.Name].Replace(configProperty.Value.ToString());
                                                }
                                            }
                                        }
                                        else
                                        {
                                            log.LogWarning($"Attempt to update deploymentId {eachConfig["deploymentId"].Value<string>()} in '{storeId}' storeversion unsuccessful. deploymentId {configItem["deploymentId"].Value<string>()} is scheduled");
                                            return null;
                                        }
                                    }
                                }
                            }
                            break;
                        default:
                            // Do nothing.
                            break;
                    }
                }

                return storeConfig;
            }
            catch (Exception ex)
            {
                log.LogError($"Error in GithubService.UpdateCancelFlagInStorePackages: {ex.Message}, Stacktrace: {ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// Updates the store configurations for Distributed or Server Centric.
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="storeDeploymentDocument"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static async Task<bool> UpdateTagsInStoreConfigs(string repository, StoreDeploymentDocument storeDeploymentDocument, ILogger log)
        {
            try
            {
                int updated = 0;

                // How many Store Configs are to be updated?
                if (storeDeploymentDocument.storeConfigs.Count > 0)
                {
                    string location = string.Empty;
                    string configFile = string.Empty;
                    JObject storeConfig = null;

                    for (int i = 0; i < storeDeploymentDocument.storeConfigs.Count; i++)
                    {
                        location = storeDeploymentDocument.storeConfigs[i].GetValue("location").ToString();
                        configFile = storeDeploymentDocument.storeConfigs[i].GetValue("configFile").ToString();

                        storeConfig = JObject.Parse(storeDeploymentDocument.storeConfigs[i].GetValue(configFile).ToString());

                        if (storeConfig != null)
                        {
                            // Update the Config file(s).
                            string currentConfigContents = string.Empty;
                            JObject updatedConfig = null;

                            if (await GithubFunctions.IsBlobExists(repository, storeDeploymentDocument.storeId, location, log))
                            {
                                currentConfigContents = await GithubFunctions.GetFileContents(repository, storeDeploymentDocument.storeId, location, log);

                                updatedConfig = UpdateTagsInStorePackages(storeDeploymentDocument.storeId, JObject.Parse(currentConfigContents), storeConfig, log);

                                if (updatedConfig != null)
                                {
                                    string commitMessage = string.Format(Environment.GetEnvironmentVariable("CommitMessage"),
                                        GlobalConstants.STORE_VERSION_JSON.StartsWith(configFile) ? GlobalConstants.STORE_VERSION_JSON :
                                        GlobalConstants.LOCAL_CONTAINER_VERSION_JSON.StartsWith(configFile) ? GlobalConstants.LOCAL_CONTAINER_VERSION_JSON : "");

                                    currentConfigContents = JsonConvert.SerializeObject(updatedConfig, Formatting.Indented);
                                    if (await GithubFunctions.CreateOrUpdateFile(repository, storeDeploymentDocument.storeId, location, storeDeploymentDocument.createdBy, storeDeploymentDocument.createdByUserId, commitMessage, currentConfigContents, log))
                                    {
                                        updated++;
                                        log.LogInformation($"Successfully updated Repos: {repository}, Branch: {storeDeploymentDocument.storeId}, Store config: {location}");
                                    }
                                    else
                                    {
                                        log.LogError($"Failed to update Repos: {repository}, Branch: {storeDeploymentDocument.storeId}, Store config: {location}");
                                    }
                                }
                            }
                            else
                            {
                                // This scenario is unlikely but possible.
                                log.LogWarning($"Unable to update Repos: {repository}, Branch: {storeDeploymentDocument.storeId}, Store config: {location} - storeversion does not exist");
                            }
                        }
                    }
                }
                return (storeDeploymentDocument.storeConfigs.Count == updated) ? true : false;
            }
            catch (Exception ex)
            {
                log.LogError($"Error in GithubService.UpdateCancelFlagInStoreConfigs: {ex.Message}, Stacktrace: {ex.StackTrace}");
                throw ex;
            }
        }
        #endregion

        /// <summary>
        /// Calls the Github methods to update the Store configuration based on the document updated in "StoreConfigurationLogs" container.
        /// </summary>
        /// <param name="storeDeploymentDocument"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<bool> UpdateGitHubRepos(StoreDeploymentDocument storeDeploymentDocument, ILogger log)
        {
            try
            {
                bool returnStatus = false;

                // Get the Repository name.
                string repository = Helper.GetRepositoryName(GlobalConstants.RepoType.RTP.ToString(), storeDeploymentDocument.market, storeDeploymentDocument.rtpType);

                if (string.IsNullOrEmpty(repository))
                {
                    log.LogError($"Unable to form Repository name: {repository}");
                }
                else
                {
                    if (GithubFunctions.InitializeClient(log))
                    {
                        if (!await GithubFunctions.IsRepositoryExists(repository, log))
                        {
                            // TODO:-
                            // Create a new Repository, Branch, Store config.
                            //await CreateNewRepository(storeDeploymentDocument.market, repository, log);
                            // THIS IS A BLOCKER AS THESE METHODS ARE THROWING AUTHZ EXCEPTION.

                            // For now, simply log an error.
                            log.LogError($"Repository '{repository}' does not exist!");
                        }

                        if (await GithubFunctions.IsRepositoryExists(repository, log))
                        {
                            if (!await GithubFunctions.IsBranchExists(repository, storeDeploymentDocument.storeId, log))
                            {
                                if (await CreateNewBranch(storeDeploymentDocument.market, repository, storeDeploymentDocument.storeId, storeDeploymentDocument.rtpType, string.Empty, log))
                                {
                                    log.LogInformation("Begin Store Config updation...");
                                }
                            }

                            // In case deployment model is Server Centric.
                            if (storeDeploymentDocument.rtpType == GlobalConstants.RtpType.SC.ToString())
                            {
                                // First run a differential update for the Repo/Branch.
                                var response = await GithubFunctions.CopyFilesFromRTPS(repository, storeDeploymentDocument.storeId, storeDeploymentDocument.market, storeDeploymentDocument.rtpType, storeDeploymentDocument.rtpVersion, storeDeploymentDocument.createdBy, storeDeploymentDocument.createdByUserId, log);

                                if (response)
                                {
                                    // Next, update rtpVersion.json.
                                    var fileResult = await UpdateRtpVersionConfig(repository, storeDeploymentDocument, log);

                                    if (fileResult)
                                        log.LogInformation($"'{Environment.GetEnvironmentVariable("RTPVersion_Json_File_Path")}' updated successfully");
                                    else
                                        log.LogError($"'{Environment.GetEnvironmentVariable("RTPVersion_Json_File_Path")}' update failed");
                                }
                                else
                                {
                                    log.LogError($"Cloning from '{string.Format(Environment.GetEnvironmentVariable("RTPS_Source_Branch_Template"), storeDeploymentDocument.rtpVersion)}' failed");
                                }
                            }

                            if (await UpdateStoreConfigs(repository, storeDeploymentDocument, log))
                            {
                                returnStatus = true;
                                log.LogInformation($"Store Configuration(s) updated for StoreId: {storeDeploymentDocument.storeId}");
                            }
                        }
                    }
                    else
                    {
                        log.LogError("Unable to connect to Github");
                    }
                }
                return returnStatus;
            }
            catch (Exception ex)
            {
                log.LogError($"Error in GithubService.UpdateGitHubRepos: {ex.Message}, Stacktrace: {ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// Calls the Github methods to update a deployment by updating the corresponding property in the Store configuration.
        /// </summary>
        /// <param name="storeDeploymentDocument"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<bool> UpdateDeploymentInGitHubRepos(StoreDeploymentDocument storeDeploymentDocument, ILogger log)
        {
            try
            {
                bool returnStatus = false;

                // Get the Repository name.
                string repository = Helper.GetRepositoryName(GlobalConstants.RepoType.RTP.ToString(), storeDeploymentDocument.market, storeDeploymentDocument.rtpType);

                if (string.IsNullOrEmpty(repository))
                {
                    log.LogError($"Unable to form Repository name: {repository}");
                }
                else
                {
                    if (GithubFunctions.InitializeClient(log))
                    {
                        if (await UpdateTagsInStoreConfigs(repository, storeDeploymentDocument, log))
                        {
                            returnStatus = true;
                            log.LogInformation($"Property updated in Store Configuration(s) for StoreId: {storeDeploymentDocument.storeId}");
                        }
                    }
                    else
                    {
                        log.LogError("Unable to connect to Github");
                    }
                }

                return returnStatus;
            }
            catch (Exception ex)
            {
                log.LogError($"Error in GithubService.CancelDeploymentInGitHubRepos: {ex.Message}, Stacktrace: {ex.StackTrace}");
                throw ex;
            }
        }
    }
}
