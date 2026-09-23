# Persona CC0 asset manifest

## Detailed PBR Persona candidates

The semantic candidate list is [`shared/persona-prop-candidates.json`](../shared/persona-prop-candidates.json). Version 2 contains 81 items across 10 self-introduction categories. An item may only be instantiated when the player's introduction directly supports that object or its semantic concept.

- 51 candidates have a reviewed CC0 source. Fifty Poly Haven models are downloaded as 1K FBX plus their source PBR maps; the OpenGameArt rabbit includes FBX, rig/animations, diffuse textures and a normal map.
- 30 candidates remain explicit `tripo_required` fallbacks. No paid generation is performed by the download script.
- Downloaded files live under `client/Assets/AboutMe/Art/ThirdParty/PersonaPBR/PolyHaven/<assetId>/`.
- The additional animated rabbit lives under `client/Assets/AboutMe/Art/ThirdParty/PersonaPBR/OpenGameArt/Rabbit/`.
- Each directory retains a `SOURCE.txt` with the original asset page and `CC0-1.0` license.

The second PBR expansion added 33 models:

| Self-introduction category | Added detailed CC0 models |
|---|---|
| 运动与健身 | 美式橄榄球、棒球、棒球棒、飞镖盘 |
| 手工与维修 | 电钻、活动扳手、螺丝刀、十字锤、卷尺 |
| 科学与医疗 | 化学实验套装、本生灯、放大镜、医疗箱 |
| 音乐与创作 | 随身听、扩音器、喷漆罐 |
| 食物与生活方式 | 可颂、茶具、切菜板、木勺、柠檬 |
| 旅行与户外 | 救生衣、保温杯、园艺手套、园艺小铲、雨靴 |
| 收藏与身份 | 电子手表、怀表、圆框眼镜、明信片套装、橡皮鸭 |
| 学习与职业 | 写字板、文具套装 |

Poly Haven states that all of its 3D models are CC0 and may be used commercially or redistributed. Model pages expose FBX together with diffuse, normal, roughness and other maps where applicable. The downloader is `client/Tools/DownloadPersonaPBR.py`.

## Legacy low-poly fallback set

The authoritative per-model manifest is [`shared/persona-visuals.json`](../shared/persona-visuals.json). Every entry records its `visualId`, original FBX path, generated Unity Resources prefab path, source pack, and `CC0-1.0` license. The import list contains exactly 48 models.

## Source packages

| Source | Author | License | Source page | Imported groups |
|---|---|---|---|---|
| KayKit Furniture Bits 1.0 | Kay Lousberg | CC0 1.0 | https://github.com/KayKit-Game-Assets/KayKit-Furniture-Bits-1.0 | books, pillows, seating, cacti, lamps, frames, rug and stool |
| KayKit Restaurant Bits 1.0 | Kay Lousberg | CC0 1.0 | https://github.com/KayKit-Game-Assets/KayKit-Restaurant-Bits-1.0 | food, ingredients, bowls, plates and pot |
| CC0Tree | Skywolf Game Studios | CC0 1.0 | https://github.com/SkywolfGameStudios/CC0Tree | sports props, tools, electronics, plants and toy props |
| Basketball lowpoly | NephthysGameDev | CC0 1.0 | https://opengameart.org/content/basketball-lowpoly | basketball FBX |

The original license notices are retained beside the corresponding FBX files in `client/Assets/AboutMe/Art/ThirdParty/PersonaCC0`. Do not add a model to the registry without a source page and a verified CC0 notice.

## Unity import contract

`About Me / Persona visuals / Build CC0 prefabs` creates one prefab per registry entry under `Resources/PersonaProps`. It preserves imported FBX materials, adds a disabled client-side bounds collider, and writes a `Grip` child plus `PersonaVisualMeta`. The game rules remain server-authoritative; those colliders are intentionally not used for gameplay collision or hit confirmation.
