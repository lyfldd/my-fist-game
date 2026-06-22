"""深入诊断模型坐标"""
import bpy

arm_obj = mesh_obj = None
for obj in bpy.data.objects:
    if obj.type == 'ARMATURE':
        arm_obj = obj
    elif obj.type == 'MESH' and len(obj.data.vertices) > 100:
        mesh_obj = obj

print("=== MESH DATA (原始顶点，无 modifier) ===")
me_data = mesh_obj.data
zs_data = [v.co.z for v in me_data.vertices]
print(f"Mesh data Z: min={min(zs_data):.4f} max={max(zs_data):.4f}")

print("\n=== MESH + Armature Modifier (世界空间) ===")
bpy.context.view_layer.update()
depsgraph = bpy.context.evaluated_depsgraph_get()
eval_obj = mesh_obj.evaluated_get(depsgraph)
me_eval = eval_obj.to_mesh()
zs_eval = [v.co.z for v in me_eval.vertices]
eval_obj.to_mesh_clear()
print(f"Evaluated Z: min={min(zs_eval):.4f} max={max(zs_eval):.4f}")

print("\n=== MESH (移除 Armature modifier 临时查看) ===")
# 禁用 modifier
mod = mesh_obj.modifiers.get("Armature")
if mod:
    mod.show_viewport = False
    bpy.context.view_layer.update()
    depsgraph2 = bpy.context.evaluated_depsgraph_get()
    eval_obj2 = mesh_obj.evaluated_get(depsgraph2)
    me_no_arm = eval_obj2.to_mesh()
    zs_no_arm = [v.co.z for v in me_no_arm.vertices]
    eval_obj2.to_mesh_clear()
    print(f"No Armature Z: min={min(zs_no_arm):.4f} max={max(zs_no_arm):.4f}")
    mod.show_viewport = True

print("\n=== BONES (armature local space) ===")
for bone in list(arm_obj.data.bones)[:5]:
    print(f"  {bone.name}: head={bone.head_local} tail={bone.tail_local}")

print("\n=== MESH object ===")
print(f"  location={mesh_obj.location}")
print(f"  matrix_world={mesh_obj.matrix_world}")
print(f"  parent={mesh_obj.parent}")

print("\n=== ARMATURE object ===")
print(f"  location={arm_obj.location}")
print(f"  matrix_world={arm_obj.matrix_world}")
print(f"  parent={arm_obj.parent}")
