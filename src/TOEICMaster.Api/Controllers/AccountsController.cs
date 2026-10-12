using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TOEICMaster.Application.Accounts.Queries.GetAccounts;

namespace TOEICMaster.Api.Controllers;

[ApiController]
[Route("api/v1/accounts")]
[Authorize(Policy = "AccountAdministration")]
public sealed class AccountsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AccountPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AccountPageDto>> GetAccounts(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GetAccountsQuery(pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }
}
