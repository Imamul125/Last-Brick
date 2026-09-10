# Playable Ads (Unity Playworks / Luna)

Everything needed to produce a playable ad for **Last Brick**, following the
[Playworks step-by-step guide](https://docs.lunalabs.io/docs/playable/getting-started/step-by-step/).

This lives on the `WebGL` branch. Nothing here changes how the Android game builds.

---

## What is already done

| Playworks step | Status |
|---|---|
| 1. Decide the ad concept | Hint → 3 brick pulls → guaranteed win → end card → store |
| 2. Dedicated scene, not the full game | `Assets/Scenes/WebGL/PlayableAds.unity`, generated from the shipped scene |
| 3. Install dependencies (.NET 4.7, MS-Build) | Verified present on this machine |
| 4. Download the plugin | **You must do this** — see below |
| 5. Select only essential assets | `luna.json` excludes; scene stripped from 911 KB to 135 KB |
| 6. Develop build | Run from the plugin once installed |
| 7. Project Diagnostics | Run from the plugin once installed |
| 8. Expose variables | Tunables are public fields on `PlayableAdController` |
| 9. Ad network requirements | `luna.json` carries per-network package blocks |
| 10. Playable API (store redirect, lifecycle) | `LunaPlayable.cs` |
| 11. Custom analytics events | `LunaPlayable.LogEvent`, wired through the flow |
| 12. Production build & upload | Run from the plugin once installed |

The whole flow was verified in a real browser against a Unity WebGL build of the same scene:
tower loads stable → hint appears → three brick pulls (`Score` 1/2/3) → `LevelWon` → end card →
`GameEnded()` → `InstallFullGame()` on the CTA. The timeout branch was verified separately and
emits `LevelFailed` before the same end card.

---

## Step 4 — install the plugin (the one thing that needs your account)

The plugin is a licensed download, not a public package.

1. Sign in at **[playworks.unity.com](https://playworks.unity.com)** and download the latest
   Playworks Plugin release (7.2.0 at time of writing).
2. Unzip it anywhere **outside** this project.
3. In Unity: `Window > Package Manager > + > Add package from disk…`
4. Select `package.json` inside the **`scripts`** folder of the unzipped `luna` folder.

Manual alternative — add to `Packages/manifest.json`, path relative to the `Packages` folder:

```json
"uk.lunalabs.luna": "file:../../Luna/scripts/"
```

Then open the plugin with `Tools > Playworks Plugin` (or `Ctrl+E`).

---

## Then build

1. **Build & Upload tab → Build Develop.** Output lands in `LunaTemp/Stage4/develop`.
2. **Open In Browser** to test in the Dev Environment.
3. **Project Diagnostics tab** — resolve anything it flags before going further.
4. **Build Production** → `LunaTemp/Stage4/create-hub`, or **Upload to Creative Library**
   to push straight to Playworks and download per-network packages.

---

## The playable flow

`PlayableAdController` on the `PlayableAd` object in the scene:

| Field | Default | What it does |
|---|---|---|
| `bricksToWin` | 3 | Successful pulls before the win fires |
| `hintDelay` | 1.2s | Idle time before the tap hint appears |
| `hintRepeatDelay` | 3s | Idle time before the hint returns |
| `maxDuration` | 30s | Hard cap — end card shows regardless |
| `celebrationDelay` | 1.1s | Settle time between win and end card |
| `collapseY` | computed | Below this Y, the tower counts as collapsed |

The playable never dead-ends: reaching the goal wins, the timer expiring still shows the end
card, and a collapsed tower is scored as a **win** rather than a failure.

### API calls

All Luna calls go through `Assets/Playable/Scripts/LunaPlayable.cs`, which compiles with or
without the plugin present via the `LUNA_PLAYABLE` define (injected by `luna.json`). That means
the scene still runs in the Editor and in a plain WebGL build.

- `LunaPlayable.InstallFullGame()` → `Luna.Unity.Playable.InstallFullGame()` — fired by the CTA
  button *and* by a tap anywhere on the end card.
- `LunaPlayable.GameEnded()` → `Luna.Unity.LifeCycle.GameEnded()` — required by Mintegral and
  Vungle. Sent once, guarded against duplicates.
- `LunaPlayable.OnPause` / `OnResume` → `Luna.Unity.LifeCycle` — sets `Time.timeScale`.
- `LunaPlayable.LogEvent(...)` → `Luna.Unity.Analytics.LogEvent(...)`.

Events emitted: `LevelStart`, `TutorialStarted`, `TutorialComplete`, `Score` (per pull),
`LevelWon` / `LevelFailed`, `EndCardShown`. Luna caps a session at 256 events and 32 per unique
name, and warns against logging during initialisation — `LevelStart` is deliberately deferred by
one frame for that reason.

---

## Regenerating the scene

The playable scene is **generated**, not hand-authored, so it can be rebuilt after the game
scene changes instead of drifting out of sync.

```bash
Unity.exe -batchmode -quit -projectPath . -executeMethod PlayableBuildTools.BuildPlayableScene
```

Or from the Editor: `Tools > Playable Ad > 1. Build Playable Scene`.

It **copies** `Assets/Scenes/LastBrick.unity` to the playable path first and then edits the copy,
so the real game scene is never opened dirty. That ordering matters: an earlier version stripped
the source scene in memory and saved a copy, which is safe headlessly but meant that running the
menu item from the Editor left the stripped scene open — and the next thing to save open scenes
wrote it straight over `LastBrick.unity`.

In the copy it keeps only `Main Camera`, `Directional Light`, `Global Volume`, `Floor`,
`EventSystem`, `Environment` and `Lights`, deleting the other 30 roots. It then instantiates
`Resources/Levels/Tower_5` the way `LevelManager` does, strips every remaining project script and
every Cinemachine component, removes all built-in asset references, and builds the camera rig,
canvas, tutorial hand and end card.

It also leaves **Build Settings alone** — `LastBrick.unity` has to stay the game's startup scene
or an Android build ships without the game in it. Playworks reads its scene list from `luna.json`,
and `BuildWebGL` passes its own, so neither needs it.

Menu items:

1. `Tools > Playable Ad > 1. Build Playable Scene`
2. `Tools > Playable Ad > 2. Configure Player Settings For Web`
3. `Tools > Playable Ad > 3. Build WebGL Preview` — plain Unity WebGL, useful for sanity-checking
   without Luna.

Reports are written to `PlayableReports/`.

---

## Project changes made for this

**`Active Input Handling` was changed from "Input System Package" to "Both".** The Playworks
compiler converts C# to JavaScript and cannot carry the Input System package's native device
bindings, so the playable needs the legacy Input Manager available. "Both" leaves the Android
game's Input System usage working unchanged.

### Input on Unity 6 WebGL under "Both" — read this before touching `PlayableInput.cs`

Verified by instrumenting the actual build. Under Active Input Handling = "Both", each backend
works only halfway:

```
[Interactor] press at (540.00, 368.00) | legacy=(0.00, 0.00, 0.00) inputSystem=(540.00, 368.00)
```

- The **legacy** manager raises `Input.GetMouseButtonDown(0)` correctly but reports
  `Input.mousePosition` as **(0,0)** — every raycast goes through the bottom-left corner and hits
  the floor.
- The **Input System** reports the correct pointer position but **never raises**
  `wasPressedThisFrame`.

`PlayableInput` therefore takes the press from the legacy manager and the position from whichever
backend returns something real. When only one backend is compiled in — the Luna build, where the
Input System is excluded — that one is used for everything.

Two consequences worth knowing:

- **uGUI buttons do not work in this configuration.** The EventSystem uses
  `InputSystemUIInputModule`, which needs the Input System press that never arrives. The end card
  keeps its `Button` wiring (it works under Luna) but also polls `PlayableInput` directly, so a
  tap anywhere on the card converts regardless.
- **Press and release can land in the same frame** on web. `PlayableBrickInteractor.Update`
  deliberately falls through from the press branch to the release check for this reason; the
  original `BrickInteractor` returns early there and would drop such a click. That is harmless on
  touch hardware, where the two are always frames apart.

**`FirebaseManager` and `GooglePlayManager` gained `#if UNITY_WEBGL` shells.** Firebase ships
Android/iOS-only libraries and Google Play Games is Android-only, so neither namespace exists on
the WebGL target and the project could not compile for web at all. Under `UNITY_WEBGL` each class
is now an inert stub with the same public surface (`Instance`, `LogLevelStarted` /
`LogLevelCompleted` / `LogLevelFailed`, `PostScore`), so the `LevelManager` call sites still
compile. The `#else` branch is the untouched original, so **the Android build is byte-for-byte
unaffected**.

**`Run In Background` was enabled.** Without it the WebGL player pauses whenever the canvas
loses focus, which freezes `Time` and stalls every timer — the 30s cap never fires and the end
card's tap handling never arms. A playable ad runs inside an iframe that frequently does not hold
focus, so this one is not optional.

WebGL player settings: .NET Framework API compatibility, uncompressed, no data caching,
exceptions off, high managed stripping, engine code stripping on.

---

## LP1025 — "Field 'UnityEditor.BuiltinResource.m_InstanceID' not found"

If the Playworks export fails at Stage 1 with `ExportCollectionException: ScenesCollection` and
that reflection error, the cause is the Unity version: Playworks 7.2.0 reflects into the internal
`UnityEditor.BuiltinResource` type, whose layout differs in 6000.4.5f1.

That code path is only taken when the scene **references a Unity built-in asset**, so the
generator removes every one it can:

- The tutorial hand and CTA background used to be `UI/Skin/Knob.psd` and `UI/Skin/UISprite.psd`
  from `AssetDatabase.GetBuiltinExtraResource`. They are now generated project assets in
  `Assets/Playable/Art/` (`PlayableHand.png`, `PlayablePanel.png`, the latter 9-sliced).
- `Floor` drew Unity's built-in Cube mesh. Its MeshFilter and MeshRenderer are stripped; the
  BoxCollider stays, so physics is unchanged, and the visible ground is `Environment/Ancient_Ground`.
- `RenderSettings.m_SpotCookie` points at a built-in cookie in every Unity scene by default and
  has no API to clear it, so the generator rewrites it to `{fileID: 0}` in the saved YAML.

The generated scene now contains **zero** built-in references — verify with:

```bash
grep -c "0000000000000000[ef]000000000000000" Assets/Scenes/WebGL/PlayableAds.unity
```

If the export still fails after this, the remaining fix is to open the project in **Unity
6000.0 LTS**, which is the newest version Playworks actually supports.

## Building in a separate 6000.0 project

Playworks 7.2.0 cannot run on Unity 6000.3+ (see LP1025 above), so the playable is built from a
copy of this branch opened in **Unity 6000.0 LTS**, leaving the main project on 6000.4.

That copy is a build environment, not a second game. It differs deliberately:

| Change | Why |
|---|---|
| `PLAYABLE_BUILD` in Scripting Define Symbols | Cinemachine is removed there; it activates the shims in `CinemachineCompat.cs` and the SDK shells, so `LevelManager` and `CinemachineDragRotate` still compile |
| `com.unity.cinemachine` removed from the manifest | The playable strips every Cinemachine component and uses `PlayableCameraRig` |
| `Assets/Firebase/` and `Assets/GoogleMobileAds/` deleted | ~232 MB of mobile-only SDKs the playable cannot use |
| `com.unity.collections`, `com.unity.modules.adaptiveperformance`, `com.unity.modules.vectorgraphics` removed | Do not exist in 6000.0; Collections is a transitive URP dependency there |

None of that is needed in this repo, and none of it affects the Android build. The `#if UNITY_WEBGL`
shells in `FirebaseManager`, `GooglePlayManager` and `GameAdManager` are what let the project
compile with those SDKs absent — their `#else` branches are the untouched originals.

`GameAdManager`'s shell invokes its callbacks immediately rather than dropping them: `LevelManager`
advances the level from inside `OnLevelCompleted`'s callback, so swallowing it would stall
progression. "No ad" has to mean "carry on".

## Caveats worth knowing

- **Unity version.** Playworks lists 2021.3 LTS, 2022.3 LTS and 6000.0 LTS as supported. This
  project is on **6000.4.5f1**, which is newer than anything Luna validates against. It will
  probably work, but if the plugin misbehaves the first thing to try is 6000.0 LTS.
- **`luna.json` may be rewritten.** The file at the repo root was authored against the schema
  the plugin uses (`version 5.0.0`, from the official tutorial project). Plugin 7.2.0 may migrate
  or regenerate it on first open. If it does, re-apply these in the plugin UI:
  - Scripts excludes: `**/Editor/`, `Assets/Scripts/`, `Assets/Firebase/`,
    `Assets/GoogleMobileAds/`, `Assets/GooglePlayGames/`, `Assets/ExternalDependencyManager/`,
    `Assets/Plugins/`, `Assets/GeneratedLocalRepo/`, `Assets/SineVFX/`,
    `Assets/UnityTechnologies/`, `Assets/TutorialInfo/`, `Assets/PolyOne/`, `Assets/GPGSIds.cs`
  - Scripting define: `LUNA_PLAYABLE`
  - Startup scene: `Assets/Scenes/WebGL/PlayableAds.unity`
  - MSBuild (Windows): `C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe`
- **Nothing from `Assets/Scripts/` is compiled into the playable.** Firebase, AdMob, Google Play
  Games and Unity IAP cannot survive the C#-to-JS conversion. The playable scripts live in
  `Assets/Playable/Scripts/` precisely so `Assets/Scripts/` can be excluded wholesale.
- **Size budget.** Ad networks cap playables at roughly 2–5 MB. The scene is small, but the
  brick and environment **textures** are the real cost — use the plugin's asset compression and
  resolution controls, and check the size readout on every develop build.
