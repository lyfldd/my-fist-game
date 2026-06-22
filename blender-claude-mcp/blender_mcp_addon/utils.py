"""Shared utilities for MCP addon handlers — zero external dependencies."""
import json
import math


def to_dict(obj, default=str):
    """Recursively convert Blender types to JSON-safe dicts/lists."""
    if obj is None:
        return None
    if isinstance(obj, (bool, int, float, str)):
        return obj
    if isinstance(obj, (list, tuple)):
        return [to_dict(v, default) for v in obj]
    if isinstance(obj, dict):
        return {str(k): to_dict(v, default) for k, v in obj.items()}
    # Blender Vector / Euler / Quaternion / Color / Matrix
    if hasattr(obj, '__iter__') and hasattr(obj, '__len__'):
        try:
            if len(obj) <= 16:  # small iterables like vectors
                return [to_dict(v, default) for v in obj]
        except Exception:
            pass
    return default(obj)


def make_response(success=True, **kwargs):
    """Build a standard JSON response dict."""
    resp = {"success": success}
    resp.update(kwargs)
    return resp


def error_response(message):
    """Build a standard error response."""
    return {"success": False, "error": message}


def find_object(name):
    """Find a Blender object by name, returns (obj, error)."""
    import bpy
    obj = bpy.data.objects.get(name)
    if obj is None:
        return None, f"Object '{name}' not found"
    return obj, None


def find_armature(name=None):
    """Find an armature by name, or return first found. Returns (armature, error)."""
    import bpy
    if name:
        obj = bpy.data.objects.get(name)
        if obj is None:
            return None, f"Armature '{name}' not found"
        if obj.type != 'ARMATURE':
            return None, f"'{name}' is not an armature"
        return obj, None
    # Find first armature
    for obj in bpy.data.objects:
        if obj.type == 'ARMATURE':
            return obj, None
    return None, "No armature found in scene"


def find_modifier(obj, modifier_type):
    """Find a modifier of given type on an object. Returns (mod, error)."""
    for mod in obj.modifiers:
        if mod.type == modifier_type:
            return mod, None
    return None, f"No '{modifier_type}' modifier on '{obj.name}'"
