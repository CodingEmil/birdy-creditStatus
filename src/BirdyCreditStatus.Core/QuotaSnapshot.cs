namespace BirdyCreditStatus.Core;

/// <summary>Normierter Stand eines Anbieters inklusive Abrufzeitpunkt.</summary>
public sealed record QuotaSnapshot(string Provider, IReadOnlyList<QuotaWindow> Windows, DateTimeOffset FetchedAt);
