using FluentAssertions;
using Lidstroem.Core.GDPR;
using Xunit;

namespace Lidstroem.Tests.Core;

public class GdprResultTests
{
    [Fact]
    public void AllSucceeded_WhenAllHandlersOk_ReturnsTrue()
    {
        var result = new GdprResult
        {
            Results = new[]
            {
                GdprHandlerResult.Ok("Handler1", 3),
                GdprHandlerResult.Ok("Handler2", 0),
                GdprHandlerResult.Skipped("Handler3"),
            }
        };

        result.AllSucceeded.Should().BeTrue();
    }

    [Fact]
    public void AllSucceeded_WhenAnyHandlerFailed_ReturnsFalse()
    {
        var result = new GdprResult
        {
            Results = new[]
            {
                GdprHandlerResult.Ok("Handler1", 2),
                GdprHandlerResult.Failed("Handler2", "DB error"),
            }
        };

        result.AllSucceeded.Should().BeFalse();
    }

    [Fact]
    public void Failed_ReturnsOnlyFailedHandlers()
    {
        var result = new GdprResult
        {
            Results = new[]
            {
                GdprHandlerResult.Ok("Handler1", 1),
                GdprHandlerResult.Failed("Handler2", "error"),
                GdprHandlerResult.Failed("Handler3", "another error"),
            }
        };

        result.Failed.Should().HaveCount(2)
            .And.AllSatisfy(r => r.Success.Should().BeFalse());
    }

    [Fact]
    public void Skipped_CountsAsSuccess()
    {
        var skipped = GdprHandlerResult.Skipped("Handler");

        skipped.Success.Should().BeTrue();
        skipped.RecordsAffected.Should().Be(0);
    }

    [Fact]
    public void ExecutedAt_IsSetToUtcNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var result = new GdprResult();
        var after  = DateTime.UtcNow.AddSeconds(1);

        result.ExecutedAt.Should().BeAfter(before).And.BeBefore(after);
    }
}
