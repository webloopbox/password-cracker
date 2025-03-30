using backend___central.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend___central.Controllers
{
    [ApiController]
    [Route("api/cracking")]
    public class CrackingController(IEnumerable<ILogService> logServices) : ControllerBase
    {
        private readonly IEnumerable<ILogService> logServices = logServices;


        [HttpPost("brute-force")]
        public IActionResult CrackBruteForce()
        {
            InfoLogService? infoLogService = logServices?.OfType<InfoLogService>().FirstOrDefault();
            infoLogService?.LogMessage("Starting cracking password for user: " + " using brute force method");
            return Ok("BruteForce cracking initiated.");
        }

        [HttpPost("dictionary")]
        public IActionResult CrackDictionary()
        {
            InfoLogService? infoLogService = logServices?.OfType<InfoLogService>().FirstOrDefault();
            infoLogService?.LogMessage("Starting cracking password for user: " + " using dictionary method");
            return Ok("Dictionary cracking initiated.");
        }
    }
}