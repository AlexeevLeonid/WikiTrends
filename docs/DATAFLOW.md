# DATAFLOW

Ниже — end-to-end поток данных: от Wikipedia до UI.

## 0) Источники

- **Wikipedia EventStreams** → Collector
- **Wikipedia API** → Enricher
- **Wikidata SPARQL / Wikidata API** → Classifier

## 1) Collector → Kafka (raw)

**Collector** читает события правок и публикует их в Kafka:
- **Topic**: `Topics.RawEdits` (`wiki.raw-edits`)
- **Message**: `RawEditEvent`
- **Key**: `PageId.ToString()`

## 2) Kafka (raw) → Enricher → Kafka (enriched)

**EnricherWorker** читает `wiki.raw-edits`.
Далее `RawEditHandler` → `EnrichmentService`:
- достаёт/обновляет данные статьи
- формирует `EnrichedEditEvent`

Публикация:
- **Topic**: `Topics.EnrichedEdits` (в текущем пайплайне: `wiki.enriched.v2`)
- **Message**: `EnrichedEditEvent`
- **Key**: `PageId.ToString()`

## 3) Kafka (enriched) → Classifier → Kafka (classified)

**ClassifierWorker** читает `wiki.enriched.v2`.
Далее `EnrichedEditHandler` → `ClassificationService` → `TopicResolverService`:

### 3.1 Резолв QID
- сначала из локального репозитория сопоставлений
- если нет — через Wikipedia API (pageprops)

### 3.2 Резолв темы (TopicResolver)
- ключ: `qid + lang`
- порядок попыток:
  1) in-memory cache `sparql-topic:{qid}:{lang}`
  2) Wikidata SPARQL (topic hierarchy)
  3) fallback: Wikidata API (`instanceOf` + labels)
  4) fallback: `Uncategorized`

### 3.3 Анти-латентность SPARQL
- SPARQL timeout: `Classifier:SparqlTimeoutSeconds`
- circuit breaker: `Classifier:SparqlCircuitOpenSeconds`
- если SPARQL начинает возвращать `429/5xx/timeout`, circuit открывается и дальнейшие запросы fail-fast, чтобы быстро уйти в fallback.

Публикация:
- **Topic**: `Topics.ClassifiedEdits` (`wiki.classified`)
- **Message**: `ClassifiedEditEvent`
- **Key**: обычно `ArticleId/PageId` (в коде — строковый ключ)

## 4) Kafka (classified) → Analytics → ClickHouse + Kafka (trend-updates)

**Analytics.EventConsumerWorker** читает `wiki.classified`.
`ClassifiedEditHandler` → `EventIngestionService`:
- пишет факты/агрегаты по темам и статьям в ClickHouse

Параллельно/периодически **TrendCalculationWorker** запускает `TrendCalculationService`:
- читает агрегаты из ClickHouse
- считает baseline + anomaly score
- формирует `TrendUpdateEvent`

Публикация:
- **Topic**: `Topics.TrendUpdates` (`wiki.trend-updates`)
- **Message**: `TrendUpdateEvent`

## 5) Kafka (trend-updates) → Aggregator → Redis + HTTP API

**Aggregator.TrendUpdateWorker** читает `wiki.trend-updates`.
Далее `TrendUpdateHandler` → `CacheService`:
- обновляет кэш трендов в Redis

Aggregator отдаёт API:
- `GET /api/trends`
- `GET /api/trends/clusters`
- `GET /api/topics/{topicId}`

Aggregator может ходить по HTTP в:
- Analytics
- Classifier
- Enricher

## 6) Scheduler → Kafka commands → Aggregator

**Scheduler** публикует команды:
- `RecalculateBaselineCommand` → `wiki.commands.recalculate-baseline`
- `InvalidateCacheCommand` → `wiki.commands.invalidate-cache`

**Aggregator.CommandWorker** читает оба топика и вызывает общий `CommandHandler`.

## 7) Gateway → SignalR / REST

**Gateway**:
- REST controllers (прокси/склейка)
- SignalR hub: `/hubs/trends`

`TrendBroadcastWorker`:
- периодически poll’ит `/api/trends` на Aggregator
- пушит `TrendNotification` в:
  - `Clients.All`
  - `Clients.Group("topic:{id}")`

## 8) Frontend

**Frontend** ходит в Gateway:
- HTTP (read модели)
- SignalR (push updates)

## Контрольные точки наблюдаемости

- Enricher/Classifier/Analytics/Aggregator: Serilog → Seq
- Kafka consumer метрики:
  - `Metric=kafka_consumer_stats`
  - `Metric=kafka_message_slow`
