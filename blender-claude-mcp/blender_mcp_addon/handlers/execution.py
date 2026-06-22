"""Execute arbitrary Python code in Blender's context — the universal escape hatch.

Any bpy operation not covered by dedicated tools can be done via this handler.
"""

import bpy
import io
import sys
import traceback
from ..utils import make_response, error_response


def execute_python(code):
    """Execute a Python code string in Blender's Python context.

    Has full access to bpy, bmesh, mathutils, and all Blender modules.
    Use this for operations not covered by dedicated MCP tools.

    Args:
        code: Python code string to execute

    Returns:
        dict with stdout output, or error traceback
    """
    try:
        # Capture stdout
        old_stdout = sys.stdout
        sys.stdout = io.StringIO()

        # Build a safe-ish namespace
        namespace = {
            'bpy': bpy,
            'C': bpy.context,
            'D': bpy.data,
            'scene': bpy.context.scene,
            'active': bpy.context.active_object,
            'selected': list(bpy.context.selected_objects),
        }

        try:
            import bmesh
            namespace['bmesh'] = bmesh
        except ImportError:
            pass

        try:
            from mathutils import Vector, Matrix, Euler, Quaternion
            namespace['Vector'] = Vector
            namespace['Matrix'] = Matrix
            namespace['Euler'] = Euler
            namespace['Quaternion'] = Quaternion
        except ImportError:
            pass

        exec(code, namespace)
        output = sys.stdout.getvalue()
        sys.stdout = old_stdout

        return make_response(
            success=True,
            output=output,
            output_lines=len(output.splitlines()) if output else 0,
        )
    except Exception as e:
        sys.stdout = old_stdout
        tb = traceback.format_exc()
        return error_response(f"{tb}")
