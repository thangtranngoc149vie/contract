using System.Data;
using Contracts.Api.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Contracts.Api.Controllers;

[ApiController]
[Route("api/v1/projects")]
public sealed class ProjectsController : ControllerBase
{
    private const string ItemsSql = @"SELECT id, code, name
FROM public.projects
WHERE (@keyword IS NULL OR name ILIKE '%'||@keyword||'%' OR code ILIKE '%'||@keyword||'%')
ORDER BY name ASC
LIMIT @page_size OFFSET @offset;";

    private const string CountSql = @"SELECT COUNT(*)
FROM public.projects
WHERE (@keyword IS NULL OR name ILIKE '%'||@keyword||'%' OR code ILIKE '%'||@keyword||'%');";

    private readonly NpgsqlConnection _connection;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(NpgsqlConnection connection, ILogger<ProjectsController> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProjectListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery(Name = "page_size")] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        _logger.LogInformation("Searching projects with keyword {Keyword}, page {Page}, pageSize {PageSize}.", keyword, page, pageSize);

        if (_connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync();
        }

        var parameters = new { keyword, page_size = pageSize, offset };
        var items = await _connection.QueryAsync<ProjectListItem>(ItemsSql, parameters);
        var total = await _connection.ExecuteScalarAsync<long>(CountSql, parameters);

        var response = new PagedResult<ProjectListItem>
        {
            Items = items.ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total
        };

        _logger.LogInformation("Found {Total} project results for keyword {Keyword} on page {Page}.", total, keyword, page);
        return Ok(response);
    }
}
