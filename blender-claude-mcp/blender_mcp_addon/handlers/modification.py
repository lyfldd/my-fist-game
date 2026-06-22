"""Scene modification tools — add/modify modifiers, bones, weights, constraints."""

import bpy
from ..utils import make_response, error_response, to_dict


def add_modifier(object_name, modifier_type, settings=None):
    """Add a modifier to an object.

    Args:
        object_name: Target object
        modifier_type: Modifier type string (e.g. 'COLLISION', 'CLOTH', 'SUBSURF', 'SOLIDIFY')
        settings: Optional dict of modifier settings

    Returns:
        Status with modifier info
    """
    try:
        obj = bpy.data.objects.get(object_name)
        if obj is None:
            return error_response(f"Object '{object_name}' not found")

        # Validate modifier type
        valid_types = [t[0] for t in bpy.types.Modifier.bl_rna.properties['type'].enum_items]
        if modifier_type not in valid_types:
            return error_response(f"Unknown modifier type: '{modifier_type}'. Valid: {valid_types}")

        mod = obj.modifiers.new(name=modifier_type, type=modifier_type)

        if settings and isinstance(settings, dict):
            _apply_settings(mod, settings)

        # Force update
        obj.data.update()

        return make_response(
            success=True,
            object=object_name,
            modifier=mod.name,
            type=mod.type,
            message=f"Added {modifier_type} modifier to '{object_name}'",
        )
    except Exception as e:
        return error_response(str(e))


def set_cloth_settings(object_name, settings):
    """Configure Cloth modifier settings.

    Args:
        object_name: Object with Cloth modifier
        settings: Dict of cloth properties to set
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

        cloth_settings = cloth.settings
        coll_settings = cloth.collision_settings

        applied = []
        for key, value in settings.items():
            # Try cloth.settings first, then collision_settings
            if hasattr(cloth_settings, key):
                setattr(cloth_settings, key, value)
                applied.append(f"cloth.{key}={value}")
            elif hasattr(coll_settings, key):
                setattr(coll_settings, key, value)
                applied.append(f"collision.{key}={value}")

        if not applied:
            return error_response(f"No valid cloth settings found in: {list(settings.keys())}")

        return make_response(
            success=True,
            object=object_name,
            applied=applied,
        )
    except Exception as e:
        return error_response(str(e))


def set_collision_settings(object_name, settings):
    """Configure Collision modifier settings.

    Args:
        object_name: Object with Collision modifier
        settings: Dict of collision properties
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

        applied = []
        for key, value in settings.items():
            if hasattr(collision.settings, key):
                setattr(collision.settings, key, value)
                applied.append(f"{key}={value}")

        if not applied:
            return error_response(f"No valid collision settings found in: {list(settings.keys())}")

        return make_response(success=True, object=object_name, applied=applied)
    except Exception as e:
        return error_response(str(e))


def set_bone_transform(armature_name, bone_name, location=None, rotation_euler=None, rotation_quaternion=None, scale=None):
    """Set transform of a pose bone.

    Args:
        armature_name: Armature object name
        bone_name: Bone name
        location: [x, y, z] tuple/list or None
        rotation_euler: [x, y, z] Euler angles in radians or None
        rotation_quaternion: [w, x, y, z] Quaternion or None
        scale: [x, y, z] tuple/list or None
    """
    try:
        arm = bpy.data.objects.get(armature_name)
        if arm is None or arm.type != 'ARMATURE':
            return error_response(f"Armature '{armature_name}' not found")

        pb = arm.pose.bones.get(bone_name)
        if pb is None:
            return error_response(f"Bone '{bone_name}' not found in '{armature_name}'")

        changes = []
        if location is not None:
            pb.location = location
            changes.append("location")
        if rotation_euler is not None:
            pb.rotation_euler = rotation_euler
            pb.rotation_mode = 'XYZ'
            changes.append("rotation_euler")
        if rotation_quaternion is not None:
            pb.rotation_quaternion = rotation_quaternion
            pb.rotation_mode = 'QUATERNION'
            changes.append("rotation_quaternion")
        if scale is not None:
            pb.scale = scale
            changes.append("scale")

        # Update viewport
        bpy.context.view_layer.update()

        return make_response(
            success=True,
            armature=armature_name,
            bone=bone_name,
            applied=changes,
            result={
                "location": to_dict(pb.location),
                "rotation_euler": to_dict(pb.rotation_euler),
                "scale": to_dict(pb.scale),
            },
        )
    except Exception as e:
        return error_response(str(e))


def set_vertex_weights(object_name, vertex_group_name, weights):
    """Set vertex weights for a vertex group.

    Args:
        object_name: Mesh object
        vertex_group_name: Target vertex group name (created if missing)
        weights: List of [vertex_index, weight] pairs, or dict {vertex_index: weight}

    Note:
        For bulk weight assignment, use blender_execute_python for better performance.
        This is for small targeted adjustments.
    """
    try:
        obj = bpy.data.objects.get(object_name)
        if obj is None:
            return error_response(f"Object '{object_name}' not found")
        if obj.type != 'MESH':
            return error_response(f"'{object_name}' is not a mesh")

        # Get or create vertex group
        vg = obj.vertex_groups.get(vertex_group_name)
        if vg is None:
            vg = obj.vertex_groups.new(name=vertex_group_name)

        # Normalize weights format
        weight_pairs = []
        if isinstance(weights, dict):
            weight_pairs = [(int(k), float(v)) for k, v in weights.items()]
        elif isinstance(weights, list):
            if weights and isinstance(weights[0], (list, tuple)):
                weight_pairs = [(int(w[0]), float(w[1])) for w in weights]
            else:
                return error_response("weights must be list of [index, weight] pairs or dict")

        set_count = 0
        for vidx, wval in weight_pairs:
            if 0 <= vidx < len(obj.data.vertices):
                vg.add([vidx], wval, 'REPLACE')
                set_count += 1

        return make_response(
            success=True,
            object=object_name,
            vertex_group=vertex_group_name,
            weights_set=set_count,
        )
    except Exception as e:
        return error_response(str(e))


def add_bone_constraint(armature_name, bone_name, constraint_type, settings=None):
    """Add a constraint to a pose bone.

    Args:
        armature_name: Armature object name
        bone_name: Bone name
        constraint_type: Constraint type string (e.g. 'COPY_ROTATION', 'LIMIT_ROTATION', 'IK')
        settings: Optional dict of constraint settings
    """
    try:
        arm = bpy.data.objects.get(armature_name)
        if arm is None or arm.type != 'ARMATURE':
            return error_response(f"Armature '{armature_name}' not found")

        pb = arm.pose.bones.get(bone_name)
        if pb is None:
            return error_response(f"Bone '{bone_name}' not found in '{armature_name}'")

        # Validate type
        valid_types = [t[0] for t in bpy.types.Constraint.bl_rna.properties['type'].enum_items]
        if constraint_type not in valid_types:
            return error_response(f"Unknown constraint type: '{constraint_type}'")

        c = pb.constraints.new(type=constraint_type)

        if settings and isinstance(settings, dict):
            _apply_settings(c, settings)

        bpy.context.view_layer.update()

        return make_response(
            success=True,
            armature=armature_name,
            bone=bone_name,
            constraint=c.name,
            type=c.type,
        )
    except Exception as e:
        return error_response(str(e))


def set_constraint_settings(armature_name, bone_name, constraint_name, settings):
    """Modify settings of an existing constraint.

    Args:
        armature_name: Armature object name
        bone_name: Bone name
        constraint_name: Constraint name
        settings: Dict of properties to change
    """
    try:
        arm = bpy.data.objects.get(armature_name)
        if arm is None or arm.type != 'ARMATURE':
            return error_response(f"Armature '{armature_name}' not found")

        pb = arm.pose.bones.get(bone_name)
        if pb is None:
            return error_response(f"Bone '{bone_name}' not found in '{armature_name}'")

        c = pb.constraints.get(constraint_name)
        if c is None:
            return error_response(f"Constraint '{constraint_name}' not found on {armature_name}/{bone_name}")

        applied = _apply_settings(c, settings)

        if not applied:
            return error_response(f"No valid settings found in: {list(settings.keys())}")

        bpy.context.view_layer.update()
        return make_response(success=True, armature=armature_name, bone=bone_name,
                             constraint=constraint_name, applied=applied)
    except Exception as e:
        return error_response(str(e))


def create_collision_proxy(object_name, proxy_type="BOX", size=None, offset=None):
    """Create a simplified collision proxy mesh for an object.

    Useful for adding collision volumes without manual modeling.

    Args:
        object_name: Reference object (for positioning/sizing)
        proxy_type: 'BOX', 'SPHERE', 'CYLINDER', 'CAPSULE'
        size: Manual [x, y, z] size override, or None for auto
        offset: [x, y, z] offset from object center, or None
    """
    try:
        obj = bpy.data.objects.get(object_name)
        if obj is None:
            return error_response(f"Object '{object_name}' not found")

        # Auto-size from bounding box
        if size is None and obj.type == 'MESH':
            bbox = [obj.matrix_world @ v.co for v in obj.data.vertices]
            min_c = [min(v[i] for v in bbox) for i in range(3)]
            max_c = [max(v[i] for v in bbox) for i in range(3)]
            size = [max_c[i] - min_c[i] for i in range(3)]

        if size is None:
            size = [1.0, 1.0, 1.0]

        if offset is None:
            offset = [0.0, 0.0, 0.0]

        # Create proxy mesh
        proxy_type_upper = proxy_type.upper()
        if proxy_type_upper == 'BOX':
            bpy.ops.mesh.primitive_cube_add(size=1)
        elif proxy_type_upper == 'SPHERE':
            bpy.ops.mesh.primitive_uv_sphere_add()
        elif proxy_type_upper == 'CYLINDER':
            bpy.ops.mesh.primitive_cylinder_add()
        elif proxy_type_upper == 'CAPSULE':
            bpy.ops.mesh.primitive_cylinder_add()  # closest match
        else:
            return error_response(f"Unknown proxy type: {proxy_type}")

        proxy = bpy.context.active_object
        proxy.name = f"{object_name}_CollisionProxy"
        proxy.location = [obj.location[i] + offset[i] for i in range(3)]
        proxy.scale = size if proxy_type_upper in ('BOX', 'SPHERE') else [size[0], size[2], size[1]]

        # Add collision modifier
        mod = proxy.modifiers.new(name="Collision", type='COLLISION')

        # Hide render
        proxy.hide_render = True
        proxy.display_type = 'WIRE'

        return make_response(
            success=True,
            object=object_name,
            proxy=proxy.name,
            type=proxy_type_upper,
            size=size,
            offset=offset,
            message=f"Created collision proxy '{proxy.name}'",
        )
    except Exception as e:
        return error_response(str(e))


# ── helpers ───────────────────────────────────────────────────

def _apply_settings(target, settings):
    """Apply a settings dict to a Blender data block. Returns list of applied keys."""
    applied = []
    for key, value in settings.items():
        if hasattr(target, key):
            setattr(target, key, value)
            applied.append(f"{key}={value}")
    return applied
