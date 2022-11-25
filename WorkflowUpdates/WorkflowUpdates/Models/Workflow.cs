using System;
using System.Collections.Generic;
using System.Text;

namespace WorkflowUpdates.Models
{
    public class Workflow
    {
        public string id { get; set; }
        public string name { get; set; }
        public string endDate { get; set; }
        public string workflowTemplate { get; set; }
        public string currentActiveWorkflowSystems { get; set; }
        public int cursorValue { get; set; }
        public UnstructuredData unstructuredData { get; set; }
        public List<WorkflowSystem> workflowSystems { get; set; }

        public enum EventStatus
        {
            EventSendPending,
            EventSent,
            NoPendingEvent
        }
    }

    public class UnstructuredData
    {
        public string deploymentId { get; set; }
        public string market { get; set; }
        public string deploymentName { get; set; }
        public string deploymentModel { get; set; }
        public string rtpVersion { get; set; }
        public string selectedWorkflowTemplate { get; set; }
        public string effectiveDate { get; set; }
        public DateTime? effectiveDateTime { get; set; }
        public string applyDate { get; set; }
        public int? timeZoneOffsetInMins { get; set; }
        public string status { get; set; }
        public string createdBy { get; set; }
        public string createdByUserId { get; set; }
        public string comments { get; set; }
        public string storeId { get; set; }
        public int? migrationMode { get; set; }
        public string allworkflowstepscompleted { get; set; } = "true";
        public List<Components> components { get; set; }
    }

    public class Components
    {
        public string Software { get; set; }
        public string DeploymentModel { get; set; }
        public string Version { get; set; }
    }

    public class WorkflowSystem
    {
        public int id { get; set; }
        public string name { get; set; }
        public string niceName { get; set; }
        public int order { get; set; }
        public string state { get; set; }
        public string active { get; set; }
        public string objectToWorkflowTemplate { get; set; }
        public string workflowTemplate { get; set; }
        public string workflowStartDate { get; set; }
        public string workflowCompletedDate { get; set; }
        public string optional { get; set; } = "false";
        public string executionStep { get; set; }
        public UnstructuredDataWorkflowSystem unstructuredDataWorkflowSystem { get; set; }
        public CheckpointMessage checkpointMessage { get; set; }
        public CheckpointMessageAnswer checkpointMessageAnswer { get; set; }

        public enum WorkflowSystemStatus
        {
            Initiated,
            Completed,
            Pending,
            Failed,
            Expired,
            Started
        }
    }

    public class UnstructuredDataWorkflowSystem
    {
        public string status { get; set; }
        public string message { get; set; }
        public string modifiedBy { get; set; }
        public string modifiedByUserId { get; set; }
    }

    public class CheckpointMessage
    {
        public string name { get; set; }
        public int order { get; set; }
        public DateTime expiration { get; set; }
        public string expirationMessageOut { get; set; }
        public string verbosity { get; set; }
        public bool parentGoToError { get; set; }
        public string errorLevel { get; set; }
        public string status { get; set; }
        public string message { get; set; }
    }

    public class CheckpointMessageAnswer
    {
        public string name { get; set; }
        public string workflow { get; set; }
        public string workflowSystem { get; set; }
        public string error { get; set; }
        public string timeStamp { get; set; }
    }
}
