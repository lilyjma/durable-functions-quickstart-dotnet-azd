using System.Net.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Company.Function
{
    public static class ConcurrentFetchOrchestration
    {
        [Function(nameof(ConcurrentFetchOrchestration))]
        public static async Task<string> RunOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(ConcurrentFetchOrchestration));
            logger.LogInformation("Fetching data.");
            var parallelTasks = new List<Task<string>>();
            
            // List of URLs to fetch titles from
            var urls = new List<string>
            {
                "https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-overview",
                "https://learn.microsoft.com/azure/azure-functions/durable/durable-task-scheduler/durable-task-scheduler",
                "https://learn.microsoft.com/azure/azure-functions/functions-scenarios",
                "https://learn.microsoft.com/azure/azure-functions/functions-create-ai-enabled-apps",
            };

            // Run fetching tasks in parallel
            foreach (var url in urls)
            {
                Task<string> task = context.CallActivityAsync<string>(nameof(FetchTitleAsync), url);
                parallelTasks.Add(task);
            }
            
            // Wait for all the parallel tasks to complete before continuing
            await Task.WhenAll(parallelTasks);
           
            // Return fetched titles as a formatted string
            return string.Join(", ", parallelTasks.Select(t => t.Result));
        }

        [Function(nameof(FetchTitleAsync))]
        public static async Task<string> FetchTitleAsync([ActivityTrigger] string url, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("FetchTitleAsync");
            logger.LogInformation("Fetching from url {url}.", url);

            HttpClient client = new HttpClient();

            try
            {
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string content = await response.Content.ReadAsStringAsync();

                // Extract page title
                var titleMatch = System.Text.RegularExpressions.Regex.Match(content, 
                    @"<title[^>]*>([^<]+?)\s*\|\s*Microsoft Learn</title>", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                string title = titleMatch.Success ? titleMatch.Groups[1].Value.Trim() : "No title found";
                
                return title;
            }
            catch (HttpRequestException ex)
            {
                return $"Error fetching from {url}: {ex.Message}";
            }
        }

        [Function("ConcurrentFetchOrchestration_HttpStart")]
        public static async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("ConcurrentFetchOrchestration_HttpStart");

            // Function input comes from the request content.
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(ConcurrentFetchOrchestration));

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // Returns an HTTP 202 response with an instance management payload.
            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
            return await client.CreateCheckStatusResponseAsync(req, instanceId);
        }
    }
}
