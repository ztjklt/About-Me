"""Render the four start-menu labels as transparent toy-clay 3D lettering.

The title asset establishes the visual language: soft cream/yellow letter faces,
rounded edges and a thick navy silhouette. Keep the entire Unity Image rectangle
clickable even though the pixels around and between these letters are transparent.
Run: Blender -b -t 4 --python scripts/render-start-menu-letters.py
"""
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "client/Assets/AboutMe/Resources/StartMenu2D"
FONT = "/System/Library/Fonts/Supplemental/Arial Rounded Bold.ttf"
COLORS = {"ink": "202E51", "cream": "FFF5E5", "yellow": "FFD348"}


def material(name):
    value = COLORS[name]
    color = tuple(int(value[index:index + 2], 16) / 255 for index in (0, 2, 4)) + (1,)
    result = bpy.data.materials.new(name)
    result.diffuse_color = color
    result.use_nodes = True
    nodes = result.node_tree.nodes
    nodes.clear()
    surface = nodes.new("ShaderNodeBsdfPrincipled")
    output = nodes.new("ShaderNodeOutputMaterial")
    result.node_tree.links.new(surface.outputs["BSDF"], output.inputs["Surface"])
    surface.inputs["Base Color"].default_value = color
    surface.inputs["Roughness"].default_value = .63
    return result


def word(text, z, font, face, ink):
    for suffix, y, dz, extrude, bevel, color in (
        ("navy depth", .08, -.055, .115, .100, ink),
        ("clay face", -.14, 0, .030, .037, face),
    ):
        shape = bpy.data.curves.new(text + " " + suffix, "FONT")
        shape.body = text
        shape.font = font
        shape.align_x = "CENTER"
        shape.align_y = "CENTER"
        shape.size = .72
        shape.extrude = extrude
        shape.bevel_depth = bevel
        shape.bevel_resolution = 4
        obj = bpy.data.objects.new(text + " " + suffix, shape)
        bpy.context.collection.objects.link(obj)
        obj.location = (0, y, z + dz)
        obj.rotation_euler.x = 1.5707963268
        obj.data.materials.append(color)
        bpy.context.view_layer.update()
        if obj.dimensions.x > 2.85:
            obj.scale *= 2.85 / obj.dimensions.x


def setup_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 48
    scene.cycles.use_denoising = True
    scene.render.film_transparent = True
    scene.render.resolution_x = 640
    scene.render.resolution_y = 360
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "Medium High Contrast"
    scene.world.color = (.65, .65, .65)
    bpy.ops.object.camera_add(location=(.26, -8, 1.15))
    camera = bpy.context.object
    camera.rotation_euler = (Vector((0, 0, 0)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 3.15
    scene.camera = camera
    for location, power, size in (((-3, -4, 5), 330, 4), ((3, -2, 2), 170, 3)):
        bpy.ops.object.light_add(type="AREA", location=location)
        light = bpy.context.object
        light.data.energy = power
        light.data.shape = "DISK"
        light.data.size = size
        light.rotation_euler = (Vector((0, 0, 0)) - light.location).to_track_quat("-Z", "Y").to_euler()
    return scene


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    font = bpy.data.fonts.load(FONT)
    for file_name, lines in (
        ("button-solo.png", ("SINGLE", "PLAYER")),
        ("button-multi.png", ("MULTI", "PLAYER")),
        ("button-intro.png", ("INTRO", "PRESETS")),
        ("button-settings.png", ("SETTINGS",)),
    ):
        scene = setup_scene()
        ink, cream, yellow = (material(name) for name in ("ink", "cream", "yellow"))
        if len(lines) == 2:
            word(lines[0], .35, font, cream, ink)
            word(lines[1], -.35, font, yellow, ink)
        else:
            word(lines[0], 0, font, yellow, ink)
        scene.render.filepath = str(OUT / file_name)
        bpy.ops.render.render(write_still=True)
        print("MENU_TITLE_LABEL_RENDERED", file_name)


if __name__ == "__main__":
    main()
