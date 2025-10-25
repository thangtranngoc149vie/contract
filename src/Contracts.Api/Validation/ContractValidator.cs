using Contracts.Api.Models;
using System.Linq;

namespace Contracts.Api.Validation;

public static class ContractValidator
{
    private static readonly HashSet<string> ValidScopeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "project", "package"
    };

    public static IReadOnlyCollection<ValidationError> Validate(ContractRequest request)
    {
        var errors = new List<ValidationError>();
        request.PaymentSchedule ??= new List<PaymentScheduleItem>();

        if (!ValidScopeTypes.Contains(request.ScopeType))
        {
            errors.Add(new ValidationError
            {
                Field = "scope_type",
                Message = "scope_type must be either project or package",
                Code = "invalid_scope_type"
            });
        }

        if (request.ProjectId == Guid.Empty)
        {
            errors.Add(new ValidationError
            {
                Field = "project_id",
                Message = "project_id is required",
                Code = "required"
            });
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            errors.Add(new ValidationError
            {
                Field = "code",
                Message = "code is required",
                Code = "required"
            });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add(new ValidationError
            {
                Field = "name",
                Message = "name is required",
                Code = "required"
            });
        }

        if (request.ValueVnd <= 0)
        {
            errors.Add(new ValidationError
            {
                Field = "value_vnd",
                Message = "value_vnd must be greater than 0",
                Code = "value_must_be_positive"
            });
        }

        if (request.WarrantyValueVnd < 0)
        {
            errors.Add(new ValidationError
            {
                Field = "warranty_value_vnd",
                Message = "warranty_value_vnd must be greater or equal to 0",
                Code = "warranty_negative"
            });
        }

        if (request.WarrantyValueVnd > request.ValueVnd)
        {
            errors.Add(new ValidationError
            {
                Field = "warranty_value_vnd",
                Message = "warranty_value_vnd must be less or equal to value_vnd",
                Code = "warranty_exceeds_value"
            });
        }

        if (request.StartDate > request.EndDate)
        {
            errors.Add(new ValidationError
            {
                Field = "start_date",
                Message = "start_date must be less or equal to end_date",
                Code = "invalid_date_range"
            });
        }

        if (string.Equals(request.ScopeType, "project", StringComparison.OrdinalIgnoreCase) && request.PackageId is not null)
        {
            errors.Add(new ValidationError
            {
                Field = "package_id",
                Message = "package_id must be null when scope_type is project",
                Code = "package_not_allowed"
            });
        }

        if (string.Equals(request.ScopeType, "package", StringComparison.OrdinalIgnoreCase) && request.PackageId is null)
        {
            errors.Add(new ValidationError
            {
                Field = "package_id",
                Message = "package_id is required when scope_type is package",
                Code = "package_required"
            });
        }

        foreach (var (item, index) in request.PaymentSchedule.Select((value, index) => (value, index)))
        {
            if (item.AmountVnd <= 0)
            {
                errors.Add(new ValidationError
                {
                    Field = $"payment_schedule[{index}].amount_vnd",
                    Message = "amount_vnd must be greater than 0",
                    Code = "invalid_payment_amount"
                });
            }

            if (item.DueDate == default)
            {
                errors.Add(new ValidationError
                {
                    Field = $"payment_schedule[{index}].due_date",
                    Message = "due_date is required",
                    Code = "required"
                });
            }
        }

        return errors;
    }
}
