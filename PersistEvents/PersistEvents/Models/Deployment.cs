using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace PersistEvents.Models
{
    /// <summary>
    /// Data models for Deployment document in Monitoring container.
    /// </summary>
    public class Deployment
    {
        private string _id;
        [Key]
        public string id
        {
            get { return _id; }
            set { _id = value; }
        }
        public string deploymentId { get; set; }
        public string market { get; set; }
        public string deploymentName { get; set; }
        public string effectiveDate { get; set; }
        public DateTime? applyDate { get; set; }
        public string status { get; set; }
        public string createdBy { get; set; }
        public string createdByUserId { get; set; }
        public int storeLimit { get; set; }
        public string comments { get; set; }
        public long releaseId { get; set; }
        public int targetId { get; set; }
        public string releaseName { get; set; }
        public string deploymentModel { get; set; }
        public string rtpVersion { get; set; }
        public string targetName { get; set; }
        public string detailedStatus { get; set; }
        public string createdDate { get; set; }
        public string workflowTemplate { get; set; }
        public string currentWorkflowTemplate { get; set; }
        public string isRecordCreated { get; set; }
        public string workflowsCreated { get; set; }
        public DateTime effectiveDateTime { get; set; }
        public List<DeploymentStore> stores { get; set; }

        public enum WorkflowTemplates
        {
            Initiation,
            DependencyCheck,
        }

        public enum WorkflowTriggered
        {
            NotTriggered = 0,
            FutureDeploymentTriggered = 1,
            CurrentDeploymentTriggered = 2,
        }

        public string ToFriendlyString(string currentStatus)
        {
            switch (currentStatus)
            {
                case nameof(DeploymentStatus.Scheduled):
                    return "Scheduled";
                case nameof(DeploymentStatus.InProgress):
                    return "In Progress";
                case nameof(DeploymentStatus.Completed):
                    return "Completed";
                case nameof(DeploymentStatus.Canceled):
                    return "Canceled";
                default:
                    return "In Progress";
            }
        }

        /// <summary>
        /// Derive the Store status for "detailedStatus" property.
        /// </summary>
        /// <returns></returns>
        public string GetStatus()
        {
            int storeCount = stores.Count();

            var storeStatuses = stores
                .GroupBy(x => x.status)
                .Select(g => new {
                    Status = g.Key,
                    Count = g.Count()
                });

            var formattedStatuses = storeStatuses.Select(x => $"{x.Count}/{storeCount} {x.Status}");

            var deploymentStatus = String.Join("~", formattedStatuses);
            return deploymentStatus;
        }

        public bool IsAllStoreStatusCompleted()
        {
            return (stores.All(s => s.status == DeploymentDetailStatus.Completed.ToString()))
                ? true : false;
        }

        public bool IsAnyStoreStatusFailed()
        {
            return (stores.Any(s => s.status == DeploymentDetailStatus.Failed.ToString()))
                ? true : false;
        }

        public bool IsDeploymentCompleted()
        {
            return (stores.Where(s => s.status == DeploymentDetailStatus.Completed.ToString() 
                || s.status == DeploymentDetailStatus.Failed.ToString()).ToList().Count == stores.Count)
                ? true : false;
        }
    }

    public enum DeploymentStatus
    {
        Scheduled,
        InProgress,
        Completed,
        Canceled
    }
    public class DeploymentStore
    {
        public int id { get; set; }
        public string storeId { get; set; }
        public string status { get; set; }
        public string dateCompleted { get; set; }
        public string workflowId { get; set; }
        public List<Components> components { get; set; }
        public List<DependencyCheckDetails> dependencyCheckDetails { get; set; }
        public DeviceStats deviceStats { get; set; }
        public List<DeviceStatus> devicesStatus { get; set; }

        /// <summary>
        /// Update 1st Dependency check results.
        /// </summary>
        /// <param name="workflowEventDependencyCheckDetails"></param>
        public void InitializeDependencyCheckDetails(WorkflowEventDependencyCheckDetails workflowEventDependencyCheckDetails)
        {
            if (dependencyCheckDetails == null)
                dependencyCheckDetails = new List<DependencyCheckDetails>();

            dependencyCheckDetails.Add(UpdateDependencyCheckDetails(workflowEventDependencyCheckDetails));
        }

        /// <summary>
        /// Update Dependency check results.
        /// </summary>
        /// <param name="workflowEventDependencyCheckDetails"></param>
        /// <returns></returns>
        public DependencyCheckDetails UpdateDependencyCheckDetails(WorkflowEventDependencyCheckDetails workflowEventDependencyCheckDetails)
        {
            var dcResults = new List<DependencyCheckResults>();

            workflowEventDependencyCheckDetails.dependencyCheckResults.ForEach(r =>
            {
                dcResults.Add(DependencyCheckResults.DCResults(r));
            });

            return new DependencyCheckDetails
            {
                dependencyCheckDate = workflowEventDependencyCheckDetails.dependencyCheckDate,
                dependencyCheckResults = dcResults
            };
        }

        /// <summary>
        /// Apply rules for excluding/including devices.
        /// </summary>
        /// <param name="devices"></param>
        /// <returns></returns>
        public List<Device> ApplyDeviceFilterRules(List<Device> devices)
        {
            if (devices != null)
            {
                // If deployment is only for component = "Kiosk", updates apply only to "CSO" node types.
                if (components.Count == 1 && components.Any(c => c.software.Trim().ToUpper() == "KIOSK"))
                {
                    devices.RemoveAll(d => !(d.deviceType.ToUpper() == "CSO"));
                }
                else
                {
                    // Exclude nodeType = "RHS" for all stores.
                    devices.RemoveAll(d => d.deviceType.ToUpper() == "RHS");

                    // Exclude "GSC02".
                    // Is this for DE stores?

                    // Exclude "ORB".
                    // Is this for DE stores?
                }
            }
            return devices;
        }

        public bool IsFirstDeviceEvent()
        {
            return devicesStatus.Count == 1 ? true : false;
        }

        public bool IsStoreUpdatePending()
        {
            return (status == DetailedStatusExtensions.ToFriendlyString(DeploymentDetailStatus.DoDeploySuccess.ToString())
                || status == DetailedStatusExtensions.ToFriendlyString(DeploymentDetailStatus.DownloadStarted.ToString())
                || status == DetailedStatusExtensions.ToFriendlyString(DeploymentDetailStatus.DownloadCompleted.ToString())
                || status == DetailedStatusExtensions.ToFriendlyString(DeploymentDetailStatus.DeploymentInProgress.ToString())
                || status == DeploymentDetailStatus.Failed.ToString()) ? true : false;
        }

        public bool IsLastDeviceEvent()
        {
            // Filter and group only "Succeeded", "Failed", "Complete" events.
            var deviceStatuses = devicesStatus
                .GroupBy(x => x.deviceId, (key, y) => y.OrderByDescending(z => z.timestamp).First())
                .Where(d => d.statusEvent.eventStatus == PackageApplicationStatus.Succeeded.ToString()
                    || d.statusEvent.eventStatus == PackageApplicationStatus.Failed.ToString()
                    || d.statusEvent.eventStatus == PackageApplicationStatus.Complete.ToString())
                .ToList();

            // Match the deviceIds to check what's pending.
            var pendingDevices = deviceStats.devices.Replace(" ", string.Empty).Split(",").ToList().Except(deviceStatuses.Select(d2 => d2.deviceId)).ToList();

            // If pendingDevices.Count = 0, last device event is being processed.
            return pendingDevices.Count == 0 ? true : false;
        }

        public bool IsLastDeviceRollbackEvent()
        {
            // Filter and group only "RollbackSucceeded", "RollbackFailed", "Complete" events.
            var deviceStatuses = devicesStatus
                .GroupBy(x => x.deviceId, (key, y) => y.OrderByDescending(z => z.timestamp).First())
                .Where(d => d.statusEvent.eventStatus == PackageApplicationStatus.RollbackSucceeded.ToString()
                    || d.statusEvent.eventStatus == PackageApplicationStatus.RollbackFailed.ToString()
                    || d.statusEvent.eventStatus == PackageApplicationStatus.Complete.ToString())
                .ToList();

            // Get the list of devices which were manually Completed - no rollback will happen for these, so don't count them.
            var manuallyCompletedDevices = devicesStatus.Where(d => d.statusEvent.eventStatus == PackageApplicationStatus.Complete.ToString()).ToList();

            // Match the deviceIds to check what's pending.
            var pendingDevices = deviceStats.devices.Replace(" ", string.Empty).Split(",").ToList().Except(deviceStatuses.Select(d2 => d2.deviceId)).ToList();
            
            // Also, remove manually Completed devices, if any.
            if (manuallyCompletedDevices != null)
                pendingDevices = pendingDevices.Except(manuallyCompletedDevices.Select(d3 => d3.deviceId)).ToList();

            // If pendingDevices.Count = 0, last device event is being processed.
            return pendingDevices.Count == 0 ? true : false;
        }

        public bool IsAllDeviceStatusSuccess()
        {
            var deviceStatuses = devicesStatus
                .GroupBy(x => x.deviceId, (key, y) => y.OrderByDescending(z => z.timestamp).First()).ToList();

            return deviceStatuses.All(d => d.statusEvent.eventStatus == PackageApplicationStatus.Succeeded.ToString()
                            || d.statusEvent.eventStatus == PackageApplicationStatus.Complete.ToString()) ? true : false;
        }

        public bool IsAllDeviceRollbackStatusSuccess()
        {
            var deviceStatuses = devicesStatus
                .GroupBy(x => x.deviceId, (key, y) => y.OrderByDescending(z => z.timestamp).First()).ToList();

            return deviceStatuses.All(d => d.statusEvent.eventStatus == PackageApplicationStatus.RollbackSucceeded.ToString()
                            || d.statusEvent.eventStatus == PackageApplicationStatus.Complete.ToString()) ? true : false;
        }

        public bool IsAnyDeviceStatusFailed()
        {
            var deviceStatuses = devicesStatus
                .GroupBy(x => x.deviceId, (key, y) => y.OrderByDescending(z => z.timestamp).First()).ToList();

            return deviceStatuses.Any(d => d.statusEvent.eventStatus == PackageApplicationStatus.Failed.ToString()) ? true : false;
        }
    }

    public class Components
    {
        public string software { get; set; }
        public string version { get; set; }
    }

    public class DependencyCheckDetails
    {
        public string dependencyCheckDate { get; set; }
        public List<DependencyCheckResults> dependencyCheckResults { get; set; }
    }

    public class DependencyCheckResults
    {
        public string deviceName { get; set; }
        public string deviceId { get; set; }
        public string deviceResult { get; set; }
        public string deviceDetails { get; set; }
        public string rules { get; set; }

        public static DependencyCheckResults DCResults(WorkflowEventDependencyCheckResults v) => new DependencyCheckResults
        {
            deviceId = v.deviceName,
            deviceResult = v.deviceResult,
            deviceDetails = v.deviceDetails
        };
    }

    public class DeviceStats
    {
        public string collectedOn { get; set; }
        public string devices { get; set; }
    }

    public enum DeploymentDetailStatus
    {
        DependencycheckPending,
        DependencycheckSuccess,
        DependencycheckFailed,
        Overridden,
        MigrationPending,
        MigrationSuccess,
        MigrationFailed,
        TransportPending,
        TransportSuccess,
        TransportFailed,
        DoDeployPending,
        DoDeploySuccess,
        DoDeployFailed,
        DeploymentInProgress,
        Completed,
        Failed,
        Canceled,
        DownloadStarted,
        DownloadCompleted,
        DownloadFailed,
        CancelDeploymentSuccess,
        CancelDeploymentFailed,
        CancelDeploymentValidationFailed,
        RollbackRequestSuccess,
        RollbackRequestFailed
    }

    public static class DetailedStatusExtensions
    {
        public static string ToFriendlyString(this string me)
        {
            switch (me)
            {
                case nameof(DeploymentDetailStatus.DependencycheckPending):
                    return "Pending Dependency Check";
                case nameof(DeploymentDetailStatus.DependencycheckSuccess):
                    return "Dependency Check Success";
                case nameof(DeploymentDetailStatus.DependencycheckFailed):
                    return "Dependency Check Failed";
                case nameof(DeploymentDetailStatus.Overridden):
                    return "Dependency Check Overridden";
                case nameof(DeploymentDetailStatus.MigrationPending):
                    return "Pending Config Migration";
                case nameof(DeploymentDetailStatus.MigrationSuccess):
                    return "Config Migration Success";
                case nameof(DeploymentDetailStatus.MigrationFailed):
                    return "Config Migration Failed";
                case nameof(DeploymentDetailStatus.DoDeployPending):
                    return "Preparing Repository";
                case nameof(DeploymentDetailStatus.DoDeploySuccess):
                    return "Repository Ready";
                case nameof(DeploymentDetailStatus.DoDeployFailed):
                    return "Preparing Repository Failed";
                case nameof(DeploymentDetailStatus.DeploymentInProgress):
                    return "Deployment In Progress";
                case nameof(DeploymentDetailStatus.Completed):
                    return "Completed";
                case nameof(DeploymentDetailStatus.Failed):
                    return "Failed";
                case nameof(DeploymentDetailStatus.DownloadStarted):
                    return "Download Started";
                case nameof(DeploymentDetailStatus.DownloadCompleted):
                    return "Download Completed";
                case nameof(DeploymentDetailStatus.DownloadFailed):
                    return "Download Failed";
                case nameof(DeploymentDetailStatus.CancelDeploymentSuccess):
                    return "Canceled";
                case nameof(DeploymentDetailStatus.CancelDeploymentValidationFailed):
                    return "Canceled";
                case nameof(DeploymentDetailStatus.RollbackRequestSuccess):
                    return "Rollback Requested";
                case nameof(DeploymentDetailStatus.RollbackRequestFailed):
                    return "Completed";     // In case the GitHub store configuration update fails, the status will remain "Completed".
                default:
                    return "Pending Dependency Check";
            }
        }
    }

    public enum DownloadPackageStatus
    {
        Started,
        Succeeded,
        Failed,
        Canceled,
        Cancelled
    }

    public static class DownloadPackageStatusMapping
    {
        public static DeploymentDetailStatus MapTo(this string me)
        {
            switch (me)
            {
                case nameof(DownloadPackageStatus.Started):
                    return DeploymentDetailStatus.DownloadStarted;
                case nameof(DownloadPackageStatus.Succeeded):
                    return DeploymentDetailStatus.DownloadCompleted;
                case nameof(DownloadPackageStatus.Failed):
                    return DeploymentDetailStatus.DownloadFailed;
                case nameof(DownloadPackageStatus.Canceled):
                    return DeploymentDetailStatus.Canceled;
                case nameof(DownloadPackageStatus.Cancelled):
                    return DeploymentDetailStatus.Canceled;
                default:
                    return DeploymentDetailStatus.DownloadFailed;
            }
        }
    }

    public enum PackageApplicationStatus
    {
        Started,
        Succeeded,
        Failed,
        Complete,
        RollbackStarted,
        RollbackSucceeded,
        RollbackFailed
    }
}
