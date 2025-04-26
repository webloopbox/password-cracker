using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using backend___central.Services;

namespace backend___central.Controllers
{
    [ApiController]
    [Route("api/cracking")]
    public class CrackingController : ControllerBase
    {
        private readonly ICrackingService crackingService;

        public CrackingController(ICrackingService crackingService)
        {
            this.crackingService = crackingService;
        }

        [HttpPost("brute-force")]
        public async Task<IActionResult> CrackBruteForce()
        {
            return await crackingService.HandleBruteForceRequest(HttpContext);
        }

        [HttpPost("dictionary")]
        public async Task<IActionResult> CrackDictionary()
        {
            return await crackingService.HandleDictionaryCracking(HttpContext);
        }
    }
}