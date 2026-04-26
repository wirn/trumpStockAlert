using TrumpStockAlert.Api.DTOs;
using TrumpStockAlert.Api.Models;

namespace TrumpStockAlert.Api.Services;

public interface ITruthPostService
{
    Task<TruthPostSaveResult> SaveAsync(
        CreateTruthPostRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TruthPost>> GetLatestAsync(
        int limit,
        CancellationToken cancellationToken);

    Task<TruthPost?> GetByIdAsync(int id, CancellationToken cancellationToken);
}
