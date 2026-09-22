using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace OrderIntake;

public class OrderIntakeService
{
    private static readonly HashSet<string> AllowedSpecimenTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Blood", "Urine", "Tissue", "Saliva"
    };

    private static readonly HashSet<string> AllowedPriorities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Routine", "Urgent"
    };

    public OrderResult Process(string json, DateTime? referenceDate = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return CreateMalformedResult("Input JSON is null, empty, or whitespace.");
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return CreateMalformedResult("Invalid JSON structure.");
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return CreateMalformedResult("Root element must be a JSON object.");
            }

            var errors = new List<ValidationError>();

            JsonElement GetField(string fieldName) =>
                doc.RootElement.TryGetProperty(fieldName, out var el) ? el : default;

            DateTime today = (referenceDate ?? DateTime.Today).Date;

            string? orderId = ValidateStringField(GetField("orderId"), "orderId", 20, errors);
            string? patientId = ValidateStringField(GetField("patientId"), "patientId", 20, errors);
            string? specimenId = ValidateStringField(GetField("specimenId"), "specimenId", 20, errors);
            string? normalizedSpecimenType = ValidateSpecimenType(GetField("specimenType"), errors);
            string? normalizedPriority = ValidatePriority(GetField("priority"), errors);
            DateTime? collectionDate = ValidateCollectionDate(GetField("collectionDate"), today, errors);
            List<string>? requestedTests = ValidateRequestedTests(GetField("requestedTests"), errors);

            if (errors.Count > 0)
            {
                return new OrderResult
                {
                    Status = OrderStatus.Rejected,
                    Order = null,
                    Errors = errors
                };
            }

            return new OrderResult
            {
                Status = OrderStatus.Accepted,
                Errors = new List<ValidationError>(),
                Order = new Order
                {
                    OrderId = orderId!,
                    PatientId = patientId!,
                    SpecimenId = specimenId!,
                    SpecimenType = normalizedSpecimenType!,
                    Priority = normalizedPriority!,
                    CollectionDate = collectionDate!.Value,
                    RequestedTests = requestedTests!
                }
            };
        }
    }

    private static OrderResult CreateMalformedResult(string message)
    {
        return new OrderResult
        {
            Status = OrderStatus.Rejected,
            Order = null,
            Errors = new List<ValidationError>
            {
                new ValidationError
                {
                    Field = "$",
                    Code = "MALFORMED_INPUT",
                    Message = message
                }
            }
        };
    }

    private static string? ValidateStringField(JsonElement el, string fieldName, int maxLength, List<ValidationError> errors)
    {
        if (el.ValueKind == JsonValueKind.Undefined || el.ValueKind == JsonValueKind.Null)
        {
            errors.Add(new ValidationError { Field = fieldName, Code = "REQUIRED", Message = $"{fieldName} is required." });
            return null;
        }

        if (el.ValueKind != JsonValueKind.String)
        {
            errors.Add(new ValidationError { Field = fieldName, Code = "MALFORMED_INPUT", Message = $"{fieldName} must be a string." });
            return null;
        }

        string val = el.GetString()!;
        if (string.IsNullOrWhiteSpace(val))
        {
            errors.Add(new ValidationError { Field = fieldName, Code = "REQUIRED", Message = $"{fieldName} cannot be empty or whitespace." });
            return null;
        }

        if (val.Length > maxLength)
        {
            errors.Add(new ValidationError { Field = fieldName, Code = "MAX_LENGTH", Message = $"{fieldName} exceeds max length of {maxLength}." });
        }

        return val;
    }

    private static string? ValidateSpecimenType(JsonElement el, List<ValidationError> errors)
    {
        if (el.ValueKind == JsonValueKind.Undefined || el.ValueKind == JsonValueKind.Null)
        {
            errors.Add(new ValidationError { Field = "specimenType", Code = "REQUIRED", Message = "specimenType is required." });
            return null;
        }

        if (el.ValueKind != JsonValueKind.String)
        {
            errors.Add(new ValidationError { Field = "specimenType", Code = "MALFORMED_INPUT", Message = "specimenType must be a string." });
            return null;
        }

        string raw = el.GetString()!;
        if (string.IsNullOrWhiteSpace(raw))
        {
            errors.Add(new ValidationError { Field = "specimenType", Code = "REQUIRED", Message = "specimenType cannot be empty." });
            return null;
        }

        var match = AllowedSpecimenTypes.FirstOrDefault(s => s.Equals(raw, StringComparison.OrdinalIgnoreCase));
        if (match == null)
        {
            errors.Add(new ValidationError { Field = "specimenType", Code = "INVALID_VALUE", Message = $"Invalid specimenType '{raw}'." });
            return null;
        }

        return match;
    }

    private static string? ValidatePriority(JsonElement el, List<ValidationError> errors)
    {
        if (el.ValueKind == JsonValueKind.Undefined || el.ValueKind == JsonValueKind.Null)
        {
            errors.Add(new ValidationError { Field = "priority", Code = "REQUIRED", Message = "priority is required." });
            return null;
        }

        if (el.ValueKind != JsonValueKind.String)
        {
            errors.Add(new ValidationError { Field = "priority", Code = "MALFORMED_INPUT", Message = "priority must be a string." });
            return null;
        }

        string raw = el.GetString()!;
        if (string.IsNullOrWhiteSpace(raw))
        {
            errors.Add(new ValidationError { Field = "priority", Code = "REQUIRED", Message = "priority cannot be empty." });
            return null;
        }

        var match = AllowedPriorities.FirstOrDefault(p => p.Equals(raw, StringComparison.OrdinalIgnoreCase));
        if (match == null)
        {
            errors.Add(new ValidationError { Field = "priority", Code = "INVALID_VALUE", Message = $"Invalid priority '{raw}'." });
            return null;
        }

        return match;
    }

    private static DateTime? ValidateCollectionDate(JsonElement el, DateTime today, List<ValidationError> errors)
    {
        if (el.ValueKind == JsonValueKind.Undefined || el.ValueKind == JsonValueKind.Null)
        {
            errors.Add(new ValidationError { Field = "collectionDate", Code = "REQUIRED", Message = "collectionDate is required." });
            return null;
        }

        if (el.ValueKind != JsonValueKind.String)
        {
            errors.Add(new ValidationError { Field = "collectionDate", Code = "MALFORMED_INPUT", Message = "collectionDate must be a string." });
            return null;
        }

        string raw = el.GetString()!;
        if (string.IsNullOrWhiteSpace(raw))
        {
            errors.Add(new ValidationError { Field = "collectionDate", Code = "REQUIRED", Message = "collectionDate cannot be empty." });
            return null;
        }

        if (!DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            errors.Add(new ValidationError { Field = "collectionDate", Code = "INVALID_FORMAT", Message = "collectionDate must strictly follow yyyy-MM-dd format and be a valid calendar date." });
            return null;
        }

        if (parsedDate.Date > today)
        {
            errors.Add(new ValidationError { Field = "collectionDate", Code = "FUTURE_DATE", Message = "collectionDate cannot be in the future." });
            return null;
        }

        return parsedDate;
    }

    private static List<string>? ValidateRequestedTests(JsonElement el, List<ValidationError> errors)
    {
        if (el.ValueKind == JsonValueKind.Undefined || el.ValueKind == JsonValueKind.Null)
        {
            errors.Add(new ValidationError { Field = "requestedTests", Code = "REQUIRED", Message = "requestedTests is required." });
            return null;
        }

        if (el.ValueKind != JsonValueKind.Array)
        {
            errors.Add(new ValidationError { Field = "requestedTests", Code = "MALFORMED_INPUT", Message = "requestedTests must be a JSON array." });
            return null;
        }

        var items = el.EnumerateArray().ToList();
        if (items.Count == 0)
        {
            errors.Add(new ValidationError { Field = "requestedTests", Code = "REQUIRED", Message = "requestedTests must contain at least one item." });
            return null;
        }

        var list = new List<string>();
        var seenCases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool hasError = false;

        foreach (var item in items)
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                errors.Add(new ValidationError { Field = "requestedTests", Code = "MALFORMED_INPUT", Message = "requestedTests items must be strings." });
                return null;
            }

            string testName = item.GetString()!;
            if (string.IsNullOrWhiteSpace(testName))
            {
                if (!hasError)
                {
                    errors.Add(new ValidationError { Field = "requestedTests", Code = "INVALID_VALUE", Message = "requestedTests items cannot be empty or whitespace." });
                    hasError = true;
                }
                continue;
            }

            if (!seenCases.Add(testName))
            {
                if (!hasError)
                {
                    errors.Add(new ValidationError { Field = "requestedTests", Code = "DUPLICATE", Message = "requestedTests contains duplicate test names." });
                    hasError = true;
                }
            }
            else
            {
                list.Add(testName);
            }
        }

        return hasError ? null : list;
    }
}