namespace PrepareGithubRepository.Models
{
    public class StoreDeployment
    {
        public string id { get; set; }
        public string market { get; set; }
        public string deploymentId { get; set; }
        public string storeId { get; set; }
        public string createdBy { get; set; }
        public string createdByUserId { get; set; }
        public string rtpType { get; set; }
        public string rtpVersion { get; set; }
        public string workflowTemplate { get; set; }
        public string workflowName { get; set; }
        public string location { get; set; }
    }
}
