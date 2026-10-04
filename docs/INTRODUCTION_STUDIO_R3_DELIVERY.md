# Introduction & wardrobe R3 delivery

Completed compact introduction studio and wardrobe on the complete R2 baseline. Direct introduction, optional questions, horizontal Persona results, player settings, zh/en, 274 props and 96 mechanics remain integrated. No feet, physics, combat or network protocol changes.

- Branch: `codex/introduction-studio-r3`; client source: `ab6ac6bd4bc2a5fff1717098765f4eaf2dce8a57` (UI core `1719cf7a044900281673cc4d9417dc0177c545c7`).
- English AI preference compatibility: `15a7f0207cdfc55c62dce77ccf08f57dd96ba1fd`. API inherits R2 / 049312f and settings snapshot `d65bd4d44b678cbda5de357195af1daeb2d04832819611eec3f6a717eaf2b07a`; only explicit English two-brand recognition and local release diagnostics differ. Existing routes, schemas and skin whitelist remain unchanged. English questionnaire produces ordinary pure English text.
- Source workspace: `/Users/jitian/.codex/worktrees/playtest-bug-fixes/About Me`; frozen source map: `artifacts/playtest-introduction-r3/source-snapshot.json`; SHA256 `050b3cb7396af760f7e36d2716b1d2bb44e410d65daff67f4611bf0ce51e8462`. No other worktree's unfinished settings or Kimi changes were included.
- Release Mac/client/authority: `builds/playtest-introduction-r3-mac/About Me.app`; API: `builds/playtest-introduction-r3-service`; assembly SHA256 `9747387609e5a481a5a8f5329b448d90aa64d05d4baba0e7036bc3db5ea3f97b`; API tree SHA256 `c48d9b7a2e2f22c39e16ca2e60d020e5005f70bcd2a292a2775b70682bd731b3`.
- Stable launcher: `scripts/start-latest.command` → `scripts/start-playtest-introduction-r3.command`. Dedicated API 8088 / authority 7788. Launcher verifies every pinned file, previous feature gates, API version/PID/path, listener PID, and the same client binary for authority; unknown occupied ports are rejected.

## Observable behavior

Half-width nickname and vertically scrollable editable introduction sit left of two raised near-square entry tiles. New drawer art and three-column small draft cards replace the previous long draft row. Save saves the draft without returning; Back navigates independently. Recording, separate expression/color rows, unlock button and backend service card were removed from the introduction page; ASR implementation remains.

13 optional questions retain the original 40 catalog-backed interest choices and add personality, actual mood IDs and the two supported AI preferences. Every question has custom text. Draft preparation/closing/reopening/language changes never overwrite original introduction or current Persona. Appending is explicit and deduplicates; the existing 1200-character introduction limit remains. Oversized preparation/append preserves complete original answers/draft rather than silently truncating. Custom interests and unknown AI text use the ordinary generation flow without fabricated catalog matches or models.

An explicit wardrobe-preview operation maps the six mood choices to friendly/joy/confident/sleepy/surprised/determined and sends selected traits to the existing personality input. AI recommendations use the existing actual provider endpoint; preview/adjustment does not replace saved appearance, and Save outfit remains explicit. AI brands are recognized only through the real supported whitelist on saving/generation, with negative/third-person/uncertain mentions rejected by targeted tests.

The current model card renders a real ToyActor and actual first Persona via ToyFactory, derived from the current saved/cached profile; sample and not-yet-generated states are labeled. Unsaved wardrobe mood is not shown as saved avatar. Preview cameras use isolated layers/positions, are disabled when hidden, and actor/light/camera/texture objects are disposed. Small avatar render target is 320×400. Short result cards are 134 pixels wide, scroll to every item, show progress/status, and open complete scrollable name/reason with explicit generation retry. Wardrobe slots are four columns of near-square 192×148 cards with an independent scroll viewport, real character preview and existing save/recommend paths. Reduced-motion preference keeps button transforms static.

## Evidence and limits

44 checks passed in a running Development Mac client at 1280×720 and 960×600, including 21 mixed result terms reaching item 21, complete long name/reason, pointer/wheel/trackpad routing, unchanged original/draft/current assets, all 13 custom fields, English AI draft, real expression/personality preview, 1200-character scroll and reduced-motion. `artifacts/playtest-introduction-r3/evidence/passed.txt` and matching screenshots document this scope. Test screenshot profile `UI regression` and generated results are constructed local regression data; the first layout screenshot used an existing real cached profile. These are actual engine screenshots, not design mockups. Release uses the same frozen source, with Development audit removed by compile guard.

17 targeted ASGI/API tests passed for English/Chinese positive and negative AI statements, saving actual appearance, supported skin whitelist/revocation and unchanged Persona assets. Model generation in API fixture tests is mocked; no new live model generation, full gameplay regression, public multiplayer, phone/human touchscreen or native IME composition certification is claimed. Chinese strings and long input were exercised through actual InputField data and pointer events.

`start-playtest-introduction-r3.command --check` successfully started and verified API 8088 and authority 7788, then cleaned only its own validation processes. No validation GUI was left running. Previously running R2/older builds and their servers were not stopped or overwritten. Stable entry was updated after this check; this release was not automatically opened for the user.

## Data storage

R3 uses a newly initialized independent local database `artifacts/playtest-introduction-r3/profiles.sqlite3` and cache directory. No live R2 database or credentials were copied. Existing root `server/.env` is referenced only at launch, without inclusion in the delivered package; `.env.example` was omitted as non-runtime data. Account tokens remain scoped to API URL. Existing local draft/cache preferences follow the inherited client behavior, while server account/assets do not automatically migrate across ports. UI mode/cancel protection does not promise cross-API account migration; R2 database and current test processes remain intact.

## New illustration

Actual consumed asset: `client/Assets/AboutMe/Resources/IntroductionUI/mascot-drawer-r3.png` with paired `.meta`; original 1254×1254 transparent RGBA PNG retained, Unity max texture import 512. Built-in ImageGen generated a new drawer scene, using the old mascot only for identity. Complete verbatim prompt: `artifacts/playtest-introduction-r3/mascot-drawer-r3-prompt.txt`; provenance and original image hash: `artifacts/playtest-introduction-r3/image-provenance.json`. Old StartMenu2D artwork was not overwritten.
