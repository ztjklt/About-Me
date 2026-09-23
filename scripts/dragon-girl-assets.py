"""Blender: rebuild the dragon girl's skin weights and prove garment isolation.

Run with Blender, for example:
  Blender --background --python scripts/dragon-girl-assets.py -- \
    --source-fbx /path/to/source.fbx \
    --mask-texture artifacts/dragon-girl/basecolor-mask-512.png

The mask texture is a downscaled copy of the source base-colour texture. It is used
only to distinguish exposed skin from the white/lilac skirt around the thighs.
No online API calls are made.
"""
import argparse
import bpy
import json
import math
import numpy as np
from collections import Counter, defaultdict
from mathutils import Vector
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT = ROOT / "client/Assets/AboutMe/Art/DragonGirl/DragonGirl_Clean.fbx"
DEFAULT_REPORT = ROOT / "artifacts/dragon-girl/weight-audit.json"
DEFAULT_STRESS_IMAGE = ROOT / "artifacts/dragon-girl/clean-limb-stress.png"


def arguments():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-fbx", type=Path, required=True)
    parser.add_argument("--mask-texture", type=Path, required=True)
    parser.add_argument("--output-fbx", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--report", type=Path, default=DEFAULT_REPORT)
    parser.add_argument("--stress-image", type=Path, default=DEFAULT_STRESS_IMAGE)
    values = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return parser.parse_args(values)


def normalize(weights):
    total = sum(weights.values())
    if total <= 1e-8:
        return weights
    return {name: value / total for name, value in weights.items() if value / total > 1e-5}


def main():
    args = arguments()
    for path in (args.source_fbx, args.mask_texture):
        if not path.is_file():
            raise FileNotFoundError(path)
    args.output_fbx.parent.mkdir(parents=True, exist_ok=True)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.stress_image.parent.mkdir(parents=True, exist_ok=True)

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(args.source_fbx), use_anim=False)
    meshes = [item for item in bpy.context.scene.objects if item.type == "MESH"]
    armatures = [item for item in bpy.context.scene.objects if item.type == "ARMATURE"]
    if len(meshes) != 1 or len(armatures) != 1:
        raise ValueError("Expected exactly one mesh and one armature")
    obj, arm = meshes[0], armatures[0]
    mesh = obj.data

    # Sample per-vertex base colour. The texture is review input, never rewritten.
    image = bpy.data.images.load(str(args.mask_texture), check_existing=False)
    width, height = image.size
    pixels = np.empty(width * height * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    pixels = pixels.reshape((height, width, 4))
    colors = np.zeros((len(mesh.vertices), 3), dtype=np.float32)
    counts = np.zeros(len(mesh.vertices), dtype=np.int32)
    uv = mesh.uv_layers.active.data
    for loop in mesh.loops:
        point = uv[loop.index].uv
        x = int(round((point.x % 1.0) * (width - 1)))
        y = int(round((point.y % 1.0) * (height - 1)))
        colors[loop.vertex_index] += pixels[y, x, :3]
        counts[loop.vertex_index] += 1
    colors /= np.maximum(counts[:, None], 1)

    group_by_index = {group.index: group.name for group in obj.vertex_groups}
    group_by_name = {group.name: group for group in obj.vertex_groups}
    required = {
        "Hips", "Spine", "Chest", "UpperChest", "Head",
        "Left_UpperArm", "Left_LowerArm", "Left_Hand",
        "Right_UpperArm", "Right_LowerArm", "Right_Hand",
        "Left_UpperLeg", "Left_LowerLeg", "Left_Foot", "Left_Toes",
        "Right_UpperLeg", "Right_LowerLeg", "Right_Foot", "Right_Toes",
    }
    missing = required.difference(group_by_name)
    if missing:
        raise ValueError("Missing required skin groups: " + ", ".join(sorted(missing)))

    def original_weights(vertex):
        return {group_by_index[item.group]: item.weight for item in vertex.groups}

    def bone_segment(name):
        bone = arm.data.bones[name]
        return arm.matrix_world @ bone.head_local, arm.matrix_world @ bone.tail_local

    def point_segment_distance(point, segment):
        start, end = segment
        difference = end - start
        fraction = max(0.0, min(1.0, (point - start).dot(difference) /
                                    max(difference.length_squared, 1e-9)))
        return (point - (start + difference * fraction)).length

    left_arm = ["Left_UpperArm", "Left_LowerArm", "Left_Hand"]
    right_arm = ["Right_UpperArm", "Right_LowerArm", "Right_Hand"]
    left_leg = ["Left_UpperLeg", "Left_LowerLeg", "Left_Foot", "Left_Toes"]
    right_leg = ["Right_UpperLeg", "Right_LowerLeg", "Right_Foot", "Right_Toes"]
    segments = {name: bone_segment(name) for name in left_arm + right_arm + left_leg + right_leg}

    def closest_weights(point, names, power=3.0):
        distances = [(name, max(point_segment_distance(point, segments[name]), .004)) for name in names]
        distances.sort(key=lambda value: value[1])
        return normalize({name: 1 / distance ** power for name, distance in distances[:2]})

    def filtered_or_closest(source, point, names, merge_prefix=None):
        result = {name: source.get(name, 0.0) for name in names}
        if merge_prefix:
            finger_weight = sum(value for name, value in source.items()
                                if name.startswith(merge_prefix) and any(
                                    part in name for part in ("Thumb", "Index", "Middle", "Ring", "Pinky")))
            result[names[-1]] += finger_weight
        if sum(result.values()) < .18:
            return closest_weights(point, names)
        return normalize(result)

    categories = defaultdict(int)
    assignments = []
    for vertex in mesh.vertices:
        point = obj.matrix_world @ vertex.co
        source = original_weights(vertex)
        red, green, blue = colors[vertex.index]
        skin = red - green > .055 and green - blue > .025 and red > .62
        head_source = source.get("Head", 0) + source.get("Neck", 0) + source.get("Neck_Twist_A", 0)

        arm_side = left_arm if point.x >= 0 else right_arm
        arm_prefix = "Left_" if point.x >= 0 else "Right_"
        arm_distance = min(point_segment_distance(point, segments[name]) for name in arm_side)
        hand_name = arm_prefix + "Hand"
        hand_source = source.get(hand_name, 0.0) + sum(
            value for name, value in source.items()
            if name.startswith(arm_prefix) and any(part in name for part in
                                                   ("Thumb", "Index", "Middle", "Ring", "Pinky")))
        # The generated rig painted most of each bell sleeve and a strip of upper
        # arm skin into the arm chain. Keep those vertices on the torso. Only the
        # compact hand shell moves, rigidly, with Hand; hierarchy motion still
        # carries it through upper-arm and forearm poses without stretching it.
        in_hand = (skin and .30 < point.z < .55 and abs(point.x) > .075 and point.y < .025
                   and arm_distance < .085 and hand_source > .10)

        leg_side = left_leg if point.x >= 0 else right_leg
        leg_distance = min(point_segment_distance(point, segments[name]) for name in leg_side)
        foot_distance = min(point_segment_distance(point, segments[name]) for name in leg_side[2:])
        leg_source = sum(source.get(name, 0) for name in leg_side)
        in_foot = point.z < .155 and abs(point.x) < .17 and foot_distance < .12 and leg_source > .10
        in_exposed_leg = (point.z < .355 and abs(point.x) < .13 and skin
                          and leg_distance < .085 and leg_source > .10)

        if point.z > .585 or (head_source > .28 and point.z > .40):
            weights, category = {"Head": 1.0}, "head_locked"
        elif in_hand:
            weights, category = {hand_name: 1.0}, "exposed_hand_allowed"
        elif in_foot or in_exposed_leg:
            weights = filtered_or_closest(source, point, leg_side)
            category = "leg_allowed"
        elif point.z >= .50:
            weights, category = {"UpperChest": 1.0}, "garment_upper_chest_locked"
        elif point.z >= .415:
            weights, category = {"Chest": 1.0}, "garment_chest_locked"
        elif point.z >= .34:
            weights, category = {"Spine": 1.0}, "garment_spine_locked"
        else:
            weights, category = {"Hips": 1.0}, "garment_hips_locked"
        assignments.append((weights, category))
        categories[category] += 1

    # A continuous triangle cannot have one corner follow a hand while its other
    # corners remain perfectly fixed to a sleeve: it will become a long spike.
    # Split only the limb/locked seams at face boundaries. Both copies start at
    # the same position, but the hand/leg shell can then move without stretching
    # any clothing triangle. Torso/head boundaries stay welded.
    def coarse(category):
        if category == "exposed_hand_allowed":
            return "hand"
        if category == "leg_allowed":
            return "leg"
        return "locked"

    def locked_assignment(point, source):
        head_source = source.get("Head", 0) + source.get("Neck", 0) + source.get("Neck_Twist_A", 0)
        if point.z > .585 or (head_source > .28 and point.z > .40):
            return {"Head": 1.0}, "head_locked"
        if point.z >= .50:
            return {"UpperChest": 1.0}, "garment_upper_chest_locked"
        if point.z >= .415:
            return {"Chest": 1.0}, "garment_chest_locked"
        if point.z >= .34:
            return {"Spine": 1.0}, "garment_spine_locked"
        return {"Hips": 1.0}, "garment_hips_locked"

    old_mesh = mesh
    old_group_names = list(group_by_name)
    old_uv = old_mesh.uv_layers.active
    old_materials = list(old_mesh.materials)
    old_sources = [original_weights(vertex) for vertex in old_mesh.vertices]
    old_points = [obj.matrix_world @ vertex.co for vertex in old_mesh.vertices]
    vertex_map, new_vertices, new_assignments, new_faces, new_uvs = {}, [], [], [], []
    for polygon in old_mesh.polygons:
        vote = Counter(coarse(assignments[index][1]) for index in polygon.vertices)
        region = max(("locked", "leg", "hand"), key=lambda name: (vote[name], name == "locked"))
        face = []
        for loop_index in polygon.loop_indices:
            old_index = old_mesh.loops[loop_index].vertex_index
            key = (old_index, region)
            if key not in vertex_map:
                vertex_map[key] = len(new_vertices)
                new_vertices.append(old_mesh.vertices[old_index].co.copy())
                source, point = old_sources[old_index], old_points[old_index]
                if region == "hand":
                    prefix = "Left_" if point.x >= 0 else "Right_"
                    assignment = ({prefix + "Hand": 1.0}, "exposed_hand_allowed")
                elif region == "leg":
                    names = left_leg if point.x >= 0 else right_leg
                    assignment = (filtered_or_closest(source, point, names), "leg_allowed")
                else:
                    assignment = locked_assignment(point, source)
                new_assignments.append(assignment)
            face.append(vertex_map[key])
            new_uvs.append(old_uv.data[loop_index].uv.copy())
        new_faces.append(face)

    rebuilt = bpy.data.meshes.new(old_mesh.name + "_seam_split")
    rebuilt.from_pydata(new_vertices, [], new_faces)
    rebuilt.update(calc_edges=True)
    for material in old_materials:
        rebuilt.materials.append(material)
    rebuilt_uv = rebuilt.uv_layers.new(name=old_uv.name)
    for loop, value in zip(rebuilt_uv.data, new_uvs):
        loop.uv = value
    obj.data = rebuilt
    bpy.data.meshes.remove(old_mesh)
    mesh, assignments = rebuilt, new_assignments
    existing_group_names = {group.name for group in obj.vertex_groups}
    for name in old_group_names:
        if name not in existing_group_names:
            obj.vertex_groups.new(name=name)
    group_by_name = {group.name: group for group in obj.vertex_groups}
    group_by_index = {group.index: group.name for group in obj.vertex_groups}
    categories = defaultdict(int)
    for _, category in assignments:
        categories[category] += 1

    all_vertices = list(range(len(mesh.vertices)))
    for group in obj.vertex_groups:
        group.remove(all_vertices)
    for index, (weights, _) in enumerate(assignments):
        for name, value in weights.items():
            group_by_name[name].add([index], float(value), "REPLACE")

    # Evaluate the real skinned surface before and after a deliberately extreme
    # limb pose. Locked clothing/head regions must not move by even one vertex.
    bpy.context.view_layer.update()
    rest_evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    rest_mesh = rest_evaluated.to_mesh()
    rest_positions = [rest_evaluated.matrix_world @ vertex.co for vertex in rest_mesh.vertices]
    rest_evaluated.to_mesh_clear()
    stress = {
        "Left_UpperArm": (0, 0, math.radians(95)), "Right_UpperArm": (0, 0, math.radians(-95)),
        "Left_LowerArm": (0, math.radians(-75), 0), "Right_LowerArm": (0, math.radians(75), 0),
        "Left_UpperLeg": (math.radians(48), 0, math.radians(-18)),
        "Right_UpperLeg": (math.radians(-42), 0, math.radians(18)),
        "Left_LowerLeg": (math.radians(-65), 0, 0), "Right_LowerLeg": (math.radians(55), 0, 0),
    }
    for name, rotation in stress.items():
        bone = arm.pose.bones[name]
        bone.rotation_mode = "XYZ"
        bone.rotation_euler = rotation
    bpy.context.view_layer.update()
    posed_evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    posed_mesh = posed_evaluated.to_mesh()
    posed_positions = [posed_evaluated.matrix_world @ vertex.co for vertex in posed_mesh.vertices]
    posed_evaluated.to_mesh_clear()
    locked = [index for index, (_, category) in enumerate(assignments) if category.endswith("_locked")]
    displacement = [(posed_positions[index] - rest_positions[index]).length for index in locked]
    maximum = max(displacement) if displacement else 0
    mean = sum(displacement) / len(displacement) if displacement else 0
    if maximum > 1e-6:
        raise ValueError(f"Locked garment displacement is {maximum}; expected <= 1e-6")
    moving = [index for index, (_, category) in enumerate(assignments) if not category.endswith("_locked")]
    moving_displacement = [(posed_positions[index] - rest_positions[index]).length for index in moving]
    moving_maximum = max(moving_displacement) if moving_displacement else 0
    moving_mean = sum(moving_displacement) / len(moving_displacement) if moving_displacement else 0
    if moving_maximum < .05 or moving_mean < .005:
        raise ValueError(f"Limb stress did not move the allowed hand/foot shell: max={moving_maximum}, mean={moving_mean}")

    # Save the same extreme pose used by the numeric audit. This is intentionally
    # a separate human-facing check: a zero garment displacement alone cannot
    # catch an unattractive hand/foot pose or a bad material import.
    minimum = Vector((min(value.x for value in posed_positions), min(value.y for value in posed_positions),
                      min(value.z for value in posed_positions)))
    maximum_bounds = Vector((max(value.x for value in posed_positions), max(value.y for value in posed_positions),
                             max(value.z for value in posed_positions)))
    center = (minimum + maximum_bounds) * .5
    span = max((maximum_bounds - minimum).x, (maximum_bounds - minimum).z)
    bpy.ops.object.camera_add(location=(center.x, minimum.y - span * 3.1, center.z + span * .02))
    camera = bpy.context.object
    camera.data.lens = 55
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = camera
    for location, energy, size in (((-1.5, -2.0, 2.2), 650, 4.0), ((1.6, -1.0, 1.2), 300, 3.0)):
        bpy.ops.object.light_add(type="AREA", location=location)
        lamp = bpy.context.object
        lamp.data.energy = energy
        lamp.data.shape = "DISK"
        lamp.data.size = size
        lamp.rotation_euler = (center - lamp.location).to_track_quat("-Z", "Y").to_euler()
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = scene.render.resolution_y = 800
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(args.stress_image)
    scene.render.film_transparent = False
    scene.world.color = (.025, .025, .025)
    scene.view_settings.look = "AgX - Medium High Contrast"
    bpy.ops.render.render(write_still=True)

    report = {
        "schemaVersion": 1,
        "source": str(args.source_fbx),
        "output": str(args.output_fbx.relative_to(ROOT) if args.output_fbx.is_relative_to(ROOT) else args.output_fbx),
        "vertices": len(mesh.vertices),
        "polygons": len(mesh.polygons),
        "categories": dict(categories),
        "lockedVertices": len(locked),
        "lockedMaxDisplacementUnderLimbStress": maximum,
        "lockedMeanDisplacementUnderLimbStress": mean,
        "movingMaxDisplacementUnderLimbStress": moving_maximum,
        "movingMeanDisplacementUnderLimbStress": moving_mean,
        "policy": {
            "garmentCore": ["Hips", "Spine", "Chest", "UpperChest"],
            "hairHorns": ["Head"],
            "sleeves": ["UpperChest", "Chest", "Spine", "locked; never driven by arm or hand bones"],
            "exposedHands": ["Hand", "rigid; arm hierarchy supplies motion"],
            "skirt": ["Hips"],
            "fingerWeights": "merged into Hand; individual finger animation disabled",
        },
    }
    args.report.write_text(json.dumps(report, indent=2), encoding="utf-8")

    for bone in arm.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.rotation_quaternion = (1, 0, 0, 0)
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=str(args.output_fbx), use_selection=True,
                             add_leaf_bones=False, bake_anim=False, path_mode="AUTO")
    print("DRAGON_GIRL_REWEIGHT_PASSED", json.dumps(report, separators=(",", ":")))


if __name__ == "__main__":
    main()
