using Newtonsoft.Json;

namespace PersistEvents.Models
{
    #region Request Data Model
    /// <summary>
    /// Data model for Device event sent from SmartUpdate.
    /// </summary>
    public class DeviceEvent
    {
        [JsonProperty("DeviceId")]
        public string deviceId { get; set; }
        [JsonProperty("Market")]
        public string market { get; set; }
        [JsonProperty("StoreId")]
        public string storeId { get; set; }
        [JsonProperty("DeploymentId")]
        public string deploymentId { get; set; }
        public string completedByUserId { get; set; }
        [JsonProperty("Timestamp")]
        public string timestamp { get; set; }
        [JsonProperty(PropertyName = "Event")]
        public EventDTO statusEvent { get; set; }
        [JsonProperty(PropertyName = "Versions")]
        public VersionDTO versions { get; set; }
    }

    public class EventDTO
    {
        [JsonProperty("EventType")]
        public string eventType { get; set; }
        [JsonProperty("EventStatus")]
        public string eventStatus { get; set; }
        [JsonProperty(PropertyName = "Reason")]
        public ReasonDTO reason { get; set; }
    }

    public class ReasonDTO
    {
        [JsonProperty("Code")]
        public int code { get; set; }
        [JsonProperty("Description")]
        public string description { get; set; }
    }

    public class VersionDTO
    {
        [JsonProperty("DatVersion")]
        public string datVersion { get; set; }
        [JsonProperty("DatUpdateType")]
        public int datUpdateType { get; set; }
        [JsonProperty("BinaryVersion")]
        public string binaryVersion { get; set; }
        [JsonProperty("BinUpdateType")]
        public int binUpdateType { get; set; }
        [JsonProperty("SmartUpdateVersion")]
        public string smartUpdateVersion { get; set; }
        [JsonProperty("NpContainerVersion")]
        public string npContainerVersion { get; set; }
        [JsonProperty("KioskBinaryVersion")]
        public string kioskBinaryVersion { get; set; }
    }
    #endregion

    #region DB Data Model
    /// <summary>
    /// Data model for Device event persisted in CosmosDB.
    /// </summary>
    public class DeviceStatus
    {
        public string deviceId { get; set; }
        public string completedByUserId { get; set; }
        public string timestamp { get; set; }
        public StatusEvent statusEvent { get; set; }
        public Version versions { get; set; }

        public static explicit operator DeviceStatus(DeviceEvent v) => new DeviceStatus
        {
            deviceId = v.deviceId,
            timestamp = v.timestamp,
            completedByUserId = v.completedByUserId,
            statusEvent = new StatusEvent
            {
                eventType = (v.statusEvent.eventType == StoreEventTypes.PackageApplication.ToString()
                        && v.statusEvent.eventStatus == PackageApplicationStatus.Complete.ToString()) ? null : v.statusEvent.eventType,
                eventStatus = StoreEventStatusExtensions.ToFriendlyString(v.statusEvent.eventStatus),
                reason = v.statusEvent.reason == null ? null: new Reason
                {
                    code = v.statusEvent.reason.code,
                    description = v.statusEvent.reason.description
                }
            },
            versions = v.versions == null ? null : new Version
            {
                datVersion = v.versions.datVersion,
                datUpdateType = v.versions.datUpdateType,
                binaryVersion = v.versions.binaryVersion,
                binUpdateType = v.versions.binUpdateType,
                smartUpdateVersion = v.versions.smartUpdateVersion,
                npContainerVersion = v.versions.npContainerVersion,
                kioskBinaryVersion = v.versions.kioskBinaryVersion
            }
        };
    }

    public class StatusEvent
    {
        public string eventType { get; set; }
        public string eventStatus { get; set; }
        public Reason reason { get; set; }
    }

    public class Reason
    {
        public int code { get; set; }
        public string description { get; set; }
    }

    public class Version
    {
        public string datVersion { get; set; }
        public int datUpdateType { get; set; }
        public string binaryVersion { get; set; }
        public int binUpdateType { get; set; }
        public string smartUpdateVersion { get; set; }
        public string npContainerVersion { get; set; }
        public string kioskBinaryVersion { get; set; }
    }
    #endregion

    public enum StoreEventTypes
    {
        PackageDownload,
        PackageApplication
    }

    public enum StoreEventStatuses
    {
        Started,
        Succeeded,
        Failed,
        Complete,
        Canceled,
        Cancelled,
        RollbackStarted,
        RollbackSucceeded,
        RollbackFailed
    }

    public static class StoreEventStatusExtensions
    {
        public static string ToFriendlyString(this string me)
        {
            switch (me)
            {
                case nameof(StoreEventStatuses.Started):
                    return "Started";
                case nameof(StoreEventStatuses.Succeeded):
                    return "Succeeded";
                case nameof(StoreEventStatuses.Failed):
                    return "Failed";
                case nameof(StoreEventStatuses.Complete):
                    return "Complete";
                case nameof(StoreEventStatuses.Canceled):
                    return "Canceled";
                case nameof(StoreEventStatuses.Cancelled):
                    return "Canceled";
                case nameof(StoreEventStatuses.RollbackStarted):
                    return "RollbackStarted";
                case nameof(StoreEventStatuses.RollbackSucceeded):
                    return "RollbackSucceeded";
                case nameof(StoreEventStatuses.RollbackFailed):
                    return "RollbackFailed";
                default:
                    return "";
            }
        }
    }
}
