using System.Data;
using System.Linq;
using System.Text.Json;
using Contracts.Api.Infrastructure;
using Contracts.Api.Models;
using Contracts.Api.Security;
using Contracts.Api.Services;
using Contracts.Api.Validation;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Contracts.Api.Controllers;

[ApiController]
[Route("api/v1/contracts")]
public sealed class ContractsController : ControllerBase
{
    private const string DuplicateCodeSql = "SELECT 1 FROM public.contracts WHERE project_id=@project_id AND code=@code LIMIT 1";

    private readonly NpgsqlConnection _connection;
    private readonly IUserContext _userContext;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ILogger<ContractsController> _logger;

    public ContractsController(NpgsqlConnection connection, IUserContext userContext, ILogger<ContractsController> logger)
    {
        _connection = connection;
        _userContext = userContext;
        _logger = logger;
        _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        _serializerOptions.Converters.Add(new DateOnlyJsonConverter());
    }

    [HttpGet("meta")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ContractsMetaResponse), StatusCodes.Status200OK)]
    public IActionResult GetMeta()
    {
        _logger.LogInformation("Returning contracts metadata.");
        var response = new ContractsMetaResponse
        {
            ScopeTypes = new[] { "project", "package" },
            SignatureStatuses = new[] { "none", "pending", "signed" },
            StateInitial = "draft",
            Currency = "VND",
            Constraints = new ContractConstraints
            {
                ValueMin = 1,
                WarrantyMin = 0,
                WarrantyLessOrEqualValue = true
            }
        };

        return Ok(response);
    }

    [HttpHead("exists")]
    [AllowAnonymous]
    public async Task<IActionResult> Exists(
        [FromQuery(Name = "project_id")] Guid projectId,
        [FromQuery(Name = "code")] string code)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(code))
        {
            _logger.LogWarning("Exists check failed because projectId or code is missing.");
            return NotFound();
        }

        _logger.LogInformation("Checking existence of contract code {Code} for project {ProjectId}.", code, projectId);
        await EnsureConnectionOpenAsync();
        var exists = await _connection.ExecuteScalarAsync<int?>(DuplicateCodeSql, new { project_id = projectId, code });
        if (exists.HasValue)
        {
            Response.Headers.Append("X-Exists", "true");
            _logger.LogInformation("Contract code {Code} already exists for project {ProjectId}.", code, projectId);
            return Ok();
        }

        _logger.LogInformation("Contract code {Code} does not exist for project {ProjectId}.", code, projectId);
        return NotFound();
    }

    [HttpPost("validate")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ContractValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ContractValidationResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Validate([FromBody] ContractRequest request)
    {
        _logger.LogInformation("Validating draft contract for project {ProjectId} with code {Code}.", request.ProjectId, request.Code);
        var errors = ContractValidator.Validate(request).ToList();

        if (request.ProjectId != Guid.Empty && !string.IsNullOrWhiteSpace(request.Code))
        {
            await EnsureConnectionOpenAsync();
            var exists = await _connection.ExecuteScalarAsync<int?>(DuplicateCodeSql, new { project_id = request.ProjectId, code = request.Code });
            if (exists.HasValue)
            {
                _logger.LogInformation("Validation found duplicate contract code {Code} for project {ProjectId}.", request.Code, request.ProjectId);
                errors.Add(new ValidationError
                {
                    Field = "code",
                    Message = "code already exists in the project",
                    Code = "duplicate_code"
                });
            }
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("Validation failed for contract code {Code} in project {ProjectId} with {ErrorCount} errors.", request.Code, request.ProjectId, errors.Count);
            return UnprocessableEntity(new ContractValidationResponse
            {
                Valid = false,
                Errors = errors
            });
        }

        _logger.LogInformation("Validation succeeded for contract code {Code} in project {ProjectId}.", request.Code, request.ProjectId);
        return Ok(new ContractValidationResponse
        {
            Valid = true,
            Errors = new List<ValidationError>()
        });
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ContractCreate)]
    [ProducesResponseType(typeof(ContractCreateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ContractValidationResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] ContractRequest request)
    {
        _logger.LogInformation("Creating contract code {Code} for project {ProjectId}.", request.Code, request.ProjectId);
        var errors = ContractValidator.Validate(request);
        if (errors.Count > 0)
        {
            _logger.LogWarning("Contract creation validation failed for contract code {Code} in project {ProjectId} with {ErrorCount} errors.", request.Code, request.ProjectId, errors.Count);
            return UnprocessableEntity(new ContractValidationResponse
            {
                Valid = false,
                Errors = errors.ToList()
            });
        }

        await EnsureConnectionOpenAsync();

        var exists = await _connection.ExecuteScalarAsync<int?>(DuplicateCodeSql, new { project_id = request.ProjectId, code = request.Code });
        if (exists.HasValue)
        {
            _logger.LogWarning("Contract creation failed because code {Code} already exists for project {ProjectId}.", request.Code, request.ProjectId);
            return Conflict(new ErrorResponse
            {
                Code = "duplicate_code",
                Message = "code already exists in the project",
                Field = "code"
            });
        }

        await using var transaction = await _connection.BeginTransactionAsync();
        try
        {
            var jsonPaymentSchedule = JsonSerializer.Serialize(request.PaymentSchedule, _serializerOptions);
            var userId = _userContext.GetCurrentUserId();

            var result = await _connection.QuerySingleAsync<(Guid Id, DateTime CreatedAt, int Version)>(
                @"INSERT INTO public.contracts (
                    scope_type, project_id, package_id, code, name,
                    value_vnd, warranty_value_vnd, start_date, end_date,
                    description, payment_schedule, created_by)
                  VALUES (
                    @ScopeType, @ProjectId, @PackageId, @Code, @Name,
                    @ValueVnd, @WarrantyValueVnd, @StartDate, @EndDate,
                    @Description, @PaymentSchedule::jsonb, @CreatedBy)
                  RETURNING id, created_at, version;",
                new
                {
                    ScopeType = request.ScopeType,
                    ProjectId = request.ProjectId,
                    PackageId = request.PackageId,
                    Code = request.Code,
                    Name = request.Name,
                    ValueVnd = request.ValueVnd,
                    WarrantyValueVnd = request.WarrantyValueVnd,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    Description = request.Description,
                    PaymentSchedule = jsonPaymentSchedule,
                    CreatedBy = userId
                }, transaction);

            await _connection.ExecuteAsync(
                @"INSERT INTO public.outbox_events(aggregate_type, aggregate_id, event_type, payload)
                  VALUES ('contract', @AggregateId, 'contract.created', @Payload::jsonb);",
                new
                {
                    AggregateId = result.Id,
                    Payload = JsonSerializer.Serialize(new
                    {
                        id = result.Id,
                        project_id = request.ProjectId,
                        code = request.Code,
                        name = request.Name,
                        value_vnd = request.ValueVnd
                    }, _serializerOptions)
                }, transaction);

            await transaction.CommitAsync();

            var etag = $"W/\"{result.Version}\"";
            Response.Headers.ETag = etag;

            var createdAt = DateTime.SpecifyKind(result.CreatedAt, DateTimeKind.Utc);

            var response = new ContractCreateResponse
            {
                Id = result.Id,
                Code = request.Code,
                State = "draft",
                ETag = etag,
                CreatedAt = new DateTimeOffset(createdAt)
            };

            _logger.LogInformation("Successfully created contract {ContractId} with code {Code} for project {ProjectId}.", result.Id, request.Code, request.ProjectId);
            return Created($"/api/v1/contracts/{result.Id}", response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error occurred while creating contract code {Code} for project {ProjectId}.", request.Code, request.ProjectId);
            throw;
        }
    }

    private async Task EnsureConnectionOpenAsync()
    {
        if (_connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync();
        }
    }
}
