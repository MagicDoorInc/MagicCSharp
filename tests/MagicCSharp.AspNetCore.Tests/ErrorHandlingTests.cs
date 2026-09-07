using System.ComponentModel.DataAnnotations;
using MagicCSharp.AspNetCore;
using DomainNotFound = MagicCSharp.Infrastructure.Exceptions.NotFoundException;

namespace MagicCSharp.AspNetCore.Tests;

public class ErrorHandlingTests
{
    [Fact]
    public void The_domain_not_found_exception_becomes_a_404()
    {
        // This is the mapping the whole module exists for: a repository saying "no such row" reaching the
        // caller as 404 rather than as a 500 with a stack trace.
        var problem = ErrorHandlingModule.Describe(new DomainNotFound("Order"), false);

        Assert.Equal(404, problem.Status);
        Assert.Equal("NotFound", problem.Title);
    }

    [Fact]
    public void A_not_found_id_exception_keeps_the_entity_in_the_detail()
    {
        var problem = ErrorHandlingModule.Describe(
            new MagicCSharp.Infrastructure.Exceptions.NotFoundIdException(42, "Order"), false);

        Assert.Equal(404, problem.Status);
        Assert.Contains("Order", problem.Detail);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(409)]
    [InlineData(422)]
    [InlineData(429)]
    [InlineData(501)]
    public void An_http_exception_carries_its_own_status(int status)
    {
        HttpException exception = status switch
        {
            400 => new BadRequestException("no"),
            401 => new UnauthorizedException(),
            403 => new ForbiddenException(),
            409 => new ConflictException("no"),
            422 => new UnprocessableEntityException("no"),
            429 => new TooManyRequestsException(),
            _ => new MagicCSharp.AspNetCore.NotImplementedException(),
        };

        Assert.Equal(status, ErrorHandlingModule.Describe(exception, false).Status);
    }

    [Fact]
    public void A_validation_failure_is_a_400_not_a_500()
    {
        Assert.Equal(400, ErrorHandlingModule.Describe(new ValidationException("Name is required"), false).Status);
    }

    [Fact]
    public void A_bad_argument_is_the_callers_fault_not_the_servers()
    {
        Assert.Equal(400, ErrorHandlingModule.Describe(new ArgumentException("id must be positive"), false).Status);
    }

    [Fact]
    public void A_cancelled_request_is_not_reported_as_a_server_error()
    {
        // The client hung up. Counting these as 500s makes an error dashboard useless during a deploy.
        Assert.Equal(499, ErrorHandlingModule.Describe(new OperationCanceledException(), false).Status);
    }

    [Fact]
    public void An_unexpected_exception_leaks_nothing_outside_development()
    {
        var problem = ErrorHandlingModule.Describe(
            new InvalidOperationException("Host=db.internal;Password=hunter2"), false);

        Assert.Equal(500, problem.Status);
        Assert.DoesNotContain("hunter2", problem.Detail);
        Assert.DoesNotContain("db.internal", problem.Detail);
    }

    [Fact]
    public void An_unexpected_exception_is_shown_in_development()
    {
        var problem = ErrorHandlingModule.Describe(new InvalidOperationException("something broke"), true);

        Assert.Equal(500, problem.Status);
        Assert.Contains("something broke", problem.Detail);
    }
}
