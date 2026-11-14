"""
アイテム（虫眼鏡・電気プラグ・リモコン）の 3D モデルを作り、Unity 用の FBX と確認用の PNG を書き出す。

使い方（リポジトリのルートで）:
    "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" --background --factory-startup --python Tools/ItemModels/build_item_models.py

・どのモデルも机に寝かせた形で、底の中心（長さ方向の真ん中）を原点に置く。長い方向を Blender の +Y（Unity の +Z）に向ける。
・大きさは実物に近いメートル単位。ゲーム内の大きさは Unity 側で合わせる。
・出力: Assets/Temp/Model/Item/Item_<名前>.fbx、Tools/ItemModels/out/<名前>.png
"""

import math
import os
import sys

import bpy
import bmesh

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FBX_DIR = os.path.join(ROOT, "Assets", "Temp", "Model", "Item")
PNG_DIR = os.path.join(ROOT, "Tools", "ItemModels", "out")


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for collection in (bpy.data.meshes, bpy.data.materials, bpy.data.curves, bpy.data.objects):
        for block in list(collection):
            collection.remove(block)


def material(name, color, metallic=0.0, roughness=0.5, alpha=1.0):
    mat = bpy.data.materials.get(name)
    if mat is not None:
        return mat
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Alpha"].default_value = alpha
    mat.diffuse_color = (*color, alpha)
    return mat


def assign(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)


def smooth(obj, angle_deg=40.0):
    for poly in obj.data.polygons:
        poly.use_smooth = True
    try:
        obj.data.set_sharp_from_angle(angle=math.radians(angle_deg))
    except AttributeError:
        pass


def bevel(obj, width, segments=3):
    mod = obj.modifiers.new("Bevel", "BEVEL")
    mod.width = width
    mod.segments = segments
    mod.limit_method = "ANGLE"
    apply_modifiers(obj)


def apply_modifiers(obj):
    bpy.context.view_layer.objects.active = obj
    for mod in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=mod.name)


def box(name, size, location, mat, bevel_width=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel_width > 0.0:
        bevel(obj, bevel_width)
    assign(obj, mat)
    smooth(obj)
    return obj


def cylinder(name, radius, depth, location, mat, rotation=(0.0, 0.0, 0.0), vertices=32):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.active_object
    obj.name = name
    assign(obj, mat)
    smooth(obj)
    return obj


def join(name, objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.name = name
    obj.data.name = name
    return obj


def magnifier():
    gold = material("Item_Gold", (0.83, 0.62, 0.22), metallic=1.0, roughness=0.25)
    glass = material("Item_Glass", (0.70, 0.86, 0.95), metallic=0.0, roughness=0.05, alpha=0.35)
    wood = material("Item_DarkWood", (0.28, 0.14, 0.06), roughness=0.45)

    ring_radius, tube = 0.045, 0.006
    bpy.ops.mesh.primitive_torus_add(major_radius=ring_radius, minor_radius=tube, major_segments=48, minor_segments=12,
                                     location=(0.0, 0.0, tube))
    ring = bpy.context.active_object
    assign(ring, gold)
    smooth(ring, 80.0)

    lens = cylinder("Lens", ring_radius - tube * 0.5, tube * 0.8, (0.0, 0.0, tube), glass, vertices=48)
    neck = cylinder("Neck", 0.008, 0.025, (0.0, ring_radius + 0.012, tube), gold, rotation=(math.radians(90), 0.0, 0.0), vertices=20)
    handle = cylinder("Handle", 0.010, 0.10, (0.0, ring_radius + 0.075, 0.010), wood, rotation=(math.radians(90), 0.0, 0.0), vertices=24)
    bpy.context.view_layer.objects.active = handle
    bevel(handle, 0.004)
    cap = cylinder("Cap", 0.011, 0.006, (0.0, ring_radius + 0.128, 0.010), gold, rotation=(math.radians(90), 0.0, 0.0), vertices=24)
    return center_length(join("Item_Magnifier", [ring, lens, neck, handle, cap]))


def power_plug():
    ivory = material("Item_Ivory", (0.92, 0.89, 0.80), roughness=0.4)
    metal = material("Item_Brass", (0.85, 0.72, 0.40), metallic=1.0, roughness=0.3)
    cord_mat = material("Item_Cord", (0.05, 0.05, 0.05), roughness=0.6)

    body = box("Body", (0.040, 0.050, 0.026), (0.0, 0.0, 0.013), ivory, bevel_width=0.006)
    grip = box("Grip", (0.030, 0.020, 0.022), (0.0, -0.033, 0.011), ivory, bevel_width=0.005)
    prongs = [box(f"Prong{i}", (0.004, 0.024, 0.012), (x, 0.036, 0.013), metal, bevel_width=0.001)
              for i, x in enumerate((-0.010, 0.010))]

    curve = bpy.data.curves.new("CordCurve", "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = 0.0045
    curve.bevel_resolution = 4
    curve.use_fill_caps = True
    spline = curve.splines.new("BEZIER")
    points = [(0.0, -0.043, 0.0045), (0.0, -0.08, 0.0045), (0.035, -0.11, 0.0045), (0.02, -0.15, 0.0045)]
    spline.bezier_points.add(len(points) - 1)
    for point, co in zip(spline.bezier_points, points):
        point.co = co
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    cord_obj = bpy.data.objects.new("Cord", curve)
    bpy.context.collection.objects.link(cord_obj)
    bpy.context.view_layer.objects.active = cord_obj
    cord_obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    cord = bpy.context.active_object
    assign(cord, cord_mat)
    smooth(cord, 80.0)

    model = join("Item_PowerPlug", [body, grip, *prongs, cord])
    return center_length(model)


def remote_control():
    shell = material("Item_RemoteBody", (0.08, 0.08, 0.09), roughness=0.35)
    red = material("Item_ButtonRed", (0.85, 0.10, 0.08), roughness=0.4)
    gray = material("Item_ButtonGray", (0.55, 0.56, 0.60), roughness=0.5)
    blue = material("Item_ButtonBlue", (0.15, 0.40, 0.85), roughness=0.4)

    height = 0.018
    body = box("Body", (0.050, 0.170, height), (0.0, 0.0, height * 0.5), shell, bevel_width=0.008)
    top = height
    parts = [body]
    parts.append(cylinder("Power", 0.0065, 0.004, (0.0, 0.066, top + 0.001), red, vertices=24))

    dpad = cylinder("DPad", 0.016, 0.003, (0.0, 0.030, top + 0.0005), gray, vertices=32)
    parts.append(dpad)
    parts.append(cylinder("Ok", 0.0065, 0.004, (0.0, 0.030, top + 0.0015), blue, vertices=24))

    for row in range(3):
        for column in range(3):
            x = (column - 1) * 0.013
            y = -0.006 - row * 0.014
            parts.append(box(f"Key{row}{column}", (0.009, 0.008, 0.004), (x, y, top + 0.001), gray, bevel_width=0.0015))

    for column, mat in enumerate((red, blue)):
        parts.append(box(f"Color{column}", (0.016, 0.007, 0.004), ((column - 0.5) * 0.020, -0.058, top + 0.001), mat, bevel_width=0.0015))
    return center_length(join("Item_RemoteControl", parts))


def center_length(obj):
    """長さ方向（Y）の真ん中を原点に合わせる（底は z=0 のまま）"""
    ys = [v.co.y for v in obj.data.vertices]
    middle = (min(ys) + max(ys)) * 0.5
    for v in obj.data.vertices:
        v.co.y -= middle
    return obj


def export_fbx(obj, name):
    os.makedirs(FBX_DIR, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    path = os.path.join(FBX_DIR, f"{name}.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             object_types={"MESH"}, use_mesh_modifiers=True, mesh_smooth_type="FACE",
                             add_leaf_bones=False, path_mode="AUTO")
    return path


def render_preview(obj, name):
    os.makedirs(PNG_DIR, exist_ok=True)
    scene = bpy.context.scene
    for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT", "CYCLES"):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            continue
    scene.render.resolution_x = 640
    scene.render.resolution_y = 480
    scene.render.film_transparent = False

    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    background = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    background.inputs["Color"].default_value = (0.20, 0.17, 0.14, 1.0)
    background.inputs["Strength"].default_value = 0.3
    scene.world = world

    bpy.ops.mesh.primitive_plane_add(size=2.0, location=(0.0, 0.0, 0.0))
    floor = bpy.context.active_object
    assign(floor, material("Preview_Table", (0.18, 0.30, 0.18), roughness=0.8))

    dims = obj.dimensions
    span = max(dims.x, dims.y, dims.z)
    bpy.ops.object.camera_add(location=(span * 0.9, -span * 1.3, span * 1.1))
    camera = bpy.context.active_object
    direction = obj.location - camera.location
    direction.z += dims.z * 0.5
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    camera.data.lens = 50
    scene.camera = camera

    bpy.ops.object.light_add(type="AREA", location=(span * 0.5, -span * 0.5, span * 2.0))
    light = bpy.context.active_object
    light.data.energy = 2.5
    light.data.size = span * 2.0

    scene.render.filepath = os.path.join(PNG_DIR, f"{name}.png")
    bpy.ops.render.render(write_still=True)


def main():
    builders = (("Item_Magnifier", magnifier), ("Item_PowerPlug", power_plug), ("Item_RemoteControl", remote_control))
    for name, build in builders:
        reset_scene()
        obj = build()
        dims = obj.dimensions
        path = export_fbx(obj, name)
        render_preview(obj, name)
        print(f"[ItemModels] {name}: size=({dims.x:.3f}, {dims.y:.3f}, {dims.z:.3f}) tris={sum(len(p.vertices) - 2 for p in obj.data.polygons)} fbx={path}")


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"[ItemModels] ERROR: {error}")
        import traceback
        traceback.print_exc()
        sys.exit(1)
