# Сверка ответов сервера с контрактом в тестах .NET

Исследование по тикету [#10](https://github.com/alexander-lis/ai-for-developers-project-386/issues/10)
(часть карты «Карта решений: спецификация записи на звонок», [#3](https://github.com/alexander-lis/ai-for-developers-project-386/issues/3)).
Дата: 2026-11-05. Все версии и факты ниже сверены с первичными источниками в этот день;
ссылки в разделе «Источники».

## Вопрос

Сервер в `backend/CalendarApi` написан вручную, компилятор не сверяет его с `contract/main.tsp`
(см. AGENTS.md). Нужно решить, чем в тестах .NET 10 (xUnit + `WebApplicationFactory`) проверять,
что реальные HTTP-ответы соответствуют `contract/tsp-output/schema/openapi.yaml`, и сравнить это
с внешним инструментом (Schemathesis) поверх запущенного сервера.

## Короткий ответ

1. Наш контракт компилируется в **OpenAPI 3.1.0** (это задано явно в `contract/tspconfig.yaml`;
   по умолчанию эмиттер выдаёт 3.0.0). Схемы в 3.1 — надмножество JSON Schema 2020-12.
2. Рекомендация: **проверять ответы прямо в xUnit-тестах** связкой
   `Microsoft.OpenApi` 2.13.0 + `Microsoft.OpenApi.YamlReader` 2.13.0 (читают 3.1, разворачивают
   `$ref`) и `JsonSchema.Net` 9.6.0 (валидирует тело по JSON Schema 2020-12). Пример ниже
   собран и прогнан против текущего сервера: позитивный тест проходит, тест с неверным телом
   падает.
3. **Schemathesis** 4.33.1 (Python ≥ 3.11) поддерживает 3.1 и за один прогон проверяет
   схему ответа, код статуса, `Content-Type` и отклонение невалидного ввода. Прогнан локально
   против нашего сервера: 10 сгенерированных запросов, 0.21 с. Но он требует Python-тулчейн,
   запущенный сервер и отдельный шаг в `check`/CI. Предлагаю не тянуть его сейчас, а вернуться,
   когда появятся операции с телом запроса (запись Гостя на Слот): там его негативные проверки
   дают больше, чем ручные тесты.
4. Готовых .NET-библиотек «валидатор ответа по OpenAPI» зрелых нет: `Evaluation.OpenAPI` 1.0.0
   зависит от `JsonSchema.Net` 5.x (два мажора назад) и почти не развивается, `SpecEnforcer`
   1.0.0 — middleware на `Microsoft.OpenApi` 1.6.x с примерами под 3.0.

## Что выдаёт наш TypeSpec

- `contract/package.json`: `@typespec/compiler`, `@typespec/http`, `@typespec/openapi`,
  `@typespec/openapi3` 1.17.0 (установлены `npm ls`).
- Опция эмиттера `openapi-versions`: тип `"3.0.0" | "3.1.0" | "3.2.0"`, **по умолчанию
  `["3.0.0"]`** (справочник эмиттера). В `contract/tspconfig.yaml` стоит `openapi-versions:
  [3.1.0]`, поэтому `npx tsp compile .` даёт файл с заголовком `openapi: 3.1.0`
  (проверено в этом исследовании). Документация эмиттера предупреждает только, что «вывод для
  3.1 может немного отличаться из-за возможностей, которых нет в 3.0».
- Спецификация OAS 3.1.1: Schema Object — «надмножество JSON Schema Draft 2020-12»; патч-версию
  «инструменты не должны учитывать», 3.1.0 и 3.1.1 для них одно и то же. Диалект по умолчанию
  `https://spec.openapis.org/oas/3.1/dialect/base` включает все словари 2020-12 (core,
  applicator, unevaluated, validation, meta-data, format-annotation, content) плюс необязательный
  словарь OAS (`discriminator`, `xml`, `externalDocs`, `example`).
- Текущая схема ответа `/api/health` в сгенерированном файле: `type: object`, `required:
  [status]`, `status: {type: string, enum: [ok]}`. Сервер отдаёт `{"status":"ok"}`.

## Кандидаты в .NET

| Пакет | Версия (дата публикации) | OpenAPI 3.1 | Роль | Вердикт |
|---|---|---|---|---|
| `Microsoft.OpenApi` | 2.13.0 (2026-10-19); 3.11.0 (2026-10-19) | да (2.x); 3.x добавляет 3.2 | объектная модель, чтение JSON, разрешение `$ref`, сериализация | **брать 2.13.0** |
| `Microsoft.OpenApi.YamlReader` | те же версии | да | чтение YAML, `settings.AddYamlReader()` | **брать 2.13.0** |
| `JsonSchema.Net` | 9.6.0 (2026-10-31) | 2020-12 полностью | валидация `JsonElement` по схеме | **брать** |
| `JsonSchema.Net.OpenApi` | 5.0.1 (2026-03-15), требует `JsonSchema.Net` ≥ 9.1.3 | словарь OAS 3.1 | ключевые слова `discriminator`, `example`, `externalDocs`, `xml` как аннотации; диалект `Dialect.OpenApi_31` | по необходимости |
| `Evaluation.OpenAPI` | 1.0.0 (2025-04-02), 7.6K загрузок всего | заявлена 3.1 | `DelegatingHandler` для `HttpClient`, оценивает запрос и ответ | нет: зависит от `JsonSchema.Net` ≥ 5.0.0, YAML только через `Yaml2JsonNode`, один автор |
| `SpecEnforcer` | 1.0.0 (2026-01-30), 183 загрузки | не заявлена, примеры 3.0.0 | ASP.NET Core middleware, `Microsoft.OpenApi` 1.6.x | нет |
| `NJsonSchema` | 11.6.1 (2026-04-20) | «draft v4+», 2020-12 явно не заявлен | валидация/генерация | нет |
| `Corvus.Text.Json.Validator` | ветка 5.x | 2020-12 и OpenAPI 3.1 заявлены | рантайм-валидация, основной фокус пакета — кодогенерация | избыточно |
| `YamlDotNet` | 18.1.0 (2026-06-26) | — | нужен, только если читать YAML без `Microsoft.OpenApi` | не нужен |

### Почему `Microsoft.OpenApi` 2.x, а не 3.x

- Обе линии живы и выпускаются одновременно (2.13.0 и 3.11.0 вышли в один день, с одинаковыми
  исправлениями).
- `Microsoft.AspNetCore.OpenApi` 10.0.14 требует `Microsoft.OpenApi >= 2.12.2 && < 3.0.0`, а
  руководство по переходу на v3 прямо говорит: для AspNetCore OpenAPI 10.x оставаться на 2.x.
  Если когда-нибудь включим встроенную генерацию OpenAPI на сервере (вариант C ниже), 3.x в
  тестовом проекте даст конфликт версий.
- 3.x нужен только ради OpenAPI 3.2, которого у нас нет; .NET 10 сам генерирует 3.1 по
  умолчанию, 3.2 станет умолчанием в .NET 11.

### Что в v2 важно знать (из upgrade-guide-2 и исходников тега v2.13.0)

- YAML читает отдельный пакет; регистрация: `var settings = new OpenApiReaderSettings();
  settings.AddYamlReader();` (namespace `Microsoft.OpenApi.Reader`).
- Загрузка: `OpenApiDocument.LoadAsync(Stream stream, string? format = null,
  OpenApiReaderSettings? settings = null, CancellationToken ct = default)` возвращает
  `ReadResult` с `Document` и `Diagnostics`; результат деконструируется в кортеж.
- Методы операции — `HttpMethod`, не enum: `Operations` имеет тип
  `Dictionary<HttpMethod, OpenApiOperation>?`.
- `OpenApiResponse.Content` — `IDictionary<string, OpenApiMediaType>?`, `OpenApiMediaType.Schema`
  — `IOpenApiSchema?`; ссылки (`OpenApiSchemaReference`) разрешаются лениво через `Target`.
- `OpenApiSchema` в v2 следует 2020-12: `Type` — флаги `JsonSchemaType` (`String | Null` вместо
  `nullable`), появились `$defs`, `patternProperties`, `unevaluatedProperties`,
  `$dynamicRef` и т. д.
- `OpenApiWriterSettings.InlineLocalReferences = true` заставляет при сериализации вместо `$ref`
  писать разрешённую цель (`ShouldInlineReference`: `reference.IsLocal && InlineLocalReferences`).
  Это даёт самостоятельную JSON Schema без зависимости от `#/components`.

### Что в `JsonSchema.Net` 9 важно знать

- В v8 библиотеку «переписали полностью»: схема строится на `JsonElement` (не `JsonNode`),
  появились `JsonSchema.Build(JsonElement, BuildOptions, Uri)`, `BuildOptions`, `Dialect`,
  `DialectRegistry`; `EvaluateAs` и `FromStream()` удалены. `FromText(string, BuildOptions,
  Uri, JsonDocumentOptions?)` и `FromFile` остались.
- `Dialect.Default` по умолчанию — грядущий JSON Schema v1/2026; документация рекомендует
  задавать `Dialect.Draft202012`. Для нас это обязательно: TypeSpec не пишет `$schema` в схемы,
  и без явного диалекта схема будет трактоваться не как 2020-12.
- `Evaluate(JsonElement instance, EvaluationOptions options)` → `EvaluationResults` с `IsValid`,
  `Errors` (`Dictionary<string, string>`), `Details`, `InstanceLocation`. `OutputFormat`: `Flag`
  (по умолчанию, только да/нет), `List`, `Hierarchical`.
- `JsonSchema.Net.OpenApi`: `Json.Schema.OpenApi.MetaSchemas.Register(buildOptions)` и диалект
  `Dialect.OpenApi_31`. Все его ключевые слова аннотативные, валидацию не меняют. Нужен, только
  когда в контракте появятся `discriminator`/`xml`/`example`, иначе они попадут в «неизвестные
  ключевые слова» (что само по себе валидацию не ломает).

## Пример: один ответ в xUnit-тесте

Собран и прогнан в одноразовом проекте вне репозитория (ссылка на `CalendarApi.csproj`,
`Microsoft.AspNetCore.Mvc.Testing` 10.0.14, xunit 2.9.3, пакеты из таблицы). Результат:
`Passed: 2` — тест ниже и контрольный тест, где тело `{"status":"broken"}` даёт `IsValid ==
false`. Путь к `openapi.yaml` в прототипе был абсолютным; поиск корня репозитория ниже — набросок.

```xml
<PackageReference Include="Microsoft.OpenApi" Version="2.13.0" />
<PackageReference Include="Microsoft.OpenApi.YamlReader" Version="2.13.0" />
<PackageReference Include="JsonSchema.Net" Version="9.6.0" />
```

```csharp
using System.Text.Json;
using Json.Schema;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

public class ContractTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_MatchesContract()
    {
        var document = await LoadContract();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        await AssertMatchesContract(document, "/api/health", HttpMethod.Get, response);
    }

    private static async Task<OpenApiDocument> LoadContract()
    {
        var settings = new OpenApiReaderSettings();
        settings.AddYamlReader();
        await using var file = File.OpenRead(ContractPath());
        var (document, diagnostics) = await OpenApiDocument.LoadAsync(file, "yaml", settings);
        Assert.Empty(diagnostics?.Errors ?? []);
        return document!;
    }

    private static JsonSchema ResponseSchema(
        OpenApiDocument document, string path, HttpMethod method, string status, string mediaType)
    {
        // Недокументированный статус или Content-Type дадут KeyNotFoundException:
        // это и есть проверка «статус и тип содержимого описаны в контракте».
        var operation = document.Paths[path].Operations![method];
        var content = operation.Responses![status].Content![mediaType];

        // Схема ответа как самостоятельная JSON Schema: локальные $ref подставлены на месте.
        using var text = new StringWriter();
        var writer = new OpenApiJsonWriter(text, new OpenApiJsonWriterSettings { InlineLocalReferences = true });
        content.Schema!.SerializeAsV31(writer);

        // TypeSpec не пишет $schema, поэтому диалект задаём явно.
        return JsonSchema.FromText(text.ToString(), new BuildOptions { Dialect = Dialect.Draft202012 });
    }

    private static async Task AssertMatchesContract(
        OpenApiDocument document, string path, HttpMethod method, HttpResponseMessage response)
    {
        var status = ((int)response.StatusCode).ToString();
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        Assert.NotNull(mediaType);

        var schema = ResponseSchema(document, path, method, status, mediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        var results = schema.Evaluate(body.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.True(results.IsValid, string.Join("\n", (results.Details ?? [])
            .Where(d => d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Key}: {e.Value}"))));
    }

    // Набросок, в прототипе не проверялся: подняться от bin/ до корня репозитория.
    private static string ContractPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "mise.toml")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "contract", "tsp-output", "schema", "openapi.yaml");
    }
}
```

Сериализованная с инлайном схема для `/api/health` выглядит так (вывод прототипа):

```json
{ "required": ["status"], "type": "object",
  "properties": { "status": { "enum": ["ok"], "type": "string" } } }
```

### Подводные камни при внедрении

- `openapi.yaml` не лежит в git; его создаёт `mise run generate`. Задача `test` в `mise.toml`
  зависит от `generate`, а шаг `backend-test` в `hk.pkl` — только от `backend-fmt`. Перед
  добавлением теста шагу `backend-test` нужно `depends = "generate"` (как у `frontend-test`),
  иначе `mise run check` на чистом клоне упадёт на отсутствующем файле. Запуск `dotnet test`
  из IDE без `generate` тоже упадёт: стоит давать понятное сообщение об ошибке.
- Проверка покрывает только те операции, для которых написан тест. Полноту можно проверить
  отдельным тестом: сравнить `document.Paths` с маршрутами сервера из `EndpointDataSource`
  (`factory.Services`). Идея не проверялась прототипом.
- Хелпер проверяет тело, статус и `Content-Type`. Заголовки ответа и схемы запросов он не
  трогает; `Assert.Empty(diagnostics.Errors)` ловит только ошибки чтения, не семантику.
- При 204/пустом теле `Content` в контракте отсутствует: хелперу нужна ветка для ответов без
  тела, когда такие появятся.

## Альтернатива A: Schemathesis поверх запущенного сервера

Факты (документация и PyPI на 2026-11-05):

- Версия 4.33.1 (2026-11-04), MIT, **Python ≥ 3.11**. Поддерживает OpenAPI 2.0, 3.0, 3.1, 3.2 и
  GraphQL.
- Запуск по локальному файлу: `uvx schemathesis run ./openapi.yaml --url http://localhost:8000`
  (`--url` обязателен для файловых схем). Полезные опции: `--wait-for-schema SECONDS`,
  `--report junit`, `--include-path`/`--exclude-tag` и т. п., `--max-examples N`, `--phases
  examples,coverage,fuzzing,stateful`.
- Проверки включены все, кроме `max_response_time`: `not_a_server_error`,
  `status_code_conformance`, `content_type_conformance`, `response_headers_conformance`,
  `response_schema_conformance`, `negative_data_rejection`, `positive_data_acceptance`,
  `missing_required_header`, `unsupported_method`, `use_after_free`, `ignored_auth` и др.
- Коды выхода: 0 — всё прошло, 1 — есть падения, 2 — не смог отработать (схема не загрузилась,
  ничего не протестировано), 130 — прервано.
- GitHub Action `schemathesis/action@v3`: входы `schema` (обязательный), `base-url`, `checks`,
  `wait-for-schema` (по умолчанию 2 с), `max-examples`, `version`, `args`, `config-file`;
  ставит Schemathesis через `uv tool install --python 3.13 "schemathesis>=4.11.1,<5.0"`.
  Пример в документации поднимает сервисы `docker compose up -d` до шага и гасит после.

Локальный прогон в этом исследовании: `uvx --python 3.13 schemathesis run
contract/tsp-output/schema/openapi.yaml --url http://127.0.0.1:5284` против работающего
dev-сервера (код тот же, что в master). Итог: `Specification: Open API 3.1.0`, 1 операция,
фазы Coverage и Fuzzing пройдены, `10 generated, 10 passed`, 0.21 с на тесты и около 24 с
полного времени вместе с установкой инструмента.

Как встроить в `mise run check` и CI, если решим:

```toml
# mise.toml
[tools]
python = "3.13"
uv = "latest"
"pypi:schemathesis" = "4.33.1"   # бекенд pypi ставит через uv, когда uv есть

[tasks."test:contract"]
description = "Run Schemathesis against the backend"
depends = ["generate"]
run = """
mise daemons start backend
schemathesis run contract/tsp-output/schema/openapi.yaml \
  --url http://127.0.0.1:5284 --wait-for-schema 30 --report junit
"""
```

и шаг в `hk.pkl` в группе `full` с `depends = "generate"`. В CI mise-action поставит Python и
uv из `mise.toml`, так что отдельный `schemathesis/action` не нужен; Docker-образ
`schemathesis/schemathesis:stable` — ещё один путь. Цена: Python-тулчейн в `mise.toml`,
поднятие и остановка сервера внутри `check` (сейчас `check` сервер не запускает), ~20–30 с на
шаг, недетерминированные примеры (seed печатается в отчёте).

## Альтернатива B: сгенерировать OpenAPI сервера и сравнить файлы

ASP.NET Core 10 генерирует OpenAPI **3.1 по умолчанию** (`Microsoft.AspNetCore.OpenApi`,
переключение через `OpenApiOptions.OpenApiVersion`), а `Microsoft.Extensions.ApiDescription.Server`
делает это при `dotnet build`. Полученный файл можно сравнить с контрактом `oasdiff breaking`
(v1.35.0 от 2026-10-28; 3.1 поддерживается с v1.15.0; brew, `go install`, Docker, GitHub
Action). Плюс: сравнивается вся поверхность (пути, методы, параметры), а не только тела
ответов, за которыми мы вручную пришли. Минус: сравниваются описания, а не реальные ответы;
генератор даст другие имена схем и, например, `type: string` без `enum` для `record
HealthStatus(string Status)`, так что без ручной нормализации diff будет шумным. Годится как
дополнительный «тест полноты», не как замена.

## Сравнение и рекомендация

| Критерий | xUnit + Microsoft.OpenApi + JsonSchema.Net | Schemathesis |
|---|---|---|
| Что проверяет | тело, статус, Content-Type конкретного сценария | то же плюс негативный ввод, заголовки, неподдерж. методы, stateful |
| Где живёт | в `dotnet test`, уже есть в `check` и CI | отдельный шаг, сервер должен быть запущен |
| Новые зависимости | 3 NuGet-пакета | Python ≥ 3.11, uv, пакет PyPI |
| Детерминизм | полный | генерация по seed |
| Сценарии с данными | тест сам создаёт Тип встречи/Слот и знает, что ждать | без хуков не знает, какой Слот свободен; stateful-фаза частично помогает |
| Стоимость падения | падает конкретный тест с указанием поля | отчёт с curl-репродьюсером |

Рекомендация: начать с варианта в xUnit как основного, потому что он встраивается в уже
существующий `backend-test` без нового тулчейна, проверяет настоящие ответы в тех же тестах,
где мы готовим данные (Тип встречи, Слот, Встреча), и падает точечно. Schemathesis добавить
вторым слоем, когда появятся операции с телом запроса и ошибки 4xx: его `negative_data_rejection`
и `status_code_conformance` там окупят Python в `mise.toml`. Вариант B держать в уме для
проверки полноты, если не сделаем сравнение с `EndpointDataSource` в тестах.

## Что пригодится тикету «Границы тестирования» (#12)

- Контрактная проверка в xUnit — это проверка **ответа конкретного сценария**, не покрытия
  контракта. Полноту (все пути и методы из `openapi.yaml` есть на сервере и наоборот) нужно
  проверять отдельно: тестом по `EndpointDataSource` или `oasdiff` по сгенерированному
  документу.
- Проверка статуса и `Content-Type` получается бесплатно из поиска по документу; схемы
  запросов, заголовки ответов и негативный ввод остаются за Schemathesis или ручными тестами.
- Тесты бекенда начинают зависеть от результата `generate`: `backend-test` в `hk.pkl` должен
  получить `depends = "generate"`, а `dotnet test` в одиночку без `mise run generate` падает.
- Диалект JSON Schema для схем из TypeSpec нужно задавать явно (`Draft202012`): `$schema` в них
  нет, а `Dialect.Default` в `JsonSchema.Net` 9 — не 2020-12.
- Любой из инструментов проверяет только JSON-тела; если появятся ответы без тела или не-JSON,
  хелперу нужна отдельная ветка.

## Источники

Все просмотрены 2026-11-05.

- TypeSpec, справочник эмиттера OpenAPI 3: <https://typespec.io/docs/emitters/openapi3/reference/emitter/>
  (опция `openapi-versions`, default `["3.0.0"]`); обзор эмиттера:
  <https://typespec.io/docs/emitters/openapi3/openapi/>
- OpenAPI Specification 3.1.1: <https://spec.openapis.org/oas/v3.1.1.html>; базовый диалект:
  <https://spec.openapis.org/oas/3.1/dialect/base>
- OpenAPI.NET: NuGet <https://www.nuget.org/packages/Microsoft.OpenApi>,
  <https://www.nuget.org/packages/Microsoft.OpenApi.YamlReader>; релизы
  <https://github.com/microsoft/OpenAPI.NET/releases>; руководства по переходу
  <https://github.com/microsoft/OpenAPI.NET/blob/main/docs/upgrade-guide-2.md>,
  <https://github.com/microsoft/OpenAPI.NET/blob/main/docs/upgrade-guide-3.md>; исходники тега
  `v2.13.0`: `src/Microsoft.OpenApi/Models/OpenApiPathItem.cs`, `OpenApiMediaType.cs`,
  `OpenApiResponse.cs`, `Writers/OpenApiWriterSettings.cs`, `Models/OpenApiDocument.cs`,
  `src/Microsoft.OpenApi.YamlReader/OpenApiReaderSettingsExtensions.cs`
- `Microsoft.AspNetCore.OpenApi` 10.0.14 (зависимость `Microsoft.OpenApi >= 2.12.2 && < 3.0.0`):
  <https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi/10.0.14>; генерация OpenAPI в
  ASP.NET Core 10 (3.1 по умолчанию, build-time через `Microsoft.Extensions.ApiDescription.Server`):
  <https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0>
- json-everything: NuGet <https://www.nuget.org/packages/JsonSchema.Net>,
  <https://www.nuget.org/packages/JsonSchema.Net.OpenApi>; основы
  <https://docs.json-everything.net/schema/basics/>; словарь OpenAPI
  <https://docs.json-everything.net/schema/vocabs/openapi/>; release notes
  <https://docs.json-everything.net/rn-json-schema/>; API `JsonSchema`, `EvaluationResults`,
  `EvaluationOptions`, `OutputFormat`, `Json.Schema.OpenApi.MetaSchemas`, `Dialect` в
  <https://docs.json-everything.net/api/JsonSchema.Net/> и
  <https://docs.json-everything.net/api/JsonSchema.Net.OpenApi/>
- Даты публикации версий: NuGet registration API
  `https://api.nuget.org/v3/registration5-gz-semver2/<package>/index.json`
- Evaluation.OpenAPI: <https://www.nuget.org/packages/Evaluation.OpenAPI>,
  <https://github.com/Fresa/OpenAPI.Evaluation>; SpecEnforcer:
  <https://www.nuget.org/packages/SpecEnforcer>; NJsonSchema:
  <https://www.nuget.org/packages/NJsonSchema>; Corvus:
  <https://github.com/corvus-dotnet/Corvus.JsonSchema>; YamlDotNet:
  <https://www.nuget.org/packages/YamlDotNet>
- Schemathesis: <https://schemathesis.readthedocs.io/en/stable/>, быстрый старт
  <https://schemathesis.readthedocs.io/en/stable/quick-start/>, CLI
  <https://schemathesis.readthedocs.io/en/stable/reference/cli/>, проверки
  <https://schemathesis.readthedocs.io/en/stable/reference/checks/>, CI/CD
  <https://schemathesis.readthedocs.io/en/stable/guides/cicd/>, action
  <https://github.com/schemathesis/action/blob/master/action.yml>, PyPI
  <https://pypi.org/project/schemathesis/>
- mise, бекенд pypi: <https://mise.jdx.dev/dev-tools/backends/pypi.html>
- oasdiff: <https://github.com/oasdiff/oasdiff> (README, `docs/OPENAPI-31.md`, релиз v1.35.0)
