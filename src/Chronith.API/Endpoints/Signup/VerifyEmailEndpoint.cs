using Chronith.Application.Features.Signup;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Signup;

public sealed class VerifyEmailEndpointRequest
{
    public string Token { get; set; } = string.Empty;
}

public sealed class VerifyEmailEndpoint(ISender sender)
    : Endpoint<VerifyEmailEndpointRequest>
{
    public override void Configure()
    {
        Post("/signup/verify-email");
        AllowAnonymous();
        Description(b => b
            .WithTags("Signup")
            .WithSummary("Verify email address using the token sent during signup"));
    }

    public override async Task HandleAsync(VerifyEmailEndpointRequest req, CancellationToken ct)
    {
        await sender.Send(new VerifyEmailCommand { Token = req.Token }, ct);
        await Send.NoContentAsync(ct);
    }
}
