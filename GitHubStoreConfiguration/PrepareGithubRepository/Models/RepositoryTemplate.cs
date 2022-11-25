using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace PrepareGithubRepo.Models
{
    /// REDUNDANT - REMOVE AFTER GitHubService.CreateNewRepository() is fixed !!!
    public class RepositoryTemplate
    {
        public string template { get; set; }
        public string location { get; set; }
        public JObject defaultConfig { get; set; }
        public List<Market> markets { get; set; }
    }

    public class Market
    {
        public string market { get; set; }
        public JObject config { get; set; }
    }
}
