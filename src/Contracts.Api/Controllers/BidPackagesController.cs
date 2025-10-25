using System.Data;
using Contracts.Api.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Contracts.Api.Controllers;

[ApiController]
[Route("api/v1/bid-packages")]
public sealed class BidPackagesController : ControllerBase
{
    private const string ItemsSql = @"SELECT id, code, name
FROM public.bid_packages
WHERE project_id = @project_id
  AND (@keyword IS NULL OR name ILIKE '%'||@keyword||'%' OR code ILIKE '%'||@keyword||'%')
ORDER BY name ASC
LIMIT @page_size OFFSET @offset;";

    private const string CountSql = @"SELECT COUNT(*)
FROM public.bid_packages
WHERE project_id = @project_id
  AND (@keyword IS NULL OR name ILIKE '%'||@keyword||'%' OR code ILIKE '%'||@keyword||'%');";

    private readonly NpgsqlConnection _connection;
    private readonly ILogger<BidPackagesController> _logger;

    public BidPackagesController(NpgsqlConnection connection, ILogger<BidPackagesController> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<BidPackageListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery(Name = "project_id")] Guid projectId, [FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery(Name = "page_size")] int pageSize = 20)
    {
        if (projectId == Guid.Empty)
        {
            _logger.LogWarning("Bid packages search rejected because project_id is missing.");
            return BadRequest(new ErrorResponse
            {
                Code = "invalid_project_id",
                Message = "project_id is required",
                Field = "project_id"
            });
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        _logger.LogInformation("Searching bid packages for project {ProjectId} with keyword {Keyword}, page {Page}, pageSize {PageSize}.", projectId, keyword, page, pageSize);

        if (_connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync();
        }

        var parameters = new { project_id = projectId, keyword, page_size = pageSize, offset };
        var items = await _connection.QueryAsync<BidPackageListItem>(ItemsSql, parameters);
        var total = await _connection.ExecuteScalarAsync<long>(CountSql, parameters);

        var response = new PagedResult<BidPackageListItem>
        {
            Items = items.ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total
        };

        _logger.LogInformation("Found {Total} bid package results for project {ProjectId} with keyword {Keyword} on page {Page}.", total, projectId, keyword, page);
        return Ok(response);
    }
}
