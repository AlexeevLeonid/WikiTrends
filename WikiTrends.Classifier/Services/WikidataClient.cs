using System.Text.Json;
using System.Text.Json.Serialization;
using WikiTrends.Classifier.Models;
using WikiTrends.Contracts.Common;

namespace WikiTrends.Classifier.Services;

public sealed class WikidataClient : IWikidataClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WikidataClient> _logger;

    public WikidataClient(
        HttpClient httpClient,
        ILogger<WikidataClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri("https://www.wikidata.org/");
        }
    }

    public async Task<Result<IReadOnlyDictionary<string, string>>> GetLabelsAsync(IEnumerable<string> entityIds, string wiki, CancellationToken ct = default)
    {
        if (entityIds == null)
        {
            return Result<IReadOnlyDictionary<string, string>>.Failure("EntityIds cannot be null");
        }

        if (string.IsNullOrWhiteSpace(wiki))
        {
            return Result<IReadOnlyDictionary<string, string>>.Failure("Wiki site code cannot be empty.");
        }

        var ids = entityIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ids.Length == 0)
        {
            return Result<IReadOnlyDictionary<string, string>>.Success(new Dictionary<string, string>());
        }

        try
        {
            var languageCode = wiki.Replace("wiki", "");
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var batch in ids.Chunk(50))
            {
                var inList = Uri.EscapeDataString(string.Join('|', batch));
                var requestUrl = $"w/api.php?action=wbgetentities&ids={inList}&props=labels&languages={languageCode}&format=json";

                using var response = await _httpClient.GetAsync(requestUrl, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Wikidata labels request returned {StatusCode}", response.StatusCode);
                    return Result<IReadOnlyDictionary<string, string>>.Failure($"Wikidata labels API error: {response.StatusCode}");
                }

                var content = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(content);
                if (!doc.RootElement.TryGetProperty("entities", out var entitiesEl) || entitiesEl.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var entityProp in entitiesEl.EnumerateObject())
                {
                    var entityId = entityProp.Name;
                    var entityEl = entityProp.Value;

                    if (entityEl.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (entityEl.TryGetProperty("missing", out _))
                    {
                        continue;
                    }

                    if (entityEl.TryGetProperty("labels", out var labelsEl)
                        && labelsEl.ValueKind == JsonValueKind.Object
                        && labelsEl.TryGetProperty(languageCode, out var labelEl)
                        && labelEl.ValueKind == JsonValueKind.Object
                        && labelEl.TryGetProperty("value", out var labelValueEl)
                        && labelValueEl.ValueKind == JsonValueKind.String)
                    {
                        var label = labelValueEl.GetString();
                        if (!string.IsNullOrWhiteSpace(label))
                        {
                            result[entityId] = label;
                        }
                    }
                }
            }

            return Result<IReadOnlyDictionary<string, string>>.Success(result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while calling Wikidata labels.");
            return Result<IReadOnlyDictionary<string, string>>.Failure("Network error while contacting Wikidata.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize Wikidata labels response.");
            return Result<IReadOnlyDictionary<string, string>>.Failure("Failed to parse Wikidata response.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while calling Wikidata labels.");
            return Result<IReadOnlyDictionary<string, string>>.Failure($"Unexpected error: {ex.Message}");
        }
    }

    private const string InstanceOfPropertyId = "P31";


    public async Task<Result<WikidataResponse>> GetEntityAsync(string title, string wiki, CancellationToken ct = default)
    {
        //  1. Провалидировать входные параметры title и wiki
        //  2. Сформировать запрос к Wikidata (SPARQL или API) для получения entity по Wikipedia sitelink
        //  3. Отправить HTTP запрос через _httpClient
        //  4. Обработать non-success статус и вернуть Result.Failure("...")
        //  5. Десериализовать ответ в WikidataResponse
        //  6. Вернуть Result.Success(response)

        if (string.IsNullOrWhiteSpace(title))
            return Result<WikidataResponse>.Failure("Title cannot be empty.");

        if (string.IsNullOrWhiteSpace(wiki))
            return Result<WikidataResponse>.Failure("Wiki site code cannot be empty.");

        try
        {
            // 2. Сформировать запрос (Action API: wbgetentities)
            // sites: код вики (например, 'enwiki', 'ruwiki')
            // titles: название статьи
            // props: запрашиваем метки (labels) и утверждения (claims)
            // languages: язык для labels (берем из кода вики, упрощенно)
            string languageCode = wiki.Replace("wiki", "");
            var encodedTitle = Uri.EscapeDataString(title);

            var requestUrl = $"w/api.php?action=wbgetentities&sites={wiki}&titles={encodedTitle}&props=labels|claims&languages={languageCode}&format=json";

            // 3. Отправить HTTP запрос
            using var response = await _httpClient.GetAsync(requestUrl, ct);

            // 4. Обработать non-success статус
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Wikidata API returned {StatusCode} for title '{Title}'", response.StatusCode, title);
                return Result<WikidataResponse>.Failure($"Wikidata API error: {response.StatusCode}");
            }

            // 5. Десериализовать ответ
            var content = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("entities", out var entitiesEl) || entitiesEl.ValueKind != JsonValueKind.Object)
            {
                return Result<WikidataResponse>.Failure("No entities returned from Wikidata.");
            }

            var firstEntity = entitiesEl.EnumerateObject().FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstEntity.Name))
            {
                return Result<WikidataResponse>.Failure("No entities returned from Wikidata.");
            }

            var entityId = firstEntity.Name;
            var entityDataEl = firstEntity.Value;

            if (entityId == "-1")
            {
                return Result<WikidataResponse>.Failure($"Entity not found for title '{title}' on '{wiki}'.");
            }

            if (entityDataEl.ValueKind == JsonValueKind.Object &&
                entityDataEl.TryGetProperty("missing", out _))
            {
                return Result<WikidataResponse>.Failure($"Entity not found for title '{title}' on '{wiki}'.");
            }

            string label = title;
            if (entityDataEl.TryGetProperty("labels", out var labelsEl) && labelsEl.ValueKind == JsonValueKind.Object &&
                labelsEl.TryGetProperty(languageCode, out var labelEl) && labelEl.ValueKind == JsonValueKind.Object &&
                labelEl.TryGetProperty("value", out var labelValueEl) && labelValueEl.ValueKind == JsonValueKind.String)
            {
                label = labelValueEl.GetString() ?? title;
            }

            var instanceOfList = new List<string>();
            var allClaims = new List<string>();

            if (entityDataEl.TryGetProperty("claims", out var claimsEl) && claimsEl.ValueKind == JsonValueKind.Object)
            {
                allClaims = claimsEl.EnumerateObject().Select(p => p.Name).ToList();

                if (claimsEl.TryGetProperty(InstanceOfPropertyId, out var p31El) && p31El.ValueKind == JsonValueKind.Array)
                {
                    foreach (var claimEl in p31El.EnumerateArray())
                    {
                        if (claimEl.ValueKind != JsonValueKind.Object) continue;
                        if (!claimEl.TryGetProperty("mainsnak", out var mainsnakEl) || mainsnakEl.ValueKind != JsonValueKind.Object) continue;
                        if (!mainsnakEl.TryGetProperty("datavalue", out var datavalueEl) || datavalueEl.ValueKind != JsonValueKind.Object) continue;
                        if (!datavalueEl.TryGetProperty("value", out var valueEl)) continue;

                        if (valueEl.ValueKind == JsonValueKind.Object && valueEl.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                        {
                            var id = idEl.GetString();
                            if (!string.IsNullOrWhiteSpace(id)) instanceOfList.Add(id);
                        }
                    }
                }
            }

            var responseDto = new WikidataResponse
            {
                Entity = new WikidataEntity
                {
                    Id = entityId,
                    Label = label,
                    InstanceOf = instanceOfList
                },
                Claims = allClaims
            };

            return Result<WikidataResponse>.Success(responseDto);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while calling Wikidata for '{Title}'", title);
            return Result<WikidataResponse>.Failure("Network error while contacting Wikidata.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize Wikidata response for '{Title}'", title);
            return Result<WikidataResponse>.Failure("Failed to parse Wikidata response.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetEntityAsync for '{Title}'", title);
            return Result<WikidataResponse>.Failure($"Unexpected error: {ex.Message}");
        }
    }

    public class WikidataApiResponse
    {
        [JsonPropertyName("entities")]
        public Dictionary<string, EntityItemDto>? Entities { get; set; }

        [JsonPropertyName("success")]
        public int Success { get; set; }
    }

    public class EntityItemDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("missing")]
        public string? Missing { get; set; }

        [JsonPropertyName("labels")]
        public Dictionary<string, LabelDto>? Labels { get; set; }

        [JsonPropertyName("claims")]
        public Dictionary<string, List<ClaimDto>>? Claims { get; set; }
    }

    public class LabelDto
    {
        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("value")]
        public required string Value { get; set; }
    }

    public class ClaimDto
    {
        [JsonPropertyName("mainsnak")]
        public SnakDto? Mainsnak { get; set; }
    }

    public class SnakDto
    {
        [JsonPropertyName("datavalue")]
        public DataValueDto? Datavalue { get; set; }
    }

    public class DataValueDto
    {
        [JsonPropertyName("value")]
        public JsonElement Value { get; set; }
    }

    public class EntityIdValueDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

}
