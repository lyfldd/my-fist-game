"""
Blender 脚本 v2：用 Envelope Weights 绑定（适合高面数 Meshy AI 模型）
"""
import bpy
import os

MESH_PATH = r"D:\游戏模型\player\Meshy_AI_参考这个，然后_0601112858_texture.fbx"
ANIM_PATH = r"D:\游戏模型\player\Walking.fbx"
OUT_DIR = r"C:\Users\Administrator\Desktop\Unity学习一\mygame"

print("=" * 60)

# ═══ 第1步：导入 Mesh ═══
print("第1步：导入 Mesh FBX ...")
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=MESH_PATH)

mesh_obj = None
for obj in bpy.data.objects:
    if obj.type == 'MESH' and len(obj.data.vertices) > 100:
        mesh_obj = obj
        break
print(f"✅ Mesh: {mesh_obj.name}, 顶点={len(mesh_obj.data.vertices)}")
mesh_obj.name = "Player_Mesh"

# ═══ 第2步：导入骨骼 ═══
print("\n第2步：导入骨骼 FBX ...")
bpy.ops.import_scene.fbx(filepath=ANIM_PATH)

arm_obj = None
for obj in bpy.data.objects:
    if obj.type == 'ARMATURE':
        arm_obj = obj
        break
print(f"✅ 骨骼: {arm_obj.name}, {len(arm_obj.data.bones)} 根")
arm_obj.name = "Player_Armature"

# ═══ 第3步：尝试 Envelope 权重绑定 ═══
print("\n第3步：尝试 Envelope Weights 绑定 ...")
bpy.ops.object.select_all(action='DESELECT')
mesh_obj.select_set(True)
arm_obj.select_set(True)
bpy.context.view_layer.objects.active = arm_obj

bpy.ops.object.parent_set(type='ARMATURE_ENVELOPE')

# 检查结果
weighted = 0
for v in mesh_obj.data.vertices:
    if len(v.groups) > 0:
        weighted += 1
print(f"Envelope 权重结果: {weighted}/{len(mesh_obj.data.vertices)} 顶点")

# ═══ 第4步：如果 Envelope 也失败，用距离暴力分配 ═══
if weighted < 100:
    print("\n⚠️ Envelope 也失败了，用距离法暴力分配权重...")
    
    # 先改为空组
    mesh_obj.parent = None
    mesh_obj.vertex_groups.clear()
    mesh_obj.modifiers.clear()
    
    # 创建 modifier 绑定
    mod = mesh_obj.modifiers.new(name="Armature", type='ARMATURE')
    mod.object = arm_obj
    
    # 为每根骨骼创建顶点组
    for bone in arm_obj.data.bones:
        mesh_obj.vertex_groups.new(name=bone.name)
    
    # 距离法分配权重：每个顶点找最近的骨骼
    import math
    depsgraph = bpy.context.evaluated_depsgraph_get()
    
    # 获取骨骼世界坐标（在 Rest Pose 下）
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode='POSE')
    # 确保在 Rest Pose
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    
    bone_positions = {}
    for bone in arm_obj.data.bones:
        # bone head 在 armature 空间
        bone_positions[bone.name] = bone.head_local.copy()
    
    mesh_data = mesh_obj.data
    vertices = mesh_data.vertices
    
    # 对每个顶点，找最近的 3 根骨骼，分配权重
    for i, v in enumerate(vertices):
        v_world = mesh_obj.matrix_world @ v.co
        
        # 计算到每根骨骼的距离
        distances = []
        for bname, bpos in bone_positions.items():
            dist = (v_world - (arm_obj.matrix_world @ bpos)).length
            distances.append((bname, dist))
        
        distances.sort(key=lambda x: x[1])
        
        # 取最近的 3 根骨骼，按距离反比分配权重
        nearest = distances[:3]
        total_inv = sum(1.0 / max(d[1], 0.001) for d in nearest)
        
        for bname, dist in nearest:
            weight = (1.0 / max(dist, 0.001)) / total_inv
            vg = mesh_obj.vertex_groups[bname]
            vg.add([i], weight, 'REPLACE')
        
        if i % 50000 == 0:
            print(f"  进度: {i}/{len(vertices)}")
    
    # 重新检查
    weighted = 0
    for v in mesh_obj.data.vertices:
        if len(v.groups) > 0:
            weighted += 1
    print(f"距离法结果: {weighted}/{len(mesh_obj.data.vertices)} 顶点")

# ═══ 第5步：保存 ═══
print("\n第5步：保存 ...")
blend_path = os.path.join(OUT_DIR, "Player_Rigged.blend")
fbx_path = os.path.join(OUT_DIR, "Player_Rigged.fbx")

bpy.ops.wm.save_as_mainfile(filepath=blend_path)
print(f"✅ .blend: {blend_path}")

# 选中 mesh + armature 导出
bpy.ops.object.select_all(action='DESELECT')
mesh_obj.select_set(True)
arm_obj.select_set(True)
bpy.context.view_layer.objects.active = arm_obj

bpy.ops.export_scene.fbx(
    filepath=fbx_path,
    use_selection=True,
    object_types={'ARMATURE', 'MESH'},
    use_mesh_modifiers=True,
    add_leaf_bones=False,
    bake_anim=False
)
print(f"✅ .fbx: {fbx_path}")
print("\n完成！")
