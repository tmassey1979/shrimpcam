using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Audit;
using ShrimpCam.Core.Persistence;

#pragma warning disable CA2007

namespace ShrimpCam.Core.Tests.Audit;

public sealed class AuditEventServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Record_async_redacts_sensitive_detail_values_before_persisting()
    {
        var repository = new InMemoryAuditRecordRepository();
        var clock = new FixedClock(new DateTimeOffset(2026, 06, 25, 07, 00, 00, TimeSpan.Zero));
        var service = new AuditEventService(repository, clock);

        var record = await service.RecordAsync(
                new AuditEventRequest(
                    AuditEventTypes.SignIn,
                    " shrimp-admin ",
                    AuditOutcomes.Failed,
                    new Dictionary<string, string>
                    {
                        ["password"] = "PlainText123",
                        ["token"] = "session-token",
                        ["reason"] = "invalidCredentials",
                    }),
                CancellationToken.None)
            .ConfigureAwait(true);

        record.ActorUserName.Should().Be("shrimp-admin");
        record.OccurredAtUtc.Should().Be(clock.UtcNow);
        record.Detail.Should().Contain("\"password\":\"[redacted]\"");
        record.Detail.Should().Contain("\"token\":\"[redacted]\"");
        record.Detail.Should().Contain("invalidCredentials");
        record.Detail.Should().NotContain("PlainText123");
        record.Detail.Should().NotContain("session-token");
        repository.Records.Should().ContainSingle().Which.Should().Be(record);
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class InMemoryAuditRecordRepository : IAuditRecordRepository
    {
        public List<AuditRecord> Records { get; } = [];

        public Task CreateAsync(AuditRecord auditRecord, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Records.Add(auditRecord);
            return Task.CompletedTask;
        }

        public Task<AuditRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Records.SingleOrDefault(record => record.Id == id));
        }

        public Task<AuditRecordPage> ListAsync(AuditRecordQuery query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new AuditRecordPage(Records, query.PageNumber, query.PageSize, Records.Count));
        }
    }
}

#pragma warning restore CA2007
