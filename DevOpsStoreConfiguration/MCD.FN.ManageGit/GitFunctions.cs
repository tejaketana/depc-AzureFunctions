using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Identity;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace MCD.FN.ManageGit
{
    public class GitFunctions
    {
        public static GitHttpClient Authenticate(ILogger log)
        {

            var _repoURL = Helper.GetEnvironmentVariable("DevOpsGitPath");
            var _pat = Helper.GetEnvironmentVariable("DevOpsPrimaryToken");
            var _defaultrepoistoryName = Helper.GetEnvironmentVariable("DefaultRepoistoryName");

            TypeDescriptor.AddAttributes(typeof(IdentityDescriptor), new TypeConverterAttribute(typeof(IdentityDescriptorConverter).FullName));
            TypeDescriptor.AddAttributes(typeof(SubjectDescriptor), new TypeConverterAttribute(typeof(SubjectDescriptorConverter).FullName));
            VssConnection connection = new VssConnection(new Uri(_repoURL), new VssBasicCredential(string.Empty, _pat));
            GitHttpClient gitClient = connection.GetClient<GitHttpClient>();
            try
            {
                var repo1 = gitClient.GetRepositoriesAsync(_defaultrepoistoryName).Result;
                foreach (var repo in repo1)
                {
                    Console.WriteLine("{0} {1} {2}", repo.Id, repo.Name, repo.RemoteUrl);
                }
            }
            catch (Exception e)
            {
                log.LogError("Error occured while Authenticating to Azure DevOps. Attempting to authenticate using secondary token. Error Message" + e.Message);
                //If any exception occurs retry with secondary token and if this throws an error let it be picked by calling program.
                _pat = Helper.GetEnvironmentVariable("DevOpsSecondaryToken");
                connection = new VssConnection(new Uri(_repoURL), new VssBasicCredential(string.Empty, _pat));
                gitClient = connection.GetClient<GitHttpClient>();
                var repo1 = gitClient.GetRepositoriesAsync(_defaultrepoistoryName).Result;
            }
            return gitClient;
        }

        public static GitRepository FindRepository(GitHttpClient _gitClient, String _project, string _repoToSearch, ILogger log)
        {
            GitRepository _gitRepo = null;
            try
            {
                //Case Sensitive
                _gitRepo = _gitClient.GetRepositoriesAsync(_project).Result.FirstOrDefault(a => a.Name.Equals(_repoToSearch, StringComparison.InvariantCultureIgnoreCase));
            }
            catch (Exception e)
            {

                //log.Error($"Caught Exception in FindRepository: {e.Message}, Source:{e.Source},InnerException {e.InnerException}");
                throw e;
            }
            return _gitRepo;
        }

        public static GitRef FindBranch(GitHttpClient _gitClient, GitRepository _gitRepo, string _branchToSearch)
        {
            GitRef _branch = null;
            try
            {
                //"refs/heads/AU_TEST0991"
                //first part will never change so it can be hardcoded
                _branchToSearch = "refs/heads/" + _branchToSearch;
                _branch = _gitClient.GetBranchRefsAsync(_gitRepo.Id).Result.FirstOrDefault(a => a.Name.Equals(_branchToSearch, StringComparison.InvariantCultureIgnoreCase));
            }
            catch (Exception e)
            {
                throw e;
            }
            return _branch;
        }

        public static void Clone(GitHttpClient _gitClient, GitRepository _gitSourceRepo,
         GitRef _gitSourceBranch, Store store, ILogger log)
        {

            try
            {
                var _destinationBranch = new GitRefUpdate
                {
                    OldObjectId = _gitSourceBranch.ObjectId,
                    Name = $"refs/heads/{Helper.CloneBranchName(store)}",
                };




                string newFileName = "README_SAMPLE.md";
                GitCommitRef newCommit = new GitCommitRef()
                {
                    Comment = "Added Sample file to commit the new branch.",

                    Changes = new GitChange[]
                    {
                    new GitChange()
                    {
                        ChangeType = VersionControlChangeType.Add,
                        Item = new GitItem() { Path = newFileName },
                        NewContent = new ItemContent()
                        {
                            Content = "# Thank you for using VSTS!",
                            ContentType = ItemContentType.RawText,
                        },
                    }
                    },
                };
                // create the push with the new branch and commit
                GitPush push = _gitClient.CreatePushAsync(new GitPush()
                {
                    RefUpdates = new GitRefUpdate[] { _destinationBranch },
                    Commits = new GitCommitRef[] { newCommit },
                }, _gitSourceRepo.Id).Result;
            }

            catch (Exception e)
            {
                throw e;
            }

        }

        public static void AddSecurityFile(GitHttpClient _gitClient, GitRepository _gitRepo, GitRef _gitDestinationBranch, string _fileName, string _fileContent, bool _base64Encoded, ILogger log)
        {
            try
            {
                if (_base64Encoded)
                {
                    byte[] data = System.Convert.FromBase64String(_fileContent);
                    _fileContent = System.Text.ASCIIEncoding.ASCII.GetString(data);
                }
                var _destinationBranch = new GitRefUpdate
                {
                    Name = _gitDestinationBranch.Name,
                    OldObjectId = _gitDestinationBranch.ObjectId
                };

                GitCommitRef newCommit = new GitCommitRef
                {
                    Comment = "Security.Data seeded based on JSON entry",
                    Changes = new GitChange[]
                    {
                    new GitChange
                    {
                        ChangeType = VersionControlChangeType.Add,
                        Item = new GitItem { Path = $"{_fileName}" },
                        NewContent = new ItemContent
                        {
                            Content = _fileContent,
                            ContentType = ItemContentType.RawText,
                        },
                    }
                    }
                };

                GitPush push = _gitClient.CreatePushAsync(new GitPush
                {
                    RefUpdates = new GitRefUpdate[] { _destinationBranch },
                    Commits = new GitCommitRef[] { newCommit },
                }, _gitRepo.Id).Result;
                log.LogInformation("Succesfully added Security.Data file to " + _fileName);
            }
            catch (Exception e)
            {
                log.LogError("Error occured while adding security.data file at " + _fileName + " Error Message" + e.Message);
            }
        }

        public static GitRepository CreateRepository(GitHttpClient _gitClient, String _name, String _project, string _RemoteURL)
        {
            GitRepository _newRepo = null;
            try
            {
                Guid myId = Guid.NewGuid();
                var _gitR = new GitRepository();
                _gitR.Id = myId;
                _gitR.Name = _name;
                _gitR.IsFork = false;
                _gitR.ParentRepository = null;
                //_gitR.RemoteUrl = "https://dev.azure.com/kartik302_11/MCD/_git/" + _gitR.Name; Test
                _newRepo = _gitClient.CreateRepositoryAsync(_gitR, _project, null).Result;
            }
            catch (Exception e)
            {
                throw e;
            }
            return _newRepo;
        }


        internal static void UpdateFiles(List<AppComponent> appComponents, GitHttpClient _gitClient, GitRepository _gitRepo, GitRef _gitBranch, Store store, ILogger log)
        {
            try
            {
                log.LogInformation("Total App Contents - " + appComponents.Count);
                List<GitChange> _gitChanges1 = new List<GitChange>();

                foreach (var c in store.Components)
                {
                    var _componentName = c.name;
                    foreach (var b in appComponents)
                    {
                        if (b.Name.Equals(c.name, StringComparison.InvariantCultureIgnoreCase))
                        {
                            foreach (var singleLocation in b.Locations)
                            {
                                var updatedString = string.Empty;

                                GitItem singleItemWithContent = null;

                                //Look for a file that has already been modified before going back to Repository. If we read the file again from repository, then
                                //old changes will be overwritten. Since, we are updating wild cards based on components we will need to follow this pattern.
                                var singleItem = _gitChanges1.Find(a1 => a1.Item.Path.Equals(singleLocation.IndividualLocation, StringComparison.InvariantCultureIgnoreCase));

                                if (singleItem != null)
                                {
                                    singleItemWithContent = singleItem.Item;
                                }

                                if (singleItem == null)
                                {
                                    try
                                    {

                                        // string filename = _gitClient.GetItemsAsync(_gitRepo.Id, scopePath: singleLocation.IndividualLocation, recursionLevel: VersionControlRecursionType.Full).Result
                                        //.Where(o => o.GitObjectType == GitObjectType.Blob).FirstOrDefault().Path;
                                        //"refs/heads/POC00001"
                                        GitVersionDescriptor gitDes = new GitVersionDescriptor() { Version = _gitBranch.Name.Substring(11) };

                                        // retrieve the contents of the file
                                        singleItemWithContent = _gitClient.GetItemAsync(_gitRepo.Id, singleLocation.IndividualLocation, versionDescriptor: gitDes, includeContent: true).Result;
                                        updatedString = Helper.ReplaceAllWildCards(b.Name, singleLocation.Pattern, singleItemWithContent.Content, store, c);

                                        if (singleItemWithContent.Content != updatedString)
                                        {

                                            singleItemWithContent.Content = updatedString;
                                            _gitChanges1.Add(new GitChange
                                            {
                                                ChangeType = VersionControlChangeType.Edit,
                                                Item = new GitItem
                                                {
                                                    Path = singleItemWithContent.Path,
                                                },
                                                NewContent = new ItemContent
                                                {
                                                    Content = singleItemWithContent.Content,
                                                    ContentType = ItemContentType.RawText,
                                                },
                                            });
                                            var _processedMessage = "Updated File-" + singleLocation.IndividualLocation + " as a result of wild card replacement for component = " + c.name;
                                            log.LogInformation(_processedMessage);
                                        }
                                        else
                                        {
                                            log.LogWarning("Wildcard Replacment Function did not find a suitable replacement for Component=" + c.name + " at this location " + singleLocation.IndividualLocation);
                                        }
                                    }
                                    catch (SystemException s)
                                    {
                                        log.LogWarning("File Not found at " + singleLocation.IndividualLocation + " of this Branch" + _gitBranch + ". Will be skipped. Error stack " + s.InnerException);
                                    }
                                    catch (Exception e)
                                    {

                                        log.LogError("File Not found at " + singleLocation.IndividualLocation + " of this Branch" + _gitBranch + " Error stack " + e.InnerException);
                                    }
                                }
                                else
                                {
                                    try
                                    {
                                        //Need to remove the old Item as new item will contain  latest updates.
                                        //var singleItem = _gitChanges1.Find(a1 => a1.Item.Path == singleLocation.IndividualLocation);
                                        updatedString = Helper.ReplaceAllWildCards(b.Name, singleLocation.Pattern, singleItem.NewContent.Content, store, c);


                                        if (singleItem.NewContent.Content != updatedString)
                                        {
                                            //Only do this if going to add.
                                            _gitChanges1.Remove(singleItem);

                                            singleItemWithContent.Content = updatedString;
                                            _gitChanges1.Add(new GitChange
                                            {
                                                ChangeType = VersionControlChangeType.Edit,
                                                Item = new GitItem
                                                {
                                                    Path = singleItemWithContent.Path,
                                                },
                                                NewContent = new ItemContent
                                                {
                                                    Content = singleItemWithContent.Content,
                                                    ContentType = ItemContentType.RawText,
                                                },
                                            });
                                            var _processedMessage = "Updated File-" + singleLocation.IndividualLocation + " as a result of wild card replacement for component = " + c.name;
                                            log.LogInformation(_processedMessage);

                                        }
                                        else
                                        {
                                            log.LogWarning("Wildcard Replacment Function did not find a suitable replacement for Component=" + c.name + " at this location " + singleLocation.IndividualLocation);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        log.LogError("Wildcard Replacment Function failed updating after first replacement." + c.name + " at this location " + singleLocation.IndividualLocation + "--Exception: " + e.InnerException);
                                    }
                                }

                            }
                        }
                    }
                }


                //Only Update when atleast 1 changed.
                if (_gitChanges1.Count > 0)
                {
                    var _destinationBranch = new GitRefUpdate
                    {
                        Name = _gitBranch.Name,
                        OldObjectId = _gitBranch.ObjectId
                    };

                    GitCommitRef newCommit = new GitCommitRef
                    {
                        Comment = "Wild Card Replacement",
                        Changes = _gitChanges1
                    };

                    var _Push = new GitPush()
                    {
                        RefUpdates = new GitRefUpdate[] { _destinationBranch },
                        Commits = new GitCommitRef[] { newCommit },
                    };

                    GitPush push = _gitClient.CreatePushAsync(_Push, _gitRepo.Id).Result;
                    log.LogInformation("Succesfully committed " + _gitChanges1.Count + " file(s) due to wild card replacements.");
                }
                else
                {
                    log.LogWarning("Nothing commited. Wildcard replacment function did not have any changes at this branch-" + _gitBranch.Name);
                }

            }
            catch (Exception e)
            {
                //log.Error($"Caught Exception in Run: {e.Message}, Source:{e.Source},InnerException {e.InnerException}");
                throw e;
            }
        }

        public static void AddPlugInFileInfo(GitHttpClient _gitClient, GitRepository _gitRepo, GitRef _gitDestinationBranch, string _fileName, Store store, ILogger log)
        {
            try
            {
                bool addOperation = false;

                GitVersionDescriptor gitDes = new GitVersionDescriptor() { Version = _gitDestinationBranch.Name.Substring(11) };
                try
                {
                    GitItem singleItemWithContent = _gitClient.GetItemAsync(_gitRepo.Id, _fileName, includeContent: true, versionDescriptor: gitDes).Result;
                    if (singleItemWithContent == null)
                    {
                        addOperation = true;
                    }
                }
                catch (AggregateException)
                {
                    //Indicates File Not Found.
                    addOperation = true;
                }


                var _fileContent = "{\"rtpVersion\": \"" + store.rtpVersion + "\"}";
                var _destinationBranch = new GitRefUpdate
                {
                    Name = _gitDestinationBranch.Name,
                    OldObjectId = _gitDestinationBranch.ObjectId
                };
                GitCommitRef newCommit;

                if (addOperation)
                {
                    newCommit = new GitCommitRef
                    {
                        Comment = "Plugin file added.",
                        Changes = new GitChange[]
                       {
                    new GitChange
                    {
                        ChangeType = VersionControlChangeType.Add,
                        Item = new GitItem { Path = $"{_fileName}" },
                        NewContent = new ItemContent
                        {
                            Content = _fileContent,
                            ContentType = ItemContentType.RawText,
                        },
                    }
                       }
                    };
                }
                else
                {
                    newCommit = new GitCommitRef
                    {
                        Comment = "Plugin file Updated.",
                        Changes = new GitChange[]
                      {
                    new GitChange
                    {
                        ChangeType = VersionControlChangeType.Edit,
                        Item = new GitItem { Path = $"{_fileName}" },
                        NewContent = new ItemContent
                        {
                            Content = _fileContent,
                            ContentType = ItemContentType.RawText,
                        },
                    }
                      }
                    };
                }

                GitPush push = _gitClient.CreatePushAsync(new GitPush
                {
                    RefUpdates = new GitRefUpdate[] { _destinationBranch },
                    Commits = new GitCommitRef[] { newCommit },
                }, _gitRepo.Id).Result;
                log.LogInformation("Succesfully added/edited Plugin Info file " + _fileName);
            }
            catch (Exception e)
            {
                log.LogError("Error occured while adding Plugin info file at " + _fileName + " Error Message" + e.Message);
            }
        }

        public static void CreateBranch(GitHttpClient _gitClient, GitRepository _gitRepo, String _name)
        {
            try
            {
                var newBranch = new GitRefUpdate
                {
                    OldObjectId = "0000000000000000000000000000000000000000",
                    RepositoryId = _gitRepo.Id,
                    Name = $"refs/heads/" + _name
                };
                //Temp To Remove
                string newFileName = "README_SAMPLE.md";
                GitCommitRef newCommit = new GitCommitRef
                {
                    Comment = "Initial commit",
                    Changes = new GitChange[]
                    {
                    new GitChange
                    {
                        ChangeType = VersionControlChangeType.Add,
                        Item = new GitItem { Path = $"{newFileName}" },
                        NewContent = new ItemContent
                        {
                            Content = "# Thank you for using !",
                            ContentType = ItemContentType.RawText,
                        },
                    }
                    }
                };

                GitPush push = _gitClient.CreatePushAsync(new GitPush
                {
                    RefUpdates = new GitRefUpdate[] { newBranch },
                    Commits = new GitCommitRef[] { newCommit },
                }, _gitRepo.Id).Result;

            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public static void RemoveReadMe(GitHttpClient _gitClient, GitRepository _gitRepo, GitRef _gitDestinationBranch, ILogger log)
        {
            string FileName = "README_SAMPLE.md";
            try
            {

                var _destinationBranch = new GitRefUpdate
                {
                    Name = _gitDestinationBranch.Name,
                    OldObjectId = _gitDestinationBranch.ObjectId
                };

                GitCommitRef newCommit = new GitCommitRef
                {
                    Comment = "Removed Sample file that was created to commit the new branch.",
                    Changes = new GitChange[]
                    {
                    new GitChange
                    {
                        ChangeType = VersionControlChangeType.Delete,
                        Item = new GitItem { Path = FileName }
                    }
                    }
                };

                GitPush push = _gitClient.CreatePushAsync(new GitPush
                {
                    RefUpdates = new GitRefUpdate[] { _destinationBranch },
                    Commits = new GitCommitRef[] { newCommit },
                }, _gitRepo.Id).Result;
                log.LogInformation("Succesfully removed Extra file " + FileName);
            }
            catch (Exception e)
            {
                log.LogError("Error occured while removing file at " + FileName + " Error Message" + e.Message);
            }
        }

        public static void Copy(GitHttpClient _gitClient, GitRepository _gitDestimationRepo, GitRef _gitDestinationBranch, Store store, ILogger log)
        {
            try
            {
                var _project = Helper.GetEnvironmentVariable("DevOpsProjectName");
                var _sourceRepoName = Helper.RepositoryName(store, "RTPS");
                var _sourceBranchName = store.rtpVersion;
                var _comment = Helper.GetEnvironmentVariable("InitialComment");

                var _gitSourceRepo = FindRepository(_gitClient, _project, _sourceRepoName, log);
                if (_gitSourceRepo != null)
                {
                    var _gitSourceBranch = FindBranch(_gitClient, _gitSourceRepo, _sourceBranchName);
                    if (_gitSourceBranch != null)
                    {
                        CopyAllSourceFiles(_gitClient, _gitDestimationRepo, _gitSourceRepo, _gitSourceBranch, _gitDestinationBranch, store.market, _comment, log);
                        log.LogInformation("Finished copying files for RTP/DCT Branch for Store:" + store.storeId);
                    }
                    else
                    {
                        log.LogError("RTP/DST Source Branch Not found. No files copied. Branch Name:" + store.rtpVersion);
                    }
                }
                else
                {
                    log.LogError("RTP/DST Source Repository Not found. No files copied. Repository Name:" + Helper.RepositoryName(store, "RTPS"));
                }

            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public static void CopyAllSourceFiles(GitHttpClient _gitClient, GitRepository _gitDestimationRepo, GitRepository _gitSourceRepo,
            GitRef _gitSourceBranch, GitRef _gitDestinationBranch, string _market, string _Comment, ILogger log)
        {
            try
            {
                //Create the branch where we will commit.
                var _destinationBranch = new GitRefUpdate
                {
                    Name = _gitDestinationBranch.Name,
                    OldObjectId = _gitDestinationBranch.ObjectId
                };

                List<GitItem> _Processed = new List<GitItem>();
                List<GitItem> _ProcessedEdit = new List<GitItem>();

                GitVersionDescriptor gitDes = new GitVersionDescriptor() { Version = _gitSourceBranch.Name.Substring(11) };

                var storeId = _gitDestinationBranch.Name.Substring(11);
                var tempDirPath = BlobUploadService.GetConfigFolder(storeId);

                // retrieve the contents of the file
                // singleItemWithContent = _gitClient.GetItemAsync(_gitRepo.Id, singleLocation.IndividualLocation, versionDescriptor: gitDes, includeContent: true).Result;

                //-O List<GitItem> _items = _gitClient.GetItemsAsync(_gitSourceRepo.Id, scopePath: null, recursionLevel: VersionControlRecursionType.Full).Result;
                List<GitItem> _items = _gitClient.GetItemsAsync(_gitSourceRepo.Id, scopePath: null, recursionLevel: VersionControlRecursionType.Full, versionDescriptor: gitDes).Result;
                foreach (var _singleItem in _items)
                {

                    GitItem singleItemWithContent;
                    if (CheckLocalizedFile(_market, _singleItem.Path))
                    {
                        if (_singleItem.IsFolder == true)
                        {
                            singleItemWithContent = _gitClient.GetItemAsync(_gitSourceRepo.Id, _singleItem.Path, includeContent: false, versionDescriptor: gitDes).Result;

                            BlobUploadService.CreateLocalFolder(_singleItem.Path, tempDirPath);
                        }
                        else
                        {
                            singleItemWithContent = _gitClient.GetItemAsync(_gitSourceRepo.Id, _singleItem.Path, includeContent: true, versionDescriptor: gitDes).Result;
                            var tempPath = string.Empty;
                            if (_singleItem.Path.ToLower().Contains("gitignore_template"))
                            {
                                tempPath = ".gitignore";
                            }
                            else
                            {
                                tempPath = _singleItem.Path;

                                BlobUploadService.CreateLocalFile(tempPath, tempDirPath, singleItemWithContent.Content.ToString());
                            }
                            if (singleItemWithContent != null && !DoesFileExist(_gitClient, _gitDestimationRepo, _gitDestinationBranch, tempPath, _market))
                            {
                                _Processed.Add(singleItemWithContent);
                            }
                            else if (singleItemWithContent != null && DoesFileExist(_gitClient, _gitDestimationRepo, _gitDestinationBranch, tempPath, _market))
                            {
                                _ProcessedEdit.Add(singleItemWithContent);
                            }
                        }
                    }
                }


                //ar removeGitIgnoreTemplate = -1;
                foreach (var _singleItem in _Processed.ToList())
                {
                    if (_singleItem.Path.StartsWith(RTPConstants.LOCALIZATION + "/" + _market))
                    {
                        var _lengthToSkip = (RTPConstants.LOCALIZATION + "/" + _market).Length;
                        var newPath = _singleItem.Path.Substring(_lengthToSkip);

                        var rootItem = _Processed.Find(a => a.Path.Equals(newPath, StringComparison.InvariantCultureIgnoreCase));
                        if (rootItem != null)
                        {
                            _Processed.Remove(rootItem);
                        }
                        _singleItem.Path = newPath;
                    }

                    if (_singleItem.Path.ToLower().Contains("gitignore_template"))
                    {
                        if (!DoesFileExist(_gitClient, _gitDestimationRepo, _gitDestinationBranch, ".gitignore", _market))
                        {
                            _singleItem.Path = ".gitignore";
                        }

                    }
                }


                foreach (var _singleItem in _ProcessedEdit.ToList())
                {
                    if (_singleItem.Path.StartsWith(RTPConstants.LOCALIZATION + "/" + _market))
                    {
                        var _lengthToSkip = (RTPConstants.LOCALIZATION + "/" + _market).Length;
                        var newPath = _singleItem.Path.Substring(_lengthToSkip);
                        var rootItem = _ProcessedEdit.Find(a => a.Path.Equals(newPath, StringComparison.InvariantCultureIgnoreCase));
                        if (rootItem != null)
                        {
                            _ProcessedEdit.Remove(rootItem);
                        }
                        _singleItem.Path = newPath;
                    }

                    if (_singleItem.Path.ToLower().Contains("gitignore_template"))
                    {
                        if (DoesFileExist(_gitClient, _gitDestimationRepo, _gitDestinationBranch, ".gitignore", _market))
                        {
                            _singleItem.Path = ".gitignore";
                        }

                    }
                }


                //If all files are same, then we don't need to copy.
                if (_Processed.Count == 0 && _ProcessedEdit.Count == 0)
                {
                    return;
                }
                //GitItem singleItemWithContent1 = _gitClient.GetItemAsync(_gitSourceRepo.Id, "/master/XMLFile.xml", includeContent: true).Result;

                List<GitChange> _gitChanges = new List<GitChange>();
                foreach (var item in _Processed)
                {
                    if (!item.IsFolder)
                    {
                        ItemContent _newContent;
                        GitChange gitChange = new GitChange();
                        gitChange.ChangeType = VersionControlChangeType.Add;
                        gitChange.Item = item;
                        _newContent = new ItemContent
                        {
                            Content = item.Content,
                            ContentType = ItemContentType.RawText,
                        };
                        gitChange.NewContent = _newContent;
                        _gitChanges.Add(gitChange);
                    }
                }

                foreach (var item in _ProcessedEdit)
                {
                    if (!item.IsFolder)
                    {
                        ItemContent _newContent;
                        GitChange gitChange = new GitChange();
                        gitChange.ChangeType = VersionControlChangeType.Edit;
                        gitChange.Item = item;
                        _newContent = new ItemContent
                        {
                            Content = item.Content,
                            ContentType = ItemContentType.RawText,
                        };
                        gitChange.NewContent = _newContent;
                        _gitChanges.Add(gitChange);
                    }
                }

                GitCommitRef newCommit = new GitCommitRef
                {
                    Comment = "Seeding Branch",
                    Changes = _gitChanges
                };
                var _Push = new GitPush()
                {
                    RefUpdates = new GitRefUpdate[] { _destinationBranch },
                    Commits = new GitCommitRef[] { newCommit },
                };
                GitPush push = _gitClient.CreatePushAsync(_Push, _gitDestimationRepo.Id).Result;

                //Updating the files locally
                BlobUploadService.UpdateFilesBeforeUploadToBlob(tempDirPath, _market, log);

                //Uploading files to blob
                BlobUploadService.UploadConfigFolder(storeId, log);
            }
            catch (Exception e)
            {
                throw e;
            }

        }

        private static bool CheckLocalizedFile(string _market, string _path)
        {

            //Example: "localization/JP/terraform/payroll.tif
            if (_path.StartsWith(RTPConstants.LOCALIZATION))
            {
                if (_path.StartsWith(RTPConstants.LOCALIZATION + "/" + _market))
                {
                    return true;
                }
                return false;
            }
            return true;
        }
        private static Boolean DoesFileExist(GitHttpClient _gitClient, GitRepository _gitDestimationRepo, GitRef _gitDestinationBranch, string path, string _market)
        {
            try
            {

                //Sanitize Path
                if (path.StartsWith(RTPConstants.LOCALIZATION + "/" + _market))
                {
                    var _lengthToSkip = (RTPConstants.LOCALIZATION + "/" + _market).Length;

                    path = path.Substring(_lengthToSkip);
                }
                GitVersionDescriptor gitDes = new GitVersionDescriptor() { Version = _gitDestinationBranch.Name.Substring(11) };

                GitItem singleItemWithContent = _gitClient.GetItemAsync(_gitDestimationRepo.Id, path, includeContent: true, versionDescriptor: gitDes).Result;
                if (singleItemWithContent.Content != null)
                {
                    return true;
                }
            }
            //Valid Case as the file is not found.
            catch (Exception e)
            {
                //Log
                var s = e.Message;
                return false;
            }

            return false;
        }
    }
}
           