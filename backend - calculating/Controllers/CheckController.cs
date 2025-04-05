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

        [HttpPost("check-connection")]
        public IActionResult CheckConnectionWithCalculatingServer()
        {
            return checkService.HandleCheckConnectionRequest(HttpContext);
        }
    }
}