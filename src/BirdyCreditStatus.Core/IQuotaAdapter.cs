namespace BirdyCreditStatus.Core;

/// <summary>Schmale Naht (D002): ein Adapter pro Anbieter.</summary>
public interface IQuotaAdapter
{
    Task<QuotaResult> FetchAsync(CancellationToken cancellationToken = default);
}
