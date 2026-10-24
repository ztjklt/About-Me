<div align="center">

# About Me

**Turn who you are into a toy-box party battle.**

[Play in your browser ↗](https://web-production-fb9b1.up.railway.app/) · [Latest showcase](#latest-showcase) · [中文](README.md) · [Development guide](docs/DEVELOPMENT.md)

</div>

**About Me** is a 3D party battle prototype where a self-introduction becomes your equipment. Interests, schools and personality traits turn into playable Personas: a basketball, a camera, a university-emblem disc, or a three-dimensional word. Throw them, fight for them, recover your own, and consume your opponents' dropped Personas in a miniature campus arena.

The playable loop connects **introduction → Persona generation and storage → solo practice or multiplayer → combat → results and replay**. The showcase baseline is **R16**, with macOS and WebGL clients, a Linux authoritative game server, and a Python API.

## Latest showcase

<div align="center">
  <a href="docs/media/about-me-poster.png">
    <img src="docs/media/about-me-poster.png" alt="About Me promotional poster: a toy character wearing yellow headphones, surrounded by a camera, a cat, pizza and a storybook" width="520">
  </a>
  <p><strong>More Than Just a Game.</strong><br>Turn interests, stories and personality into playable Personas.</p>
  <p><a href="docs/media/about-me-poster.png">View the full poster</a></p>
</div>

### Promotional video · 2:14

<div align="center">
  <a href="docs/media/about-me-promo.mp4"><img src="docs/media/about-me-promo-cover.jpg" alt="About Me gameplay video: toy characters battling with Persona items in a miniature campus" width="880"></a>
  <p><a href="docs/media/about-me-promo.mp4"><strong>▶ Watch the promotional and gameplay video</strong></a></p>
</div>

Select the cover or watch link to open the video file page. The recorded interface is labelled R15; the current public demo baseline is R16. See this page and the delivery record for current capabilities and validation scope.

## Try it

Open the **[browser demo](https://web-production-fb9b1.up.railway.app/)** and choose **Single Player → practice items (non-AI)**. This uses explicitly labelled sample equipment and does not require your own API key. Solo play supports 1–6 bots; multiplayer rooms support 2–6 players.

Use **Intro Presets** to edit an introduction and generate Personas. Live generation depends on the demo server's provider configuration and available quota; failures are shown explicitly. Desktop browsers are recommended for the first visit. WebGL assets may take a moment to load.

## Highlights

| Feature | Experience |
| --- | --- |
| Personal equipment | Up to 20 Personas per player, grounded in the introduction, with saved inventory and five quick slots. |
| Physical party combat | Throwing, knockdowns, grabs, tugging, timed consumption and recovery, and interrupted interactions. |
| A changing identity | Dropped equipment still belongs to its owner until consumption completes; elimination, spectating and results follow. |
| Expressive item catalogue | The R16 delivery includes 277 items, 16 MBTI types, personality and mood words, and four animal presentations. |
| A miniature campus | A two-level desk-sized arena, shared props, environmental mechanics and timed giant-hand events. |
| Solo and multiplayer | Bots share the player's combat rules; online play uses authoritative decisions, WebSocket synchronization and identity-based reconnection. |

The standard match opens with a five-second reveal, triggers giant hands at 2:30 and 5:00, and ends at 7:30 or when one player remains. Time-limit ranking compares completed consumption, then remaining Personas.

## Screens

<img src="docs/images/about-me-r16-home.png" alt="About Me R16 main menu in a warm, toy-like classroom" width="100%">

| Miniature campus | Persona props |
| --- | --- |
| ![In-engine campus scene study](docs/previews/desk-campus/in-engine-overview.png) | ![Imported CC0 Persona props](docs/persona-library/new-four-preview.png) |
| **Persona inventory** | **Character wardrobe** |
| ![Inventory UI design preview](docs/ui-touch-polish/bag-1600x900.png) | ![Wardrobe UI design preview](docs/ui-interaction/wardrobe-front.png) |

The menu is an archived R16 capture. The gallery preserves development previews of the campus, imported CC0 props and interface design. The inventory uses fixed preview data without a server connection; historical UI values do not represent R16 capacity settings. See the [R16 delivery record](docs/INPUT_R16_DELIVERY.md) for validation scope.

## Run from source

```sh
git clone https://github.com/ztjklt/About-Me.git
cd About-Me
```

Add `client/` in **Tuanjie Hub**, use editor **2022.3.62t11**, open `Assets/AboutMe/Scenes/Bootstrap.unity`, and press Play. Solo practice works without the AI API. Service configuration, multiplayer, build commands and tests are documented in the [development guide](docs/DEVELOPMENT.md) in Chinese.

The repository contains source and game assets. Prebuilt applications, local account data and credentials are excluded. `scripts/start-latest.command` expects an existing local R16 bundle; build from source after a fresh clone.

**Controls:** `WASD` to move, double-tap a direction to run, mouse to look, `Space` to jump, `F` to use an item, `G` to grab a player or shared prop, `H` to grab an opponent's item, `E` to consume or recover, hold `Q` to escape, `1`–`5` for quick slots, `B` for inventory, `Tab` to change spectator target, and `Esc` to release the cursor.

## Explore the code

- [`client/Assets/AboutMe/Runtime`](client/Assets/AboutMe/Runtime): C# rules, character physics, UI, Mirror networking and platform bridges.
- [`client/Assets/AboutMe/Editor`](client/Assets/AboutMe/Editor): scene generation, rule checks and build tools.
- [`server`](server): FastAPI, SQLite, accounts, AI generation, image recognition, emblems and public-item roasts.
- [`shared`](shared): controlled catalogues and gameplay contracts.
- [`deploy`](deploy): Docker, Caddy, HTTPS/WSS and Railway staging tools.

Original introductions remain in authenticated owner data and on the server; public match data exposes only the item fields required by gameplay. Provider and game-server credentials are configured through server-side environment variables.

## Status and credits

This is a playable showcase prototype. The historical R16 delivery covers builds and automated public-network checks; the public-repository preparation separately reran **538 backend tests** and reviewed credentials and Git history. Human balance testing, geographically separate networks and WeChat devices require further validation.

[Architecture](docs/ARCHITECTURE.md) · [Deployment](docs/DEPLOYMENT.md) · [Validation history](docs/STATUS.md) · [Repository audit](docs/PUBLIC_REPOSITORY_AUDIT.md) · [Security](SECURITY.md) · [Contributing](CONTRIBUTING.md)

The complete project does not currently carry a single open-source licence. Mirror, fonts and third-party models retain their own licences and notices. See the [asset manifest](docs/THIRD_PARTY_PERSONA_ASSETS.md); brand characters, university emblems and other marks retain their respective source terms.
