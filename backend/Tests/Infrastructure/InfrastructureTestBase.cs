using Lidstroem.Core.Constants;
using Lidstroem.Core.Interfaces;
using Lidstroem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Lidstroem.Tests.Infrastructure;

public abstract class InfrastructureTestBase : IDisposable
{
    protected readonly AppDbContext Db;
    protected readonly Guid TenantA = new("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");
    protected readonly Guid TenantB = new("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB");

    protected InfrastructureTestBase()
    {
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.TenantId).Returns(TenantA);
        tenantContext.Setup(t => t.OwnerId).Returns((Guid?)null);
        tenantContext.Setup(t => t.IsSystemContext).Returns(false);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source=InfraTest_{Guid.NewGuid():N};Mode=Memory;Cache=Shared")
            .Options;

        Db = new AppDbContext(options, tenantContext.Object);
        Db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Db.Database.EnsureDeleted();
        Db.Dispose();
        GC.SuppressFinalize(this);
    }
}
