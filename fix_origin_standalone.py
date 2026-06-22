"""
Blender 修复 v9：直接在 Edit Mode 移动骨骼 + 网格顶点
对象 Transform 对 Armature Modifier 无效，必须编辑数据本身
"""
import bpy
import shutil

print("=== 修复 v9 (Edit Mode 骨骼 + 网格) ===")

arm_obj = mesh_obj = None
for obj in bpy.data.objects:
    if obj.type == 'ARMATURE':
        arm_obj = obj
    elif obj.type == 'MESH' and len(obj.data.vertices) > 100:
        mesh_obj = obj

# 1. 重置对象位置
bpy.ops.object.mode_set(mode='OBJECT')
mesh_obj.location = (0, 0, 0)
arm_obj.location = (0, 0, 0)

# 2. 清除 Pose + 动画
bpy.ops.object.select_all(action='DESELECT')
arm_obj.select_set(True)
bpy.context.view_layer.objects.active = arm_obj
bpy.ops.object.mode_set(mode='POSE')
bpy.ops.pose.select_all(action='SELECT')
bpy.ops.pose.loc_clear()
bpy.ops.pose.rot_clear()
bpy.ops.pose.scale_clear()
bpy.ops.object.mode_set(mode='OBJECT')
if arm_obj.animation_data:
    arm_obj.animation_data_clear()

# 3. 测量脚底位置
bpy.context.view_layer.update()
depsgraph = bpy.context.evaluated_depsgraph_get()
eval_obj = mesh_obj.evaluated_get(depsgraph)
me = eval_obj.to_mesh()
min_z = min(v.co.z for v in me.vertices)
eval_obj.to_mesh_clear()
print(f"脚底 Z = {min_z:.4f}")

offset = -min_z  # 需要上移的量
print(f"上移量 = {offset:.4f}")

# 4. 在 Edit Mode 移动骨骼（修改 Rest Pose）
bpy.ops.object.select_all(action='DESELECT')
arm_obj.select_set(True)
bpy.context.view_layer.objects.active = arm_obj
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.armature.select_all(action='SELECT')
bpy.ops.transform.translate(value=(0, 0, offset), orient_type='GLOBAL')
bpy.ops.object.mode_set(mode='OBJECT')
print(f"骨骼 Edit Mode 上移 {offset:.4f}")

# 5. 在 Edit Mode 移动网格顶点
bpy.ops.object.select_all(action='DESELECT')
mesh_obj.select_set(True)
bpy.context.view_layer.objects.active = mesh_obj
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.transform.translate(value=(0, 0, offset), orient_type='GLOBAL')
bpy.ops.object.mode_set(mode='OBJECT')
print(f"网格 Edit Mode 上移 {offset:.4f}")

# 6. 验证
bpy.context.view_layer.update()
depsgraph2 = bpy.context.evaluated_depsgraph_get()
eval_obj2 = mesh_obj.evaluated_get(depsgraph2)
me2 = eval_obj2.to_mesh()
final_min_z = min(v.co.z for v in me2.vertices)
final_max_z = max(v.co.z for v in me2.vertices)
eval_obj2.to_mesh_clear()
print(f"最终: 脚底 Z = {final_min_z:.4f} 头顶 Z = {final_max_z:.4f} height = {final_max_z - final_min_z:.4f}")

if abs(final_min_z) < 0.02:
    print("✅ 脚底对齐 Z=0!")
else:
    print(f"⚠️ 残余: {final_min_z:.4f}")

# 7. 保存 + 导出
fbx_root = r"C:\Users\Administrator\Desktop\Unity学习一\mygame\Player_Rigged.fbx"
fbx_assets = r"C:\Users\Administrator\Desktop\Unity学习一\mygame\mygame1\Assets\_Game\Config\Models\Characters\Player\Player_Rigged.fbx"

bpy.ops.wm.save_mainfile()
print("✅ .blend saved")

bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='DESELECT')
mesh_obj.select_set(True)
arm_obj.select_set(True)
bpy.context.view_layer.objects.active = arm_obj

bpy.ops.export_scene.fbx(
    filepath=fbx_root, use_selection=True,
    object_types={'ARMATURE', 'MESH'},
    use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
)
print(f"✅ FBX -> {fbx_root}")
shutil.copy(fbx_root, fbx_assets)
print(f"✅ FBX -> {fbx_assets}")
print("=== DONE ===")
