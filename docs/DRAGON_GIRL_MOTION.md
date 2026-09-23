# Dragon Girl motion integration

## Result

The supplied FBX is usable as a Unity Humanoid character. Its original automatic
weights were not acceptable for gameplay: hand and leg motion influenced the
skirt, sleeves, hair, wings, and tail. `scripts/dragon-girl-assets.py` rebuilds
the weights before the asset enters the game.

The reviewed runtime prefab is:

`Assets/AboutMe/Resources/CampusDoll/DragonGirl.prefab`

`DragonGirlMotionAdapter` consumes the same `DollCombatMotion.Input` used by the
current campus doll. It currently covers idle, walk, run, light hit, heavy hit,
grab, hurt, and fall. The adapter works in Unity Humanoid muscle space so it does
not assume that the imported FBX has the same local bone axes as the procedural
doll.

## Garment isolation rule

- Sleeve, skirt, coat, hair, horn, wing, and tail vertices cannot receive arm,
  hand, leg, or foot weights.
- Only exposed arm/hand skin and the leg/foot shell can receive limb weights.
- Finger weights are collapsed into the hand because the game does not animate
  individual fingers.
- The current seam-split audit locks 9,100 of 9,942 runtime vertices. Under an intentionally
  extreme limb pose, the maximum and mean displacement of every locked vertex
  are both exactly `0.0`.
- The inverse check requires the allowed hand/foot shell to move as well. Its
  current maximum displacement is `0.3963` and mean displacement is `0.1354`,
  preventing an all-rigid mesh from falsely passing the garment test.

The source mesh is a single connected surface. The rebuild duplicates seam
vertices where a moving hand/foot shell meets locked clothing, so hands and feet
can separate slightly from wide sleeves or the skirt during the strongest
actions without pulling or shredding clothing. This matches the requested
"move limbs, not clothes" rule.

## Repeatable validation

Rebuild the reviewed FBX and weight audit:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python scripts/dragon-girl-assets.py -- \
  --source-fbx '/Users/jitian/Desktop/chibi+dragon+girl+3d+model/tripo_convert_f02caf4d-fd67-4fe7-88cb-a84bdad2479d.fbx' \
  --mask-texture artifacts/dragon-girl/basecolor-mask-512.png
```

Then run `About Me > Dragon Girl > Import, validate and review` in Tuanjie. The
command rejects an invalid Humanoid avatar, incomplete bone map, garment motion,
non-finite skinning result, or broken action pose, and renders front/side review
frames to `artifacts/dragon-girl/`.
