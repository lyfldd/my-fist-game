"""检查是否有动画在影响骨骼位置"""
import bpy

arm_obj = mesh_obj = None
for obj in bpy.data.objects:
    if obj.type == 'ARMATURE':
        arm_obj = obj
    elif obj.type == 'MESH' and len(obj.data.vertices) > 100:
        mesh_obj = obj

print(f"Armature: {arm_obj.name}")
print(f"  animation_data: {arm_obj.animation_data}")
if arm_obj.animation_data:
    ad = arm_obj.animation_data
    print(f"  action: {ad.action}")
    if ad.action:
        print(f"  action name: {ad.action.name}")
        print(f"  action frame_range: {ad.action.frame_range}")
        print(f"  action fcurves: {len(ad.action.fcurves)}")

# Check NLA tracks
if arm_obj.animation_data and arm_obj.animation_data.nla_tracks:
    for track in arm_obj.animation_data.nla_tracks:
        print(f"  NLA track: {track.name}, strips: {len(track.strips)}")

# Check scene frame
scene = bpy.context.scene
print(f"\nScene frame: start={scene.frame_start} end={scene.frame_end} current={scene.frame_current}")

# Check pose bone positions at current frame vs rest
print("\n=== First 3 bones: Rest vs Pose at current frame ===")
bpy.context.view_layer.update()
for bone in list(arm_obj.data.bones)[:3]:
    pose_bone = arm_obj.pose.bones.get(bone.name)
    if pose_bone:
        print(f"  {bone.name}: rest head={bone.head_local}, pose head={pose_bone.head}, pose location={pose_bone.location}")
