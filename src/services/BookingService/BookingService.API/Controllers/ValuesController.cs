using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TBE.BookingService.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> all()
        {
            return Ok(new { message="testing"});
        }


    }
}
