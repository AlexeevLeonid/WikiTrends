using WikiTrends.Contracts.Common;
using System.Net.Http.Json;

namespace WikiTrends.Aggregator.DataSources;

public sealed class ClassifierDataSource : IClassifierDataSource
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClassifierDataSource> _logger;

    public ClassifierDataSource(
        HttpClient httpClient,
        ILogger<ClassifierDataSource> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result<object>> GetTopicDictionaryAsync(CancellationToken ct = default)
    {
        // TODO: 1. Сформировать URL Classifier сервиса для справочника тем
        // TODO: 2. Выполнить GET запрос
        // TODO: 3. Вернуть Result.Success(объект) или Result.Failure
        try
        {
            using var response = await _httpClient.GetAsync("/api/topics/dictionary", ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Classifier topic dictionary request failed: {StatusCode}. Body: {Body}", (int)response.StatusCode, body);
                return Result<object>.Failure($"Classifier returned {(int)response.StatusCode}: {response.ReasonPhrase}");
            }

            var data = await response.Content.ReadFromJsonAsync<object>(cancellationToken: ct);
            return data == null
                ? Result<object>.Failure("Classifier returned empty response")
                : Result<object>.Success(data);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Classifier topic dictionary request failed.");
            return Result<object>.Failure(ex.Message);
        }
    }
}
