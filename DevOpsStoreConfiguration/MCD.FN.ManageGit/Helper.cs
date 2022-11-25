using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MCD.FN.ManageGit
{
    public class Helper
    {


        public class MatchEvaluatorWrapper
        {
            public string _strPattaern;
            private Store store;
            private Component component;
            private string rtpVersion { get; set; }
            private string region { get; set; }
            private string profile { get; set; }
            private string market { get; set; }
            private string target { get; set; }
            private string version { get; set; }
            private string bundleMarket { get; set; }

            private string previousVersion { get; set; }
            public MatchEvaluatorWrapper(string strPattern, Store pStore, Component pcomponent)
            {
                _strPattaern = strPattern;
                store = pStore;
                component = pcomponent;

                SetLocalVariables();

            }
            private void SetLocalVariables()
            {
                rtpVersion = store.rtpVersion;
                region = store.region;
                profile = store.profile;
                market = store.market;
                target = component.target;
                version = component.version;
                bundleMarket = component.bundleMarket;
                previousVersion = component.previousVersion;

            }


            public string MatchHandler(Match match)
            {
                _strPattaern = _strPattaern.Replace("{target}", target);
                _strPattaern = _strPattaern.Replace("{version}", version);
                _strPattaern = _strPattaern.Replace("{region}", region);
                _strPattaern = _strPattaern.Replace("{profile}", profile);
                _strPattaern = _strPattaern.Replace("{market}", market);
                _strPattaern = _strPattaern.Replace("{previousVersion}", previousVersion);
                if (!string.IsNullOrWhiteSpace(bundleMarket))
                {
                    _strPattaern = _strPattaern.Replace("{bundleMarket}", bundleMarket);
                }
                else
                {
                    _strPattaern = _strPattaern.Replace("{bundleMarket}", market);
                }
                /*
                _strPattaern = StringExtensions.ReplaceInsensitive(_strPattaern, "{target}", target);
                _strPattaern = StringExtensions.ReplaceInsensitive(_strPattaern,"{version}", version);
                _strPattaern = StringExtensions.ReplaceInsensitive(_strPattaern,"{region}", region);
                _strPattaern = StringExtensions.ReplaceInsensitive(_strPattaern,"{profile}", profile);
                _strPattaern = StringExtensions.ReplaceInsensitive(_strPattaern,"{PreviousVersion}", previousVersion);*/
                return _strPattaern;
            }
        }
        public static string GetEnvironmentVariable(string name)
        {
            return
                System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }

        public static string GetEnvironmentVariable(string v, EnvironmentVariableTarget process)
        {
            return
               System.Environment.GetEnvironmentVariable(v, process);
        }


        public static string ErrorToWrite(List<string> _errorList)
        {
            var _conString = string.Empty;
            foreach (var s in _errorList)
            {
                _conString = _conString + s + " | ";
            }
            return _conString;
        }


        public static async Task AddtoRTPHistory(Store store, StoreResults storeResults)
        {

            string EndpointUri = GetEnvironmentVariable("RTPHistory_EndPointURL");
            string PrimaryKey = GetEnvironmentVariable("RTPHistory_AuthorizationKey");
            string _db = GetEnvironmentVariable("RTPHistory_DatabaseId");
            string _col = GetEnvironmentVariable("RTPHistory_CollectionId");

            DocumentClient client;

            if (storeResults.errorList == "" || storeResults.errorList == null)
            {
                store.description = string.Empty;
            }

            dynamic _rtpHistory = new
            {
                restaurantConfigurationId = store.id,
                storeResults.errorList,
                //storeResults.RCTStateBranchFriendlyURL,
                storeResults.RCTStateBranchRemoteURL,
                //storeResults.RTPBranchFriendlyURL,
                storeResults.RTPBranchRemoteUrl,
                //storeResults.RCTBranchFriendlyURL,
                storeResults.RCTBranchRemoteUrl,
                storeId = store.storeId,
                region = store.region,
                market = store.market,
                ProcessedDate_UTC = DateTime.UtcNow,
                OriginalJSON = store
            };

            client = new DocumentClient(new Uri(EndpointUri), PrimaryKey);

            var link = UriFactory.CreateDocumentCollectionUri(_db, _col);

            Document updateddoc = await client.CreateDocumentAsync(link, _rtpHistory);
        }

        public static string RepositoryName(Store store, string RepoType)
        {
            if (RepoType == "RTP")
            {
                //RTP2020_SC_JP
                return store.series + "_" + store.rtpType + "_" + store.market;
            }
            else if (RepoType == "RCTS")
            {
                return System.Environment.GetEnvironmentVariable("RCTStateRepository");
            }
            else if (RepoType == "RTPS")
            {
                //RTP2020_SC
                return store.series + "_" + store.rtpType;
            }
            else
            {
                //RCT_SC_JP
                return Helper.GetEnvironmentVariable("Prefix_RCT") + "_" + store.rtpType + "_" + store.market;
            }
        }

        public static string BranchName(Store store, string RepoType)
        {
            return store.market + store.storeId;
        }
        public static string CloneBranchName(Store store)
        {
            return store.market + store.storeId;
        }

        public static string ReplaceAllWildCards(string wildString, string pattern, string source, Store pStore, Component component)
        {
            string processedString = string.Empty;
            MatchEvaluatorWrapper matchEvaluatorWrapper = new MatchEvaluatorWrapper(pattern, pStore, component);
            MatchEvaluator evaluator = new MatchEvaluator(matchEvaluatorWrapper.MatchHandler);
            processedString = Regex.Replace(source, ProcessedWildString(wildString, string.Empty), evaluator, RegexOptions.IgnoreCase);

            //Special Override for Previous Version variable defined in same  file.
            if (!string.IsNullOrEmpty(component.previousVersion))
            {
                matchEvaluatorWrapper._strPattaern = pattern.Replace("{version}", "{previousVersion}");
                processedString = Regex.Replace(processedString, ProcessedWildString(wildString, "1"), evaluator, RegexOptions.IgnoreCase);
            }
            return processedString;

        }

        private static string ProcessedWildString(string key, string key2)
        {
            if (string.IsNullOrEmpty(key2))
            {
                return "<%component_" + key.ToLower() + "%>";
            }
            else
            {
                return "<%component_" + key.ToLower() + "_previousversion" + "%>";
            }
        }

        public static List<AppComponent> ReadApplicationSettings()
        {
            var _pURL = Helper.GetEnvironmentVariable("ApplicationSettings_EndPointURL");
            var _authorizationKey = Helper.GetEnvironmentVariable("ApplicationSettings_AuthorizationKey");
            var _databaseId = Helper.GetEnvironmentVariable("ApplicationSettings_DatabaseId");
            var _collectionId = GetEnvironmentVariable("ApplicationSettings_CollectionId");

            if (!(string.IsNullOrEmpty(_pURL)) && !(string.IsNullOrEmpty(_authorizationKey)) && !(string.IsNullOrEmpty(_databaseId)) && !(string.IsNullOrEmpty(_collectionId)))
            {
                return ReadWildCardJSONObject(_pURL, _authorizationKey, _databaseId, _collectionId);
            }
            else
            {
                return null;
            }
        }

        internal static List<AppComponent> ReadWildCardJSONObject(string pEndPointURL, string pauthKey, string pdatabaseId, string pcollectionId)
        {
            var client = new DocumentClient(new Uri(pEndPointURL), pauthKey);
            FeedOptions queryOptions = new FeedOptions { EnableCrossPartitionQuery = true };
            List<AppComponent> _component = new List<AppComponent>();
            IQueryable<AppSettingsDoc> ComponentsSQL = client.CreateDocumentQuery<AppSettingsDoc>(
            UriFactory.CreateDocumentCollectionUri(pdatabaseId, pcollectionId),
            "SELECT top 1 * FROM WildCards w order by w._ts desc",
            queryOptions);
            AppSettingsDoc item = ComponentsSQL.AsEnumerable().First();
            foreach (AppComponent c in item.Components)
            {
                _component.Add(c);
            }
            //var mycomp = _component.Find(a => a.Name == "KA2");

            return _component;
        }

        public static async Task SendEmailAsync(string _subject, string _message)
        {
            var apiKey = Environment.GetEnvironmentVariable("SENDGRID_KEY");
            var fromEmail = Environment.GetEnvironmentVariable("FROM_EMAIL");
            var fromName = Environment.GetEnvironmentVariable("FROM_NAME");
            var toEmail = Environment.GetEnvironmentVariable("TO_EMAIL");
            var toName = Environment.GetEnvironmentVariable("TO_NAME");

            var client = new SendGridClient(apiKey);
            var from = new EmailAddress(fromEmail, fromName);
            var subject = _subject;
            var to = new EmailAddress(toEmail, toName);

            var plainTextContent = _message;
            var htmlContent = "<strong>" + _message + "</strong>";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
            var response = await client.SendEmailAsync(msg);
        }
    }
}
