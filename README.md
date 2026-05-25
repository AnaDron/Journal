# EventJournal SDK

Инфраструктурный SDK журнала событий: append-only история изменений сущностей с расшифровкой
имени, восстановлением «было» и резолвом полей-ссылок. Типо-агностичное ядро — все типы и поля
проходят как строки; знание о доменных сущностях консьюмер подключает через четыре plugin-points.

Самостоятельная переиспользуемая библиотека, развязанная от прикладного кода.

## Структура

```
.
├── EventJournal.slnx
├── Directory.Build.props
├── README.md
├── src/
│   ├── EventJournal.Core/              # доменное ядро без внешних зависимостей
│   ├── EventJournal.Application/       # сервисы, обогащение, plugin-point интерфейсы
│   └── EventJournal.Infrastructure/    # EF-сущности, конфигурации, репозиторий
└── test/
    ├── EventJournal.Application.Tests/
    └── EventJournal.Infrastructure.Tests/
```

## Концепция

**Событие журнала** (`JournalEvent`) — это контейнер с причиной (`Reason`), инициатором
(`InitiatorKind` + `InitiatorId`) и набором изменений. **Изменение** (`JournalChange`) ссылается
на сущность парой `(EntityType, EntityKey)` и хранит операцию (Create/Update/Delete) и плоский
список полей (`Path`, `ValueJson`). Поля сериализуются в JSON как сырые значения; SDK ничего
не знает о доменных типах.

**AsyncLocal-контекст** (`JournalScope`) удерживает текущее событие между `BeginEvent()` и
`Dispose()`, и `IJournalControlService.Add/Update/Remove` через рефлексию записывают изменения
доменных объектов в текущее событие. Сохранение в БД происходит вместе с обычным
`SaveChangesAsync` консьюмерского `DbContext`.

**Обогащение** (`JournalEnricher`) восстанавливает имя сущности (`EntityName`) и «было»
(`OldValueJson`) для каждой записи в текущей странице, проигрывая историю журнала по затронутым
сущностям. Доменные особенности приходят через plugin-points.

## Plugin-points

| Интерфейс | Назначение | Дефолт SDK |
|---|---|---|
| `IJournalEntityNameResolver` | Как собирается имя сущности из field-state. Привязан к `EntityType`, декларирует `NameRelevantFields` для корректного снимка перед Delete. | Поле `name` (раскавыченное JSON). |
| `IJournalReferenceResolver` | Декларирует, что поле `(EntityType, FieldPath)` — это ссылка на сущность `ReferencedEntityType`. Enricher резолвит имя связанной сущности из её истории. | Нет ссылок. |
| `IJournalFieldFormatter` | Преобразует сырое JSON-значение поля для отображения (например, enum-число → имя). | Возвращает исходное значение. |
| `IJournalEntityKeyResolver` | Конвенция построения `EntityKey` из `(EntityType, Guid)` для резолва ссылок. | `{EntityType}/{id:D}`. |

Каждая реализация регистрируется как `IEnumerable<I*>` в DI; Enricher собирает их в словари
при создании. Несколько резолверов на один `(EntityType, FieldPath)` не поддерживаются — это
явная ошибка регистрации.

## Использование (минимальный пример)

### 1. DbContext консьюмера реализует `IJournalDbContext` и подключает схему

```csharp
public sealed class MyDbContext : DbContext, IJournalDbContext {
    public DbSet<JournalEventEntity> JournalEvents => Set<JournalEventEntity>();
    public DbSet<JournalChangeEntity> JournalChanges => Set<JournalChangeEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyJournalSchema();  // регистрирует EF-конфигурации журнала
    }
}
```

Миграции остаются в проекте консьюмера — SDK поставляет только `IEntityTypeConfiguration`.

### 2. DI

```csharp
services.AddJournalApplication();                              // Enricher + сервисы записи/чтения
services.AddJournalInfrastructure<MyDbContext>();              // репозиторий + мост IJournalDbContext

// Доменные адаптеры — на стороне консьюмера, по одному на сущность/поле:
services.AddSingleton<IJournalEntityNameResolver, MyCompositeNameResolver>();
services.AddSingleton<IJournalReferenceResolver, MyForeignKeyResolver>();
services.AddSingleton<IJournalFieldFormatter, MyEnumFieldFormatter>();
services.AddSingleton<IJournalEntityKeyResolver, MyEntityKeyConvention>();
```

### 3. Запись

```csharp
using var scope = journalControl.BeginEvent(new JournalEventScopeOptions(
    Reason: "Импорт каталога",
    InitiatorKind: JournalInitiatorKind.Integration,
    InitiatorId: "external-system"));

journalControl.Add<Item>(key: $"item/{id}", entity: created);
journalControl.Update<Item>(key: $"item/{id}", current: before, updated: after);
journalControl.Remove<Item>(key: $"item/{id}");

await db.SaveChangesAsync();  // событие и изменения уходят одной транзакцией
```

### 4. Чтение

```csharp
Page<JournalEventView> page = await journalService.ListEventsAsync(
    new JournalListFilter(Page: 1, PageSize: 50, EntityType: "Item", ...));

JournalEntityHistoryView history = await journalService.GetEntityHistoryAsync(
    entityType: "Item", entityKey: "item/abc-...");
```

В `JournalEventView` и `JournalEntityHistoryView` каждое изменение уже содержит вычисленные
`EntityName`, `OldValueJson` для Update'ов, и `RelatedNewName`/`RelatedOldName` для полей-ссылок.

## Что НЕ берёт на себя SDK

- **Миграции БД** — генерируются в консьюмерском проекте, который владеет своим `DbContext`.
  SDK даёт `IEntityTypeConfiguration` и `ModelBuilder.ApplyJournalSchema()`.
- **MediatR/HTTP** — обёртки в виде query-handlers и контроллеров — задача консьюмера. SDK
  поставляет чистые сервисы.
- **Доменные знания** — какие сущности есть, как из их полей собирается имя, какие поля
  ссылаются на другие сущности, как преобразуются enum'ы. Всё это входит через plugin-points.

## Контракт хранения

Две таблицы: `journal_events` и `journal_changes` (FK с `ON DELETE CASCADE`). Поле `FieldsJson`
в `journal_changes` — это `jsonb` массив `[{ "path", "value" }]`. Для операции Delete оно `null`.
Индексы: `journal_events(CreatedAtUtc DESC)`, `journal_events(OccurredAtUtc DESC)`,
`journal_changes(EventId)`, `journal_changes(EntityType, EntityKey)`.
