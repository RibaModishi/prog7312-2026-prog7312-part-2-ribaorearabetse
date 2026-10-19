# prog7312-2026-prog7312-part-2-ribaorearabetse

Demo video: https://youtu.be/krwKFzlkeeE

# Smart-X — IoT Mesh Data Ingestion Gateway

Smart-X is a simulated hybrid IoT mesh ecosystem for monitoring distributed environments (hydroponic farms, utility trackers, smart grid installations). This repository contains **Part 2** of the Portfolio of Evidence: sensor ingestion from Part 1, plus the real-time command stream, named queues and dictionaries, and the predictive action engine.

## Architecture

The solution is split into three projects:

| Project | Purpose |
|---|---|
| `SmartX.Api` | ASP.NET Core Web API (.NET 10) — the ingestion gateway |
| `SmartX.Client` | .NET MAUI application — the configuration and monitoring dashboard |
| `SmartX.Shared` | Class library — domain models, telemetry structures, and API contracts shared by both the API and the client |
| `SmartX.Shared.Tests` | xUnit test suite for the shared domain logic |

`SmartX.Client` was built as a cross-platform desktop/mobile application communicating with a self-hosted local Web API, rather than a browser-based frontend — chosen to stay within a single language (C#) across the whole stack.

## Key technical features

- **`TelemetryPacket<T>`** — a generic wrapper (`where T : struct`) that uniformly handles float, int, and bool sensor readings without boxing.
- **`PowerReading`** — a struct with overloaded `+`, `-`, `>`, and `<` operators, supporting direct aggregation and delta comparisons of sensor values (e.g. `meter3 = meter1 + meter2`).
- **`DeploymentValidator`** — a recursive algorithm validating nested device deployment trees (Facility → Zone → Sub-Zone → Sensor), checking for blank names, duplicate sibling names, and structurally invalid sensor nodes, and reporting every violation found rather than stopping at the first.
- **`TelemetryBatchProcessor<T>`** — accumulates historical telemetry as a jagged array (`T[][]`), reflecting that real sensor polling cycles can vary in length, and flattens that raw data into optimised `List<TelemetryPacket<T>>` collections.
- **`TelemetryHistory<T>`** — a custom bounded ring collection (`IReadOnlyList<TelemetryPacket<T>>`) for live ingest. Newest packets overwrite the oldest once the ring is full.
- **Firebase Authentication** — the MAUI client signs in with Firebase email and password (`identitytoolkit` `signInWithPassword`). The API checks the Firebase ID token (`https://securetoken.google.com/{projectId}`). There is no password in this repository. Put the class project's web API key and project id in `SmartX.Client/FirebaseConfig.cs` and `Firebase:ProjectId` in `SmartX.Api/appsettings.json`.
- **Part 2 collections** — `SmartX.Api/Services/RealtimeStructures.cs` holds `TelemetryQueue`, `CriticalAlertQueue`, `CommandUndoStack`, `DeviceRegistry`, `SensorTimeline`, and `DisconnectedNodes`.
- **Sensor identity regex** — registrations accept an IEEE MAC (`AA:BB:CC:DD:EE:FF`) or a Smart-X unique id (`SX-NODE01`). Anything else is rejected on the client and the API.
- **Attachment guards** — uploads are limited to `.jpg`, `.png`, `.txt`, `.log`, `.json`, `.csv` and 2 MB. After save, the gateway reads the file and returns a summary (last log lines, JSON field count, photo size).
- **Alert aggregation with contextual anomaly visualisation** — the dashboard's dynamic engagement feature. Rather than showing every threshold breach or sensor disconnect as a separate, disconnected notification, `AlertAggregationService` groups alerts by sensor and time-window proximity into single, contextualised incidents — directly implementing the strategy researched and justified in the Part 1 research report.
- **Mock telemetry simulation** — `SensorTelemetrySimulator` POSTs to `api/telemetry/environmental`, `api/telemetry/power`, and `api/telemetry/actuator`, the same endpoints a physical device calls. Some cycles are skipped so a device can show as disconnected, and some readings breach the normal or critical thresholds.

## Getting started

### Prerequisites
- .NET 10 SDK
- Visual Studio 2022 (17.x or later) with the .NET MAUI workload installed, **or** the MAUI workload via `dotnet workload install maui`

### Clone and restore
```bash
git clone https://github.com/EMGPRS/prog7312-2026-prog7312-part-2-ribaorearabetse.git
cd prog7312-2026-prog7312-part-2-ribaorearabetse
dotnet restore
```

### Run the API
```bash
dotnet run --project SmartX.Api --urls http://localhost:5022
```
The API starts on `http://localhost:5022`. Swagger UI is available in development mode at `http://localhost:5022/swagger` for testing endpoints directly.

> The API runs on plain HTTP for local development on purpose — the ASP.NET Core HTTPS development certificate is not trusted by the Android emulator, so HTTPS is skipped locally to avoid that certificate-trust failure.

### Run the client
With the API already running, launch `SmartX.Client` from Visual Studio (select your target: Windows Machine, or an Android emulator) and press Run — or from the command line:
```bash
dotnet build SmartX.Client -t:Run -f net10.0-windows10.0.19041.0
```
(Substitute the target framework moniker for Android/iOS/Mac Catalyst as needed.)

The client's API base address is configured in `MauiProgram.cs` (`ApiHttpPort` constant, currently `5022`). If you change the API's port, update this constant to match.

**Note for Android emulator testing:** the emulator cannot reach `localhost` directly (it resolves to the emulator itself, not your development machine) — the client automatically switches to `10.0.2.2` when running on Android, so no manual configuration is needed there.

### Run the tests
```bash
dotnet test SmartX.Shared.Tests
```

## Using the application

1. The Smart-X Firebase project is already set in `SmartX.Client/FirebaseConfig.cs` and `SmartX.Api/appsettings.json` (`smart-x-741d7`). Sign-in uses a user created under Authentication → Users.
2. Launch the app and sign in with that Firebase email and password.
3. The home page shows three modules. **Sensor Data Ingestion and Telemetry** and **Real-Time Command Stream and History** are active. **Network Topology and Mesh Routing** stays greyed out for the final PoE.
4. Register a sensor with an IEEE MAC (`A4:CF:12:01:AA:01`) or a unique id (`SX-NODE01`), or import `samples/devices.txt` from the command stream page.
5. Open the command stream to watch sparklines, filter by category or alert state, start or stop a pump, toggle a relay, and undo the last override.
6. Suggested actions appear in two ways. Searching for `Pump A` twice with **Find**, while a moisture reading from the last five minutes is below 20, suggests starting that pump. Starting the same pump twice while moisture is below 20 does the same. Disconnected devices and out-of-range readings are highlighted before you search.

## Testing the queues and dictionaries

Automated checks live in `SmartX.Shared.Tests/RealtimeStructureTests.cs`:

```bash
dotnet test SmartX.Shared.Tests --filter RealtimeStructureTests
```

The script below starts the API with the test token (used only when `SmartX__AllowTestAuth=true`; the product path still requires Firebase):

```powershell
./scripts/verify-api.ps1
```

### Simulate a high-priority alert

A moisture reading of 15 or below, or a power reading of 2000W or above, skips `TelemetryQueue` and is placed on `CriticalAlertQueue`. The processor drains that queue before any normal packet.

1. Start the API with the test token:

```powershell
$env:SmartX__AllowTestAuth = 'true'
dotnet run --project SmartX.Api --urls http://localhost:5022
```

2. In a second terminal, ask the gateway to queue a normal moisture packet and then a 2100W packet before anything is drained:

```powershell
$headers = @{ Authorization = 'Bearer test-operator' }
Invoke-RestMethod http://localhost:5022/api/command-stream/priority-demo -Method Post -Headers $headers
Invoke-RestMethod http://localhost:5022/api/alerts -Headers $headers
```

3. `processedOrder` lists `critical:AA:BB:CC:DD:EE:02` before `normal:AA:BB:CC:DD:EE:01`. The alerts response includes the 2100W breach for Power Rail A. A moisture value of `10` is the same bypass: `POST /api/telemetry/environmental` with `{"sensorId":"AA:BB:CC:DD:EE:01","value":10}` after the demo sensor exists.

A moisture value of `10` on `POST /api/telemetry/environmental` is the same bypass for a critical moisture drop.

`DeviceRegistry` is a dictionary keyed by MAC: telemetry lookup calls `TryGetValue` and does not scan a list. `SensorTimeline` is a `SortedDictionary` keyed by timestamp, which is what the sparkline reads. `DisconnectedNodes` is a `HashSet`, so a second disconnect warning for the same MAC is ignored.

## Project status

Part 1 ingestion and Part 2 command stream are both on the home page. Network Topology and Mesh Routing stays disabled for the final Portfolio of Evidence.

## References

Cormen, T.H., Leiserson, C.E., Rivest, R.L. and Stein, C. (2022) *Introduction to algorithms*. 4th edn. Cambridge, MA: MIT Press.

East, D. (n.d.) *Getting started with Firebase Auth on the Web*. Available at: https://www.youtube.com/watch?v=-OKrloDzGpU (Accessed: 19 October 2026).

Firebase (n.d.) *Firebase Authentication REST API*. Available at: https://firebase.google.com/docs/reference/rest/auth (Accessed: 19 October 2026).

Firebase (n.d.) *Get started with Firebase Authentication on websites*. Available at: https://firebase.google.com/docs/auth/web/start (Accessed: 19 October 2026).

Firebase (n.d.) *Verify ID tokens*. Available at: https://firebase.google.com/docs/auth/admin/verify-id-tokens (Accessed: 19 October 2026).

Google Cloud (n.d.) *Method: accounts.signInWithPassword*. Available at: https://docs.cloud.google.com/identity-platform/docs/reference/rest/v1/accounts/signInWithPassword (Accessed: 19 October 2026).

Janbodnar (n.d.) *C# PriorityQueue*. Available at: https://zetcode.com/csharp/priority-queue/ (Accessed: 19 October 2026).

Microsoft (n.d.) *Dictionary<TKey,TValue> class*. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2 (Accessed: 19 October 2026).

Microsoft (n.d.) *HashSet<T> class*. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1 (Accessed: 19 October 2026).

Microsoft (n.d.) *Overview of ASP.NET Core authentication*. Available at: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/ (Accessed: 19 October 2026).

Microsoft (n.d.) *PriorityQueue<TElement,TPriority> class*. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.priorityqueue-2 (Accessed: 19 October 2026).

Microsoft (n.d.) *Queue<T> class*. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.queue-1 (Accessed: 19 October 2026).

Microsoft (n.d.) *SortedDictionary<TKey,TValue> class*. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.sorteddictionary-2 (Accessed: 19 October 2026).

Microsoft (n.d.) *Stack<T> class*. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.stack-1 (Accessed: 19 October 2026).

Microsoft (n.d.) *Use HttpClientFactory to implement resilient HTTP requests*. Available at: https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory (Accessed: 19 October 2026).

NetMentor (2021) *PriorityQueue in C#*. Available at: https://www.netmentor.es/entrada/en/priorityqueue-csharp (Accessed: 19 October 2026).

.NET Core Tutorials (n.d.) *PriorityQueue in C# .NET*. Available at: https://dotnetcoretutorials.com/priorityqueue-in-net/ (Accessed: 19 October 2026).

Roy Chowdhury, K. (2024) *Learn Advanced C# Part 1: Step by step build PriorityQueue*. Available at: https://www.youtube.com/watch?v=ikbp6vph-6s (Accessed: 19 October 2026).

YouTube (n.d.) *C# Collections tutorial: List, HashSet, Dictionary, Queue and Stack*. Available at: https://www.youtube.com/watch?v=4Mi4OuSMo4A (Accessed: 19 October 2026).

YouTube (n.d.) *C# collections: Stack, Queue and Dictionary*. Available at: https://www.youtube.com/watch?v=GmftVtxrKUc (Accessed: 19 October 2026).

YouTube (n.d.) *C# hands on: queues*. Available at: https://www.youtube.com/watch?v=huzE208-ha8 (Accessed: 19 October 2026).

YouTube (n.d.) *Master the PriorityQueue, a collection in .NET*. Available at: https://www.youtube.com/watch?v=d262kUQKT0E (Accessed: 19 October 2026).
