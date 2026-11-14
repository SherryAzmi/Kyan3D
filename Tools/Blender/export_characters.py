"""
Exports the two Two Worlds characters from Art/Source/Characters_v2.blend as rigged FBX files.

    blender -b Art/Source/Characters_v2.blend -P Tools/Blender/export_characters.py -- [--preview]

Writes Assets/TwoWorlds/Characters/Character_Present.fbx (depressed) and Character_Future.fbx (light).
Both share the same 19-bone humanoid skeleton (Hips, Spine, Chest, Neck, Head, arms, legs), so Unity's
Humanoid rig and Mixamo-style animations can be retargeted onto them.
"""
import bpy, os, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "TwoWorlds", "Characters")
PREV = os.path.join(ROOT, "Tools", "Blender", "previews_twoworlds")
PREVIEW = "--preview" in (sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])

PAIRS = [("Player_Depressed", "Depressed_Body", "Character_Present"),
         ("Player_Light", "Light_Body", "Character_Future")]

os.makedirs(OUT, exist_ok=True)
for arm_name, body_name, out_name in PAIRS:
    arm = bpy.data.objects[arm_name]; body = bpy.data.objects[body_name]
    old_x = arm.location.x
    arm.location.x = 0.0                       # put each character at the origin
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True); body.select_set(True)
    bpy.context.view_layer.objects.active = arm
    path = os.path.join(OUT, out_name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
        apply_scale_options='FBX_SCALE_ALL', bake_space_transform=False,
        axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE',
        add_leaf_bones=False, use_armature_deform_only=True, bake_anim=False,
        use_mesh_modifiers=False, path_mode='AUTO')
    print("EXPORTED", out_name)
    if PREVIEW:
        os.makedirs(PREV, exist_ok=True)
        sc = bpy.context.scene
        cam = bpy.data.objects.new("PCam", bpy.data.cameras.new("PCam"))
        bpy.context.collection.objects.link(cam)
        from mathutils import Vector
        cam.location = Vector((0.3, -4.2, 1.0)); cam.rotation_euler = (1.5708 - 0.02, 0, 0.07)
        cam.data.lens = 50
        sc.camera = cam
        sc.render.engine = 'BLENDER_WORKBENCH'
        sc.render.resolution_x, sc.render.resolution_y = 384, 512
        sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'MATERIAL'
        for o in bpy.data.objects:
            if o.type == 'MESH': o.hide_render = (o != body)
        sc.render.filepath = os.path.join(PREV, out_name + ".png")
        bpy.ops.render.render(write_still=True)
        bpy.data.objects.remove(cam)
    arm.location.x = old_x
print("DONE")
