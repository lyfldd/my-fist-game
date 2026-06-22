"""Scene inspection / query tools — read-only, no modifications."""

import bpy
from ..utils import make_response, error_response, to_dict


def get_connection_status():
    """Return basic scene info to confirm Blender is connected and alive."""
    try:
        scene = bpy.context.scene
        objects = [o.name for o in bpy.data.objects]
        meshes = [o.name for o in bpy.data.objects if o.type == 'MESH']
        armatures = [o.name for o in bpy.data.objects if o.type == 'ARMATURE']
        cameras = [o.name for o in bpy.data.objects if o.type == 'CAMERA']

        return make_response(
            success=True,
            status="connected",
            blend_file=bpy.data.filepath or "(untitled)",
            object_count=len(objects),
            mesh_count=len(meshes),
            armature_count=len(armatures),
            camera_count=len(cameras),
            current_frame=scene.frame_current,
            frame_start=scene.frame_start,
            frame_end=scene.frame_end,
            objects=objects,
        )
    except Exception as e:
        return error_response(str(e))


def list_objects():
    """List all objects in the scene with type and basic info."""
    try:
        result = []
        for obj in bpy.data.objects:
            info = {
                "name": obj.name,
                "type": obj.type,
                "location": to_dict(obj.location),
                "rotation_euler": to_dict(obj.rotation_euler),
                "scale": to_dict(obj.scale),
                "visible": not obj.hide_viewport,
                "parent": obj.parent.name if obj.parent else None,
                "children": [c.name for c in obj.children],
                "modifier_count": len(obj.modifiers),
            }
            if obj.type == 'MESH':
                info["vertices"] = len(obj.data.vertices) if obj.data else 0
                info["faces"] = len(obj.data.polygons) if obj.data else 0
                info["materials"] = [m.name for m in obj.data.materials if m] if obj.data else []
            if obj.type == 'ARMATURE':
                info["bones"] = [b.name for b in obj.data.bones] if obj.data else []
            result.append(info)

        return make_response(success=True, objects=result, count=len(result))
    except Exception as e:
        return error_response(str(e))


def list_bones(armature_name=None):
    """List bone hierarchy of an armature.

    Args:
        armature_name: Target armature name, or first found if None
    """
    try:
        if armature_name:
            arm = bpy.data.objects.get(armature_name)
            if arm is None:
                return error_response(f"Armature '{armature_name}' not found")
            if arm.type != 'ARMATURE':
                return error_response(f"'{armature_name}' is not an armature")
        else:
            arms = [o for o in bpy.data.objects if o.type == 'ARMATURE']
            if not arms:
                return error_response("No armature found in scene")
            arm = arms[0]

        bones = []
        for bone in arm.data.bones:
            info = {
                "name": bone.name,
                "parent": bone.parent.name if bone.parent else None,
                "children": [c.name for c in bone.children],
                "head": to_dict(bone.head_local),
                "tail": to_dict(bone.tail_local),
                "length": bone.length,
                "use_deform": bone.use_deform,
                "use_connect": bone.use_connect,
            }
            # Pose bone info (if armature is in pose mode accessible)
            if arm.pose:
                pose_bone = arm.pose.bones.get(bone.name)
                if pose_bone:
                    info["pose_location"] = to_dict(pose_bone.location)
                    info["pose_rotation"] = to_dict(pose_bone.rotation_euler)
                    info["pose_scale"] = to_dict(pose_bone.scale)
                    info["constraints"] = [c.name for c in pose_bone.constraints]

            bones.append(info)

        return make_response(
            success=True,
            armature=arm.name,
            bones=bones,
            bone_count=len(bones),
        )
    except Exception as e:
        return error_response(str(e))


def list_modifiers(object_name):
    """List all modifiers on an object with their settings.

    Args:
        object_name: Target object name
    """
    try:
        obj = bpy.data.objects.get(object_name)
        if obj is None:
            return error_response(f"Object '{object_name}' not found")

        mods = []
        for mod in obj.modifiers:
            info = {"name": mod.name, "type": mod.type, "show_viewport": mod.show_viewport,
                    "show_render": mod.show_render}
            # Common settings
            for attr in ['quality', 'time_scale', 'mass', 'air_damping', 'tension_stiffness',
                         'compression_stiffness', 'shear_stiffness', 'bending_stiffness',
                         'collision_quality', 'distance_min', 'distance_max',
                         'use_collision', 'use_self_collision']:
                if hasattr(mod, attr):
                    info[attr] = getattr(mod, attr)
                if hasattr(mod, 'settings') and hasattr(mod.settings, attr):
                    info[attr] = getattr(mod.settings, attr)
            # Special: collision settings
            if mod.type == 'CLOTH' and hasattr(mod, 'collision_settings'):
                cs = mod.collision_settings
                info['collision_quality'] = cs.collision_quality
                info['distance_min'] = cs.distance_min
                info['distance_max'] = cs.distance_max
                info['use_collision'] = cs.use_collision
                info['use_self_collision'] = cs.use_self_collision
            mods.append(info)

        return make_response(success=True, object=object_name, modifiers=mods, count=len(mods))
    except Exception as e:
        return error_response(str(e))


def get_cloth_settings(object_name):
    """Get cloth modifier settings for an object.

    Args:
        object_name: Object with a Cloth modifier
    """
    try:
        obj = bpy.data.objects.get(object_name)
        if obj is None:
            return error_response(f"Object '{object_name}' not found")

        cloth = None
        for mod in obj.modifiers:
            if mod.type == 'CLOTH':
                cloth = mod
                break

        if cloth is None:
            return error_response(f"No Cloth modifier on '{object_name}'")

        settings = {
            "quality": cloth.settings.quality,
            "time_scale": cloth.settings.time_scale,
            "mass": cloth.settings.mass,
            "air_damping": cloth.settings.air_damping,
            "tension_stiffness": cloth.settings.tension_stiffness,
            "compression_stiffness": cloth.settings.compression_stiffness,
            "shear_stiffness": cloth.settings.shear_stiffness,
            "bending_stiffness": cloth.settings.bending_stiffness,
            "use_pressure": cloth.settings.use_pressure,
            "uniform_pressure_force": cloth.settings.uniform_pressure_force,
            "use_dynamic_mesh": cloth.settings.use_dynamic_mesh,
        }

        # Vertex group assignments
        vg_info = {}
        for vg_attr in ['vertex_group_mass', 'vertex_group_pressure', 'vertex_group_shear',
                         'vertex_group_bending', 'vertex_group_shrink', 'vertex_group_structural_stiffness']:
            if hasattr(cloth.settings, vg_attr) and getattr(cloth.settings, vg_attr):
                vg_info[vg_attr] = getattr(cloth.settings, vg_attr)

        settings["vertex_groups"] = vg_info

        if hasattr(cloth, 'collision_settings'):
            cs = cloth.collision_settings
            settings["collision"] = {
                "collision_quality": cs.collision_quality,
                "distance_min": cs.distance_min,
                "distance_max": cs.distance_max,
                "use_collision": cs.use_collision,
                "use_self_collision": cs.use_self_collision,
            }

        return make_response(success=True, object=object_name, cloth=settings)
    except Exception as e:
        return error_response(str(e))


def get_collision_settings(object_name):
    """Get Collision modifier settings for an object.

    Args:
        object_name: Object with a Collision modifier
    """
    try:
        obj = bpy.data.objects.get(object_name)
        if obj is None:
            return error_response(f"Object '{object_name}' not found")

        collision = None
        for mod in obj.modifiers:
            if mod.type == 'COLLISION':
                collision = mod
                break

        if collision is None:
            return error_response(f"No Collision modifier on '{object_name}'")

        settings = {
            "use_culling": collision.settings.use_culling,
            "absorption": collision.settings.absorption,
            "damping": collision.settings.damping,
            "damping_factor": collision.settings.damping_factor,
            "damping_random": collision.settings.damping_random,
            "friction": collision.settings.friction,
            "friction_random": collision.settings.friction_random,
            "thickness_inner": collision.settings.thickness_inner,
            "thickness_outer": collision.settings.thickness_outer,
            "use": collision.settings.use,
            "use_cloth_collision": collision.settings.use_cloth_collision,
            "use_softbody_collision": collision.settings.use_softbody_collision,
        }

        return make_response(success=True, object=object_name, collision=settings)
    except Exception as e:
        return error_response(str(e))


def list_constraints(armature_name=None, bone_name=None):
    """List constraints on bones or objects.

    Args:
        armature_name: Target armature (optional)
        bone_name: Specific bone (optional, requires armature_name)
    """
    try:
        if armature_name and bone_name:
            arm = bpy.data.objects.get(armature_name)
            if arm is None or arm.type != 'ARMATURE':
                return error_response(f"Armature '{armature_name}' not found")
            pb = arm.pose.bones.get(bone_name)
            if pb is None:
                return error_response(f"Bone '{bone_name}' not found in '{armature_name}'")
            constraints_list = pb.constraints
            target_label = f"{armature_name}/{bone_name}"
        elif armature_name:
            arm = bpy.data.objects.get(armature_name)
            if arm is None or arm.type != 'ARMATURE':
                return error_response(f"Armature '{armature_name}' not found")
            result = {}
            for bone in arm.pose.bones:
                if bone.constraints:
                    result[bone.name] = _describe_constraints(bone.constraints)
            return make_response(success=True, armature=armature_name, bones=result)
        else:
            constraints_list = bpy.context.active_object.constraints if bpy.context.active_object else []
            target_label = bpy.context.active_object.name if bpy.context.active_object else "(none)"

        constraints_info = _describe_constraints(constraints_list)
        return make_response(success=True, target=target_label, constraints=constraints_info)
    except Exception as e:
        return error_response(str(e))


def _describe_constraints(constraints):
    result = []
    for c in constraints:
        info = {
            "name": c.name,
            "type": c.type,
            "mute": c.mute,
            "influence": c.influence,
        }
        if hasattr(c, 'target') and c.target:
            info["target"] = c.target.name
        if hasattr(c, 'subtarget') and c.subtarget:
            info["subtarget"] = c.subtarget
        result.append(info)
    return result


def get_animation_data(object_name=None):
    """Get animation data: action name, frame range, keyframe counts.

    Args:
        object_name: Target object, or active object if None
    """
    try:
        obj = None
        if object_name:
            obj = bpy.data.objects.get(object_name)
            if obj is None:
                return error_response(f"Object '{object_name}' not found")
        else:
            obj = bpy.context.active_object

        if obj is None:
            return error_response("No object specified and no active object")

        adata = obj.animation_data
        if adata is None:
            return make_response(success=True, object=obj.name, has_animation=False)

        info = {
            "has_animation": True,
        }

        if adata.action:
            action = adata.action
            info["action"] = action.name
            info["frame_range"] = to_dict(action.frame_range)
            info["fcurves"] = len(action.fcurves)
            curves = {}
            for fc in action.fcurves:
                curves[fc.data_path] = {
                    "array_index": fc.array_index,
                    "keyframes": len(fc.keyframe_points),
                }
            info["curves"] = curves

        if adata.nla_tracks:
            info["nla_tracks"] = [t.name for t in adata.nla_tracks]

        return make_response(success=True, object=obj.name, animation=info)
    except Exception as e:
        return error_response(str(e))


def inspect_vertex_weights(object_name, vertex_group_name=None):
    """Inspect vertex group weights on a mesh object.

    Args:
        object_name: Mesh object name
        vertex_group_name: Specific vertex group, or list all groups if None
    """
    try:
        obj = bpy.data.objects.get(object_name)
        if obj is None:
            return error_response(f"Object '{object_name}' not found")
        if obj.type != 'MESH':
            return error_response(f"'{object_name}' is not a mesh")

        if vertex_group_name:
            vg = obj.vertex_groups.get(vertex_group_name)
            if vg is None:
                return error_response(f"Vertex group '{vertex_group_name}' not found on '{object_name}'")

            weights = []
            for v in obj.data.vertices:
                for g in v.groups:
                    if g.group == vg.index and g.weight > 0.001:
                        weights.append({"vertex": v.index, "weight": round(g.weight, 4)})

            # Summary stats
            if weights:
                wvals = [w["weight"] for w in weights]
                summary = {
                    "min": round(min(wvals), 4),
                    "max": round(max(wvals), 4),
                    "avg": round(sum(wvals) / len(wvals), 4),
                    "assigned_vertices": len(weights),
                    "total_vertices": len(obj.data.vertices),
                }
            else:
                summary = {"assigned_vertices": 0, "total_vertices": len(obj.data.vertices)}

            return make_response(success=True, object=object_name, vertex_group=vertex_group_name,
                                 summary=summary, weights=weights[:50])  # cap at 50 to avoid huge payloads
        else:
            groups = []
            for vg in obj.vertex_groups:
                count = 0
                for v in obj.data.vertices:
                    for g in v.groups:
                        if g.group == vg.index and g.weight > 0.001:
                            count += 1
                            break
                groups.append({"name": vg.name, "index": vg.index, "assigned_vertices": count})

            return make_response(success=True, object=object_name, vertex_groups=groups)
    except Exception as e:
        return error_response(str(e))
