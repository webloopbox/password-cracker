using backend___calculating.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend___calculating.Controllers
{
    [ApiController]
    [Route("api/calculating")]
    public class CheckController : ControllerBase
    {
        private readonly CheckService checkService;

        public CheckController(CheckService checkService) {
            this.checkService = checkService;
        }

        [HttpGet("check-connection")]
        public IActionResult CheckConnectionWithCalculatingServer()
        {
            return new OkResult();
        }

        [HttpPost("check-dictionary-hash")]
        public IActionResult CheckDictionaryHash()
        {
            return checkService.HandleCheckDictionaryHashRequest(HttpContext);
        }
    }
}