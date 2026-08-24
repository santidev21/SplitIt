using System.ComponentModel.DataAnnotations;
using SplitIt.Application.DTOs;

namespace SplitIt.Tests;

public class ValidationTests
{
    private static IList<ValidationResult> Validate(object model)
    {
        var ctx = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, ctx, results, true);
        return results;
    }

    [Theory]
    [InlineData("", "test@example.com", "Password123!")] // name too short
    [InlineData("A", "test@example.com", "Password123!")]
    [InlineData("Valid Name", "not-an-email", "Password123!")]
    [InlineData("Valid Name", "test@example.com", "short")]
    public void RegisterRequest_Invalid_ShouldFail(string name, string email, string pwd)
    {
        var dto = new RegisterRequestDto { Name = name, Email = email, Password = pwd };
        Assert.NotEmpty(Validate(dto));
    }

    [Fact]
    public void RegisterRequest_Valid_ShouldPass()
    {
        var dto = new RegisterRequestDto { Name = "Alice", Email = "alice@test.com", Password = "StrongPass123!" };
        Assert.Empty(Validate(dto));
    }

    [Theory]
    [InlineData(0, 1)] // amount 0
    [InlineData(-5, 1)]
    [InlineData(0.001, 0)] // paidBy 0 invalid
    public void CreateExpense_Invalid_ShouldFail(decimal amount, int paidBy)
    {
        var dto = new CreateExpenseDto
        {
            Title = "Test",
            Amount = amount,
            Date = DateTime.UtcNow,
            GroupId = 1,
            PaidById = paidBy,
            Participants = new List<ExpenseParticipantDto> { new() { UserId = 1, AmountOwed = amount } }
        };
        // If amount 0, also sum mismatch etc, but DataAnnotations already fails Range
        Assert.NotEmpty(Validate(dto));
    }

    [Fact]
    public void CreateExpense_NoParticipants_ShouldFail()
    {
        var dto = new CreateExpenseDto
        {
            Title = "Test",
            Amount = 100,
            Date = DateTime.UtcNow,
            GroupId = 1,
            PaidById = 1,
            Participants = new List<ExpenseParticipantDto>()
        };
        Assert.NotEmpty(Validate(dto));
    }

    [Fact]
    public void CreateGroup_InvalidName_ShouldFail()
    {
        var dto = new CreateGroupDto { Name = "A", Description = "Desc", CurrencyId = 1 };
        Assert.NotEmpty(Validate(dto));
    }

    [Fact]
    public void RegisterPayment_ZeroAmount_ShouldFail()
    {
        var dto = new RegisterPaymentDto { PayerUserId = 1, GroupId = 1, Amount = 0 };
        Assert.NotEmpty(Validate(dto));
    }
}
