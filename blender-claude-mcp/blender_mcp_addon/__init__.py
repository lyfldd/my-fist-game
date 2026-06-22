"""Blender MCP Addon — Claude Code ↔ Blender bridge via WebSocket.

Registers:
  - A 3D Viewport sidebar panel ("Claude MCP") with Start/Stop buttons
  - WebSocket server for MCP Bridge communication
  - Timer callback for main-thread-safe bpy operations

Install: copy this entire blender_mcp_addon/ folder to Blender's addon directory,
  then enable "Claude MCP Bridge" in Edit → Preferences → Add-ons.

Blender addon directory (typical):
  %APPDATA%\\Blender Foundation\\Blender\\5.1\\scripts\\addons\\
"""

import bpy

bl_info = {
    "name": "Claude MCP Bridge",
    "author": "Claude Code + DeepSeek",
    "version": (1, 0, 0),
    "blender": (5, 0, 0),
    "location": "3D Viewport → Sidebar → Claude MCP",
    "description": "WebSocket bridge for Claude Code MCP to control Blender",
    "category": "Interface",
}


# ── UI Panel ──────────────────────────────────────────────────

class MCP_PT_Panel(bpy.types.Panel):
    bl_label = "Claude MCP"
    bl_idname = "MCP_PT_Panel"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "Claude MCP"

    def draw(self, context):
        layout = self.layout
        scene = context.scene

        # Server status
        from . import server
        running = server.is_running()

        status_icon = 'CHECKBOX_HLT' if running else 'CHECKBOX_DEHLT'
        status_text = "● Running" if running else "○ Stopped"
        layout.label(text=f"Status: {status_text}", icon=status_icon)

        if running:
            pending = server.get_request_count()
            layout.label(text=f"Pending: {pending}")

        layout.separator()

        # Start/Stop buttons
        row = layout.row(align=True)
        row.operator("mcp.start_server", text="Start Server", icon='PLAY')
        row.operator("mcp.stop_server", text="Stop Server", icon='PAUSE')

        layout.separator()

        # Port display
        layout.label(text=f"Port: {scene.mcp_port}")

        # Info
        layout.separator()
        box = layout.box()
        box.label(text="Tools: 24", icon='TOOL_SETTINGS')
        box.label(text="Handlers: 6", icon='FILE_SCRIPT')
        box.label(text="Qwen-VL: Bridge layer", icon='HIDE_ON')


class MCP_OT_StartServer(bpy.types.Operator):
    bl_idname = "mcp.start_server"
    bl_label = "Start MCP Server"
    bl_description = "Start the WebSocket server for MCP Bridge communication"

    def execute(self, context):
        from . import server
        port = context.scene.mcp_port
        server.start_server("127.0.0.1", port)
        return {'FINISHED'}


class MCP_OT_StopServer(bpy.types.Operator):
    bl_idname = "mcp.stop_server"
    bl_label = "Stop MCP Server"
    bl_description = "Stop the WebSocket server"

    def execute(self, context):
        from . import server
        server.stop_server()
        return {'FINISHED'}


# ── Auto-start ─────────────────────────────────────────────────

_auto_start_done = False


def _auto_start_server():
    """Start server automatically after Blender loads (called via timer)."""
    global _auto_start_done
    if _auto_start_done:
        return
    from . import server
    if not server.is_running():
        port = bpy.context.scene.mcp_port if hasattr(bpy.context.scene, "mcp_port") else 9876
        server.start_server("127.0.0.1", port)
    _auto_start_done = True


def _load_post_handler(_dummy):
    """Re-start server on file load (including initial startup)."""
    from . import server
    global _auto_start_done
    _auto_start_done = False
    if not server.is_running():
        bpy.app.timers.register(_auto_start_server, first_interval=1.0)


# ── Registration ──────────────────────────────────────────────

classes = (
    MCP_PT_Panel,
    MCP_OT_StartServer,
    MCP_OT_StopServer,
)


def register():
    for cls in classes:
        bpy.utils.register_class(cls)
    bpy.types.Scene.mcp_port = bpy.props.IntProperty(
        name="MCP Port",
        default=9876,
        min=1024,
        max=65535,
        description="WebSocket server port for MCP Bridge"
    )
    # Auto-start server when Blender loads
    bpy.app.handlers.load_post.append(_load_post_handler)
    # Also start on initial addon enable (no file load event)
    bpy.app.timers.register(_auto_start_server, first_interval=1.5)


def unregister():
    # Stop server if running
    from . import server
    server.stop_server()

    # Remove auto-start handler
    if _load_post_handler in bpy.app.handlers.load_post:
        bpy.app.handlers.load_post.remove(_load_post_handler)

    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
    del bpy.types.Scene.mcp_port


if __name__ == "__main__":
    register()
