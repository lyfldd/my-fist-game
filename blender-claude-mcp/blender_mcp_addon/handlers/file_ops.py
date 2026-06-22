"""File operations — save and undo."""

import bpy
import os
from ..utils import make_response, error_response


def save_file(filepath=None):
    """Save the current Blender file.

    Args:
        filepath: Path to save to, or None (overwrite current file)

    Returns:
        Status with saved file path
    """
    try:
        if filepath:
            # Ensure directory exists
            directory = os.path.dirname(filepath)
            if directory and not os.path.exists(directory):
                os.makedirs(directory, exist_ok=True)
            bpy.ops.wm.save_as_mainfile(filepath=filepath)
            saved_path = filepath
        else:
            bpy.ops.wm.save_mainfile()
            saved_path = bpy.data.filepath

        return make_response(
            success=True,
            filepath=saved_path,
            message=f"Saved to {saved_path}",
        )
    except Exception as e:
        return error_response(str(e))


def undo():
    """Undo the last operation.

    Returns:
        Status
    """
    try:
        bpy.ops.ed.undo()
        return make_response(success=True, message="Undo successful")
    except Exception as e:
        return error_response(str(e))
