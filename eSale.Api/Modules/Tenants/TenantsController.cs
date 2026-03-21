using eSale.Application.Modules.Tenants.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace eSale.Api.Modules.Tenants;

[ApiController]
[Route("api/[controller]")]
public class TenantsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TenantsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("provision")]
    public async Task<ActionResult<Guid>> Provision(
        [FromBody] ProvisionTenantCommand command,
        CancellationToken cancellationToken)
    {
        var tenantId = await _mediator.Send(command, cancellationToken);
        return Ok(tenantId);
    }
}
