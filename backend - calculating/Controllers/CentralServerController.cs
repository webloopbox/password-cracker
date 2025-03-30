using Microsoft.AspNetCore.Mvc;

namespace backend___calculating.Controllers
{
    [ApiController]
    [Route("api/central")]
    public class CalculatingServerController() : ControllerBase
    {
        [HttpGet("check-connection")]
        public IResult CheckConnectionWithCalculatingServer()
        {
            return Results.Ok();
        }
    }
}