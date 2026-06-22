"""Animation control tools — frame navigation, baking, playback."""

import bpy
from ..utils import make_response, error_response


def set_frame(frame):
    """Jump to a specific frame.

    Args:
        frame: Target frame number
    """
    try:
        frame = int(frame)
        bpy.context.scene.frame_set(frame)
        return make_response(
            success=True,
            current_frame=frame,
            frame_start=bpy.context.scene.frame_start,
            frame_end=bpy.context.scene.frame_end,
        )
    except Exception as e:
        return error_response(str(e))


def set_frame_range(frame_start, frame_end):
    """Set the scene frame range (timeline).

    Args:
        frame_start: Start frame
        frame_end: End frame
    """
    try:
        bpy.context.scene.frame_start = int(frame_start)
        bpy.context.scene.frame_end = int(frame_end)
        return make_response(
            success=True,
            frame_start=int(frame_start),
            frame_end=int(frame_end),
        )
    except Exception as e:
        return error_response(str(e))


def bake_simulation(object_name, frame_start=None, frame_end=None):
    """Bake a physics simulation (cloth, soft body, etc.) for an object.

    Args:
        object_name: Object with a physics modifier
        frame_start: Start frame (default: scene start)
        frame_end: End frame (default: scene end)
    """
    try:
        obj = bpy.data.objects.get(object_name)
        if obj is None:
            return error_response(f"Object '{object_name}' not found")

        # Verify at least one sim modifier
        sim_types = {'CLOTH', 'SOFT_BODY', 'FLUID', 'DYNAMIC_PAINT', 'OCEAN'}
        has_sim = any(mod.type in sim_types for mod in obj.modifiers)
        if not has_sim:
            return error_response(f"Object '{object_name}' has no physics simulation modifier")

        scene = bpy.context.scene
        start = int(frame_start) if frame_start is not None else scene.frame_start
        end = int(frame_end) if frame_end is not None else scene.frame_end

        # Select the object and bake
        original_active = bpy.context.view_layer.objects.active
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)

        # Point cache baking
        # For cloth: use modifier's point_cache
        baked_count = 0
        for mod in obj.modifiers:
            if mod.type in sim_types and hasattr(mod, 'point_cache'):
                cache = mod.point_cache
                # Use the operator to bake
                override = bpy.context.copy()
                override['object'] = obj
                cache.frame_start = start
                cache.frame_end = end

                # Set frame to start
                scene.frame_set(start)
                try:
                    bpy.ops.ptcache.bake(override, bake=True)
                    baked_count += 1
                except Exception:
                    # Fallback: manual frame-by-frame baking
                    for f in range(start, end + 1):
                        scene.frame_set(f)
                        scene.frame_set(f)  # double set to force update

        # Restore
        if original_active:
            bpy.context.view_layer.objects.active = original_active

        scene.frame_set(start)

        return make_response(
            success=True,
            object=object_name,
            baked_count=baked_count,
            frame_start=start,
            frame_end=end,
            message=f"Baked simulation for '{object_name}' ({start}-{end})",
        )
    except Exception as e:
        return error_response(str(e))


def bake_action(object_name=None, frame_start=None, frame_end=None):
    """Bake object animation to keyframes (visual keying).

    Args:
        object_name: Object to bake, or active object
        frame_start, frame_end: Range (default: scene range)
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
                return error_response("No object selected")

        scene = bpy.context.scene
        start = int(frame_start) if frame_start is not None else scene.frame_start
        end = int(frame_end) if frame_end is not None else scene.frame_end

        # Select object
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj

        bpy.ops.nla.bake(
            frame_start=start,
            frame_end=end,
            only_selected=True,
            visual_keying=True,
            clear_constraints=False,
            clear_parents=False,
            bake_types={'OBJECT'},
        )

        return make_response(
            success=True,
            object=obj.name,
            frame_start=start,
            frame_end=end,
            message=f"Baked action for '{obj.name}'",
        )
    except Exception as e:
        return error_response(str(e))


def play_animation():
    """Toggle animation playback. Returns current playback state."""
    try:
        # Check if already playing
        if bpy.context.screen.is_animation_playing:
            bpy.ops.screen.animation_play()  # this toggles it off
            state = "stopped"
        else:
            bpy.ops.screen.animation_play()
            state = "playing"

        return make_response(
            success=True,
            state=state,
            current_frame=bpy.context.scene.frame_current,
        )
    except Exception as e:
        return error_response(str(e))
