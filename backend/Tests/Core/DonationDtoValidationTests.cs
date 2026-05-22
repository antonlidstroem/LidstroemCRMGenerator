using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Lidstroem.Plugins.Donations.DTOs;
using Xunit;

namespace Lidstroem.Tests.Core;

public class DonationDtoValidationTests
{
    private static List<ValidationResult> Validate(DonationDto dto)
    {
        var context = new ValidationContext(dto);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, context, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void ValidDto_WithProjectTarget_PassesValidation()
    {
        var dto = new DonationDto
        {
            Amount     = 100m,
            Currency   = "SEK",
            DonorId    = 1,
            DonorType  = "Actor",
            TargetId   = 5,
            TargetType = "Project"
        };

        var errors = Validate(dto);
        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidDto_WithActivityTarget_PassesValidation()
    {
        var dto = new DonationDto
        {
            Amount     = 50m,
            Currency   = "EUR",
            TargetId   = 3,
            TargetType = "Activity"
        };

        var errors = Validate(dto);
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Actor")]
    [InlineData("Company")]
    [InlineData("Whatever")]
    public void InvalidTargetType_FailsValidation(string badType)
    {
        var dto = new DonationDto
        {
            Amount     = 100m,
            Currency   = "SEK",
            TargetId   = 1,
            TargetType = badType
        };

        var errors = Validate(dto);
        errors.Should().Contain(r =>
            r.MemberNames.Contains(nameof(DonationDto.TargetType)));
    }

    [Theory]
    [InlineData("Company")]
    [InlineData("Project")]
    [InlineData("Unknown")]
    public void InvalidDonorType_FailsValidation(string badType)
    {
        var dto = new DonationDto
        {
            Amount    = 100m,
            Currency  = "SEK",
            DonorId   = 1,
            DonorType = badType
        };

        var errors = Validate(dto);
        errors.Should().Contain(r =>
            r.MemberNames.Contains(nameof(DonationDto.DonorType)));
    }

    [Fact]
    public void TargetIdWithoutTargetType_FailsValidation()
    {
        var dto = new DonationDto
        {
            Amount   = 100m,
            Currency = "SEK",
            TargetId = 1,
            // TargetType intentionally missing
        };

        var errors = Validate(dto);
        errors.Should().Contain(r =>
            r.MemberNames.Contains(nameof(DonationDto.TargetType)));
    }

    [Fact]
    public void TargetTypeWithoutTargetId_FailsValidation()
    {
        var dto = new DonationDto
        {
            Amount     = 100m,
            Currency   = "SEK",
            TargetType = "Project",
            // TargetId intentionally missing
        };

        var errors = Validate(dto);
        errors.Should().Contain(r =>
            r.MemberNames.Contains(nameof(DonationDto.TargetId)));
    }

    [Fact]
    public void ZeroAmount_FailsValidation()
    {
        var dto = new DonationDto { Amount = 0m, Currency = "SEK" };

        var errors = Validate(dto);
        errors.Should().Contain(r =>
            r.MemberNames.Contains(nameof(DonationDto.Amount)));
    }

    [Fact]
    public void NegativeAmount_FailsValidation()
    {
        var dto = new DonationDto { Amount = -1m, Currency = "SEK" };

        var errors = Validate(dto);
        errors.Should().Contain(r =>
            r.MemberNames.Contains(nameof(DonationDto.Amount)));
    }

    [Fact]
    public void NullCurrency_FailsValidation()
    {
        var dto = new DonationDto { Amount = 10m, Currency = null! };

        var errors = Validate(dto);
        errors.Should().Contain(r =>
            r.MemberNames.Contains(nameof(DonationDto.Currency)));
    }
}
