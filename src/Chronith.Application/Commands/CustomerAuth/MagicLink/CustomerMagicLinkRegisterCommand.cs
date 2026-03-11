using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.CustomerAuth.MagicLink;

public sealed record CustomerMagicLinkRegisterCommand : IRequest<MagicLinkInitiatedDto>
{
    public required string TenantSlug { get; init; }
    public required string Email { get; init; }
    public required string Name { get; init; }
    public string? Phone { get; init; }
}

public sealed class CustomerMagicLinkRegisterCommandValidator
    : AbstractValidator<CustomerMagicLinkRegisterCommand>
{
    public CustomerMagicLinkRegisterCommandValidator()
    {
        RuleFor(x => x.TenantSlug).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class CustomerMagicLinkRegisterCommandHandler(
    ITenantRepository tenantRepository,
    ITenantAuthConfigRepository authConfigRepository,
    ICustomerRepository customerRepository,
    ITokenService tokenService,
    IEnumerable<INotificationChannel> notificationChannels,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CustomerMagicLinkRegisterCommand, MagicLinkInitiatedDto>
{
    public async Task<MagicLinkInitiatedDto> Handle(
        CustomerMagicLinkRegisterCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetBySlugAsync(request.TenantSlug, cancellationToken)
            ?? throw new NotFoundException("Tenant", request.TenantSlug);

        var authConfig = await authConfigRepository.GetByTenantIdAsync(tenant.Id, cancellationToken);
        if (authConfig is null || !authConfig.MagicLinkEnabled)
            throw new InvalidOperationException(
                "Magic link authentication is not enabled for this tenant.");

        var existing = await customerRepository.GetByEmailAsync(tenant.Id, request.Email, cancellationToken);
        if (existing is not null)
            throw new ConflictException($"A customer with email '{request.Email}' already exists.");

        var customer = Customer.Create(
            tenant.Id, request.Email, passwordHash: null,
            request.Name, request.Phone, authProvider: "magic-link");

        await customerRepository.AddAsync(customer, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var token = tokenService.CreateMagicLinkToken(customer, request.TenantSlug);

        var emailChannel = notificationChannels.FirstOrDefault(c => c.ChannelType == "email");
        if (emailChannel is not null)
        {
            await emailChannel.SendAsync(
                new NotificationMessage(
                    Recipient: request.Email,
                    Subject: "Complete your registration",
                    Body: token),
                cancellationToken);
        }

        return new MagicLinkInitiatedDto("Check your email to complete registration.");
    }
}
