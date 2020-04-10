using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace fanout_in
{
    public static class foiprocessorder
    {
        [FunctionName("foiprocessorder_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]
        HttpRequestMessage req, [DurableClient] IDurableOrchestrationClient starter, ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("foiprocessorder", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("foiprocessorder")]
        public static async Task<List<string>> RunOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var parallelTasks = new List<Task<double>>();

            var outputs = new List<string>();

            string[] products = await context.CallActivityAsync<string[]>("GetAvailableItems", "Mi Band 4, Optical Mouse - red");
            outputs.Add($"No of products in the request - {products.Count()}");
            for(int i = 0; i < products.Length; i++)
            {
                Task<double> task = context.CallActivityAsync<double>("GetInvoiceLineAmount", products[i]);
                parallelTasks.Add(task);
                outputs.Add($"Product - {products[i]}");
            }

            await Task.WhenAll(parallelTasks);
            double sum = parallelTasks.Sum(t => t.Result);

            await context.CallActivityAsync("PrintInvoice", sum);
            outputs.Add($"Invoice total - {sum}");

            return outputs;
        }

        [FunctionName("GetAvailableItems")]
        public static string[] GetAvailableItems([ActivityTrigger] string productList, ILogger log)
        {
            log.LogInformation($"Product List - {productList}.");
            return productList.Split(',');
        }

        [FunctionName("GetInvoiceLineAmount")]
        public static double GetInvoiceLineAmount([ActivityTrigger] string product, ILogger log)
        {
            Random random = new Random();
            double lineAmount = random.Next();
            log.LogInformation($"Product - {product} , Amount - {lineAmount}" );
            return lineAmount;
        }

        [FunctionName("PrintInvoice")]
        public static void PrintInvoice([ActivityTrigger] double totalAmount, ILogger log)
        {
            log.LogInformation($"Invoice Amount {totalAmount}.");
        }


    }
}