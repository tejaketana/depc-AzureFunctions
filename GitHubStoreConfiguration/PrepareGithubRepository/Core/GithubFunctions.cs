using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octokit;
using PrepareGithubRepository.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrepareGithubRepo.Core
{
    /// <summary>
    /// Methods to access Github Enterprise using Octokit client library.
    /// </summary>
    public static class GithubFunctions
    {
        private static string _token = Environment.GetEnvironmentVariable("GithubToken");
        private static GitHubClient _client;
        private static ApiConnection _apiConnection;
        private static GitDatabaseClient _gitDatabaseClient;
        private const string _BASEREFERENCE = "refs/heads/";

        #region Private Methods
        /// <summary>
        /// Used in Server Centric deployment model.
        /// </summary>
        /// <param name="market"></param>
        /// <param name="path"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static bool ProcessPath(string market, string path, ILogger log)
        {
            if (path.StartsWith(GlobalConstants.LOCALIZATION))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Used in Server Centric deployment model.
        /// </summary>
        /// <param name="market"></param>
        /// <param name="path"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static bool LocalizationPath(string market, string path, ILogger log)
        {
            if (path.StartsWith(GlobalConstants.LOCALIZATION + "/" + market))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Used in Server Centric deployment model.
        /// </summary>
        /// <param name="market"></param>
        /// <param name="path"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static string GetDestinationPath(string market, string path, ILogger log)
        {
            var lengthToSkip = (GlobalConstants.LOCALIZATION + "/" + market + "/").Length;
            return path.Substring(lengthToSkip);
        }

        /// <summary>
        /// Used in Server Centric deployment model.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static bool IsGitignoreTemplatePath(string path, ILogger log)
        {
            if (path.EndsWith(GlobalConstants.GITIGNORE_TEMPLATE))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Used in Server Centric deployment model.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        private static string ReplaceGitignoreTemplateName(string path, ILogger log)
        {
            return path.Replace(GlobalConstants.GITIGNORE_TEMPLATE, GlobalConstants.GITIGNORE);
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Initialize GitHub client using the Personal Access Token created in GitHub (use a Service Account).
        /// </summary>
        /// <param name="log"></param>
        /// <returns></returns>
        public static bool InitializeClient(ILogger log)
        {
            bool isInitialized = false;

            try
            {
                var authToken = new Credentials(Environment.GetEnvironmentVariable("GithubToken"));

                _client = new GitHubClient(new ProductHeaderValue("Owner"));
                _client.Credentials = authToken;

                isInitialized = true;
            }
            catch (UnauthorizedAccessException auth)
            {
                log.LogError($"Not authorized to access Github: {auth.Message}, Stacktrace: {auth.StackTrace}");
            }
            catch (Exception ex)
            {
                log.LogError($"Unable to access Github: {ex.Message}, Stacktrace: {ex.StackTrace}");
            }

            return isInitialized;
        }

        /// <summary>
        /// Look for a Repository in GitHub.
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<Repository> FindRepository(string repository, ILogger log)
        {
            Repository targetRepository = null;

            try
            {
                targetRepository = await _client.Repository.Get(Environment.GetEnvironmentVariable("Owner"), repository);
            }
            catch (NotFoundException findException)
            {
                log.LogInformation($"Repository not found: {findException.Message}, Stacktrace: {findException.StackTrace}");
            }
            catch (Exception ex)
            {
                log.LogError($"Unable to access Github: {ex.Message}, Stacktrace: {ex.StackTrace}");
            }

            return targetRepository;
        }

        /// <summary>
        /// Check if a Repository exists.
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<bool> IsRepositoryExists(string repository, ILogger log)
        {
            bool repositoryExists = false;

            try
            {
                if (await _client.Repository.Get(Environment.GetEnvironmentVariable("Owner"), repository) != null)
                {
                    repositoryExists = true;
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Unable to find Repository: {ex.Message}, Stacktrace: {ex.StackTrace}");
            }

            return repositoryExists;
        }

        /// <summary>
        /// Check if a Branch exists.
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="branch"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<bool> IsBranchExists(string repository, string branch, ILogger log)
        {
            bool branchExists = false;

            try
            {
                Branch lookupBranch = await _client.Repository.Branch.Get(Environment.GetEnvironmentVariable("Owner"), repository, branch);

                branchExists = true;
            }
            catch (Exception ex)
            {
                log.LogError($"Unable to find Branch: {ex.Message}, Stacktrace: {ex.StackTrace}");
            }

            return branchExists;
        }

        /// <summary>
        /// Return the lookup branch.
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="branch"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<Branch> FindBranch(string repository, string branch, ILogger log)
        {
            try
            {
                return await _client.Repository.Branch.Get(Environment.GetEnvironmentVariable("Owner"), repository, branch);
            }
            catch (Exception ex)
            {
                log.LogError($"Unable to find Branch: {ex.Message}, Stacktrace: {ex.StackTrace}");
            }

            return null;
        }

        /// <summary>
        /// Check if the Blob exists.
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="branch"></param>
        /// <param name="path"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<bool> IsBlobExists(string repository, string branch, string path, ILogger log)
        {
            bool storeConfigExists = false;

            try
            {
                var fileContents = await _client.Repository.Content.GetRawContentByRef(Environment.GetEnvironmentVariable("Owner"), repository, path, branch);
                storeConfigExists = fileContents != null ? true : storeConfigExists;
            }
            catch (Exception ex)
            {
                log.LogInformation($"Unable to find Repos: {repository}, Branch: {branch}, File: {path}");
                log.LogError($"Unable to find Repos/Branch: {ex.Message}, Stacktrace: {ex.StackTrace}");
            }

            return storeConfigExists;
        }

        /// <summary>
        /// Create a new Repository - add a README.md file, by default.
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="branch"></param>
        /// <param name="path"></param>
        /// <param name="storeConfig"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<Repository> CreateRepository(string market, string repository, ILogger log)
        {
            Repository newRepository = null;

            try
            {
                NewRepository createRepository = new NewRepository(repository);
                createRepository.AutoInit = true;
                createRepository.Description = string.Format(Environment.GetEnvironmentVariable("New_Repository_Description_Label"), market);

                newRepository = await _client.Repository.Create(Environment.GetEnvironmentVariable("Owner"), createRepository);

                if (newRepository != null)
                {
                    log.LogInformation($"Created Repository: {newRepository.Id}, {newRepository.Name}");
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Unable to create Repository: {ex.Message}, Stacktrace: {ex.StackTrace}");
            }

            return newRepository;
        }

        /// <summary>
        /// Create a new Branch - will create a new Branch and add an empty storeversion.json.
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="repositoryId"></param>
        /// <param name="branch"></param>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<Reference> CreateBranch(string repository, long repositoryId, string branch, string path, JObject contents, ILogger log)
        {
            Reference newBranch = null;

            try
            {
                // First get the SHA for the Parent - this will be the first commit in the Repo which contains only a README.md.
                var allCommits = _client.Repository.Commit.GetAll(repositoryId);
                log.LogInformation($"Found SHA value: {allCommits.Result.Where(c => c.Parents.Count == 0).FirstOrDefault().Sha} for first commit in Repository: {repositoryId}");

                if (allCommits.Result.Where(c => c.Parents.Count == 0).FirstOrDefault().Sha != null)
                {
                    NewReference newReference = new NewReference(_BASEREFERENCE + branch, allCommits.Result.Where(c => c.Parents.Count == 0).FirstOrDefault().Sha);

                    _apiConnection = new ApiConnection(_client.Connection);
                    _gitDatabaseClient = new GitDatabaseClient(_apiConnection);

                    newBranch = await _gitDatabaseClient.Reference.Create(repositoryId, newReference);
                    if (newBranch != null)
                    {
                        log.LogInformation($"Created new Branch: {branch}");
                        
                        // For "DST" only.
                        if (!string.IsNullOrEmpty(path) && contents != null)
                        {
                            // Create a new Blob.
                            var commit = await CreateFileCommit(repositoryId, branch, path, contents, log);

                            if (commit == null)
                            {
                                log.LogError($"Error creating contents for Branch: {branch} at {path}");
                            }
                            else
                            {
                                log.LogInformation($"Created contents for Branch: {branch} at {path}");

                                // Update the Branch to reference the new Blob/Contents.
                                ReferenceUpdate updateReference = new ReferenceUpdate(commit.Sha);
                                Reference branchUpdated = await _gitDatabaseClient.Reference.Update(repositoryId, _BASEREFERENCE + branch, updateReference);
                            }
                        }
                        else
                        {
                            Reference branchUpdated = await _gitDatabaseClient.Reference.Update(repositoryId, _BASEREFERENCE + branch, 
                                new ReferenceUpdate(allCommits.Result.Where(c => c.Parents.Count == 0).FirstOrDefault().Sha));
                        }
                    }
                }
                else
                {
                    log.LogError($"Error looking up first commit in Repository: {repository}");
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Unable to create Branch: {ex.Message}, Stacktrace: {ex.StackTrace}");
            }

            return newBranch;
        }

        /// <summary>
        /// This method will copy the Terraform and PowerShell scripts from "RTP2020_SC/<rtpVersion>" to the "RTP2020_SC_<market>/<storeId>" 
        /// repo+branch in Azure DevOps --> (from "depc-sc/<rtpVersion>" to "depc-<market>-sc/<storeId>" equivalent in GitHub Enterprise).
        /// </summary>
        /// <param name="destinationRepositoryName"></param>
        /// <param name="destinationBranchName"></param>
        /// <param name="market"></param>
        /// <param name="rtpType"></param>
        /// <param name="rtpVersion"></param>
        /// <param name="userName"></param>
        /// <param name="userEmailId"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<bool> CopyFilesFromRTPS(string destinationRepositoryName, string destinationBranchName, string market, string rtpType, string rtpVersion, string userName, string userEmailId, ILogger log)
        {
            bool filesCopied = false;

            try
            {
                string sourceRepositoryName = Helper.GetRepositoryName(GlobalConstants.RepoType.RTPS.ToString(), market, rtpType);
                string sourceBranchName = string.Format(Environment.GetEnvironmentVariable("RTPS_Source_Branch_Template"), rtpVersion);

                var temp = await FindRepository(destinationRepositoryName, log);
                var destinationRepositoryId = temp.Id;

                var sourceRepository = await FindRepository(sourceRepositoryName, log);
                if (sourceRepository == null)
                {
                    log.LogError($"Source repository '{sourceRepositoryName}' does not exist. Unable to create Store branch '{destinationBranchName}', copy files");
                }
                else
                {
                    var sourceBranch = await FindBranch(sourceRepositoryName, sourceBranchName, log);
                    if (sourceBranch == null)
                    {
                        log.LogError($"Source branch '{sourceBranchName}' does not exist. Unable to create Store branch '{destinationBranchName}',copy files");
                    }
                    else
                    {
                        // At this point the destination Repo/Branch already exists, so start copying all files.
                        // Read all folders/files in source Repo/Branch.
                        _apiConnection = new ApiConnection(_client.Connection);
                        _gitDatabaseClient = new GitDatabaseClient(_apiConnection);

                        // Get the SHA for the last commit into the Store branch.
                        var storeBranchRef = await _gitDatabaseClient.Reference.Get(destinationRepositoryId, _BASEREFERENCE + destinationBranchName);
                        var latestCommitInStoreBranchRef = await _gitDatabaseClient.Commit.Get(destinationRepositoryId, storeBranchRef.Object.Sha);

                        var newTree = new NewTree
                        {
                            BaseTree = latestCommitInStoreBranchRef.Tree.Sha
                        };

                        List<TreeItem> allBlobsFromSource = null;
                        
                        // Get all the files from the source Repo/Branch.
                        var sourceGitItems = await _gitDatabaseClient.Tree.GetRecursive(sourceRepository.Id, sourceBranch.Commit.Sha);
                        if (sourceGitItems != null)
                        {
                            if (sourceGitItems.Tree.Count > 0)
                            {
                                log.LogInformation($"Total count: {sourceGitItems.Tree.Count}");
                                int totalBlobCount = 0;
                                int totalBlobMinusLocalizedBlobCount = 0;

                                foreach (var tree in sourceGitItems.Tree)
                                {
                                    if (tree.Type == TreeType.Blob)
                                    {
                                        totalBlobCount++;

                                        var isProcessPath = ProcessPath(market, tree.Path, log);

                                        if (isProcessPath)
                                        {
                                            totalBlobMinusLocalizedBlobCount++;

                                            if (allBlobsFromSource == null)
                                                allBlobsFromSource = new List<TreeItem>();

                                            allBlobsFromSource.Add(tree);
                                        }
                                    }
                                }
                                
                                log.LogInformation($"Total Blob count: {totalBlobCount}");
                                log.LogInformation($"Total Blob - Localized Blob count: {totalBlobMinusLocalizedBlobCount}");
                                log.LogInformation($"allBlobsFromSource.Count: {allBlobsFromSource.Count}");
                                int totalLocalizedBlobCount = 0;
                                int removedCount = 0;

                                foreach (var tree in sourceGitItems.Tree)
                                {
                                    if (tree.Type == TreeType.Blob)
                                    {
                                        var isLocalizationPath = LocalizationPath(market, tree.Path, log);

                                        if (isLocalizationPath)
                                        {
                                            totalLocalizedBlobCount++;

                                            var checkPath = GetDestinationPath(market, tree.Path, log);
                                            var lookupItem = allBlobsFromSource.FirstOrDefault(t => t.Path == checkPath);

                                            if (lookupItem != null)
                                            {
                                                allBlobsFromSource.Remove(lookupItem);

                                                log.LogInformation($"Removing Blob: {checkPath}");
                                                removedCount++;
                                            }

                                            allBlobsFromSource.Add(tree);
                                        }
                                    }
                                }
                                log.LogInformation($"Total Localized Blob count: {totalLocalizedBlobCount}");
                                log.LogInformation($"Removed count: {removedCount}");
                                log.LogInformation($"allBlobsFromSource.Count: {allBlobsFromSource.Count}");

                                if (allBlobsFromSource != null)
                                {
                                    foreach (var blobItem in allBlobsFromSource) 
                                    {
                                        var sourceBlob = await _gitDatabaseClient.Blob.Get(sourceRepository.Id, blobItem.Sha);

                                        var checkPath = blobItem.Path;
                                        if (LocalizationPath(market, blobItem.Path, log))
                                        {
                                            checkPath = GetDestinationPath(market, checkPath, log);
                                        }
                                        if (IsGitignoreTemplatePath(blobItem.Path, log))
                                        {
                                            checkPath = ReplaceGitignoreTemplateName(checkPath, log);
                                        }
                                        var result = CreateOrUpdateFile(destinationRepositoryName, destinationBranchName, checkPath, userName, userEmailId, "Seeding Branch", Encoding.UTF8.GetString(Convert.FromBase64String(sourceBlob.Content)), log);
                                    }

                                    // Remove the README.md file.
                                    if (await IsBlobExists(destinationRepositoryName, destinationBranchName, "README.md", log))
                                    {
                                        var fileDeleted = await DeleteFile(destinationRepositoryName, destinationBranchName, "README.md", userName, userEmailId, "Deleted README.md", log);
                                        if (fileDeleted)
                                            log.LogInformation($"File 'README.md' deleted");
                                        else
                                            log.LogInformation($"File 'README.md' deleted");
                                    }

                                    filesCopied = true;
                                }
                            }
                        }
                    }
                }
                return filesCopied;
            }
            catch (Exception ex)
            {
                log.LogError($"Error in GithubFunctions.CopyFilesFromRTPS: {ex.Message}, Stacktrace: {ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// Create a new Blob/Contents in the Repository + Branch + Path.
        /// </summary>
        /// <param name="repositoryId"></param>
        /// <param name="branch"></param>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<Commit> CreateFileCommit(long repositoryId, string branch, string path, JObject contents, ILogger log)
        {
            Commit newCommit = null;
            
            try
            {
                RepositoryContentChangeSet changeSet = await _client.Repository.Content.CreateFile(repositoryId, path, new CreateFileRequest("Commit message", contents.ToString(Formatting.Indented), branch));
                newCommit = changeSet.Commit;

                log.LogInformation($"New Blob created in Repository: {newCommit.Repository}, SHA: {newCommit.Sha}, Url: {newCommit.Url}");
            }
            catch (Exception ex)
            {
                log.LogError($"Unable to create Blob/Contents: {ex.Message}, Stacktrace: {ex.StackTrace}");
            }

            return newCommit;    
        }

        /// <summary>
        /// This method will create/update the script/config files.
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="branch"></param>
        /// <param name="path"></param>
        /// <param name="committerName"></param>
        /// <param name="committerUserId"></param>
        /// <param name="commitMessage"></param>
        /// <param name="fileContents"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<bool> CreateOrUpdateFile(string repository, string branch, string path, string committerName, string committerUserId, string commitMessage, string fileContents, ILogger log)
        {
            try
            {
                RepositoryContentChangeSet changeSet = null;

                _apiConnection = new ApiConnection(_client.Connection);
                _gitDatabaseClient = new GitDatabaseClient(_apiConnection);

                var targetRepository = await FindRepository(repository, log);

                // Get the SHA for the last commit into the Store branch.
                var storeBranchRef = await _gitDatabaseClient.Reference.Get(targetRepository.Id, _BASEREFERENCE + branch);

                // If the script/config file exists in the Repos + Branch, update it.
                if (await IsBlobExists(repository, branch, path, log))
                {
                    // Get the contents from the Repos + Branch.
                    var currentContents = await _client.Repository.Content.GetAllContentsByRef(Environment.GetEnvironmentVariable("Owner"), repository, path, branch);
                    var fileSHA = currentContents.First().Sha;

                    UpdateFileRequest updateRequest = new UpdateFileRequest(commitMessage, fileContents, fileSHA, branch)
                    {
                        Author = new Committer(committerName, committerUserId, DateTime.UtcNow),
                    };
                    changeSet = await _client.Repository.Content.UpdateFile(Environment.GetEnvironmentVariable("Owner"), repository, path, updateRequest);
                }
                else
                {
                    // Create a new one and commit it.
                    CreateFileRequest createRequest = new CreateFileRequest(commitMessage, fileContents, branch)
                    {
                        Author = new Committer(committerName, committerUserId, DateTime.UtcNow)
                    };
                    changeSet = await _client.Repository.Content.CreateFile(Environment.GetEnvironmentVariable("Owner"), repository, path, createRequest);
                }

                log.LogInformation($"Config {path} created/updated: {changeSet.Commit.Sha}");

                return changeSet == null ? false : true;
            }
            catch (Exception ex)
            {
                log.LogError($"Error in GithubFunctions.CreateOrUpdateFile: {ex.Message}, Stacktrace: {ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// Delete a file in Github.
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="branch"></param>
        /// <param name="path"></param>
        /// <param name="committerName"></param>
        /// <param name="committerUserId"></param>
        /// <param name="commitMessage"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<bool> DeleteFile(string repository, string branch, string path, string committerName, string committerUserId, string commitMessage, ILogger log)
        {
            try
            {
                // Get the contents from the Repos + Branch.
                var currentContents = await _client.Repository.Content.GetAllContentsByRef(Environment.GetEnvironmentVariable("Owner"), repository, path, branch);
                var fileSHA = currentContents.First().Sha;

                DeleteFileRequest deleteRequest = new DeleteFileRequest(commitMessage, fileSHA, branch)
                {
                    Author = new Committer(committerName, committerUserId, DateTime.UtcNow)
                };

                await _client.Repository.Content.DeleteFile(Environment.GetEnvironmentVariable("Owner"), repository, path, deleteRequest);

                return true;
            }
            catch (Exception ex)
            {
                log.LogError($"Error in GithubFunctions.DeleteFile: {ex.Message}, Stacktrace: {ex.StackTrace}");
                throw ex;
            }
        }

        /// <summary>
        /// Returns the contents of a file.
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="branch"></param>
        /// <param name="path"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<string> GetFileContents(string repository, string branch, string path, ILogger log)
        {
            try
            {
                // Get the contents from the Repos + Branch.
                var currentContents = await _client.Repository.Content.GetAllContentsByRef(Environment.GetEnvironmentVariable("Owner"), repository, path, branch);
                var fileSHA = currentContents.First().Sha;

                if (currentContents == null)
                    return string.Empty;
                else
                    return currentContents.First().Content;
            }
            catch (Exception ex)
            {
                log.LogError($"Error in GithubFunctions.GetFileContants: {ex.Message}, Stacktrace: {ex.StackTrace}");
                throw ex;
            }
        }
        #endregion
    }
}