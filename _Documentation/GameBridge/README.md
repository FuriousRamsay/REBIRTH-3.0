> Batch-testing update (2026-10-18): use [PREPARED_BATCH_TESTING_20261018.md](PREPARED_BATCH_TESTING_20261018.md). The bridge now uses exclusive batch ownership, immutable session pinning and exact launcher-owned process identity. The discovery alias is not a permanent session identity. The external respawn watcher is retired; native instincts remain on.

# REBIRTH Game Bridge (automated in-game testing)

**Target:** REBIRTH 3.0 for 7 Days to Die 3.0 (b259). The APIs used were checked against
`7DaysToDie_Data/Managed/Assembly-CSharp.dll` of the installed build.
**Purpose:** let an external coding agent (Codex) or a script launch the game, run console commands,
read player/world state, capture logs and screenshots, and run scripted test scenarios. The agent can
then check its own changes in-game and adjust them.

## Architecture

```
Codex / shell ──► Tools/GameBridge/gamebridge.ps1 ──HTTP (127.0.0.1 only, token)──► RebirthGameBridge (in RebirthUtils.dll)
                        │                                                           │
                        ├─ build (dotnet build -c Debug)                            ├─ HttpListener thread → ConcurrentQueue
                        ├─ clone test save from baseline                            ├─ RebirthGameBridgePump (MonoBehaviour, main thread)
                        └─ start 7DaysToDie.exe with launch args                    └─ Log ring buffer (Log.LogCallbacks)
```

### In-game side: `Scripts/GameBridge/`
| File | Role |
|---|---|
| `RebirthGameBridge.cs` | `IModApi` entry, gating, `HttpListener`, token auth, session file, request/response plumbing, main-thread pump |
| `RebirthGameBridgeHandlers.cs` | Endpoint implementations (main thread only) |
| `RebirthGameBridgeLog.cs` | 20,000-entry ring buffer of log lines with sequence numbers |

**Gating.** Nothing starts unless the process command line contains `-rebirthbridge` or
`-rebirthbridge=PORT`. Normal play and dedicated servers are unaffected: no listener, no GameObject,
no log hook. The game logs one harmless warning for the unknown argument.

**Security.** The listener binds `http://127.0.0.1:<port>/` only. Each session creates a random token
and writes it to `Tools/GameBridge/out/session.json`. Requests without a matching `X-Bridge-Token`
header get 401. The session file is deleted on clean quit, and the client ignores it when its `pid` is dead.

**Threading.** Requests are queued and run in `RebirthGameBridgePump.Update()`, at most 16 per frame.
Screenshots finish in a coroutine after `WaitForEndOfFrame`. `/ping` is answered on the listener thread
from state cached each frame, so it still responds while the main thread is blocked loading a world.
It reports `mainThreadStallMs`.

### Endpoints
| Endpoint | Notes |
|---|---|
| `GET /ping` | `state` (`starting/menu/loading/spawning/ingame/dead/server`), `lastLogSeq`, `pid` |
| `GET /state?sections=` | player, stats (health/stamina/food/water), buffs, cvars, inventory (toolbelt + backpack), equipment, skills (level > 0), world (day/time), ui (open windows) |
| `POST /console?cmd=` or body | Runs each line via `SdtdConsole.ExecuteSync(cmd, null)` (local-game sender). Returns console output **plus log lines emitted during the command**. `hadError` is set on `*** ERROR` output or error-level log lines. |
| `GET /log?since&level&limit&contains` | From the ring buffer |
| `GET/POST /cvar` | Read (`names=a,b`) or set (`name=&value=`) local-player CVars |
| `POST /screenshot?name&maxWidth` | PNG in `Tools/GameBridge/out/screenshots/`, downscaled to `maxWidth` (default 1600) |
| `GET/POST /ui?action&window` | List open windows, or open/close a window group via `GUIWindowManager` |
| `GET /entities?radius&limit&contains` | Nearby entities: class, type, distance, health |
| `POST /look?yaw&pitch` | `EntityPlayerLocal.SetRotation` (best effort) |
| `POST /quit` | `Application.Quit()` |

### Launch automation
`gamebridge.ps1 launch` starts `7DaysToDie.exe` directly with:
`-noeac -rebirthbridge=PORT -LoadSaveGame=true -GameWorld=W -GameName=S -SkipNewsScreen=true -SkipSpawnButton=true -skipintro`,
plus windowed-mode Unity arguments.

`LoadSaveGame` is the game's own automation (`PlatformApplicationManager.LoadSaveGame`,
`XUiC_ContinueGame.doLoadSaveGameAutomation`). It selects the save in the Continue Game window and
presses Start. `SkipSpawnButton` skips the "Ready to spawn" screen.

Test saves: the configured `save` (default `CodexTest`) is cloned from `baselineSave` when it is missing,
or on every `launch -Reset` / `reset-save`. The baseline itself is never modified.

## Project changes
- `RebirthUtils.csproj`: added a reference to the game-shipped `Newtonsoft.Json.dll` (`Private=False`).
- `.gitignore`: `/Tools/GameBridge/out/`.
- `AGENTS.md` (repo root): usage instructions that Codex reads automatically.

## Known limitations
- The agent drives the game through commands and state, not through mouse and keyboard.
  Movement is done with `teleport`, looking with `/look`.
- Changes to XML or the DLL need a full game restart (about 1–3 minutes).
- The Release configuration does not currently compile because of existing `RebirthSurvivorDebug`
  references in `XUiC_RebirthSurvivorCreator.cs`. This predates the bridge. The bridge builds in Debug,
  and is not in a Debug-only folder, so it will also ship in Release builds, where it stays inert without
  the launch argument.
- Steam must be running (the game is started directly, with `steam_appid.txt`).

### Vehicle storage radial input (2026-10-03)
Hold `permanent.Activate`, wait for the radial to appear, hover the storage sack, then release **only** `permanent.Activate`. Releasing all inputs also clears the virtual cursor and can erase the radial selection before the native release handler runs. At the 1280x720 test resolution the observed storage sack was near (565,290); inspect a fresh screenshot rather than assuming those coordinates at another resolution. Verify `bagStorage` and `rebirthContextNavigation` actually opened.


### REBIRTH custom input actions
The bridge actions listing includes the registered `rebirth` action set. Use `press action=rebirth.OreSense` to exercise the actual custom action; it follows normal on-foot eligibility and open-window restrictions. Other registered custom actions use the same prefix. This does not directly invoke feature methods or bypass gameplay eligibility. Qualified action names avoid ambiguity with native sets. Runtime routing was exercised in CodexTest on2026-10-10; see PerformanceAudit_20261010/LIVE_ORE_CAPTURE_FINAL.json. Visual marker correctness is a separate check.
