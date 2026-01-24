# WikiTrends

WikiTrends — пайплайн обработки live-правок Wikipedia: сбор → обогащение → классификация по темам → аналитика трендов → API/SignalR → UI.
Стек: .NET + ASP.NET, Kafka, PostgreSQL, Clickhouse, Redis, SignalR, 

## Состав репозитория

- **WikiTrends.Contracts** — общие контракты (events/DTO/Result).
- **WikiTrends.Infrastructure** — общая инфраструктура (Kafka, опции, логирование, persistence-extensions).
- **WikiTrends.Collector** — подключается к Wikipedia EventStreams и публикует raw-события в Kafka.
- **WikiTrends.Enricher** — читает raw-события, ходит в Wikipedia API, добавляет extract/категории/линки, публикует enriched-события.
- **WikiTrends.Classifier** — читает enriched-события, определяет тему статьи (Wikidata SPARQL + fallback на Wikidata API), публикует classified-события.
- **WikiTrends.Analytics** — читает classified-события, пишет агрегации в ClickHouse, считает тренды/аномалии, публикует trend-updates.
- **WikiTrends.Aggregator** — читает trend-updates, кэширует в Redis и отдаёт API (объединяя данные из Analytics/Classifier/Enricher).
- **WikiTrends.Gateway** — API gateway + SignalR (`/hubs/trends`), проксирует/агрегирует, пушит уведомления.
- **WikiTrends.Frontend** — UI (Blazor/Razor Components), ходит в Gateway.
- **WikiTrends.Scheduler** — фоновые джобы и команды в Kafka (recalculate baseline / invalidate cache).

## Быстрый старт (Docker)

Требования:
- Docker Desktop
- (опционально) .NET SDK 10 — если хочешь собирать/запускать локально без Docker

Запуск инфраструктуры и сервисов:

```bash
docker-compose up -d --build
```

Адреса при развертывании локально:
- **Seq**: http://localhost:5341
- **Gateway**: http://localhost:5080
- **Aggregator** (profile `extended`): http://localhost:5084
- **Frontend** (profile `extended`): http://localhost:5085
- **Postgres**: localhost:5432
- **Kafka (host listener)**: localhost:29092

## Kafka топики (по умолчанию)

- `wiki.raw-edits`
- `wiki.enriched` (legacy)
- `wiki.enriched.v2`
- `wiki.classified`
- `wiki.trend-updates`
- `wiki.commands.recalculate-baseline`
- `wiki.commands.invalidate-cache`

## Производительность / параллелизм

В `Enricher` и `Classifier` используется in-process параллелизм: несколько hosted worker’ов внутри одного контейнера.

Настройки:
- `Enricher__WorkerCount`
- `Classifier__WorkerCount`

Для classifier дополнительно:
- `Classifier__SparqlTimeoutSeconds`
- `Classifier__SparqlCircuitOpenSeconds`
- `Classifier__SparqlCacheHours`

## Документация

- `docs/ARCHITECTURE.md` — архитектура по сервисам/слоям.
- `docs/DATAFLOW.md` — end-to-end датафлоу (Kafka → БД → API → UI).

## Разработка без Docker (минимум)

Сначала подними инфраструктуру (Kafka/Postgres/Redis/ClickHouse/Seq) через docker-compose, затем запускай сервисы из IDE.

```bash
dotnet build WikiTrends.sln
```

## Наблюдаемость

Основные метрики в логах Kafka consumer’ов:
- `Metric=kafka_consumer_stats` (RPS/AvgProcessingMs/MaxProcessingMs)
- `Metric=kafka_message_slow`

Смотри в Seq, фильтр по `Service` + `Metric`.
