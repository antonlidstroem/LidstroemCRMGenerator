using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Lidstroem.Plugins.WorkManagement.Activities.DTOs;
using Xunit;

namespace Lidstroem.Tests.Core;

public class ActivityDtoValidationTests
{
    private static List<ValidationResult> Validate(ActivityDto dto)
    {
        var context = new ValidationContext(dto);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, context, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void ValidDto_PassesValidation()
    {
        var dto = new ActivityDto
        {
            Title     = "Team meeting",
            ProjectId = 1
        };

        Validate(dto).Should().BeEmpty();
    }

    [Fact]
    public void MissingTitle_FailsValidation()
    {
        var dto = new ActivityDto { Title = "", ProjectId = 1 };

        var errors = Validate(dto);
        errors.Should().Contain(r => r.MemberNames.Contains(nameof(ActivityDto.Title)));
    }

    [Fact]
    public void ProjectId_Zero_FailsValidation()
    {
        var dto = new ActivityDto { Title = "Meeting", ProjectId = 0 };

        var errors = Validate(dto);
        errors.Should().Contain(r => r.MemberNames.Contains(nameof(ActivityDto.ProjectId)));
    }

    [Fact]
    public void ProjectId_Negative_FailsValidation()
    {
        var dto = new ActivityDto { Title = "Meeting", ProjectId = -5 };

        var errors = Validate(dto);
        errors.Should().Contain(r => r.MemberNames.Contains(nameof(ActivityDto.ProjectId)));
    }

    [Fact]
    public void TitleExceedingMaxLength_FailsValidation()
    {
        var dto = new ActivityDto
        {
            Title     = new string('x', 301),
            ProjectId = 1
        };

        var errors = Validate(dto);
        errors.Should().Contain(r => r.MemberNames.Contains(nameof(ActivityDto.Title)));
    }

    [Fact]
    public void OptionalDates_NullIsAccepted()
    {
        var dto = new ActivityDto
        {
            Title     = "Meeting",
            ProjectId = 1,
            StartDate = null,
            EndDate   = null
        };

        Validate(dto).Should().BeEmpty();
    }
}
