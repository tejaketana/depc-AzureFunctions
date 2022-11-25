using Microsoft.VisualStudio.TestTools.UnitTesting;
using PersistEvents.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistEvents.Tests
{
    [TestClass]
    public class DeploymentStatusTests
    {
        [TestMethod]
        public void DeploymentRepository_GetDeploymentStatus_Empty()
        {
            TestDeploymentStatus(String.Empty);
        }

        [TestMethod]
        public void DeploymentRepository_GetDeploymentStatus_OneDependencyCheckPending()
        {
            TestDeploymentStatus("1/1 Pending Dependency Check", dependencycheckPendingCount: 1);
        }

        [TestMethod]
        public void DeploymentRepository_GetDeploymentStatus_OneDependencyCheckPending_OneConfigFailed()
        {
            TestDeploymentStatus("1/2 Pending Dependency Check~1/2 Config Migration Failed",
                dependencycheckPendingCount: 1, migrationFailedCount: 1);
        }

        [TestMethod]
        public void DeploymentRepository_GetDeploymentStatus_OneDependencyCheckPending_OneConfigFailed_OneComplete()
        {
            TestDeploymentStatus("1/3 Pending Dependency Check~1/3 Config Migration Failed~1/3 Completed",
                dependencycheckPendingCount: 1, migrationFailedCount: 1, completedCount: 1);
        }

        private void TestDeploymentStatus(string expectedResult,
            int dependencycheckPendingCount = 0,
            int dependencycheckSuccessCount = 0,
            int dependencycheckFailedCount = 0,
            int overriddenCount = 0,
            int migrationPendingCount = 0,
            int migrationSuccessCount = 0,
            int migrationFailedCount = 0,
            int transportPendingCount = 0,
            int transportSuccessCount = 0,
            int transportFailedCount = 0,
            int doDeployPendingCount = 0,
            int doDeploySuccessCount = 0,
            int doDeployFailedCount = 0,
            int deploymentInProgressCount = 0,
            int completedCount = 0,
            int failedCount = 0,
            int canceledCount = 0,
            int downloadStartedCount = 0,
            int downloadCompletedCount = 0,
            int downloadFailedCount = 0)
        {
            var deployment = new Deployment
            {
                stores = new List<DeploymentStore>()
            };
            #region Map Statuses
            AddStores(deployment, DeploymentDetailStatus.DependencycheckPending, dependencycheckPendingCount);
            AddStores(deployment, DeploymentDetailStatus.DependencycheckSuccess, dependencycheckSuccessCount);
            AddStores(deployment, DeploymentDetailStatus.DependencycheckFailed, dependencycheckFailedCount);
            AddStores(deployment, DeploymentDetailStatus.Overridden, overriddenCount);
            AddStores(deployment, DeploymentDetailStatus.MigrationPending, migrationPendingCount);
            AddStores(deployment, DeploymentDetailStatus.MigrationSuccess, migrationSuccessCount);
            AddStores(deployment, DeploymentDetailStatus.MigrationFailed, migrationFailedCount);
            AddStores(deployment, DeploymentDetailStatus.TransportPending, transportPendingCount);
            AddStores(deployment, DeploymentDetailStatus.TransportSuccess, transportSuccessCount);
            AddStores(deployment, DeploymentDetailStatus.TransportFailed, transportFailedCount);
            AddStores(deployment, DeploymentDetailStatus.DoDeployPending, doDeployPendingCount);
            AddStores(deployment, DeploymentDetailStatus.DoDeploySuccess, doDeploySuccessCount);
            AddStores(deployment, DeploymentDetailStatus.DoDeployFailed, doDeployFailedCount);
            AddStores(deployment, DeploymentDetailStatus.DeploymentInProgress, deploymentInProgressCount);
            AddStores(deployment, DeploymentDetailStatus.Completed, completedCount);
            AddStores(deployment, DeploymentDetailStatus.Failed, failedCount);
            AddStores(deployment, DeploymentDetailStatus.Canceled, canceledCount);
            AddStores(deployment, DeploymentDetailStatus.DownloadStarted, downloadStartedCount);
            AddStores(deployment, DeploymentDetailStatus.DownloadCompleted, downloadCompletedCount);
            AddStores(deployment, DeploymentDetailStatus.DownloadFailed, downloadFailedCount);
            #endregion

            var status = deployment.GetStatus();
            var oldStatus = GetStatusOld(deployment);
            oldStatus = oldStatus == null ? String.Empty: oldStatus.TrimEnd('~');//Ignore trailing tilde

            Assert.AreEqual(expectedResult, status);
            Assert.AreEqual(oldStatus, status);
        }

        private void AddStores(Deployment deployment, DeploymentDetailStatus status, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var store = new DeploymentStore
                {
                    status = DetailedStatusExtensions.ToFriendlyString(status.ToString())
                };
                deployment.stores.Add(store);
            }
        }

        public string GetStatusOld(Deployment deployment)
        {
            string checkPending = DetailedStatusExtensions.ToFriendlyString(DeploymentDetailStatus.DependencycheckPending.ToString());
            string checkSuccess = DetailedStatusExtensions.ToFriendlyString(DeploymentDetailStatus.DependencycheckSuccess.ToString());//"DependencycheckSuccess";
            string checkFailed = DetailedStatusExtensions.ToFriendlyString(DeploymentDetailStatus.DependencycheckFailed.ToString());//"DependencycheckFailed";
            string checkOverridden = DetailedStatusExtensions.ToFriendlyString(DeploymentDetailStatus.Overridden.ToString());//"Overridden";
            string migrationPending = DetailedStatusExtensions.ToFriendlyString(DeploymentDetailStatus.MigrationPending.ToString());//"MigrationPending";
            string migrationSuccess = DetailedStatusExtensions.ToFriendlyString(DeploymentDetailStatus.MigrationSuccess.ToString());//"MigrationSuccess";
            string migrationFailed = DetailedStatusExtensions.ToFriendlyString(DeploymentDetailStatus.MigrationFailed.ToString());//"MigrationFailed";
            string completed = DetailedStatusExtensions.ToFriendlyString(DeploymentDetailStatus.Completed.ToString());//"Completed"
            string failed = DetailedStatusExtensions.ToFriendlyString(DeploymentDetailStatus.Failed.ToString());//"Failed"
            string doDeployPending = DetailedStatusExtensions.ToFriendlyString(DeploymentDetailStatus.DoDeployPending.ToString());//"DoDeployPending";
            string doDeploySuccess = DetailedStatusExtensions.ToFriendlyString(DeploymentDetailStatus.DoDeploySuccess.ToString());//"DoDeploySuccess";
            string doDeployFailed = DetailedStatusExtensions.ToFriendlyString(DeploymentDetailStatus.DoDeployFailed.ToString());//"DoDeployFailed";
            string deploymentInProgress = DetailedStatusExtensions.ToFriendlyString(DeploymentDetailStatus.DeploymentInProgress.ToString());//"DeploymentInProgress";
            string statusdeployment = null;


            var statusDCP = deployment.stores.Where(x => x.status == checkPending).Count();
            var statusDCS = deployment.stores.Where(x => x.status == checkSuccess).Count();
            var statusDCF = deployment.stores.Where(x => x.status == checkFailed).Count();
            var statusDCO = deployment.stores.Where(x => x.status == checkOverridden).Count();
            var statusMP = deployment.stores.Where(x => x.status == migrationPending).Count();
            var statusMS = deployment.stores.Where(x => x.status == migrationSuccess).Count();
            var statusMF = deployment.stores.Where(x => x.status == migrationFailed).Count();
            var statusCom = deployment.stores.Where(x => x.status == completed).Count();
            var statusFailed = deployment.stores.Where(x => x.status == failed).Count();
            var statusDDP = deployment.stores.Where(x => x.status == doDeployPending).Count();
            var statusDDS = deployment.stores.Where(x => x.status == doDeploySuccess).Count();
            var statusDDF = deployment.stores.Where(x => x.status == doDeployFailed).Count();
            var statusDIP = deployment.stores.Where(x => x.status == deploymentInProgress).Count();

            int storeCount = deployment.stores.Count();
            var storecountText = storeCount + "/" + storeCount + " ";

            if (statusDCP > 0 && statusDCP != storeCount)
            {
                statusdeployment = statusDCP + "/" + storeCount + " " + checkPending + "~";
            }
            else if (statusDCP > 0 && statusDCP == storeCount)
            {
                statusdeployment = storecountText + checkPending;
            }
            if (statusDCS > 0 && statusDCS != storeCount)
            {
                statusdeployment = statusdeployment + statusDCS + "/" + storeCount + " " + checkSuccess + "~";
            }
            else if (statusDCS > 0 && statusDCS == storeCount)
            {
                statusdeployment = storecountText + checkSuccess;
            }
            if (statusDCF > 0 && statusDCF != storeCount)
            {
                statusdeployment = statusdeployment + statusDCF + "/" + storeCount + " " + checkFailed + "~";
            }
            else if (statusDCF > 0 && statusDCF == storeCount)
            {
                statusdeployment = storecountText + checkFailed;
            }
            if (statusDCO > 0 && statusDCO != storeCount)
            {
                statusdeployment = statusdeployment + statusDCO + "/" + storeCount + " " + checkOverridden + "~";
            }
            else if (statusDCO > 0 && statusDCO == storeCount)
            {
                statusdeployment = storecountText + checkOverridden;
            }

            if (statusMP > 0 && statusMP != storeCount)
            {
                statusdeployment = statusdeployment + statusMP + "/" + storeCount + " " + migrationPending + "~";
            }
            else if (statusMP > 0 && statusMP == storeCount)
            {
                statusdeployment = storecountText + migrationPending;
            }

            if (statusMS > 0 && statusMS != storeCount)
            {
                statusdeployment = statusdeployment + statusMS + "/" + storeCount + " " + migrationSuccess + "~";
            }
            else if (statusMS > 0 && statusMS == storeCount)
            {
                statusdeployment = storecountText + migrationSuccess;
            }
            if (statusMF > 0 && statusMF != storeCount)
            {
                statusdeployment = statusdeployment + statusMF + "/" + storeCount + " " + migrationFailed + "~";
            }
            else if (statusMF > 0 && statusMF == storeCount)
            {
                statusdeployment = storecountText + migrationFailed;
            }
            if (statusCom > 0 && statusCom != storeCount)
            {
                statusdeployment = statusdeployment + statusCom + "/" + storeCount + " " + completed + "~";
            }
            else if (statusCom > 0 && statusCom == storeCount)
            {
                statusdeployment = storecountText + completed;
            }
            if (statusFailed > 0 && statusFailed != storeCount)
            {
                statusdeployment = statusdeployment + statusFailed + "/" + storeCount + " " + failed + "~";
            }
            else if (statusFailed > 0 && statusFailed == storeCount)
            {
                statusdeployment = storecountText + failed;
            }
            if (statusDDP > 0 && statusDDP != storeCount)
            {
                statusdeployment = statusdeployment + statusDDP + "/" + storeCount + " " + doDeployPending + "~";
            }
            else if (statusDDP > 0 && statusDDP == storeCount)
            {
                statusdeployment = storecountText + doDeployPending;
            }
            if (statusDDS > 0 && statusDDS != storeCount)
            {
                statusdeployment = statusdeployment + statusDDS + "/" + storeCount + " " + doDeploySuccess + "~";
            }
            else if (statusDDS > 0 && statusDDS == storeCount)
            {
                statusdeployment = storecountText + doDeploySuccess;
            }
            if (statusDDF > 0 && statusDDF != storeCount)
            {
                statusdeployment = statusdeployment + statusDDF + "/" + storeCount + " " + doDeployFailed + "~";
            }
            else if (statusDDF > 0 && statusDDF == storeCount)
            {
                statusdeployment = storecountText + doDeployFailed;
            }
            if (statusDIP > 0 && statusDIP != storeCount)
            {
                statusdeployment = statusdeployment + statusDIP + "/" + storeCount + " " + deploymentInProgress + "~";
            }
            else if (statusDIP > 0 && statusDIP == storeCount)
            {
                statusdeployment = storecountText + deploymentInProgress;
            }

            return statusdeployment;
        }
    }
}
