using Microsoft.AspNetCore.Mvc;
using backend___central.Services;
using System.Threading.Tasks;

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
            return await crackingService.HandlBruteForceRequest(HttpContext);
        }

        [HttpPost("dictionary")]
        public IActionResult CrackDictionary()
        {
            return crackingService.HandleDictionaryCracking(HttpContext);
        }
    }
}