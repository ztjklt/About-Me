# Poly Haven Persona source assets

These four source models and diffuse textures were downloaded from Poly Haven on 2026-09-23. Poly Haven publishes its assets under CC0 1.0; the source pages are:

| Local folder | Source page | Kept source files | Game preparation |
| --- | --- | --- | --- |
| `football` | https://polyhaven.com/a/football | `football_1k.fbx`, `football_diff_1k.jpg` | Retains the inflated football mesh. |
| `gamepad` | https://polyhaven.com/a/gamepad | `gamepad_1k.fbx`, `gamepad_diff_1k.jpg` | Removes the long cable and plug for a readable handheld silhouette. |
| `Ukulele_01` | https://polyhaven.com/a/Ukulele_01 | `Ukulele_01_1k.fbx`, `Ukulele_01_diff_1k.jpg` | Keeps the ukulele mesh. |
| `binder_notebook` | https://polyhaven.com/a/binder_notebook | `binder_notebook_1k.fbx`, `binder_notebook_diff_1k.jpg` | Retains the open paper notebook mesh. |

The `*_game.fbx` files are reproducibly derived by `scripts/build-persona-polyhaven.py`: one visual mesh, diffuse texture, normalized toy scale and bottom-center pivot. Other texture maps were omitted from the game package to keep the mobile/WebGL asset size down. The original 1K FBX and diffuse texture remain for provenance and rebuilding.
