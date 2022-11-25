namespace PrepareGithubRepository.Core
{
    public class GlobalConstants
    {
        public const string LOCALIZATION = "localization";
        public const string GITIGNORE_TEMPLATE = "gitignore_template";
        public const string GITIGNORE = "gitignore";
        public const string STORE_VERSION_JSON = "storeversion.json";
        public const string LOCAL_CONTAINER_VERSION_JSON = "localcontainerversion.json";
        public const string RTP_VERSION_JSON = "rtpVersion.json";

        public enum RtpType
        { 
            DST,
            SC
        }

        public enum RepoType
        {
            RTP,
            RTPS,
            RCT
        }
    }
}
