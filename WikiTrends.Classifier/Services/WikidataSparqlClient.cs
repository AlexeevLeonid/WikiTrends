using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using WikiTrends.Classifier.Configuration;
using WikiTrends.Contracts.Common;

namespace WikiTrends.Classifier.Services;

public sealed class WikidataSparqlClient : IWikidataSparqlClient
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ClassifierOptions _options;
    private readonly ILogger<WikidataSparqlClient> _logger;

    private const string CircuitOpenUntilCacheKey = "sparql-circuit-open-until";

    public WikidataSparqlClient(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<ClassifierOptions> options,
        ILogger<WikidataSparqlClient> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<WikidataSparqlNode>>> GetTopicHierarchyAsync(string qid, string lang, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(qid))
        {
            return Result<IReadOnlyList<WikidataSparqlNode>>.Failure("QID cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(lang))
        {
            return Result<IReadOnlyList<WikidataSparqlNode>>.Failure("Lang cannot be empty.");
        }

        var sparql = BuildQuery(qid.Trim(), lang.Trim());

        try
        {
            if (TryGetCircuitOpenUntil(out var openUntil))
            {
                return Result<IReadOnlyList<WikidataSparqlNode>>.Failure($"SPARQL circuit is open until {openUntil:O}");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "sparql")
            {
                Content = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("query", sparql)
                ])
            };
            request.Headers.Accept.ParseAdd("application/sparql-results+json");

            string content;
            try
            {
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode)
                {
                    if (ShouldOpenCircuit(response.StatusCode))
                    {
                        OpenCircuit($"StatusCode:{(int)response.StatusCode}");
                    }

                    var body = await response.Content.ReadAsStringAsync(ct);
                    var snippet = body.Length <= 500 ? body : body[..500];
                    return Result<IReadOnlyList<WikidataSparqlNode>>.Failure($"SPARQL endpoint error: {response.StatusCode}. {snippet}");
                }

                content = await response.Content.ReadAsStringAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException ex)
            {
                OpenCircuit("Timeout");
                _logger.LogWarning(ex, "SPARQL request timed out");
                return Result<IReadOnlyList<WikidataSparqlNode>>.Failure("SPARQL timeout");
            }
            catch (HttpRequestException ex)
            {
                OpenCircuit("HttpRequestException");
                _logger.LogWarning(ex, "Network error while calling SPARQL endpoint");
                return Result<IReadOnlyList<WikidataSparqlNode>>.Failure("Network error while calling SPARQL endpoint");
            }

            using var doc = JsonDocument.Parse(content);

            if (!doc.RootElement.TryGetProperty("results", out var resultsEl)
                || !resultsEl.TryGetProperty("bindings", out var bindingsEl)
                || bindingsEl.ValueKind != JsonValueKind.Array)
            {
                return Result<IReadOnlyList<WikidataSparqlNode>>.Failure("SPARQL response missing bindings.");
            }

            var list = new List<WikidataSparqlNode>();

            foreach (var binding in bindingsEl.EnumerateArray())
            {
                if (binding.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var classQid = ReadWikidataId(binding, "class");
                var classLabel = ReadString(binding, "classLabel");
                var sitelinks = ReadInt(binding, "sitelinks");

                if (string.IsNullOrWhiteSpace(classQid) || string.IsNullOrWhiteSpace(classLabel))
                {
                    continue;
                }

                list.Add(new WikidataSparqlNode
                {
                    Qid = classQid,
                    Label = classLabel,
                    Sitelinks = sitelinks
                });
            }

            return Result<IReadOnlyList<WikidataSparqlNode>>.Success(list);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse SPARQL response.");
            return Result<IReadOnlyList<WikidataSparqlNode>>.Failure("Failed to parse SPARQL response.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while calling SPARQL endpoint.");
            return Result<IReadOnlyList<WikidataSparqlNode>>.Failure($"Unexpected error: {ex.Message}");
        }
    }

    private bool TryGetCircuitOpenUntil(out DateTimeOffset openUntil)
    {
        if (_cache.TryGetValue<DateTimeOffset>(CircuitOpenUntilCacheKey, out var value)
            && value > DateTimeOffset.UtcNow)
        {
            openUntil = value;
            return true;
        }

        openUntil = default;
        return false;
    }

    private void OpenCircuit(string reason)
    {
        var openSeconds = Math.Clamp(_options.SparqlCircuitOpenSeconds, 10, 3600);
        var openUntil = DateTimeOffset.UtcNow.AddSeconds(openSeconds);

        _cache.Set(CircuitOpenUntilCacheKey, openUntil, TimeSpan.FromSeconds(openSeconds));

        _logger.LogWarning(
            "SPARQL circuit opened for {OpenSeconds}s. Reason={Reason}",
            openSeconds,
            reason);
    }

    private static bool ShouldOpenCircuit(HttpStatusCode statusCode)
    {
        return statusCode == (HttpStatusCode)429
               || statusCode == HttpStatusCode.ServiceUnavailable
               || statusCode == HttpStatusCode.GatewayTimeout
               || statusCode == HttpStatusCode.BadGateway
               || statusCode == HttpStatusCode.InternalServerError;
    }

    private static string BuildQuery(string qid, string lang)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PREFIX wd: <http://www.wikidata.org/entity/>");
        sb.AppendLine("PREFIX wdt: <http://www.wikidata.org/prop/direct/>");
        sb.AppendLine("PREFIX wikibase: <http://wikiba.se/ontology#>");
        sb.AppendLine("PREFIX bd: <http://www.bigdata.com/rdf#>");
        sb.AppendLine();
        sb.AppendLine("SELECT ?class ?classLabel ?sitelinks WHERE {");
        sb.AppendLine("  {");
        sb.AppendLine("    wd:" + qid + " wdt:P31 wd:Q5 .");
        sb.AppendLine("    OPTIONAL { wd:" + qid + " wdt:P106 ?occupation . }");
        sb.AppendLine("    BIND(COALESCE(?occupation, wd:Q5) AS ?startNode)");
        sb.AppendLine("  }");
        sb.AppendLine("  UNION");
        sb.AppendLine("  {");
        sb.AppendLine("    wd:" + qid + " wdt:P31 ?startNode .");
        sb.AppendLine("    FILTER NOT EXISTS { wd:" + qid + " wdt:P31 wd:Q5 }");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  ?startNode wdt:P279* ?class .");
        sb.AppendLine("  ?class wikibase:sitelinks ?sitelinks .");
        sb.AppendLine("  FILTER(?sitelinks >= 50)");
        sb.AppendLine("  SERVICE wikibase:label { bd:serviceParam wikibase:language \"" + lang + "\". }");
        sb.AppendLine("}");
        sb.AppendLine("ORDER BY ASC(?sitelinks)");
        sb.AppendLine("LIMIT 250");
        return sb.ToString();
    }

    private static string? ReadString(JsonElement binding, string field)
    {
        if (!binding.TryGetProperty(field, out var el) || el.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!el.TryGetProperty("value", out var valueEl) || valueEl.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return valueEl.GetString();
    }

    private static int ReadInt(JsonElement binding, string field)
    {
        var s = ReadString(binding, field);
        return int.TryParse(s, out var val) ? val : 0;
    }

    private static string? ReadWikidataId(JsonElement binding, string field)
    {
        var value = ReadString(binding, field);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var idx = value.LastIndexOf('/');
        if (idx < 0 || idx == value.Length - 1)
        {
            return null;
        }

        return value[(idx + 1)..];
    }
}
