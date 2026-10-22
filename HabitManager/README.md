# Ieradumu pārvaldnieks (Habit Manager)

Ieradumu uzskaites lietotne, kas tiek veidota pakāpeniski kursa laikā.

| Mājas darbs | Kas tika pievienots |
|---|---|
| 1 | Domēna modelis, koplietojamie datu modeļi (DTO), testa dati, pārbaude konsoles lietotnē |
| 2 | .NET MAUI lietotne, kas strādā bez servera un glabā datus lokālā JSON failā |
| 3 | REST API (aplikācijas un infrastruktūras slānis, datu glabāšana atmiņā); lietotne tagad strādā caur API |

## Risinājuma struktūra

```
HabitManager.sln
src/
  Habits.Domain/          entītijas, biznesa noteikumi, testa datu ģenerators
  Habits.Shared/          DTO + API galveņu nosaukumi (kopīgi API un lietotnei)
  Habits.Application/     lietojuma gadījumi (HabitService) + datu glabāšanas abstrakcija (IHabitRepository)
  Habits.Infrastructure/  InMemoryHabitRepository (datubāzes implementācija būs 4. mājas darbā)
  Habits.Api/             ASP.NET Core Web API ar "plāniem" kontrolieriem
  Habits.ConsoleApp/      domēna un testa datu pārbaude konsolē
Habits.Mobile/            .NET MAUI lietotne (MVVM)
```

Visi projekti izmanto **.NET 9**.

### Atkarības

```
Habits.Api ──► Habits.Application ──► Habits.Domain
     │                  └───────────► Habits.Shared
     └──► Habits.Infrastructure ──► Habits.Application

Habits.Mobile ──► Habits.Shared (+ Habits.Domain bezsaistes režīmam)
Habits.ConsoleApp ──► Habits.Domain
```

`Habits.Domain` un `Habits.Shared` nav atkarīgi ne no kā (tajos nav UI, API vai datubāzes koda).

### Viena pieprasījuma ceļš

```
Lietotne:  View → ViewModel → IHabitService = ApiHabitService ──HTTP/JSON (Shared DTO)──►
API:       HabitsController → HabitService → IHabitRepository = InMemoryHabitRepository
                                   │
                                   └── Habit (Domain): visi biznesa noteikumi
```

## Habits.Domain

| Fails | Nozīme |
|---|---|
| `Entities/Habit.cs` | Agregāta sakne: `Create`, `Restore`, `UpdateDetails`, `Archive`, `Activate`, `MarkCompleted`, `UnmarkCompleted`, `IsCompletedOn` |
| `Entities/HabitCompletion.cs` | Izpildes ieraksts; to var izveidot tikai caur `Habit` |
| `ValueObjects/RecurrenceRule.cs` | Grafiks: `Daily()` (katru dienu) vai `Weekly(days)` (noteiktās nedēļas dienās) |
| `TestData/TestDataGenerator.cs` | Testa ieradumi lietotājiem `demo-user-1` un `demo-user-2` |

**Biznesa noteikumi (tiek kontrolēti klasē `Habit`):**

- nosaukums nedrīkst būt tukšs un drīkst saturēt ne vairāk kā 200 rakstzīmes;
- katram ieradumam jāpieder lietotājam (`UserId`);
- arhivētu ieradumu nevar atzīmēt kā izpildītu;
- vienam ieradumam vienā datumā var būt tikai viens izpildes ieraksts;
- nedēļas grafikam jānorāda vismaz viena diena.

Ieradumu un izpildes ierakstu identifikatori ir `Guid`; `UserId` ir teksts (`string`),
lai to vēlāk būtu viegli sasaistīt ar ASP.NET Core Identity.

## Habits.Application

- `Abstractions/IHabitRepository` — datu glabāšanas abstrakcija. 4. mājas darbā tiks
  pievienota tikai datubāzes implementācija; servisi un API paliks nemainīgi.
- `Services/HabitService` — lietojuma gadījumi: saraksts, viens ieradums, izveide,
  labošana, arhivēšana/atjaunošana, dzēšana, izpildes atzīmēšana/noņemšana,
  izpildes vēsture. Katra metode saņem pieprasītāja lietotāja ID, tāpēc lietotājs
  redz un maina tikai savus ieradumus.
  Katras komandas shēma: *ielādēt → izsaukt domēna metodi → saglabāt → atgriezt DTO*.
- `Exceptions/HabitNotFoundException` — ieradums neeksistē vai pieder citam lietotājam.
- `Mapping/HabitDtoMappings` — pārveidošana starp domēna objektiem un DTO.

## Habits.Infrastructure

`InMemoryHabitRepository` glabā ieradumus vārdnīcā (`Dictionary`), kamēr API darbojas
(pēc pārstartēšanas dati pazūd), un palaišanas brīdī tiek aizpildīts ar
`TestDataGenerator` datiem.
Repozitorijs glabā un atgriež **kopijas** (izmantojot `Habit.Restore`), tāpēc tas
uzvedas kā īsta datubāze: izmaiņas tiek saglabātas tikai pēc `UpdateAsync`.
Piekļuve ir sinhronizēta ar `lock`, jo API apstrādā vairākus pieprasījumus vienlaikus.

## Habits.Api

Kontrolieri tikai nolasa pieprasījumu, izsauc `HabitService` un atgriež rezultātu.
Kamēr nav autentifikācijas, lietotājs tiek noteikts pēc galvenes **`X-User-Id`**
(`ApiHeaders.UserId` projektā Habits.Shared); pieprasījuma ķermenī nosūtītais `UserId` tiek ignorēts.

| Metode | Ceļš | Nozīme | Atbildes |
|---|---|---|---|
| GET | `/api/habits?includeArchived=false&today=yyyy-MM-dd` | lietotāja ieradumu saraksts | 200 |
| GET | `/api/habits/{id}?today=yyyy-MM-dd` | viens ieradums | 200 / 404 |
| POST | `/api/habits` | izveidot | 201 / 400 |
| PUT | `/api/habits/{id}` | labot | 200 / 400 / 404 |
| POST | `/api/habits/{id}/archive` | arhivēt | 204 / 404 |
| POST | `/api/habits/{id}/restore` | atjaunot no arhīva | 204 / 404 |
| DELETE | `/api/habits/{id}` | dzēst | 204 / 404 |
| GET | `/api/habits/{id}/completions` | izpildes vēsture | 200 / 404 |
| POST | `/api/habits/{id}/completions` | atzīmēt datumu kā izpildītu | 201 / 404 / 409 |
| DELETE | `/api/habits/{id}/completions/{yyyy-MM-dd}` | noņemt izpildes ierakstu | 204 / 404 |

### Datumi un laika zonas

- **Laika momenti** (`CreatedAt`, `UpdatedAt`) tiek glabāti kā `DateTimeOffset` UTC laikā
  un lietotnē tiek parādīti lietotāja vietējā laikā.
- **Izpildes datums** ir `DateOnly` — kalendāra diena no lietotāja skatpunkta; to vienmēr izvēlas klients.
- **"Šodiena"** (lauks `IsCompletedToday`) tiek aprēķināta pēc klienta nosūtītā parametra
  `today`, nevis pēc servera pulksteņa. Tā lietotājs citā laika zonā neredz kļūdu par vienu
  dienu. Ja parametrs nav norādīts (piem., manuāls pieprasījums), tiek izmantots šodienas datums UTC.

### Kļūdas

Visas kļūdas tiek atgrieztas **ProblemDetails** formātā (`title` + saprotams `detail`),
un tās apstrādā viens globāls apstrādātājs `ErrorHandling/ApiExceptionHandler` —
kontrolieros nav try/catch bloku.

| Situācija | Statuss | Avots |
|---|---|---|
| Ieradums neeksistē | 404 | `HabitNotFoundException` |
| Nederīgs nosaukums vai dati, nav lietotāja galvenes | 400 | `ArgumentException` no domēna vai pieprasījuma validācija |
| Arhivēta ieraduma atzīmēšana, atkārtots datums | 409 | `InvalidOperationException` no domēna |
| Jebkura neparedzēta kļūda | 500 | tiek žurnalēta; klientam iekšējās detaļas netiek rādītas |

Failā `Habits.Api.http` ir sagatavoti pieprasījumi katram endpointam un katrai
kļūdas situācijai (tos var palaist tieši no Visual Studio).

## Habits.Mobile

### Funkcijas

| Funkcija | Kur |
|---|---|
| Ieradumu saraksts, šodien izpildītie | `HabitsPage` — "Šodien izpildīti: X no Y", atzīmēšana katram ieradumam, reakcija uz peles kursoru un nospiešanu |
| Ieraduma izveide | "+ Jauns" → `HabitEditPage` |
| Ieraduma detaļas un izpildes vēsture | nospiežot uz ieraduma → `HabitDetailPage` |
| Labošana | "Labot" detaļu lapā |
| Arhivēšana / atjaunošana | poga detaļu lapā; sarakstā var parādīt arī arhivētos |
| Konkrēta datuma atzīmēšana | datuma izvēle detaļu lapā |
| Dzēšana | "Dzēst" detaļu lapā (ar apstiprinājumu) |

### Arhitektūra (MVVM)

- **Views/** — XAML lapas; code-behind tikai piesaista view model, nodod `OnAppearing`
  un atskaņo kartītes animāciju.
- **ViewModels/** — ekrānu loģika (CommunityToolkit.Mvvm); atkarīgi tikai no saskarnēm.
- **Services/**
  - `IHabitService` — viss, ko lietotāja saskarne var darīt ar ieradumiem, izmantojot Shared DTO.
  - `Api/ApiHabitService` — **pašreizējā implementācija**: HTTP pieprasījumi uz Habits.Api.
    Tīkla kļūdas tiek parādītas kā "Serveris nav sasniedzams", API kļūdas — ar servera
    ProblemDetails tekstu. Katram pieprasījumam tiek nosūtīta lietotāja šodienas datuma vērtība.
  - `LocalHabitService` — 2. mājas darba bezsaistes implementācija (lokāls JSON fails),
    saglabāta kā alternatīva.
  - `ICurrentUserService` / `DemoUserService` — pašreizējais lietotājs (`demo-user-1`),
    kas tiek nosūtīts uz API galvenē `X-User-Id`.
  - `IDialogService`, `INavigationService` — paziņojumi un navigācija, lai view model
    nebūtu tieši atkarīgi no lapām.

Pārslēgšanās starp API un bezsaistes režīmu ir viena reģistrācija failā `MauiProgram.cs`.
API adrese atrodas failā `Services/Api/ApiSettings.cs`
(`http://localhost:5080/`, Android emulatoram: `http://10.0.2.2:5080/`).

## Palaišana

1. Risinājums → **Konfigurēt startēšanas projektus…** → *Vairāki startēšanas projekti*:
   `Habits.Api` = Sākt (pirmais), `Habits.Mobile` = Sākt.
2. Projektam `Habits.Mobile` izvēlēties **Windows Machine** (vai Android emulatoru).
3. **Atkļūdošana → Sākt bez atkļūdošanas** (Ctrl+F5).

API klausās adresē `http://localhost:5080`. Ja lietotne atveras ātrāk, nekā API ir gatavs,
pietiek pavilkt sarakstu uz leju, lai to atjaunotu.

Tikai konsoles pārbaude: iestatīt `Habits.ConsoleApp` kā startēšanas projektu un palaist.
