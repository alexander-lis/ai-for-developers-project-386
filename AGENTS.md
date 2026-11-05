# Календарь звонков

Прототип сервиса записи на звонки. Сценарии и ограничения продукта: [README.md](README.md), читать перед работой над фичей.

## Команды

Запускать из корня.

- `mise run setup`: зависимости и git-хуки, один раз после клонирования
- `mise run dev`: серверы в фоне, приложение на http://localhost:5173, API на :5284
- `mise run dev:stop`: остановить серверы
- `mise daemons logs backend`: логи сервера, так же `frontend`
- `mise daemons restart backend`: применить правки C#, фронтенд обновляется сам
- `mise run generate`: пересоздать клиент после правки контракта
- `mise run test`: все тесты
- `mise run lint`: линтеры, форматтеры, типы
- `mise run fix`: исправить форматирование
- `mise run check`: все проверки, то же запускает CI; задача готова, когда он зелёный

Один тест:

- `dotnet test backend/CalendarApi.slnx --filter FullyQualifiedName~Health_ReturnsOk`
- `cd frontend && npx vitest run src/App.test.tsx`

## Устройство

`contract/main.tsp` это источник правды об API. `mise run generate` превращает его в `contract/tsp-output/schema/openapi.yaml`, а затем в `frontend/src/client`. Оба результата в git не входят, клиент импортируется как `@/client`.

Сервер в `backend/CalendarApi` (.NET 10, Minimal API) написан вручную: зрелого генератора под него нет. Компилятор не сверяет его с контрактом, поэтому при правке контракта обновляй `Program.cs` и тесты бекенда вместе с ним.

Все эндпоинты лежат под `/api`: по этому префиксу Vite проксирует запросы на бекенд.

Компоненты shadcn/ui лежат в `frontend/src/components/ui`. Добавлять: `npx shadcn@latest add <name>` из `frontend/`, затем `mise run fix`.

## Правила

- Коммиты по Conventional Commits (`feat:`, `fix:`, `chore:`); строки тела до 100 символов.
- Свои проверки добавляй в задачу `check` через `hk.pkl`, а workflow в `ci.yml`. `.github/workflows/hexlet-check.yml` принадлежит Hexlet и остаётся без правок.
- TypeScript во `frontend` держим на 6.x: `@hey-api/openapi-ts` 0.99.0 падает на версии 7. По этой же причине в `tsconfig` только `paths`: `baseUrl` в TS 6 даёт ошибку.
- Типы проверяет `tsc -b`: корневой `tsconfig.json` содержит только ссылки, и `tsc --noEmit` на нём проверяет 0 файлов.
- Vite в daemon запускается с `--host 127.0.0.1`: `ready_port` проверяет IPv4, а без флага Vite слушает только `[::1]`, и `mise run dev` виснет.

## Agent skills

### Issue tracker

Задачи ведём в GitHub Issues этого репозитория через `gh`. См. `docs/agents/issue-tracker.md`.

### Triage labels

Стандартные пять меток триажа: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. См. `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `GLOSSARY.md` и `docs/adr/` в корне. См. `docs/agents/domain.md`.
