# Браузерный сценарий с настоящим сервером

Исследование по тикету [#11](https://github.com/alexander-lis/ai-for-developers-project-386/issues/11),
часть карты [#3](https://github.com/alexander-lis/ai-for-developers-project-386/issues/3). Дата: 2026-11-05.

## Вопрос

Как запускать браузерный сценарий «Гость выбирает Тип встречи, день, Слот, вводит имя и видит
Встречу в списке» против настоящего бекенда .NET (`:5284`) и фронтенда Vite (`:5173`): Playwright
для Node или для .NET, как поднимать оба сервера локально и в GitHub Actions, как ставить браузеры,
сколько это добавляет к `mise run check`.

## Краткий ответ

1. **`@playwright/test` 1.64.1 (Node)**, один проект `chromium`, тесты в `frontend/e2e/`.
   Playwright для .NET умеет то же самое в браузере, но не умеет поднимать серверы (нет аналога
   `webServer`), требует `pwsh` для установки браузеров и тянет NuGet-пакет на 214 МБ.
2. **Серверы поднимает сам Playwright** через массив `webServer` из двух записей: `dotnet run` с
   проверкой готовности по `http://localhost:5284/api/health` и `vite` по `http://127.0.0.1:5173`.
   Локально `reuseExistingServer: !process.env.CI` подхватывает уже запущенные `mise run dev`
   демоны, в CI поднимает свои и убивает после прогона. `mise daemons` для этого не нужны: они
   остаются работать после задачи и устроены под разработку, а не под CI.
3. **В CI** добавляется один шаг `npx playwright install --with-deps --only-shell chromium` без кеша
   браузеров (Playwright это прямо не рекомендует). Браузеры не входят в кеш `mise-action`.
4. **Цена**: сейчас job `check` в CI занимает 33 с. Браузерный сценарий добавит порядка 1–2 минут:
   установка headless shell (99 МиБ) с системными зависимостями, старт бекенда и Vite, сам
   прогон. Локально при живых `mise run dev` прогон одного сценария займёт секунды.
5. **TypeScript 6.x сохраняется**: типы `@playwright/test` 1.64.1 проходят `tsc` 6.0.3 со
   `skipLibCheck: false` (проверено), сам Playwright тесты не типизирует и TypeScript как зависимость
   не требует.

## Сценарий как тест

Черновик, пока экрана записи нет. Локаторы по видимым пользователю ролям и подписям, как
рекомендует Playwright: `getByRole`, `getByLabel`, `getByText`, при необходимости `getByTestId`
([locators](https://playwright.dev/docs/locators)).

```ts
// frontend/e2e/book.spec.ts
import { expect, test } from "@playwright/test";

test("гость записывается на встречу и видит её в списке", async ({ page }) => {
  await page.goto("/book");
  await page.getByRole("button", { name: "Созвон 30 минут" }).click(); // Тип встречи
  await page.getByRole("button", { name: /^1[0-9] ноября/ }).first().click(); // день
  await page.getByRole("button", { name: "10:00" }).click(); // Слот
  await page.getByRole("button", { name: "Записаться" }).click();
  await page.getByLabel("Имя").fill("Иван");
  await page.getByRole("button", { name: "Подтвердить" }).click();

  await page.goto("/meetings");
  await page.getByRole("button", { name: "Иван" }).click();
  await expect(page.getByText("Созвон 30 минут")).toBeVisible(); // Встреча в списке
});
```

Что важно для устойчивости:

- Каждый тест получает изолированные `page`, `context` и `request`; браузер общий
  ([fixtures](https://playwright.dev/docs/test-fixtures)). Данные на сервере между тестами
  **не** изолируются: Встреча, созданная тестом, остаётся в бекенде. Нужен либо сброс состояния
  (эндпоинт или перезапуск сервера), либо уникальные имена Гостей и Слоты в каждом тесте.
- Подготовить состояние (создать Тип встречи) можно без браузера через фикстуру `request`
  (`request.post('/api/...', { data })`) в `beforeAll`
  ([api-testing](https://playwright.dev/docs/api-testing)).
- Дата зависит от «сегодня» (окно 14 дней): выбирать день относительно, а не по фиксированной дате.
  При необходимости Playwright умеет фиксировать время в браузере (`page.clock`), но бекенд
  живёт по своим часам: это вопрос для #12 и #5.
- Записать черновик теста можно рекордером: `npx playwright codegen http://127.0.0.1:5173`
  ([codegen](https://playwright.dev/docs/codegen)).

## Playwright для Node против Playwright для .NET

| | `@playwright/test` (Node) | `Microsoft.Playwright.*` (.NET) |
|---|---|---|
| Версия на 2026-11-05 | 1.64.1 (npm, 2026-10-14) | 1.64.1 (NuGet: `Microsoft.Playwright`, `.Xunit`, `.Xunit.v3`, `.NUnit`) |
| Поднять серверы перед тестами | `webServer` (объект или массив), ждёт URL/порт, убивает после прогона ([test-webserver](https://playwright.dev/docs/test-webserver)) | нет; документация CI для .NET «не описывает запуск серверов приложения» ([dotnet/ci](https://playwright.dev/dotnet/docs/ci)); писать свой `Process.Start` + ожидание порта |
| Установка браузеров | `npx playwright install --with-deps --only-shell chromium` ([browsers](https://playwright.dev/docs/browsers)) | `pwsh bin/Debug/net10.0/playwright.ps1 install --with-deps --only-shell`; нужен PowerShell (`pwsh`), на этой машине его нет; в `ubuntu-latest` есть 7.6.9 ([dotnet/intro](https://playwright.dev/dotnet/docs/intro)) |
| Размер пакета | `@playwright/test` → `playwright` → `playwright-core`, браузеры отдельно | `Microsoft.Playwright` 1.64.1 nupkg: 214 387 506 байт (бандлит драйвер; `PlaywrightPlatform` сужает) ([dotnet/library](https://playwright.dev/dotnet/docs/library)) |
| Инструменты | UI mode, trace viewer, HTML-отчёт, `codegen`, репортер `github` с аннотациями ([test-reporters](https://playwright.dev/docs/test-reporters)) | тот же trace viewer и `codegen`, но без тест-раннера Playwright: отчёты и параллелизм от xUnit/NUnit ([dotnet/test-runners](https://playwright.dev/dotnet/docs/test-runners)) |
| Ближе к коду | Фронтенд уже на TypeScript, Vitest, Node 26 | Бекенд-тесты уже на xUnit 2.9.3, `Microsoft.Playwright.Xunit` 1.64.1 встаёт рядом |
| Node.js | «latest 22.x, 24.x or 26.x», `engines: node >=20` ([intro](https://playwright.dev/docs/intro)) | драйвер в пакете, Node ставить не нужно |

Довод за .NET один: тесты бекенда уже на xUnit. Но браузерный сценарий проверяет весь продукт, а
не сервер, и основная тяжесть в нём это фронтовые локаторы; `webServer`, UI mode и `codegen`
снимают больше работы, чем даёт единство языка с бекендом. Сам ASP.NET Core для SPA отправляет к
Playwright: «For testing SPAs, we recommend a tool such as Playwright for .NET, which can automate a
browser» ([integration-tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)),
при этом `WebApplicationFactory` даёт только in-memory `TestServer` без реального порта, так что
браузер к нему не подключить ни из .NET, ни из Node.

## Как поднимать оба сервера

### Вариант A (рекомендация): массив `webServer` в `playwright.config.ts`

```ts
// frontend/playwright.config.ts
import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "e2e",
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: process.env.CI ? "github" : "list",
  use: {
    baseURL: "http://127.0.0.1:5173",
    trace: "on-first-retry",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: [
    {
      name: "backend",
      command: "dotnet run --project ../backend/CalendarApi",
      url: "http://localhost:5284/api/health",
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
    },
    {
      name: "frontend",
      command: "npm run dev -- --strictPort --host 127.0.0.1",
      url: "http://127.0.0.1:5173",
      reuseExistingServer: !process.env.CI,
    },
  ],
});
```

Факты из [test-webserver](https://playwright.dev/docs/test-webserver):

- Несколько серверов «can be launched simultaneously by providing an array of `webServer`
  configurations»; `name` становится префиксом логов.
- Готовность по `url`: сервер готов, когда отвечает «2xx, 3xx, 400, 401, 402, or 403». `port`
  устарел, использовать `url`. Таймаут ожидания по умолчанию 60 000 мс.
- `reuseExistingServer: true` подхватывает уже слушающий процесс; `false` падает, если порт занят.
  Рекомендация документации: `reuseExistingServer: !process.env.CI`.
- `cwd` по умолчанию каталог конфига, в `env` добавляется `PLAYWRIGHT_TEST=1`.
- После тестов «the process group is forcefully `SIGKILL`ed», если не задан `gracefulShutdown`.
  `dotnet run` порождает дочерний процесс `CalendarApi`, группа убивается целиком.

Проверено локально 2026-11-05 (macOS arm64, .NET SDK 10.0.403, Vite 8.3.5):

- `dotnet run --project backend/CalendarApi --urls http://127.0.0.1:5399` с холодным `obj/`: health
  через **48 с** (restore + build + старт); повторный запуск: **~10 с**. Это аргумент за
  `timeout: 120_000` для бекенда. `--urls` переопределяет `applicationUrl` из
  `launchSettings.json` ([kestrel endpoints](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-10.0)).
- Vite: «ready in 1643 ms», прокси `/api/health` отвечает 200. `--host 127.0.0.1` нужен по той же
  причине, что и в `mise.toml`: без него Vite слушает `localhost`, который Node может
  разрешить в `::1` ([server-options](https://vite.dev/config/server-options)).
- Vite dev отвечает 200 на `/` сразу, бекенд ещё может собираться; но Playwright ждёт готовности
  **всех** серверов из массива, поэтому гонки нет.

Почему dev-сервер, а не `vite build && vite preview`: `preview.proxy` по умолчанию берёт
`server.proxy`, так что сборка тоже сработала бы ([preview-options](https://vite.dev/config/preview-options)),
но dev-сервер уже закреплён в `mise.toml`, стартует за секунды и даёт один и тот же стенд
локально и в CI. Сборку можно добавить вторым проектом Playwright позже.

### Вариант B: `mise daemons` / pitchfork

`mise.toml` уже объявляет `[daemons.backend]` и `[daemons.frontend]` с `ready_port`, а у задачи есть
ключ `daemons`: «mise starts missing daemons and waits until they are ready before the task runs»
([task-configuration](https://mise.jdx.dev/tasks/task-configuration.html)). Но «Daemons that are
already running are reused, and they keep running after the task exits»
([daemons](https://mise.jdx.dev/daemons.html)): в CI пришлось бы добавлять `mise daemons stop`
через `depends_post`, следить за состоянием pitchfork (`~/.local/state/pitchfork/state.toml`,
супервизор, сокет) на runner'е, и всё это ради того, что `webServer` делает одной настройкой.
Документация mise и pitchfork не содержит рекомендаций для CI. Функция экспериментальная: «Daemon
support can change in any release», требуется pitchfork ≥ 2.25.0 (в проекте 2.33.0).

Разумное сочетание: локально разработчик держит `mise run dev`, а Playwright через
`reuseExistingServer` подключается к этим же процессам; в CI Playwright всё поднимает сам.

### Вариант C: `WebApplicationFactory`

Не подходит: `TestServer` «in-memory», браузеру нужен настоящий порт. Остаётся для тестов API в
`CalendarApi.Tests` (см. #10).

## Встраивание в проект

- **Где лежит.** `frontend/e2e/*.spec.ts` и `frontend/playwright.config.ts`, зависимость
  `@playwright/test` в `frontend/package.json`. Отдельный пакет в корне тоже возможен, но тогда
  второй `node_modules` с TypeScript и свой lint/fmt; в `frontend/` всё уже настроено.
- **Vitest.** Его `include` по умолчанию `**/*.{test,spec}.?(c|m)[jt]s?(x)`, `exclude` только
  `node_modules` и `.git` ([include](https://vitest.dev/config/include),
  [exclude](https://vitest.dev/config/exclude)), значит Vitest подхватит `e2e/*.spec.ts`.
  Добавить в `vite.config.ts`: `test: { exclude: [...configDefaults.exclude, "e2e/**"] }`.
- **TypeScript 6.x.** Playwright «does not check the types and will run tests even if there are
  non-critical TypeScript compilation errors»; читает из `tsconfig` только `allowJs`, `baseUrl`,
  `paths`, `jsx*`, `references`, `extends` ([test-typescript](https://playwright.dev/docs/test-typescript)).
  Типизацию делает наш `tsc -b`: добавить `tsconfig.e2e.json` (include `e2e`,
  `playwright.config.ts`; `lib` с `DOM`, иначе типы `playwright-core` не находят `HTMLElement`) и
  ссылку на него в `tsconfig.json`. Проверено: `@playwright/test` 1.64.1 + `typescript` 6.0.3,
  `skipLibCheck: false`, `verbatimModuleSyntax`, `erasableSyntaxOnly` — ошибок нет. Пакет не
  объявляет `peerDependencies` на TypeScript.
- **knip.** Плагин включается при `@playwright/test` в `devDependencies`, читает
  `playwright.config.{js,ts,...}` и считает входом `**/*.@(spec|test).?(c|m)[jt]s?(x)`
  ([knip playwright](https://knip.dev/reference/plugins/playwright)); дополнительной настройки не
  нужно.
- **.gitignore** (`frontend/`): `test-results/`, `playwright-report/`, `blob-report/`.
- **mise.toml**: задача `e2e` (`npm run e2e --prefix frontend`, `depends = ["generate"]`), чтобы
  запускать руками и из hk одной командой. `generate` обязателен: клиент `@/client` не в git.
- **hk.pkl**: шаг в хуке `check` (не в `pre-push`, чтобы не тормозить пуш):

  ```pkl
  ["e2e"] {
    // dotnet run и dotnet test делят obj/, а Vite занимает порт: шаг идёт последним.
    depends = List("backend-test", "frontend-test")
    exclusive = true
    dir = "frontend"
    check = "npm run --silent e2e"
  }
  ```

  `exclusive`: «this step will wait for any previous steps to finish before running… No other steps
  will start until this one finishes»; `depends` фиксирует порядок
  ([hk configuration](https://hk.jdx.dev/configuration.html)). Если шаг окажется долгим, hk умеет
  `profiles`: шаг с профилем `slow` бежит только при `hk check --profile slow`.

## GitHub Actions

Текущий `ci.yml`: один job `check` на `ubuntu-latest` (Ubuntu 24.04), tools через
`jdx/mise-action` (кеш `~/.local/share/mise`, то есть **только инструменты mise**, не браузеры
([mise-action](https://github.com/jdx/mise-action))), затем `mise run install` и `mise run check`.
Добавляется один шаг перед `mise run check`:

```yaml
      - name: Install Playwright browsers
        run: npx --prefix frontend playwright install --with-deps --only-shell chromium

      - name: Run checks
        run: mise run check

      - name: Upload Playwright report
        if: ${{ !cancelled() }}
        uses: actions/upload-artifact@<sha> # v7.0.2
        with:
          name: playwright-report
          path: frontend/playwright-report/
          retention-days: 30
```

Факты:

- Официальный шаблон: `npx playwright install --with-deps`, затем `npx playwright test`, артефакт
  `playwright-report/` ([ci-intro](https://playwright.dev/docs/ci-intro), [ci](https://playwright.dev/docs/ci)).
  `--with-deps` ставит и системные библиотеки через apt; `--only-shell` скачивает только Chromium
  headless shell, «if you are only running tests in headless shell… for example on CI»
  ([browsers](https://playwright.dev/docs/browsers)).
- Кеш браузеров: «Caching browser binaries is not recommended, since the amount of time it takes to
  restore the cache is comparable to the time it takes to download the binaries»; системные
  зависимости в кеш не положить ([ci](https://playwright.dev/docs/ci)). Путь, если всё же нужно:
  `~/.cache/ms-playwright`, ключ по версии Playwright.
- `ubuntu-latest` содержит Chrome 156, Chromium 156, Firefox 158, pwsh 7.6.9, .NET SDK 10.0.403,
  но **не** браузеры Playwright ([runner-images](https://github.com/actions/runner-images/blob/main/images/ubuntu/Ubuntu2404-Readme.md));
  Playwright требует свои сборки (или `channel: 'chrome'` для системного Chrome).
- Альтернатива: job в контейнере `mcr.microsoft.com/playwright:v1.64.1-noble` с
  предустановленными браузерами ([docker](https://playwright.dev/docs/docker)). Для нас хуже:
  внутри контейнера придётся ставить mise и .NET, и теряется кеш `mise-action`.
- В CI: `workers: 1` («We recommend setting workers to "1" in CI environments»), репортер
  `github` даёт аннотации к коду, `retries: 2` + `trace: 'on-first-retry'` оставляют трассу
  для разбора падения ([trace-viewer-intro](https://playwright.dev/docs/trace-viewer-intro)).
  Публичный репозиторий: минуты GitHub-hosted runner'ов бесплатны
  ([billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions)); лимит
  job 6 часов ([limits](https://docs.github.com/en/actions/reference/limits)), наш `timeout-minutes: 20`
  остаётся с запасом.

## Сколько добавляет к `mise run check`

Замеры и оценки на 2026-11-05.

| Этап | Сейчас | С браузерным сценарием | Источник |
|---|---|---|---|
| Весь job `check` в CI (run 42045303137) | 33 с (шаг «Run checks» 15 с) | оценка 1,5–3 мин | `gh api …/jobs` |
| Скачивание Chromium headless shell | – | 99 МиБ архив (+1,4 МиБ ffmpeg), 203 МБ на диске; локально 61 с, на runner'е сеть быстрее | `npx playwright install chromium --only-shell` |
| Системные зависимости (`--with-deps`, apt) | – | оценка 20–60 с, замерить в первом прогоне | – |
| Старт бекенда внутри Playwright | – | ~10 с, сборка уже сделана `dotnet test` в том же `check` (общие `bin/obj`) | замер `dotnet run` |
| Старт Vite | – | ~2 с | лог Vite |
| Прогон одного сценария в Chromium | – | единицы секунд; таймаут теста 30 с, expect 5 с ([test-timeouts](https://playwright.dev/docs/test-timeouts)) | – |
| `npm ci` фронта с новым пакетом | 10 с | +несколько секунд | шаг «Install dependencies» |

Локально при работающих `mise run dev` серверах `reuseExistingServer` убирает всё, кроме самого
прогона. Браузер скачивается один раз в `~/Library/Caches/ms-playwright` (macOS) или
`~/.cache/ms-playwright` (Linux); при смене версии Playwright старые сборки удаляются сборщиком
мусора автоматически ([browsers](https://playwright.dev/docs/browsers)).

## Версии на 2026-11-05

| Что | Версия | Откуда |
|---|---|---|
| `@playwright/test` / `playwright` | 1.64.1 (2026-10-14); 1.64.0 — 2026-10-07, 1.63.0 — 2026-09-04 | `npm view @playwright/test time` |
| Браузеры в 1.64 | Chromium 156.0.8078.4, Firefox 157.0, WebKit 27.2 | [release-notes](https://playwright.dev/docs/release-notes) |
| `Microsoft.Playwright`, `.Xunit`, `.Xunit.v3`, `.NUnit` | 1.64.1 | api.nuget.org |
| Node.js для Playwright | 22.x, 24.x, 26.x (проект: Node 26) | [intro](https://playwright.dev/docs/intro) |
| TypeScript | проект `~6.0.3`; в npm latest уже 7.0.2, остаёмся на 6 (правило AGENTS.md) | `npm view typescript` |
| Docker-образ | `mcr.microsoft.com/playwright:v1.64.1-noble` | [docker](https://playwright.dev/docs/docker) |
| `actions/upload-artifact` | v7.0.2 (в доке Playwright v4/v5) | GitHub releases |
| `ubuntu-latest` | Ubuntu 24.04 | runner-images README |
| mise / pitchfork | mise 2026.10.25, pitchfork 2.33.0 (в `mise.toml`) | `mise --version`, `mise.toml` |

## Команды

```sh
# один раз, из frontend/
npm i -D @playwright/test@1.64.1
npx playwright install --only-shell chromium          # локально; в CI добавить --with-deps

# разработка (серверы уже подняты mise run dev и переиспользуются)
npx playwright test                                   # headless
npx playwright test --ui                              # UI mode
npx playwright codegen http://127.0.0.1:5173          # записать черновик
npx playwright show-report                            # HTML-отчёт после падения

# как в CI
CI=1 npx playwright test                              # поднимет свои серверы, workers=1
```

Проверка типов идёт через существующий `npm run typecheck` (`tsc -b`) после добавления
`tsconfig.e2e.json` в `references`.

## Что осталось проверить при реализации

- Реальное время шагов в первом CI-прогоне (установка браузера с `--with-deps`, весь job).
- Сброс состояния бекенда между тестами: пока хранилище в памяти, вариант «перезапуск сервера на
  каждый файл» не нужен; достаточно уникальных Гостей и Слотов в тесте. Решение принадлежит #12 и #6.
- `dotnet run` в `webServer` и `dotnet test` в `check` делят `obj/`: шаг e2e в hk обязан идти после
  `backend-test` (`depends` + `exclusive`), иначе возможны ошибки сборки.
- Что делать, если порт 5284/5173 занят в CI: с `reuseExistingServer: false` Playwright упадёт с
  понятной ошибкой, это желаемое поведение.

## Источники

- Playwright: [intro](https://playwright.dev/docs/intro), [test-webserver](https://playwright.dev/docs/test-webserver),
  [test-configuration](https://playwright.dev/docs/test-configuration), [test-typescript](https://playwright.dev/docs/test-typescript),
  [browsers](https://playwright.dev/docs/browsers), [ci](https://playwright.dev/docs/ci), [ci-intro](https://playwright.dev/docs/ci-intro),
  [docker](https://playwright.dev/docs/docker), [test-reporters](https://playwright.dev/docs/test-reporters),
  [test-timeouts](https://playwright.dev/docs/test-timeouts), [test-parallel](https://playwright.dev/docs/test-parallel),
  [test-projects](https://playwright.dev/docs/test-projects), [test-fixtures](https://playwright.dev/docs/test-fixtures),
  [api-testing](https://playwright.dev/docs/api-testing), [locators](https://playwright.dev/docs/locators),
  [codegen](https://playwright.dev/docs/codegen), [trace-viewer-intro](https://playwright.dev/docs/trace-viewer-intro),
  [test-cli](https://playwright.dev/docs/test-cli), [release-notes](https://playwright.dev/docs/release-notes).
- Playwright .NET: [intro](https://playwright.dev/dotnet/docs/intro), [ci](https://playwright.dev/dotnet/docs/ci),
  [browsers](https://playwright.dev/dotnet/docs/browsers), [test-runners](https://playwright.dev/dotnet/docs/test-runners),
  [library](https://playwright.dev/dotnet/docs/library).
- ASP.NET Core: [integration tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0),
  [Kestrel endpoints](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-10.0).
- Vite: [server-options](https://vite.dev/config/server-options), [preview-options](https://vite.dev/config/preview-options).
  Vitest: [include](https://vitest.dev/config/include), [exclude](https://vitest.dev/config/exclude).
  knip: [playwright plugin](https://knip.dev/reference/plugins/playwright).
- mise: [daemons](https://mise.jdx.dev/daemons.html), [task configuration](https://mise.jdx.dev/tasks/task-configuration.html),
  [mise-action](https://github.com/jdx/mise-action); pitchfork: [README](https://github.com/jdx/pitchfork);
  hk: [configuration](https://hk.jdx.dev/configuration.html).
- GitHub: [runner-images Ubuntu 24.04](https://github.com/actions/runner-images/blob/main/images/ubuntu/Ubuntu2404-Readme.md),
  [Actions limits](https://docs.github.com/en/actions/reference/limits),
  [Actions billing](https://docs.github.com/en/billing/concepts/product-billing/github-actions).
- Реестры: `npm view @playwright/test`, `npm view typescript`, `api.nuget.org` (flatcontainer index и
  размер nupkg), `gh api` по workflow run 42045303137 этого репозитория.
- Локальные замеры 2026-11-05 на macOS arm64: `npx playwright install chromium --only-shell`,
  `dotnet run --project backend/CalendarApi --urls http://127.0.0.1:5399`,
  `npm run dev --prefix frontend -- --port 5398 --strictPort --host 127.0.0.1`,
  `tsc -p` с `typescript@6.0.3` и `@playwright/test@1.64.1`.
