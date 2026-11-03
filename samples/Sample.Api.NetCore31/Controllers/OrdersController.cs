using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Sample.Api.NetCore31.Controllers;

public sealed class CreateOrderRequest
{
    [Required]
    public string? Customer { get; set; }

    [Range(1, 1000)]
    public int Amount { get; set; }
}

[ApiController]
[Route("orders")]
public sealed class OrdersController : ControllerBase
{
    // An invalid body gets the template with field errors.
    [HttpPost]
    public IActionResult Create(CreateOrderRequest request) => Ok(request);

    // An empty 400 or 404 gets the template.
    [HttpGet("{id:int}")]
    public IActionResult Get(int id) => id > 0 ? NotFound() : BadRequest();
}
