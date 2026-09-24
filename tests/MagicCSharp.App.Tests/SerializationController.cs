using MagicCSharp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MagicCSharp.App.Tests;

[ApiController]
[Route("serialization")]
public class SerializationController : ControllerBase
{
    [HttpGet("status")]
    public StatusBody GetStatus()
    {
        return new StatusBody { Status = SampleStatus.Cancelled };
    }

    [HttpPost("status")]
    public StatusBody PostStatus([FromBody] StatusBody body)
    {
        return body;
    }

    [HttpPost("patch")]
    public object Patch([FromBody] PatchBody body)
    {
        return new
        {
            name = body.Name,
            nicknameWasGiven = body.Nickname.HasValue,
            nickname = body.Nickname.HasValue ? body.Nickname.Value : null,
        };
    }

    [HttpGet("computed")]
    public ComputedBody GetComputed()
    {
        return new ComputedBody { Value = 21 };
    }
}

public enum SampleStatus
{
    Pending,
    Paid,
    Cancelled,
}

public record StatusBody
{
    public SampleStatus Status { get; init; }
}

public record PatchBody
{
    public string Name { get; init; } = "";
    public Optional<string?> Nickname { get; init; }
}

public record ComputedBody
{
    public int Value { get; init; }
    public int Doubled => Value * 2;
}
