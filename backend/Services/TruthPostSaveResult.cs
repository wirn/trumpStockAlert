using TrumpStockAlert.Api.Models;

namespace TrumpStockAlert.Api.Services;

public sealed record TruthPostSaveResult(TruthPost Post, bool Created);
