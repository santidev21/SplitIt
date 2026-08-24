using System.Text.Json;

namespace SplitIt.Tests;

/// <summary>
/// Ensures DTOs don't bind sensitive properties.
/// We simulate deserialization: extra JSON properties like RoleId should be ignored, not mapped.
/// </summary>
public class MassAssignmentTests
{
    [Fact]
    public void RegisterRequest_ExtraRoleProperty_ShouldBeIgnored()
    {
        var json = """{"name":"Alice","email":"alice@test.com","password":"Pass12345!","roleId":1,"Role":"admin"}""";
        var dto = JsonSerializer.Deserialize<SplitIt.Application.DTOs.RegisterRequestDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(dto);
        // DTO has no Role property, so deserialization shouldn't throw and shouldn't set anything extra
        // We verify that dotnet's default doesn't create extra property
        Assert.Equal("Alice", dto!.Name);
        // Ensure no Role property exists via reflection
        Assert.Null(dto.GetType().GetProperty("RoleId"));
        Assert.Null(dto.GetType().GetProperty("Role"));
    }

    [Fact]
    public void CreateExpense_ExtraCreatedBy_ShouldBeIgnored()
    {
        var json = """{"title":"Test","amount":100,"date":"2026-01-01T00:00:00Z","groupId":1,"paidById":1,"createdById":999,"participants":[{"userId":1,"amountOwed":100}]}""";
        var dto = JsonSerializer.Deserialize<SplitIt.Application.DTOs.CreateExpenseDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(dto);
        Assert.Null(dto!.GetType().GetProperty("CreatedById"));
    }
}
