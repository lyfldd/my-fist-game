"""Main-thread scheduler — bpy-safe timer loop.

WebSocket server runs in background thread and pushes requests into a queue.
This module's timer callback drains the queue on the main thread (where bpy is
safe to call), dispatches to the right handler, and pushes results back.
"""

import bpy

from .websocket import WebSocketServer
from .protocol import dispatch

_ws_server = None
_timer_handle = None


def start_server(host="127.0.0.1", port=9876):
    """Start WebSocket server + timer callback. Call from main thread."""
    global _ws_server, _timer_handle
    if _ws_server is not None:
        return

    _ws_server = WebSocketServer(host, port)
    _ws_server.start()
    _timer_handle = bpy.app.timers.register(_on_timer, first_interval=0.05, persistent=True)
    print(f"[MCP] Server started on {host}:{port}")


def stop_server():
    """Stop WebSocket server + timer callback."""
    global _ws_server, _timer_handle
    if _timer_handle is not None:
        bpy.app.timers.unregister(_timer_handle)
        _timer_handle = None
    if _ws_server is not None:
        _ws_server.stop()
        _ws_server = None
        print("[MCP] Server stopped")


def is_running():
    return _ws_server is not None and _ws_server._running


def get_request_count():
    """Rough count of pending requests (for UI display)."""
    if _ws_server is None:
        return 0
    return _ws_server.request_queue.qsize()


def _on_timer():
    """Called by Blender every ~0.05s on the main thread.

    Drains the request queue and dispatches each message. Returns the interval
    to keep the timer alive.
    """
    global _ws_server
    if _ws_server is None:
        return 0.05

    # Drain up to 5 requests per tick to avoid blocking the UI
    for _ in range(5):
        item = _ws_server.get_request()
        if item is None:
            break
        req_id, method, params = item
        result = dispatch(method, params)
        _ws_server.send_response(req_id, result)

    return 0.05
