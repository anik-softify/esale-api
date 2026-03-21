using eSale.Application.Common.Interfaces;
using eSale.Domain.Modules.Tenants.Entities;
using eSale.Domain.Modules.Tenants.Interfaces;
using MediatR;

namespace eSale.Application.Modules.Tenants.Commands;

public sealed record ProvisionTenantCommand(string Name) : IRequest<Guid>;

public sealed class ProvisionTenantCommandHandler : IRequestHandler<ProvisionTenantCommand, Guid>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantDbInitializer _tenantDbInitializer;

    public ProvisionTenantCommandHandler(
        ITenantRepository tenantRepository,
        ITenantDbInitializer tenantDbInitializer)
    {
        _tenantRepository = tenantRepository;
        _tenantDbInitializer = tenantDbInitializer;
    }

    public async Task<Guid> Handle(ProvisionTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            DatabaseName = $"esale_tenant_{request.Name.ToLowerInvariant().Replace(" ", "_")}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _tenantRepository.AddAsync(tenant, cancellationToken);
        await _tenantRepository.SaveChangesAsync(cancellationToken);

        await _tenantDbInitializer.InitializeTenantDatabaseAsync(tenant.Id, cancellationToken);

        return tenant.Id;
    }
}
