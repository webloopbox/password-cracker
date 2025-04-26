using backend___calculating.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace backend___calculating.Controllers
{
    [ApiController]
    [Route("api/calculating")]
    public class CheckController : ControllerBase
    {
        private readonly ICheckService checkService;

        public CheckController(ICheckService checkService) {
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