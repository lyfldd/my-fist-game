"""Viewport capture via OpenGL offscreen rendering.

Returns screenshots as base64-encoded PNG strings.
The MCP Bridge detects image_base64 fields and calls Qwen-VL for visual analysis.
"""

import bpy
import base64
import io
import json
from ..utils import make_response, error_response


def _render_to_base64(camera_name=None, width=1920, height=1080):
    """Render current viewport to a base64 PNG string.

    Uses OpenGL offscreen render (gpu module) when available,
    falls back to view3d render.
    """
    # Prefer OpenGL offscreen
    try:
        import gpu
        import numpy as np

        scene = bpy.context.scene
        render = scene.render
        old_x, old_y = render.resolution_x, render.resolution_y
        old_pct = render.resolution_percentage
        old_filepath = render.filepath
        old_format = render.image_settings.file_format

        render.resolution_x = width
        render.resolution_y = height
        render.resolution_percentage = 100
        render.image_settings.file_format = 'PNG'

        # Render to an in-memory buffer
        buf = io.BytesIO()
        # Store render result in memory using a temp path
        import tempfile
        import os
        tmp = os.path.join(tempfile.gettempdir(), "_mcp_render.png")
        render.filepath = tmp
        bpy.ops.render.render(write_still=True)

        with open(tmp, 'rb') as f:
            img_data = base64.b64encode(f.read()).decode('ascii')
        try:
            os.remove(tmp)
        except OSError:
            pass

        # Restore
        render.resolution_x = old_x
        render.resolution_y = old_y
        render.resolution_percentage = old_pct
        render.filepath = old_filepath
        render.image_settings.file_format = old_format

        return img_data
    except (ImportError, ModuleNotFoundError):
        pass

    # Fallback: use view3d capture if possible
    try:
        for area in bpy.context.screen.areas:
            if area.type == 'VIEW_3D':
                for region in area.regions:
                    if region.type == 'WINDOW':
                        # Use bpy.ops.screen.screenshot
                        import tempfile
                        import os
                        tmp = os.path.join(tempfile.gettempdir(), "_mcp_screenshot.png")
                        old_filepath = bpy.context.scene.render.filepath
                        old_format = bpy.context.scene.render.image_settings.file_format
                        bpy.context.scene.render.filepath = tmp
                        bpy.context.scene.render.image_settings.file_format = 'PNG'
                        bpy.ops.render.opengl(write_still=True, view_context=True)
                        with open(tmp, 'rb') as f:
                            img_data = base64.b64encode(f.read()).decode('ascii')
                        try:
                            os.remove(tmp)
                        except OSError:
                            pass
                        bpy.context.scene.render.filepath = old_filepath
                        bpy.context.scene.render.image_settings.file_format = old_format
                        return img_data
    except Exception:
        pass

    raise RuntimeError("Could not render viewport — no suitable render method available")


def capture_viewport(camera=None, width=1920, height=1080):
    """Capture the current 3D viewport.

    Args:
        camera: Camera name or None (use active camera / viewport)
        width, height: Resolution in pixels

    Returns:
        dict with image_base64, camera_used, width, height
    """
    try:
        cam_name = camera
        if camera:
            cam_obj = bpy.data.objects.get(camera)
            if cam_obj and cam_obj.type == 'CAMERA':
                bpy.context.scene.camera = cam_obj
                cam_name = camera
        else:
            cam_name = bpy.context.scene.camera.name if bpy.context.scene.camera else "Viewport"

        img = _render_to_base64(cam_name, width, height)
        return make_response(
            success=True,
            image_base64=img,
            camera_used=cam_name,
            frame=bpy.context.scene.frame_current,
            width=width,
            height=height,
        )
    except Exception as e:
        return error_response(str(e))


def capture_animation_frame(frame, camera=None, width=1920, height=1080):
    """Jump to a frame and capture the viewport.

    Args:
        frame: Frame number
        camera: Camera name or None
        width, height: Resolution
    """
    try:
        original_frame = bpy.context.scene.frame_current
        bpy.context.scene.frame_set(int(frame))

        if camera:
            cam_obj = bpy.data.objects.get(camera)
            if cam_obj and cam_obj.type == 'CAMERA':
                bpy.context.scene.camera = cam_obj

        img = _render_to_base64(camera, width, height)
        bpy.context.scene.frame_set(original_frame)

        return make_response(
            success=True,
            image_base64=img,
            frame=int(frame),
            camera_used=camera or "Viewport",
            width=width,
            height=height,
        )
    except Exception as e:
        return error_response(str(e))


def capture_multiview(frame=None, width=1024, height=768):
    """Capture from 4 standard angles: front, right, top, perspective.

    Returns 4 separate base64 images with view labels.
    """
    try:
        scene = bpy.context.scene
        original_frame = scene.frame_current
        original_cam = scene.camera

        if frame is not None:
            scene.frame_set(int(frame))

        views = {}
        # Create temp cameras if needed
        views_config = {
            "FRONT":  (0, -10, 0,    0, 0, 0,   0, 0, 1),
            "RIGHT":  (10, 0, 0,     0, 0, 0,   0, 0, 1),
            "TOP":    (0, 0, 10,     0, 0, 0,   0, 1, 0),
        }

        # Use existing cameras or create temporary ones
        import tempfile, os
        for view_name, (lx, ly, lz, tx, ty, tz, ux, uy, uz) in views_config.items():
            cam_data = bpy.data.cameras.new(f"_mcp_temp_{view_name}")
            cam_obj = bpy.data.objects.new(f"_mcp_temp_{view_name}", cam_data)
            scene.collection.objects.link(cam_obj)
            cam_obj.location = (lx, ly, lz)
            # Point to origin
            from mathutils import Vector
            direction = Vector((tx - lx, ty - ly, tz - lz)).normalized()
            cam_obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()

            scene.camera = cam_obj
            tmp = os.path.join(tempfile.gettempdir(), f"_mcp_{view_name}.png")
            scene.render.filepath = tmp
            scene.render.image_settings.file_format = 'PNG'
            scene.render.resolution_x = width
            scene.render.resolution_y = height
            scene.render.resolution_percentage = 100

            bpy.ops.render.render(write_still=True)
            with open(tmp, 'rb') as f:
                views[view_name] = base64.b64encode(f.read()).decode('ascii')
            try:
                os.remove(tmp)
            except OSError:
                pass

            # Cleanup temp camera
            bpy.data.objects.remove(cam_obj, do_unlink=True)
            bpy.data.cameras.remove(cam_data)

        # For perspective, use original camera or viewport
        if original_cam:
            scene.camera = original_cam
        views["PERSPECTIVE"] = _render_to_base64(None, width, height)

        scene.frame_set(original_frame)
        if original_cam:
            scene.camera = original_cam

        return make_response(
            success=True,
            views=views,
            frame=frame or original_frame,
        )
    except Exception as e:
        return error_response(str(e))


def capture_clipping_region(object_name, margin=0.2, width=1024, height=768):
    """Focus the view on a specific object and capture.

    Args:
        object_name: Target object name
        margin: Extra space around the object (proportion)
        width, height: Resolution
    """
    try:
        obj = bpy.data.objects.get(object_name)
        if obj is None:
            return error_response(f"Object '{object_name}' not found")
        if obj.type != 'CAMERA':
            # Select and frame
            bpy.ops.object.select_all(action='DESELECT')
            obj.select_set(True)
            bpy.context.view_layer.objects.active = obj
            bpy.ops.view3d.view_selected()

        img = _render_to_base64(None, width, height)
        return make_response(
            success=True,
            image_base64=img,
            object=object_name,
            frame=bpy.context.scene.frame_current,
            width=width,
            height=height,
        )
    except Exception as e:
        return error_response(str(e))
