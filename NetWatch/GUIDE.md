# NetWatch — подробное руководство

Это руководство объясняет три вещи:

1. **Как запустить** готовый стартовый проект (разделы 1–4).
2. **Как он устроен**: что делает каждый файл и как данные проходят через программу (разделы 5–6).
3. **Как развивать проект дальше**: пошаговый план с конкретными задачами (раздел 7).

Если вы раньше не работали с Avalonia — не страшно. Раздел 6 — короткий курс ровно по тому, что используется в проекте.

---

## Содержание

1. [Установка инструментов](#1-установка-инструментов)
2. [Как этот проект создавался с нуля (команды)](#2-как-этот-проект-создавался-с-нуля)
3. [Первый запуск](#3-первый-запуск)
4. [Настройка анализаторов: Kaspersky OpenTIP и ИИ](#4-настройка-анализаторов)
5. [Архитектура и устройство кода](#5-архитектура-и-устройство-кода)
6. [Avalonia за 15 минут — всё, что нужно для этого проекта](#6-avalonia-за-15-минут)
7. [План развития: этапы и задачи](#7-план-развития)
8. [Частые проблемы](#8-частые-проблемы)

---

## 1. Установка инструментов

### 1.1. .NET 10 SDK

Проверьте, что уже установлено:

```bash
dotnet --list-sdks
```

Нужна строка, начинающаяся с `10.`. Если её нет:

- **Windows:** скачайте установщик SDK с https://dotnet.microsoft.com/download или выполните в PowerShell
  `winget install Microsoft.DotNet.SDK.10`.
- **Linux (Ubuntu):** в свежих версиях Ubuntu `sudo apt install dotnet-sdk-10.0`. На Ubuntu 24.04 пакет может
  понадобиться брать из репозитория `ppa:dotnet/backports` или из репозитория Microsoft —
  инструкция для вашего дистрибутива: https://learn.microsoft.com/dotnet/core/install/linux

> Если у вас только .NET 8 или 9 — можно поменять `net10.0` на `net8.0`/`net9.0` в файле `Directory.Build.props`
> и `10.0.*` на `8.0.*`/`9.0.*` у пакета `Microsoft.Data.Sqlite` в `NetWatch.Core.csproj`.
> Файл решения `.slnx` требует SDK 9.0.200+; на .NET 8 откройте сами `.csproj` или создайте `.sln` командой из раздела 2.

### 1.2. Среда разработки (выберите одну)

| IDE | Плюсы | Что поставить для Avalonia |
|---|---|---|
| **JetBrains Rider** | Лучшая поддержка Avalonia, работает в Windows и Linux, бесплатен для некоммерческого использования | Плагин **AvaloniaRider** (превью XAML) |
| **Visual Studio 2022/2026** (Windows) | Привычна, если работали с WPF/MAUI | Расширение **Avalonia for Visual Studio** |
| **VS Code** | Лёгкий, везде работает | Расширения **C# Dev Kit** и **Avalonia for VSCode** |

### 1.3. Шаблоны Avalonia (нужны, только если будете создавать новые проекты/окна)

```bash
dotnet new install Avalonia.Templates
```

После этого появятся шаблоны `avalonia.app`, `avalonia.mvvm`, `avalonia.window` и т.д.

---

## 2. Как этот проект создавался с нуля

Эти команды повторять **не нужно** — проект уже готов. Но полезно понимать, откуда что взялось,
чтобы потом самостоятельно добавлять проекты (например, тесты).

```bash
mkdir NetWatch && cd NetWatch

# Решение — "контейнер" для проектов
dotnet new sln -n NetWatch

# Три проекта
dotnet new classlib -n NetWatch.Core    -o src/NetWatch.Core      # библиотека с логикой
dotnet new console  -n NetWatch.Cli     -o src/NetWatch.Cli       # консольная программа
dotnet new avalonia.app -n NetWatch.Desktop -o src/NetWatch.Desktop  # приложение Avalonia

# Добавляем проекты в решение
dotnet sln add src/NetWatch.Core/NetWatch.Core.csproj
dotnet sln add src/NetWatch.Cli/NetWatch.Cli.csproj
dotnet sln add src/NetWatch.Desktop/NetWatch.Desktop.csproj

# Ссылки: консоль и GUI используют ядро
dotnet add src/NetWatch.Cli     reference src/NetWatch.Core
dotnet add src/NetWatch.Desktop reference src/NetWatch.Core

# Пакеты NuGet
dotnet add src/NetWatch.Core    package Microsoft.Data.Sqlite
dotnet add src/NetWatch.Desktop package Avalonia.Controls.DataGrid
dotnet add src/NetWatch.Desktop package CommunityToolkit.Mvvm
```

Затем: общие настройки вынесены в `Directory.Build.props`, из шаблона удалены лишние файлы,
написан код. Шаблон `avalonia.mvvm` создаёт ещё `ViewLocator.cs` и `ViewModelBase.cs` —
в нашем проекте одно окно, поэтому они не понадобились.

---

## 3. Первый запуск

Все команды выполняются из корня репозитория (папка, где лежит `NetWatch.slnx`).

### 3.1. Сборка

```bash
dotnet build
```

Первая сборка скачает пакеты NuGet (Avalonia, SQLite...) — нужен интернет. Должно закончиться `Build succeeded`.
Предупреждения (warnings) не мешают запуску.

### 3.2. Консольная версия

```bash
# Таблица всех текущих соединений
dotnet run --project src/NetWatch.Cli -- --once

# Режим наблюдения: печатает появление [+] и закрытие [-] соединений. Выход — Ctrl+C
dotnet run --project src/NetWatch.Cli

# Таблица + проверка анализаторами (нужны ключи, см. раздел 4)
dotnet run --project src/NetWatch.Cli -- --analyze
```

> `--` отделяет аргументы для `dotnet run` от аргументов для нашей программы.

Откройте в браузере любой сайт, пока работает режим наблюдения, — увидите новые соединения браузера на порт 443.

### 3.3. Графическое приложение

```bash
dotnet run --project src/NetWatch.Desktop
```

Что есть в окне:

- **Фильтр** — по процессу, IP, порту, хосту, PID или состоянию (например, `chrome`, `443`, `ESTABLISHED`).
- **Скрыть localhost** — убирает соединения внутри компьютера (127.0.0.1, ::1). По умолчанию включено.
- **Скрыть слушающие порты** — оставляет только реальные соединения.
- **Пауза** — останавливает обновление таблицы.
- **Проверить в Kaspersky OpenTIP / Спросить ИИ** — анализ видимых соединений
  (или только выделенного, если стоит галочка «Анализ только выделенного»).
- Вкладки **Активные / История / Журнал**.
- Нажатие на заголовок колонки — сортировка; колонки можно перетаскивать и менять ширину.

### 3.4. Права администратора — это важно

| ОС | Без прав | С правами |
|---|---|---|
| **Windows** | Видны все соединения и их PID, но путь к exe у системных процессов не определяется | Запустите терминал/IDE «от имени администратора» — будут видны пути всех процессов |
| **Linux** | Видны все соединения, но PID и имя — **только у ваших процессов** (у чужих «[нет доступа]») | `sudo` — видно всё |

Запуск консольной версии через sudo в Linux (dotnet может быть не в PATH у root, поэтому сначала собираем):

```bash
dotnet build
sudo ./src/NetWatch.Cli/bin/Debug/net10.0/netwatch --once
```

Графическое приложение под root в Linux запускать не рекомендуется (проблемы с доступом к графическому сеансу,
особенно в Wayland). Для учебных целей достаточно запускать GUI от своего пользователя, а полную картину смотреть
в консольной версии через sudo. Продвинутый вариант — дать опубликованной программе «возможности» (capabilities)
вместо полного root, например `sudo setcap cap_sys_ptrace,cap_dac_read_search+ep ./NetWatch`
(работает для self-contained публикации, см. этап 9).

---

## 4. Настройка анализаторов

### 4.1. Файл настроек

Скопируйте `settings.example.json` в `settings.json` в той же папке (корень репозитория) и заполните.
Программа ищет `settings.json` рядом с exe, в текущей папке и во всех родительских папках —
поэтому файл в корне репозитория видят и консоль, и GUI.

`settings.json` добавлен в `.gitignore` — **ключи не попадут в git**.

Вместо файла ключи можно задать переменными окружения:

```bash
# Linux/macOS
export NETWATCH_OPENTIP_KEY="ваш_ключ"
export ANTHROPIC_API_KEY="ваш_ключ"
```
```powershell
# Windows PowerShell (на текущую сессию)
$env:NETWATCH_OPENTIP_KEY = "ваш_ключ"
$env:ANTHROPIC_API_KEY = "ваш_ключ"
```

### 4.2. Kaspersky OpenTIP

1. Зарегистрируйтесь на https://opentip.kaspersky.com.
2. В профиле найдите раздел API и создайте токен.
3. Впишите его в `settings.json` → `OpenTip.ApiKey`.

Что делает анализатор (`KasperskyOpenTipAnalyzer.cs`): для каждого **уникального публичного** IP делает запрос
`GET /api/v1/search/ip?request=IP` и переводит «зону» Касперского в уровень риска:
Red → ОПАСНО, Orange → Подозрительно, Yellow → Низкий, Green → Чисто, Grey → Нет данных.
Локальные адреса (192.168.x.x, 10.x.x.x и т.п.) не отправляются. Ответы кэшируются на час.

> У бесплатного ключа есть суточный лимит запросов. Если получаете ошибку о лимите — уменьшите
> `MaxRequestsPerRun` или проверяйте только выделенные соединения.
> Если формат API изменится — актуальное описание в разделе Help на сайте OpenTIP; правки нужны только
> в методах `LookupAsync` и `ParseResponse`.

**Про «подключить Касперского»:** у антивируса Kaspersky на ПК нет публичного программного интерфейса,
через который сторонняя программа могла бы «отдать ему соединение на проверку». OpenTIP — это та же база угроз
«Лаборатории Касперского», только доступная через облачный API. Для корпоративных продуктов (Kaspersky
Security Center, KATA) существуют свои API — их можно подключить как отдельный анализатор по тому же принципу.

### 4.3. ИИ: облачный Claude

1. Создайте ключ на https://console.anthropic.com.
2. `settings.json` → `Llm.Provider = "Anthropic"`, `Llm.ApiKey = "..."`.
3. Модель по умолчанию `claude-haiku-5-5` — быстрая и недорогая. Для более вдумчивого анализа
   можно указать `claude-sonnet-5-5`. Актуальный список моделей — https://docs.claude.com.

Модели отправляется только таблица соединений с публичными адресами и открытые порты:
имя и путь процесса, адреса, порты, имена хостов. **Учтите это с точки зрения приватности.**

### 4.4. ИИ: локальная модель через Ollama (данные не покидают компьютер)

1. Установите Ollama: https://ollama.com.
2. Скачайте модель: `ollama pull llama3.1` (или любую другую, например `qwen2.5`).
3. `settings.json` → `Llm.Provider = "Ollama"`, `Llm.OllamaModel = "llama3.1"`.

Маленькие локальные модели чаще ошибаются и хуже соблюдают формат ответа — если анализатор пишет
«ИИ ответил не в формате JSON», попробуйте модель покрупнее или уменьшите `MaxConnections`.

> **ИИ — это «второе мнение», а не антивирус.** Он может пометить нормальную программу как подозрительную
> и пропустить настоящую угрозу. Используйте его, чтобы обратить внимание на необычное, а выводы проверяйте.

---

## 5. Архитектура и устройство кода

### 5.1. Общая схема

```
┌──────────────────────────── NetWatch.Core (логика, без интерфейса) ─────────────────────────────┐
│                                                                                                 │
│  IConnectionProvider ──────► ConnectionMonitor ────── событие Updated ─────┬──► HistoryStore    │
│   ├ WindowsConnectionProvider   (опрос раз в N сек,                        │     (SQLite)       │
│   │   WinAPI iphlpapi.dll        сравнение снимков,                        │                    │
│   └ LinuxConnectionProvider      FirstSeen/ClosedAt)                       │                    │
│       /proc/net/*                    │         │                           │                    │
│                              ProcessResolver  DnsResolver                  │                    │
│                              (PID → имя/путь) (IP → имя хоста)             │                    │
│                                                                            │                    │
│  IConnectionAnalyzer ◄──── NetWatchEngine.AnalyzeAsync ◄── по кнопке ──┐   │                    │
│   ├ KasperskyOpenTipAnalyzer                                           │   │                    │
│   └ LlmAnalyzer (Claude / Ollama)                                      │   │                    │
└────────────────────────────────────────────────────────────────────────│───│────────────────────┘
                                                                         │   ▼
               NetWatch.Cli (консоль)  ──────────────────────────────────┴── NetWatch.Desktop (Avalonia)
```

**Главная идея:** вся логика в `NetWatch.Core`. Консоль и окно — лишь два разных «экрана» к одному ядру.
Это позволяет отлаживать логику в консоли, а позже добавить третий «экран» (веб-панель, MCP-сервер для ИИ-агентов)
без переписывания ядра.

### 5.2. Путь данных за один «тик» (раз в 2 секунды)

1. `ConnectionMonitor.RunAsync` просыпается по таймеру и вызывает `PollOnce()`.
2. Провайдер ОС возвращает список `RawConnection` — «сырые» строки таблицы сокетов.
3. Монитор сравнивает их со словарём `_active` по ключу `Протокол|Локальный|Удалённый`:
   - нового ключа не было → создаётся `ConnectionInfo` с `FirstSeen = сейчас` → список **Opened**;
   - ключ был → обновляются `LastSeen` и `State`;
   - ключ пропал → `ClosedAt = сейчас` → список **Closed**.
4. Для новых соединений с публичными адресами в фоне запускается обратный DNS.
5. Событие `Updated` получают:
   - `NetWatchEngine` → пишет Opened/Closed в SQLite;
   - консоль → печатает `[+]`/`[-]`;
   - GUI → через `Dispatcher.UIThread.Post` обновляет таблицу.

### 5.3. Файлы и что в них читать

| Файл | Что внутри | Ключевые идеи для изучения |
|---|---|---|
| `Core/Models/Enums.cs` | Протокол, состояния TCP, направление | `enum`, методы-расширения, `switch`-выражения |
| `Core/Models/RawConnection.cs` | Строка таблицы ОС | `record`, ключ для сравнения |
| `Core/Models/ConnectionInfo.cs` | Полная информация о соединении | `required`/`init`, вычисляемые свойства |
| `Core/Providers/WindowsConnectionProvider.cs` | Таблица соединений Windows | **P/Invoke**, неуправляемая память, порядок байт |
| `Core/Providers/LinuxConnectionProvider.cs` | Таблица соединений Linux | Разбор `/proc`, hex, символические ссылки |
| `Core/Services/ConnectionMonitor.cs` | Цикл опроса и сравнение снимков | `PeriodicTimer`, `lock`, события |
| `Core/Services/ProcessResolver.cs` | PID → имя/путь процесса | Кэш с временем жизни |
| `Core/Services/DnsResolver.cs` | IP → имя хоста | Кэширование `Task`, таймауты |
| `Core/Services/HistoryStore.cs` | История в SQLite | Параметры SQL, транзакции |
| `Core/Analysis/AnalysisModels.cs` | `IConnectionAnalyzer`, `RiskLevel` | Интерфейс как точка расширения |
| `Core/Analysis/KasperskyOpenTipAnalyzer.cs` | Проверка IP в OpenTIP | `HttpClient`, `JsonDocument` |
| `Core/Analysis/LlmAnalyzer.cs` | Анализ через ИИ | Промпт, разбор ответа модели |
| `Core/NetWatchEngine.cs` | Сборка всех частей | Композиция, `IDisposable` |
| `Cli/Program.cs` | Консольная версия | Top-level statements, Ctrl+C |
| `Desktop/ViewModels/MainWindowViewModel.cs` | Логика окна | MVVM, команды, UI-поток |
| `Desktop/Views/MainWindow.axaml` | Разметка окна | XAML, привязки, DataGrid |

### 5.4. Ограничения стартовой версии (их снимают этапы раздела 7)

- **Время подключения** = момент, когда программа *впервые увидела* соединение (точность — интервал опроса).
  Соединения, жившие меньше 2 секунд, могут быть пропущены.
- Соединения, существовавшие до запуска программы, помечены `≤` — их настоящее время открытия неизвестно.
- Linux-провайдер рассчитан на процессоры little-endian (x86, x64, ARM — то есть практически все).
- В Windows TCP-соединения в состоянии TIME_WAIT принадлежат PID 0 («[система]») — это нормально:
  программа уже закрыла соединение, а система ещё держит его. Мы сохраняем имя процесса, который был до этого.

---

## 6. Avalonia за 15 минут

Если вы работали с WPF или .NET MAUI — 90% будет знакомо: тот же XAML, те же привязки, тот же MVVM.

### 6.1. Из чего состоит приложение

- `Program.cs` — точка входа, запускает Avalonia.
- `App.axaml` + `App.axaml.cs` — приложение целиком: тема, стили, создание главного окна.
- `Views/MainWindow.axaml` — **как выглядит** окно (разметка).
- `Views/MainWindow.axaml.cs` — «код за разметкой»; в MVVM почти пустой.
- `ViewModels/*.cs` — **что показывает** окно и **что делают** кнопки.

### 6.2. Разметка (XAML)

```xml
<StackPanel Orientation="Horizontal" Spacing="8">
  <TextBox Width="200" Watermark="Подсказка" Text="{Binding FilterText}" />
  <Button Content="Нажми меня" Command="{Binding DoSomethingCommand}" />
</StackPanel>
```

Основные контейнеры (панели):

| Панель | Как располагает детей |
|---|---|
| `StackPanel` | В строку или столбец |
| `WrapPanel` | В строку с переносом (как слова в тексте) |
| `DockPanel` | Прижимает к краям (`DockPanel.Dock="Top"`), последний ребёнок занимает остаток |
| `Grid` | Сетка: `RowDefinitions="Auto,*"` — первая строка по содержимому, вторая — всё остальное |

### 6.3. Привязки (Binding)

У каждого элемента есть `DataContext` — объект, из которого берутся данные. Он наследуется вниз по дереву:
мы задали `DataContext = viewModel` для окна, значит, все элементы окна видят свойства `MainWindowViewModel`.

- `{Binding FilterText}` — значение свойства. Для `TextBox.Text` привязка двусторонняя: пользователь печатает →
  свойство меняется → вызывается `OnFilterTextChanged` → таблица фильтруется.
- Внутри `DataGrid` у каждой строки свой `DataContext` — элемент коллекции (`ConnectionRowViewModel`).
- `{Binding Risk, Converter={StaticResource RiskToBrush}}` — значение проходит через конвертер (риск → цвет).

Чтобы интерфейс узнал об изменении свойства, ViewModel должна вызывать событие `PropertyChanged`.
MVVM Toolkit делает это за нас:

```csharp
// Пишем:
[ObservableProperty] private string _filterText = "";

// Toolkit генерирует (упрощённо):
public string FilterText
{
    get => _filterText;
    set { if (_filterText != value) { _filterText = value; OnFilterTextChanged(value); OnPropertyChanged(); } }
}
partial void OnFilterTextChanged(string value);  // можем реализовать, если нужно реагировать
```

### 6.4. Команды

```csharp
[RelayCommand]
private async Task AnalyzeAiAsync() { ... }   // → свойство AnalyzeAiCommand
```

В XAML: `Command="{Binding AnalyzeAiCommand}"`. Пока асинхронная команда выполняется, кнопка сама становится
неактивной. `CanExecute = nameof(CanAnalyze)` — дополнительное условие доступности.

### 6.5. UI-поток — самое важное правило

**Элементы интерфейса и привязанные к ним коллекции можно менять только из UI-потока.**
Монитор работает в фоне, поэтому во ViewModel все события оборачиваются так:

```csharp
_engine.Monitor.Updated += update => Dispatcher.UIThread.Post(() => OnMonitorUpdated(update));
```

Если забыть — получите исключение `Call from invalid thread` или «случайные» падения.

### 6.6. Типичные ошибки новичка в Avalonia

| Симптом | Причина |
|---|---|
| DataGrid вообще не видно | Не подключены стили: `<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"/>` в `App.axaml` |
| Привязка «молчит», данных нет | Опечатка в имени свойства. Ошибки привязок пишутся в окно Output/Debug в IDE |
| Значение на экране не обновляется | Свойство не вызывает `PropertyChanged` (нет `[ObservableProperty]` или `OnPropertyChanged`) |
| Изменения в коллекции не видны | Используется `List<T>` вместо `ObservableCollection<T>` |
| `InitializeComponent` не найден | Не совпадает `x:Class` в .axaml и namespace/имя класса в .axaml.cs |

### 6.7. Полезные ссылки

- Документация: https://docs.avaloniaui.net
- Примеры элементов управления: приложение **ControlCatalog** в репозитории https://github.com/AvaloniaUI/Avalonia (папка samples)
- MVVM Toolkit: https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/

> Проект использует Avalonia 11.3. Уже вышла Avalonia 12 — переходить на неё стоит, когда освоитесь:
> поменяйте версии пакетов в `NetWatch.Desktop.csproj` и сверьтесь с официальным руководством по миграции.

---

## 7. План развития

Этапы упорядочены от простого к сложному. После каждого этапа программа должна собираться и работать —
делайте коммит в git после каждого этапа.

### Этап 0. Освоиться (1–2 вечера)

- [ ] Собрать и запустить обе версии, посмотреть на свои соединения.
- [ ] Поставить точку останова (breakpoint) в `ConnectionMonitor.PollOnce` и пройти один тик по шагам.
- [ ] Открыть `netwatch.db` в **DB Browser for SQLite** и посмотреть таблицу `connections`.
- [ ] Маленькая доработка для разминки: добавить в таблицу GUI колонку «Путь к процессу» (`ProcessPath`) —
      одна строка в `MainWindow.axaml`.

### Этап 1. Модульные тесты ядра

Зачем: провайдеры разбирают «байты» и «hex» — там легко ошибиться, а тесты ловят ошибки сразу.

```bash
dotnet new xunit -n NetWatch.Tests -o tests/NetWatch.Tests
dotnet sln add tests/NetWatch.Tests/NetWatch.Tests.csproj
dotnet add tests/NetWatch.Tests reference src/NetWatch.Core
dotnet test
```

- [ ] Тесты для `NetworkHelpers.IsPublic` (10.0.0.1 → false, 8.8.8.8 → true, ::1 → false...).
- [ ] Тесты для `Formatting.Duration`.
- [ ] Вынести разбор строки `/proc/net/tcp` в отдельный `public static` метод и протестировать на строках-примерах
      (их можно взять командой `cat /proc/net/tcp`).

### Этап 2. Страна и провайдер (GeoIP)

- [ ] Скачать бесплатную базу **GeoLite2-City** и **GeoLite2-ASN** (https://dev.maxmind.com, нужна регистрация).
- [ ] Пакет `MaxMind.GeoIP2` в `NetWatch.Core`.
- [ ] Сервис `GeoIpResolver`: `IPAddress → (Country, City, Asn, Organization)`; вызывать при создании соединения.
- [ ] Колонки «Страна» и «Организация» в GUI, поля в SQLite.

Работает офлайн и очень наглядно показывает, «куда ходит» каждая программа.

### Этап 3. Проверка самого файла программы

Часто подозрителен не IP, а сам процесс.

- [ ] Считать SHA-256 файла процесса (`System.Security.Cryptography.SHA256.HashData(stream)`), кэшировать по пути.
- [ ] Новый анализатор `KasperskyFileHashAnalyzer`: запрос `GET /api/v1/search/hash?request=<sha256>` в OpenTIP.
- [ ] Windows: проверка цифровой подписи exe (подписан ли Microsoft/известным издателем) — эвристика «неподписанный
      процесс из AppData лезет в интернет».

### Этап 4. Правила и уведомления

- [ ] Класс `Rule` + `RuleEngine`: условия вроде «процесс X подключился к новой стране», «открыт порт на 0.0.0.0»,
      «соединение с адресом, который OpenTIP пометил Red».
- [ ] Автоматическая проверка новых соединений (с учётом лимитов API!) вместо проверки по кнопке.
- [ ] Значок в трее (`TrayIcon` в Avalonia) и всплывающие уведомления.
- [ ] Белый список «доверенных» процессов, чтобы не шуметь.

### Этап 5. Точное время и короткие соединения

Опрос раз в 2 секунды пропускает короткие соединения. Решения уровня ОС:

- [ ] **Windows:** ETW (Event Tracing for Windows) — провайдер `Microsoft-Windows-Kernel-Network`, библиотека
      `Microsoft.Diagnostics.Tracing.TraceEvent`. Даёт событие на каждое подключение с точным временем. Требует прав администратора.
- [ ] Альтернатива в Windows: таблица класса `TCP_TABLE_OWNER_MODULE_ALL` — в строке есть поле `liCreateTimestamp`
      (время создания соединения).
- [ ] **Linux:** netlink `SOCK_DIAG` (так работает утилита `ss`) — быстрее, чем `/proc`; или eBPF (например, через
      утилиты `bpftrace`/`tcpconnect` из bcc) для отслеживания каждого `connect()`.
- [ ] Новые провайдеры сделать «событийными»: `IConnectionEventSource` с событием на каждое подключение.

### Этап 6. Настоящие плагины (загрузка анализаторов из DLL)

Сейчас анализаторы прописаны в `NetWatchEngine`. Сделаем, чтобы их можно было добавлять, просто положив DLL в папку.

- [ ] Вынести `IConnectionAnalyzer`, `AnalysisResult`, `ConnectionInfo` в отдельную маленькую библиотеку
      `NetWatch.Abstractions` (на неё будут ссылаться плагины).
- [ ] При запуске сканировать папку `plugins/`, загружать сборки через `AssemblyLoadContext` и создавать
      все классы, реализующие `IConnectionAnalyzer`:

```csharp
foreach (string dll in Directory.GetFiles(pluginsDir, "*.dll"))
{
    var context = new AssemblyLoadContext(Path.GetFileNameWithoutExtension(dll), isCollectible: true);
    Assembly assembly = context.LoadFromAssemblyPath(dll);
    foreach (Type type in assembly.GetTypes()
                 .Where(t => typeof(IConnectionAnalyzer).IsAssignableFrom(t) && !t.IsAbstract))
    {
        analyzers.Add((IConnectionAnalyzer)Activator.CreateInstance(type)!);
    }
}
```

- [ ] В GUI вместо двух фиксированных кнопок — выпадающий список анализаторов (`ComboBox` + `ItemsSource="{Binding Analyzers}"`).
- [ ] Примеры плагинов: **VirusTotal** (`/api/v3/ip_addresses/{ip}`), **AbuseIPDB** (`/api/v2/check`).

### Этап 7. MCP-сервер — подключение любых ИИ-агентов

Сейчас программа сама «звонит» модели. Обратный подход: программа становится **инструментом** для ИИ-агентов.
Model Context Protocol (MCP) — открытый стандарт, через который агенты (Claude Desktop, Claude Code, IDE-ассистенты
и др.) вызывают внешние инструменты. Тогда агент сам решает, что посмотреть: «покажи соединения процесса X»,
«что было ночью», «проверь вот этот IP».

- [ ] Новый проект `NetWatch.Mcp` (консольный), пакет официального C# SDK для MCP (`ModelContextProtocol` на NuGet).
- [ ] Инструменты (tools):
  - `get_active_connections(filter?)` — текущие соединения;
  - `get_history(since, process?)` — история из SQLite;
  - `analyze_ip(ip)` — проверка через OpenTIP;
  - `get_process_info(pid)` — путь, хэш, подпись.
- [ ] Транспорт stdio: агент сам запускает `NetWatch.Mcp` как процесс.
- [ ] Прописать сервер в настройках агента (например, в конфигурации MCP-серверов Claude Desktop).
- [ ] Подумать о безопасности: только чтение, никаких действий вроде «закрыть соединение» без подтверждения.

### Этап 8. Управление (осторожно!)

- [ ] Кнопка «Завершить процесс» (`Process.Kill`) с подтверждением.
- [ ] Windows: блокировка адреса через брандмауэр (`netsh advfirewall firewall add rule ...` или COM API `INetFwPolicy2`).
- [ ] Linux: `nftables`/`iptables`.
- [ ] Обязательно: подтверждение, журнал действий, возможность отменить.

### Этап 9. Публикация

```bash
# Windows: один exe-файл, .NET на целевом ПК не нужен
dotnet publish src/NetWatch.Desktop -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# Linux
dotnet publish src/NetWatch.Desktop -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```

- [ ] Windows: файл `app.manifest` с `requestedExecutionLevel level="requireAdministrator"`, чтобы программа
      сама запрашивала права.
- [ ] Иконка приложения (`<ApplicationIcon>` в csproj и `Icon` у окна).
- [ ] Включить скомпилированные привязки (`AvaloniaUseCompiledBindingsByDefault=true` + `x:DataType` в XAML) —
      ошибки в привязках станут ошибками компиляции, а программа быстрее.
- [ ] Linux: `.desktop`-файл для меню приложений.

---

## 8. Частые проблемы

**`error NETSDK1045: The current .NET SDK does not support targeting .NET 10.0`**
Установите .NET 10 SDK или смените версию в `Directory.Build.props` (см. раздел 1.1).

**Не скачиваются пакеты NuGet (`Unable to load the service index`)**
Нет доступа к https://api.nuget.org — проверьте интернет/прокси. `dotnet nuget list source` покажет источники.

**В консоли Windows «кракозябры» вместо русских букв**
Программа включает UTF-8 сама. Если всё равно плохо — используйте Windows Terminal или выполните `chcp 65001`.

**Linux: у всех процессов «[нет доступа]»**
Нормально без root — см. раздел 3.4.

**Linux: GUI не запускается, ошибка про X11/display**
Avalonia в Linux работает через X11 (в Wayland — через XWayland). Убедитесь, что запускаете из графического сеанса,
не через sudo и не по SSH без перенаправления X.

**OpenTIP: «ключ неверный или без доступа к API»**
Проверьте ключ и что он вставлен без пробелов. У некоторых аккаунтов доступ к API нужно отдельно включить в профиле.

**ИИ: «Claude API вернул 401»** — неверный ключ. **«404 / model not found»** — неверное имя модели в `Llm.Model`.
**Ollama: «Connection refused»** — не запущен сервер Ollama (`ollama serve`) или неверный `OllamaUrl`.

**База истории растёт**
Каждое соединение — одна строка. Добавьте (этап «на будущее») удаление записей старше N дней:
`DELETE FROM connections WHERE first_seen < $date` при запуске программы.
