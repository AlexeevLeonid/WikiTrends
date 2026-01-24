# ARCHITECTURE

## Цель системы

WikiTrends превращает поток live-правок Wikipedia в:
- обогащённые события по статьям
- классификацию статей по темам
- агрегаты/тренды и аномалии
- API + пуш-уведомления + UI

## Репозиторий и границы ответственности

### Shared libraries

- **WikiTrends.Contracts**
  - события Kafka (например, `RawEditEvent`, `EnrichedEditEvent`, `ClassifiedEditEvent`, `TrendUpdateEvent`)
  - DTO для API (`WikiTrends.Contracts.Api`)
  - общая модель результата (`WikiTrends.Contracts.Common.Result<T>`)

- **WikiTrends.Infrastructure**
  - конфигурация (Options + валидация)
    - `TopicsOptions`, `ServiceUrlsOptions`, `DatabaseOptions`
  - Kafka инфраструктура
    - consumer/producer abstraction
    - сериализация
    - метрики логированием (`Metric=kafka_consumer_stats`, `Metric=kafka_message_slow`)
  - persistence helpers (например, `MigrateDbAsync<TDbContext>()`)
  - logging helpers (bootstrap logger)

### Services

#### 1) Collector (`WikiTrends.Collector`)
- **Роль**: подключение к Wikipedia EventStreams и публикация событий правок.
- **Вход**:
  - Wikipedia EventStreams (SSE/stream)
- **Выход**:
  - Kafka: `Topics.RawEdits` (`wiki.raw-edits`)
- **Ключ партиционирования**:
  - `PageId.ToString()`

#### 2) Enricher (`WikiTrends.Enricher`)
- **Роль**: обогащение raw-событий данными статьи.
- **Вход**:
  - Kafka: `Topics.RawEdits`
- **Основная логика**:
  - получение/кэширование статьи (Postgres)
  - запросы к Wikipedia API (extract, категории, линкованные статьи)
- **Выход**:
  - Kafka: `Topics.EnrichedEdits` (в текущем пайплайне: `wiki.enriched.v2`)
- **Хранилища**:
  - Postgres (enricher DB)
  - Redis (подключён, используется как инфраструктурная зависимость)
- **Параллелизм**:
  - несколько `EnricherWorker` внутри одного контейнера (`Enricher__WorkerCount`)

#### 3) Classifier (`WikiTrends.Classifier`)
- **Роль**: определение темы статьи.
- **Вход**:
  - Kafka: `Topics.EnrichedEdits` (`wiki.enriched.v2`)
- **Основная логика**:
  - резолв QID по (wiki+title)
  - попытка построить topic hierarchy через Wikidata SPARQL
  - быстрый fallback на Wikidata API
  - сохранение темы в Postgres
  - публикация `ClassifiedEditEvent`
- **Выход**:
  - Kafka: `Topics.ClassifiedEdits` (`wiki.classified`)
- **Хранилища**:
  - Postgres (classifier DB)
  - MemoryCache (локальный кэш в процессе)
- **Критичные non-functional требования**:
  - SPARQL может давать 429/5xx/timeout → используется timeout + circuit breaker + кэш по `qid+lang`
  - настройки:
    - `Classifier__SparqlTimeoutSeconds`
    - `Classifier__SparqlCircuitOpenSeconds`
    - `Classifier__SparqlCacheHours`
- **Параллелизм**:
  - несколько `ClassifierWorker` внутри одного контейнера (`Classifier__WorkerCount`)

#### 4) Analytics (`WikiTrends.Analytics`)
- **Роль**: ingestion classified-событий, хранение агрегатов и расчёт трендов.
- **Вход**:
  - Kafka: `Topics.ClassifiedEdits`
- **Основная логика**:
  - запись фактов/агрегатов в ClickHouse
  - расчёт anomaly score (baseline + детектор)
  - публикация `TrendUpdateEvent`
- **Выход**:
  - Kafka: `Topics.TrendUpdates` (`wiki.trend-updates`)
- **Хранилища**:
  - ClickHouse (основные агрегаты/аналитика)
  - Redis (подключён как зависимость)

#### 5) Aggregator (`WikiTrends.Aggregator`)
- **Роль**: fast API поверх аналитики + кэш.
- **Вход**:
  - Kafka: `Topics.TrendUpdates`
  - Kafka commands:
    - `Topics.RecalculateBaselineCommands`
    - `Topics.InvalidateCacheCommands`
  - HTTP to:
    - Analytics
    - Classifier
    - Enricher
- **Выход**:
  - HTTP API:
    - `/api/trends`
    - `/api/trends/clusters`
    - `/api/topics/{topicId}`
- **Хранилища**:
  - Redis (кэш)

#### 6) Gateway (`WikiTrends.Gateway`)
- **Роль**: единая точка входа для UI + SignalR.
- **Вход/Выход**:
  - Controllers (REST)
  - SignalR hub: `/hubs/trends`
- **Фоновая логика**:
  - `TrendBroadcastWorker` периодически poll’ит тренды из Aggregator и пушит уведомления в SignalR.

#### 7) Frontend (`WikiTrends.Frontend`)
- **Роль**: UI.
- **Интеграция**:
  - HTTP client к Gateway (`Gateway:BaseUrl`)
  - подписка на SignalR (через Gateway)

#### 8) Scheduler (`WikiTrends.Scheduler`)
- **Роль**: фоновые джобы и публикация команд в Kafka.
- **Выход**:
  - Kafka:
    - `RecalculateBaselineCommand` → `wiki.commands.recalculate-baseline`
    - `InvalidateCacheCommand` → `wiki.commands.invalidate-cache`

## Инфраструктурные компоненты (docker-compose)

- Kafka + Zookeeper
- Postgres
- Redis
- ClickHouse
- Seq (логирование)

## Конфигурация

Стандарт: `.NET Options` + env overrides.
- `Topics:*` — имена Kafka топиков
- `Kafka:*` — настройки клиента
- `ServiceUrls:*` — base URL для межсервисного HTTP
- `Database:*` — migrate on startup

## Наблюдаемость

- Serilog → Console + Seq
- Kafka consumer метрики:
  - `Metric=kafka_consumer_stats` (через интервал)
  - `Metric=kafka_message_slow` (per message, если обработка > threshold)

## Масштабирование

- по умолчанию — 1 контейнер на сервис
- `Enricher`/`Classifier` масштабируются in-process worker’ами
- Kafka partitions (например, `wiki.enriched.v2` — 6 partitions)
