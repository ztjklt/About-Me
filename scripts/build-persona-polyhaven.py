"""Prepare four CC0 Poly Haven props for the Unity Persona prefab builder.

Run with Blender: blender -b --python scripts/build-persona-polyhaven.py
The original downloaded FBX and textures remain alongside each prepared model.
"""
from pathlib import Path
from math import pi

import bpy
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parents[1] / "client/Assets/AboutMe/Art/ThirdParty/PersonaCC0/PolyHaven"
ASSETS = {
    "football": ("football_inflated", "football_diff_1k.jpg", 0),
    "gamepad": ("gamepad", "gamepad_diff_1k.jpg", pi / 2),
    "Ukulele_01": ("Ukulele_01", "Ukulele_01_diff_1k.jpg", 0),
    "binder_notebook": ("binder_notebook", "binder_notebook_diff_1k.jpg", pi / 2),
}


for asset, (mesh_name, texture_name, upright_angle) in ASSETS.items():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    folder = ROOT / asset
    bpy.ops.import_scene.fbx(filepath=str(folder / f"{asset}_1k.fbx"))
    chosen = next((obj for obj in bpy.data.objects if obj.type == "MESH" and obj.name == mesh_name), None)
    if chosen is None:
        raise RuntimeError(f"Missing expected mesh: {asset}/{mesh_name}")
    if asset == "gamepad":
        # The source includes a long loose cable that overwhelms the handheld silhouette.
        bpy.ops.object.select_all(action="DESELECT")
        chosen.select_set(True)
        bpy.context.view_layer.objects.active = chosen
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.separate(type="LOOSE")
        bpy.ops.object.mode_set(mode="OBJECT")
        parts = [obj for obj in bpy.context.selected_objects if obj.type == "MESH"]
        cable = max(parts, key=lambda obj: len(obj.data.polygons))
        if len(cable.data.polygons) < 2000:
            raise RuntimeError("Gamepad cable geometry changed; inspect source before preparing")
        discarded = {cable}
        discarded.update(part for part in parts if min((part.matrix_world @ Vector(corner)).y for corner in part.bound_box) > 0)
        survivors = [part for part in parts if part not in discarded]
        for part in discarded:
            bpy.data.objects.remove(part, do_unlink=True)
        bpy.ops.object.select_all(action="DESELECT")
        for part in survivors:
            part.select_set(True)
        chosen = survivors[0]
        bpy.context.view_layer.objects.active = chosen
        bpy.ops.object.join()
    for obj in list(bpy.data.objects):
        if obj is not chosen:
            bpy.data.objects.remove(obj, do_unlink=True)

    rotation = Matrix.Rotation(upright_angle, 4, "X")
    transform = rotation @ chosen.matrix_world
    bounds = [transform @ Vector(corner) for corner in chosen.bound_box]
    low = Vector(tuple(min(p[axis] for p in bounds) for axis in range(3)))
    high = Vector(tuple(max(p[axis] for p in bounds) for axis in range(3)))
    extent = high - low
    factor = 0.8 / max(extent)
    anchor = Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z))
    for vertex in chosen.data.vertices:
        vertex.co = (transform @ vertex.co - anchor) * factor
    chosen.matrix_world = Matrix.Identity(4)

    material = bpy.data.materials.new(f"{asset}_toy")
    material.use_nodes = True
    material.node_tree.nodes.clear()
    image = bpy.data.images.load(str(folder / "textures" / texture_name), check_existing=True)
    image.filepath = str(folder / "textures" / texture_name)
    node = material.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = image
    shader = material.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
    output = material.node_tree.nodes.new("ShaderNodeOutputMaterial")
    material.node_tree.links.new(node.outputs["Color"], shader.inputs["Base Color"])
    material.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    shader.inputs["Roughness"].default_value = 0.65
    chosen.data.materials.clear()
    chosen.data.materials.append(material)

    bpy.ops.object.select_all(action="DESELECT")
    chosen.select_set(True)
    bpy.context.view_layer.objects.active = chosen
    destination = folder / f"{asset}_game.fbx"
    bpy.ops.export_scene.fbx(
        filepath=str(destination), use_selection=True, object_types={"MESH"},
        apply_unit_scale=True, path_mode="COPY", embed_textures=True,
        axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
    )
    print(f"PERSONA_PREPARED {asset} {destination} {tuple(round(v * factor, 3) for v in extent)}")
