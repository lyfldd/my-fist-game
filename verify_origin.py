"""验证模型原点是否已对齐"""
import bpy
import sys

# 找 Armature 和 Mesh
arm_obj = mesh_obj = None
for obj in bpy.data.objects:
    if obj.type == 'ARMATURE':
        arm_obj = obj
    elif obj.type == 'MESH' and len(obj.data.vertices) > 100:
        mesh_obj = obj

print(f"Armature: {arm_obj.name}, location={arm_obj.location}")
print(f"Mesh: {mesh_obj.name}, location={mesh_obj.location}")

# 计算 mesh 世界空间最低点
bpy.context.view_layer.update()
depsgraph = bpy.context.evaluated_depsgraph_get()
eval_obj = mesh_obj.evaluated_get(depsgraph)
me = eval_obj.to_mesh()
min_z = min(v.co.z for v in me.vertices)
max_z = max(v.co.z for v in me.vertices)
eval_obj.to_mesh_clear()
print(f"Mesh Z range: min={min_z:.4f} max={max_z:.4f}")
print(f"Model height: {max_z - min_z:.4f}")
if abs(min_z) < 0.01:
    print("OK: Feet at ground level")
else:
    print(f"ISSUE: Feet offset by {min_z:.4f}")
