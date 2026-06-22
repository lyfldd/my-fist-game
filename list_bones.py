"""列出所有骨骼名称"""
import bpy

arm_obj = None
for obj in bpy.data.objects:
    if obj.type == 'ARMATURE':
        arm_obj = obj
        break

print(f"Armature: {arm_obj.name}, {len(arm_obj.data.bones)} bones:")
for i, bone in enumerate(arm_obj.data.bones):
    parent_name = bone.parent.name if bone.parent else "(root)"
    print(f"  {i:2d}. {bone.name:40s} parent={parent_name}")
