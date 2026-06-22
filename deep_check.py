"""深入检查骨骼和 Modifier"""
import bpy

arm_obj = mesh_obj = None
for obj in bpy.data.objects:
    if obj.type == 'ARMATURE':
        arm_obj = obj
    elif obj.type == 'MESH' and len(obj.data.vertices) > 100:
        mesh_obj = obj

# Armature modifier
mod = mesh_obj.modifiers.get("Armature")
print(f"Modifier: {mod.name}")
print(f"  object: {mod.object}")
print(f"  use_vertex_groups: {mod.use_vertex_groups}")
print(f"  use_bone_envelopes: {mod.use_bone_envelopes}")
print(f"  vertex_group: '{mod.vertex_group}'")
print(f"  invert_vertex_group: {mod.invert_vertex_group}")

# Check if mesh is parented to armature (indirectly)
print(f"\nMesh parent: {mesh_obj.parent}")
print(f"Armature parent: {arm_obj.parent}")

# Bone rest positions
print("\n=== Bones - full chain ===")
for bone in arm_obj.data.bones:
    if bone.parent is None:
        print(f"  [ROOT] {bone.name}: head={bone.head_local} tail={bone.tail_local}")

# Check a vertex's bone weights
print("\n=== Sample vertex weights ===")
sample_v = mesh_obj.data.vertices[0]
print(f"Vertex 0: co={sample_v.co}, groups={len(sample_v.groups)}")
for g in sample_v.groups[:5]:
    vg = mesh_obj.vertex_groups[g.group]
    print(f"  group={vg.name} weight={g.weight}")

# Check pose bones - is there still a pose?
print("\n=== Pose data ===")
for pbone in list(arm_obj.pose.bones)[:3]:
    print(f"  {pbone.name}: location={pbone.location} rotation={pbone.rotation_euler} scale={pbone.scale}")
    print(f"    matrix_basis={pbone.matrix_basis}")
    print(f"    matrix (world): {pbone.matrix}")

# Force rest pose
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='DESELECT')
arm_obj.select_set(True)
bpy.context.view_layer.objects.active = arm_obj
bpy.ops.object.mode_set(mode='POSE')
bpy.ops.pose.select_all(action='SELECT')
bpy.ops.pose.rot_clear()
bpy.ops.pose.loc_clear()
bpy.ops.pose.scale_clear()
bpy.ops.object.mode_set(mode='OBJECT')

print("\n=== After pose clear ===")
bpy.context.view_layer.update()
depsgraph = bpy.context.evaluated_depsgraph_get()
eval_obj = mesh_obj.evaluated_get(depsgraph)
me = eval_obj.to_mesh()
min_z = min(v.co.z for v in me.vertices)
max_z = max(v.co.z for v in me.vertices)
eval_obj.to_mesh_clear()
print(f"Mesh Z = [{min_z:.4f}, {max_z:.4f}]")
