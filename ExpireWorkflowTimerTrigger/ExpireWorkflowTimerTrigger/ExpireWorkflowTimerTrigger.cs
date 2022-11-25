using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace ExpireWorkflowTimerTrigger
{
    public static class ExpireWorkflowTimerTrigger
    {
        [FunctionName("ExpireWorkflowTimerTrigger")]
        public static async Task Run([TimerTrigger("%TimerSchedule%")] TimerInfo myTimer, ILogger log)
        {
            try
            {
                // REST call to the Target Microservice.
                using (var httpClient = new HttpClient())
                {
                    using (var request = new HttpRequestMessage(HelperMethods.GetHttpMethod(Environment.GetEnvironmentVariable("UseHttpMethod")), Environment.GetEnvironmentVariable("TargetServiceUrl")))
                    {
                        using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                        {
                            response.EnsureSuccessStatusCode();

                            log.LogInformation($"C# Timer trigger function received response: {response.ToString()}");
                        }
                    }
                }
                log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            }
            catch (Exception ex)
            {
                log.LogInformation($"C# Timer trigger function errored at {ex}", ex);
            }
        }
    }
}
