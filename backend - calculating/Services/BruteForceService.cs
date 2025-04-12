using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System;
using Microsoft.AspNetCore.Mvc;

namespace backend___calculating.Services
{
    public class BruteForceService : IBruteForceService
    {
        public async Task<IActionResult> SynchronizeBruteForce(HttpContext httpContext)
        {
            if (httpContext == null || httpContext.Request?.Body == null)
            {
                return new BadRequestObjectResult("HttpContext or Request.Body is null.");
            }

            using StreamReader reader = new(httpContext.Request.Body);
            string bodyContent = await reader.ReadToEndAsync();

            // Log the request body content
            Console.WriteLine($"Request body content from calculating host: {bodyContent}");

            // Simulate handling the brute force cracking
            Console.WriteLine("Handling brute force cracking...");

            return new OkObjectResult("Synchronization completed successfully.");
        }
    }
}