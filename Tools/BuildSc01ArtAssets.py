import bpy
import math
import os
import sys


# Paths come from the environment, falling back to locations relative to this
# script, so the repo carries no machine-specific directory or username.
#   SHOTFIELD_PROJECT  = Unity project root (default: the parent of Tools/)
#   SHOTFIELD_SOURCE   = folder holding the source glTF files
#   SHOTFIELD_WORKSPACE = repo root that contains 04_Project and 06_* (default: project/..)
_HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.environ.get("SHOTFIELD_PROJECT") or os.path.normpath(os.path.join(_HERE, ".."))
_WORKSPACE = os.environ.get("SHOTFIELD_WORKSPACE") or os.path.normpath(os.path.join(PROJECT, ".."))
SOURCE = os.environ.get("SHOTFIELD_SOURCE") or os.path.join(
    _WORKSPACE, u"06_\u7d20\u6750", u"\u53c2\u8003")
FURNITURE = {
    "modern_arm_chair_01": "modern_arm_chair_01_1k.gltf",
    "modern_coffee_table_01": "modern_coffee_table_01_1k.gltf",
    "Sofa_01": "Sofa_01_1k.gltf",
    "modern_wooden_cabinet": "modern_wooden_cabinet_1k.gltf",
    "potted_plant_02": "potted_plant_02_1k.gltf",
}


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def export_fbx(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        path_mode="COPY",
        embed_textures=True,
        add_leaf_bones=False,
    )


def convert_furniture():
    for name, gltf_name in FURNITURE.items():
        clear_scene()
        source = os.path.join(SOURCE, name, gltf_name)
        output = os.path.join(PROJECT, "Assets", "Models", "Furniture", name, name + ".fbx")
        bpy.ops.import_scene.gltf(filepath=source)
        export_fbx(output)
        print(f"[SC01_ART] exported {name}: {output}")


def material(name, color, metallic=0.0, roughness=0.5):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic IOR Level"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    return mat


def cube(name, location, scale, mat, bevel=0.001):
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    modifier = obj.modifiers.new("SoftEdges", "BEVEL")
    modifier.width = bevel
    modifier.segments = 2
    obj.data.materials.append(mat)
    return obj


def build_envelope():
    clear_scene()
    paper = material("EnvelopePaper", (0.94, 0.91, 0.84), roughness=0.75)
    red = material("SealRed", (0.55, 0.025, 0.025), roughness=0.38)
    body = cube("Envelope_Body", (0, 0, 0), (0.16, 0.004, 0.11), paper)

    flap_mesh = bpy.data.meshes.new("Envelope_FlapMesh")
    flap_mesh.from_pydata(
        [(-0.078, -0.0023, 0.053), (0.078, -0.0023, 0.053), (0, -0.0023, -0.012)],
        [],
        [(0, 1, 2)],
    )
    flap = bpy.data.objects.new("Envelope_Flap", flap_mesh)
    bpy.context.collection.objects.link(flap)
    flap.data.materials.append(paper)

    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=0.014, depth=0.002, location=(0, -0.0038, 0.002))
    seal = bpy.context.object
    seal.name = "Red_Wax_Seal"
    seal.rotation_euler = (math.radians(90), 0, 0)
    seal.data.materials.append(red)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    for obj in (body, flap, seal):
        obj.select_set(True)
    output = os.path.join(PROJECT, "Assets", "Models", "Props", "Envelope.fbx")
    export_fbx(output)
    print(f"[SC01_ART] exported envelope: {output}")


if __name__ == "__main__":
    convert_furniture()
    build_envelope()
    print("SC01-ART-BLENDER-RESULT=PASS")
