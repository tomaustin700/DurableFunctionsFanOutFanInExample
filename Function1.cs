using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace DurableFunctionFanOutInExample
{
    public static class Function1
    {
        private static readonly HttpClient _client = new HttpClient();

        [FunctionName(nameof(RunOrchestrator))]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var parallelTasks = new List<Task<string>>();

            var outputs = new List<string>();

            parallelTasks.Add(context.CallActivityAsync<string>(nameof(CallAPI), "mark"));
            parallelTasks.Add(context.CallActivityAsync<string>(nameof(CallAPI), "jeremy"));
            parallelTasks.Add(context.CallActivityAsync<string>(nameof(CallAPI), "hans"));

            await Task.WhenAll(parallelTasks);

            foreach(var task in parallelTasks)
            {
                outputs.Add(task.Result);
            }

            return outputs;
        }

        [FunctionName(nameof(CallAPI))]
        public static async Task<string> CallAPI([ActivityTrigger] string details)
        {
            var result = await _client.GetAsync($"https://api.peepquote.com/v2/search?searchTerm={details}");
            return await result.Content.ReadAsStringAsync();
        }

        [FunctionName(nameof(CheckStatusResponse))]
        public static async Task<HttpResponseMessage> CheckStatusResponse(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync(nameof(RunOrchestrator), null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName(nameof(WaitForResponseAndReturn))]
        public static async Task<HttpResponseMessage> WaitForResponseAndReturn(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestMessage req,
           [DurableClient] IDurableOrchestrationClient starter,
           ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync(nameof(RunOrchestrator), null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return await starter.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId);
        }
    }
}