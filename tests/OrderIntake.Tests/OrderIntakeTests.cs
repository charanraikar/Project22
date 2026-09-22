using System;
using OrderIntake;
using Xunit;

namespace OrderIntake.Tests;

public class OrderIntakeTests
{
    private readonly OrderIntakeService _service = new();

    [Fact]
    public void Group1_AcceptedOrder_NormalizesCasing_IgnoresUnknownFields()
    {
        string json = """
        {
            "orderId": "ORD-1005",
            "patientId": "PAT-505",
            "specimenId": "SP-9005",
            "specimenType": "bLoOd",
            "priority": "uRgEnT",
            "collectionDate": "2026-09-18",
            "requestedTests": ["Glucose", "CompleteBloodCount"],
            "senderNote": "ignore me"
        }
        """;

        var result = _service.Process(json);

        Assert.Equal(OrderStatus.Accepted, result.Status);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.Order);
        Assert.Equal("ORD-1005", result.Order.OrderId);
        Assert.Equal("Blood", result.Order.SpecimenType);
        Assert.Equal("Urgent", result.Order.Priority);
        Assert.Equal(new DateTime(2026, 9, 18), result.Order.CollectionDate);
        Assert.Equal(2, result.Order.RequestedTests.Count);
    }

    [Fact]
    public void Group2_AllErrorsAtOnce_ReturnsCompleteSetOfErrors()
    {
        string json = """
        {
            "orderId": " ",
            "patientId": "PAT-123",
            "specimenId": "SP-001",
            "specimenType": "InvalidSpecimen",
            "priority": "InvalidPriority",
            "collectionDate": "invalid-date",
            "requestedTests": []
        }
        """;

        var result = _service.Process(json);

        Assert.Equal(OrderStatus.Rejected, result.Status);
        Assert.Null(result.Order);
        Assert.Contains(result.Errors, e => e.Field == "orderId" && e.Code == "REQUIRED");
        Assert.Contains(result.Errors, e => e.Field == "specimenType" && e.Code == "INVALID_VALUE");
        Assert.Contains(result.Errors, e => e.Field == "priority" && e.Code == "INVALID_VALUE");
        Assert.Contains(result.Errors, e => e.Field == "collectionDate" && e.Code == "INVALID_FORMAT");
        Assert.Contains(result.Errors, e => e.Field == "requestedTests" && e.Code == "REQUIRED");
    }

    [Fact]
    public void Group3_IdLength_20CharsAccepted_21CharsProducesMaxLength()
    {
        string json20 = """
        {
            "orderId": "12345678901234567890",
            "patientId": "PAT-100",
            "specimenId": "SP-100",
            "specimenType": "blood",
            "priority": "routine",
            "collectionDate": "2026-09-01",
            "requestedTests": ["Glucose"]
        }
        """;

        var res20 = _service.Process(json20);
        Assert.Equal(OrderStatus.Accepted, res20.Status);

        string json21 = """
        {
            "orderId": "123456789012345678901",
            "patientId": "PAT-100",
            "specimenId": "SP-100",
            "specimenType": "blood",
            "priority": "routine",
            "collectionDate": "2026-09-01",
            "requestedTests": ["Glucose"]
        }
        """;

        var res21 = _service.Process(json21);
        Assert.Equal(OrderStatus.Rejected, res21.Status);
        Assert.Contains(res21.Errors, e => e.Field == "orderId" && e.Code == "MAX_LENGTH");
    }

    [Theory]
    [InlineData("2026-02-30", "INVALID_FORMAT")]
    [InlineData("20-09-2026", "INVALID_FORMAT")]
    [InlineData("2026/09/20", "INVALID_FORMAT")]
    public void Group4_CollectionDate_RejectsInvalidFormatAndCalendar(string dateStr, string expectedCode)
    {
        string json = $$"""
        {
            "orderId": "ORD-1",
            "patientId": "PAT-1",
            "specimenId": "SP-1",
            "specimenType": "blood",
            "priority": "routine",
            "collectionDate": "{{dateStr}}",
            "requestedTests": ["Glucose"]
        }
        """;

        var result = _service.Process(json);
        Assert.Equal(OrderStatus.Rejected, result.Status);
        Assert.Contains(result.Errors, e => e.Field == "collectionDate" && e.Code == expectedCode);
    }

    [Fact]
    public void Group4_CollectionDate_FutureDateCalculatedFromToday()
    {
        string futureDate = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");

        string json = $$"""
        {
            "orderId": "ORD-1",
            "patientId": "PAT-1",
            "specimenId": "SP-1",
            "specimenType": "blood",
            "priority": "routine",
            "collectionDate": "{{futureDate}}",
            "requestedTests": ["Glucose"]
        }
        """;

        var result = _service.Process(json);
        Assert.Equal(OrderStatus.Rejected, result.Status);
        Assert.Contains(result.Errors, e => e.Field == "collectionDate" && e.Code == "FUTURE_DATE");
    }

    [Fact]
    public void Group5_RequestedTests_EmptyListRejected()
    {
        string json = """
        {
            "orderId": "ORD-1",
            "patientId": "PAT-1",
            "specimenId": "SP-1",
            "specimenType": "blood",
            "priority": "routine",
            "collectionDate": "2026-09-01",
            "requestedTests": []
        }
        """;

        var result = _service.Process(json);
        Assert.Equal(OrderStatus.Rejected, result.Status);
        Assert.Contains(result.Errors, e => e.Field == "requestedTests" && e.Code == "REQUIRED");
    }

    [Fact]
    public void Group5_RequestedTests_CaseInsensitiveDuplicatesRejected()
    {
        string json = """
        {
            "orderId": "ORD-1",
            "patientId": "PAT-1",
            "specimenId": "SP-1",
            "specimenType": "blood",
            "priority": "routine",
            "collectionDate": "2026-09-01",
            "requestedTests": ["Glucose", "GLUCOSE"]
        }
        """;

        var result = _service.Process(json);
        Assert.Equal(OrderStatus.Rejected, result.Status);
        Assert.Contains(result.Errors, e => e.Field == "requestedTests" && e.Code == "DUPLICATE");
    }

    [Theory]
    [InlineData("broken json{")]
    [InlineData("[1, 2, 3]")]
    public void Group6_BrokenJson_ProducesSingleMalformedInputError(string json)
    {
        var result = _service.Process(json);

        Assert.Equal(OrderStatus.Rejected, result.Status);
        Assert.Null(result.Order);
        Assert.Single(result.Errors);
        Assert.Equal("$", result.Errors[0].Field);
        Assert.Equal("MALFORMED_INPUT", result.Errors[0].Code);
    }
}