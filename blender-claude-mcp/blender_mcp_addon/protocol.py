"""Message routing — maps method names → handler functions."""

from .handlers import capture
from .handlers import inspection
from .handlers import modification
from .handlers import animation
from .handlers import execution
from .handlers import file_ops

# ── tool registry ─────────────────────────────────────────────

TOOL_TABLE = {
    # Connection
    "blender_get_connection_status": inspection.get_connection_status,

    # Capture (5)
    "blender_capture_viewport":          capture.capture_viewport,
    "blender_capture_animation_frame":   capture.capture_animation_frame,
    "blender_capture_multiview":         capture.capture_multiview,
    "blender_capture_clipping_region":   capture.capture_clipping_region,

    # Inspection (8)
    "blender_list_objects":              inspection.list_objects,
    "blender_list_bones":               inspection.list_bones,
    "blender_list_modifiers":           inspection.list_modifiers,
    "blender_get_cloth_settings":       inspection.get_cloth_settings,
    "blender_get_collision_settings":   inspection.get_collision_settings,
    "blender_list_constraints":         inspection.list_constraints,
    "blender_get_animation_data":       inspection.get_animation_data,
    "blender_inspect_vertex_weights":   inspection.inspect_vertex_weights,

    # Modification (8)
    "blender_add_modifier":              modification.add_modifier,
    "blender_set_cloth_settings":       modification.set_cloth_settings,
    "blender_set_collision_settings":   modification.set_collision_settings,
    "blender_set_bone_transform":       modification.set_bone_transform,
    "blender_set_vertex_weights":       modification.set_vertex_weights,
    "blender_add_bone_constraint":      modification.add_bone_constraint,
    "blender_set_constraint_settings":  modification.set_constraint_settings,
    "blender_create_collision_proxy":   modification.create_collision_proxy,

    # Animation (5)
    "blender_set_frame":                animation.set_frame,
    "blender_set_frame_range":          animation.set_frame_range,
    "blender_bake_simulation":          animation.bake_simulation,
    "blender_bake_action":              animation.bake_action,
    "blender_play_animation":           animation.play_animation,

    # Execution (1)
    "blender_execute_python":           execution.execute_python,

    # File Ops (2)
    "blender_save_file":                file_ops.save_file,
    "blender_undo":                     file_ops.undo,
}


def dispatch(method, params):
    """Route a method name to its handler. Returns a JSON-safe dict."""
    handler = TOOL_TABLE.get(method)
    if handler is None:
        return {"success": False, "error": f"Unknown method: {method}"}
    try:
        if params:
            return handler(**params)
        else:
            return handler()
    except TypeError as e:
        return {"success": False, "error": f"Invalid parameters: {e}"}
    except Exception as e:
        import traceback
        traceback.print_exc()
        return {"success": False, "error": str(e)}
