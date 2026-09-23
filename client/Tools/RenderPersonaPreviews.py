"""Render one neutral preview image for every approved Persona CC0 FBX."""
import bpy
import json
import math
import os
import sys
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
root = argv[0]
output = argv[1]
with open(os.path.join(root, "shared", "persona-visuals.json"), encoding="utf-8") as f:
    visuals = json.load(f)["items"]

os.makedirs(output, exist_ok=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 512
scene.render.resolution_y = 512
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.world.color = (0.035, 0.045, 0.07)

def clear():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)

def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()

def light(name, location, energy, size, color):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    data.color = color
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    look_at(obj, (0, 0, 0))

for index, visual in enumerate(visuals, 1):
    clear()
    source = os.path.join(root, "client", visual["sourceAssetPath"])
    bpy.ops.import_scene.fbx(filepath=source)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    # The OpenGameArt basketball FBX includes a large default construction cube
    # beside the actual ball; it is not part of the prop.
    if len(meshes) > 1:
        for obj in [obj for obj in meshes if obj.name.lower().startswith("cube")]:
            bpy.data.objects.remove(obj, do_unlink=True)
        meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if visual["visualId"] == "basketball-cc0":
        # This legacy FBX stores an unsupported transparency setup.  Keep the
        # approved mesh, but use its registry's orange visual treatment here.
        material = bpy.data.materials.new("Basketball preview")
        material.diffuse_color = (1.0, 0.28, 0.06, 1.0)
        material.use_nodes = True
        shader = next(node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
        shader.inputs["Base Color"].default_value = (1.0, 0.28, 0.06, 1.0)
        shader.inputs["Roughness"].default_value = 0.55
        for obj in meshes:
            obj.data.materials.clear()
            obj.data.materials.append(material)
    if not meshes:
        raise RuntimeError("No mesh: " + source)
    coords = []
    for obj in meshes:
        for corner in obj.bound_box:
            coords.append(obj.matrix_world @ Vector(corner))
    low = Vector((min(v[i] for v in coords) for i in range(3)))
    high = Vector((max(v[i] for v in coords) for i in range(3)))
    center = (low + high) / 2
    extent = high - low
    scale = max(extent.x, extent.y, extent.z, 0.01)
    # Center on X/Z and put the asset on the preview floor, retaining its rotation/materials.
    offset = Vector((-center.x, -center.y, -low.z))
    for obj in meshes:
        obj.location += offset
    center += offset
    low += offset
    high += offset
    # Ground plane.
    bpy.ops.mesh.primitive_plane_add(size=scale * 12, location=(0, 0, -0.012 * scale))
    floor = bpy.context.object
    floor_mat = bpy.data.materials.new("Preview floor")
    floor_mat.diffuse_color = (0.075, 0.095, 0.14, 1)
    floor.data.materials.append(floor_mat)
    # Camera is mildly elevated for recognisable prop silhouettes.
    cam_data = bpy.data.cameras.new("Preview Camera")
    cam = bpy.data.objects.new("Preview Camera", cam_data)
    bpy.context.collection.objects.link(cam)
    cam.location = (scale * 1.7, -scale * 2.3, center.z + scale * 1.3)
    look_at(cam, center + Vector((0, 0, scale * 0.05)))
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = scale * 2.25
    scene.camera = cam
    # Light power scales with the model's dimensions, so a 27 cm basketball is
    # not overexposed while a couch remains readable.
    power = 300 * scale * scale
    light("Key", (scale * 2, -scale * 2, scale * 3), power, scale * 2.5, (1.0, 0.87, 0.7))
    light("Fill", (-scale * 2, -scale, scale * 1.5), power * 0.65, scale * 2.0, (0.55, 0.72, 1.0))
    light("Rim", (0, scale * 2.5, scale * 3), power * 0.8, scale * 1.5, (0.85, 0.5, 1.0))
    scene.render.filepath = os.path.join(output, f"{index:02d}-{visual['visualId']}.png")
    bpy.ops.render.render(write_still=True)
    print(f"RENDERED {index:02d} {visual['visualId']}")
