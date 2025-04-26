using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using backend___calculating.Interfaces;

namespace backend___calculating.Controllers
{
    [ApiController]
    [Route("api/synchronizing")]
    public class BruteForceController : ControllerBase
    {
        private readonly IBruteForceService bruteForceService;

        public BruteForceController(IBruteForceService bruteForceService)
        {
            this.bruteForceService = bruteForceService ?? throw new ArgumentNullException(nameof(bruteForceService));
        }

        [HttpPost("brute-force")]
        public async Task<IActionResult> SynchronizeBruteForce()
        {
            Console.WriteLine("Synchronizing brute force results...");
            if (HttpContext == null)
            {
                return BadRequest("HttpContext is null.");
            }
            return await bruteForceService.SynchronizeBruteForce(HttpContext);
        }
    }
}