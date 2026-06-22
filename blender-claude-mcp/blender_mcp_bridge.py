#!/usr/bin/env python3
"""blender_mcp_bridge.py — MCP Bridge between Claude Code and Blender.

Architecture:
  Claude Code (MCP Client) ←→ stdin/stdout JSON-RPC ←→ this bridge
    ←→ WebSocket client ←→ Blender Addon (127.0.0.1:9876)
    ←→ Qwen-VL-Max API (visual translation layer)

Key behavior:
  - When a tool call returns image_base64, the bridge automatically calls
    Qwen-VL-Max to generate a text description. DeepSeek (text-only) reads
    this description to "see" the Blender viewport.
  - Connection to Blender is lazy (connects on first tool call) and
    auto-reconnects on failure.
  - All 24 MCP tools are defined inline with JSON Schema parameters.
"""

import json
import os
import sys
import socket
import struct
import hashlib
import base64
import time
import random
import string
import traceback
import http.client
import ssl


# ═══════════════════════════════════════════════════════════════
#  WebSocket client (sync, stdlib only)
# ═══════════════════════════════════════════════════════════════

class BlenderWSClient:
    """Synchronous RFC 6455 WebSocket client to Blender addon."""

    def __init__(self, host="127.0.0.1", port=9876):
        self.host = host
        self.port = port
        self.sock = None
        self._connected = False

    def connect(self, timeout=5.0):
        """Connect to Blender WebSocket server with HTTP upgrade."""
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.sock.settimeout(timeout)
        self.sock.connect((self.host, self.port))

        key_bytes = base64.b64encode(hashlib.sha1(os.urandom(16)).digest()).decode()
        request = (
            f"GET / HTTP/1.1\r\n"
            f"Host: {self.host}:{self.port}\r\n"
            f"Upgrade: websocket\r\n"
            f"Connection: Upgrade\r\n"
            f"Sec-WebSocket-Key: {key_bytes}\r\n"
            f"Sec-WebSocket-Version: 13\r\n"
            f"\r\n"
        )
        self.sock.sendall(request.encode())

        response = b""
        while b"\r\n\r\n" not in response and len(response) < 16384:
            chunk = self.sock.recv(4096)
            if not chunk:
                raise ConnectionError("No handshake response")
            response += chunk

        if b"101" not in response:
            raise ConnectionError(f"Handshake failed: {response[:200]}")

        self._connected = True

    def call(self, method, params=None):
        """Send a method call and return the response dict."""
        req_id = "".join(random.choices(string.ascii_lowercase + string.digits, k=10))
        msg = json.dumps({"id": req_id, "method": method, "params": params or {}})
        self._send_text(msg)
        resp_str = self._recv_text()
        resp = json.loads(resp_str)
        return resp.get("result", {"success": False, "error": "No result"})

    def close(self):
        """Close the WebSocket connection."""
        if self.sock:
            try:
                frame = bytearray([0x88, 0x00])  # close frame
                self.sock.sendall(frame)
            except Exception:
                pass
            try:
                self.sock.close()
            except Exception:
                pass
        self.sock = None
        self._connected = False

    @property
    def is_connected(self):
        return self._connected

    # ── frame-level I/O ─────────────────────────────────────

    def _send_text(self, data):
        payload = data.encode("utf-8") if isinstance(data, str) else data
        frame = bytearray([0x81])  # FIN + TEXT
        plen = len(payload)
        if plen < 126:
            frame.append(0x80 | plen)
        elif plen < 65536:
            frame.append(0x80 | 126)
            frame.extend(struct.pack(">H", plen))
        else:
            frame.append(0x80 | 127)
            frame.extend(struct.pack(">Q", plen))

        mask_key = os.urandom(4)
        frame.extend(mask_key)
        frame.extend(bytes(b ^ mask_key[i % 4] for i, b in enumerate(payload)))
        self.sock.sendall(bytes(frame))

    def _recv_text(self):
        while True:
            opcode, payload = self._recv_frame()
            if opcode == 0x01:  # TEXT
                return payload.decode("utf-8")
            elif opcode == 0x08:  # CLOSE
                raise ConnectionError("Server closed WebSocket")
            elif opcode == 0x09:  # PING
                self._send_frame(0x0A, payload)  # PONG
            # else: continue

    def _recv_frame(self):
        header = self._recv_exact(2)
        byte1, byte2 = header[0], header[1]
        opcode = byte1 & 0x0F
        masked = (byte2 & 0x80) != 0
        length = byte2 & 0x7F

        if length == 126:
            length = struct.unpack(">H", self._recv_exact(2))[0]
        elif length == 127:
            length = struct.unpack(">Q", self._recv_exact(8))[0]

        mask_key = self._recv_exact(4) if masked else b""
        payload = self._recv_exact(length)
        if masked:
            payload = bytes(b ^ mask_key[i % 4] for i, b in enumerate(payload))
        return opcode, payload

    def _send_frame(self, opcode, payload):
        frame = bytearray([0x80 | opcode])     # FIN + opcode
        plen = len(payload)
        if plen < 126:
            frame.append(0x80 | plen)
        elif plen < 65536:
            frame.append(0x80 | 126)
            frame.extend(struct.pack(">H", plen))
        else:
            frame.append(0x80 | 127)
            frame.extend(struct.pack(">Q", plen))
        mask_key = os.urandom(4)
        frame.extend(mask_key)
        frame.extend(bytes(b ^ mask_key[i % 4] for i, b in enumerate(payload)))
        self.sock.sendall(bytes(frame))

    def _recv_exact(self, n):
        data = b""
        while len(data) < n:
            chunk = self.sock.recv(min(n - len(data), 65536))
            if not chunk:
                raise ConnectionError("Connection closed")
            data += chunk
        return data


# ═══════════════════════════════════════════════════════════════
#  Vision translator — Qwen-VL-Max
# ═══════════════════════════════════════════════════════════════

class VisionTranslator:
    """Image → text description via Qwen-VL-Max (DashScope / Alibaba)."""

    def __init__(self, api_key=None):
        self.api_key = api_key or os.environ.get("DASHSCOPE_API_KEY", "")

    def describe(self, image_base64, context=""):
        """Send a screenshot to Qwen-VL and return a text description."""
        if not self.api_key:
            return "[视觉分析不可用] 请设置 DASHSCOPE_API_KEY 环境变量"

        prompt = (
            f"请详细描述这张Blender 3D视口的截图内容。{context}"
            f"注意以下细节：模型姿态、是否穿模、布料位置、材质表现、光照情况、骨骼姿态。"
            f"用中文回答，尽量具体。"
        )

        body = json.dumps({
            "model": "qwen-vl-max",
            "messages": [{
                "role": "user",
                "content": [
                    {"type": "image_url",
                     "image_url": {"url": f"data:image/png;base64,{image_base64}"}},
                    {"type": "text", "text": prompt}
                ]
            }],
            "max_tokens": 800,
        })

        try:
            ctx = ssl.create_default_context()
            conn = http.client.HTTPSConnection("dashscope.aliyuncs.com", timeout=30,
                                               context=ctx)
            headers = {
                "Content-Type": "application/json",
                "Authorization": f"Bearer {self.api_key}",
            }
            conn.request("POST", "/compatible-mode/v1/chat/completions",
                         body, headers)
            resp = conn.getresponse()
            raw = resp.read().decode("utf-8")
            conn.close()

            data = json.loads(raw)
            if "choices" in data and len(data["choices"]) > 0:
                return data["choices"][0]["message"]["content"]
            elif "error" in data:
                return f"[视觉API错误] {data['error'].get('message', data['error'])}"
            else:
                return f"[视觉API异常响应] {raw[:300]}"
        except Exception as e:
            return f"[视觉分析异常] {e}"


# ═══════════════════════════════════════════════════════════════
#  Tool definitions (24 tools)
# ═══════════════════════════════════════════════════════════════

TOOLS = [
    # ── Connection ──
    {
        "name": "blender_get_connection_status",
        "description": "获取 Blender 连接状态和场景基本信息。验证 Bridge ↔ Blender 是否正常通信。",
        "inputSchema": {"type": "object", "properties": {}, "required": []},
    },
    # ── Capture (4) ──
    {
        "name": "blender_capture_viewport",
        "description": "截取当前 3D 视口画面，返回 base64 图片。截图会自动经过 Qwen-VL 翻译为文字描述供主模型理解。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "camera": {"type": "string", "description": "相机名称，默认为当前活动相机"},
                "width": {"type": "integer", "default": 1920, "description": "截图宽度"},
                "height": {"type": "integer", "default": 1080, "description": "截图高度"},
            },
        },
    },
    {
        "name": "blender_capture_animation_frame",
        "description": "跳转到指定帧并截图，完成后恢复原帧。自动经过 Qwen-VL 翻译。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "frame": {"type": "integer", "description": "目标帧号"},
                "camera": {"type": "string", "description": "相机名称"},
                "width": {"type": "integer", "default": 1920},
                "height": {"type": "integer", "default": 1080},
            },
            "required": ["frame"],
        },
    },
    {
        "name": "blender_capture_multiview",
        "description": "从 4 个标准角度截图（前/右/上/透视），用于全面诊断。每张图都会翻译。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "frame": {"type": "integer", "description": "帧号，默认当前帧"},
                "width": {"type": "integer", "default": 1024},
                "height": {"type": "integer", "default": 768},
            },
        },
    },
    {
        "name": "blender_capture_clipping_region",
        "description": "聚焦到指定对象并截图。用于近距离检查模型细节。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "object_name": {"type": "string", "description": "目标对象名称"},
                "margin": {"type": "number", "default": 0.2},
                "width": {"type": "integer", "default": 1024},
                "height": {"type": "integer", "default": 768},
            },
            "required": ["object_name"],
        },
    },
    # ── Inspection (8) ──
    {
        "name": "blender_list_objects",
        "description": "列出场景中所有对象及其类型、位置、顶点数等基本信息。",
        "inputSchema": {"type": "object", "properties": {}, "required": []},
    },
    {
        "name": "blender_list_bones",
        "description": "列出骨骼的层级结构、位置和姿态信息。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "armature_name": {"type": "string", "description": "骨骼对象名称，默认第一个"},
            },
        },
    },
    {
        "name": "blender_list_modifiers",
        "description": "列出对象的所有修改器及其设置。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "object_name": {"type": "string", "description": "目标对象名称"},
            },
            "required": ["object_name"],
        },
    },
    {
        "name": "blender_get_cloth_settings",
        "description": "获取布料修改器的详细设置。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "object_name": {"type": "string", "description": "有布料修改器的对象"},
            },
            "required": ["object_name"],
        },
    },
    {
        "name": "blender_get_collision_settings",
        "description": "获取碰撞修改器的详细设置。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "object_name": {"type": "string", "description": "有碰撞修改器的对象"},
            },
            "required": ["object_name"],
        },
    },
    {
        "name": "blender_list_constraints",
        "description": "列出骨骼或对象的约束。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "armature_name": {"type": "string"},
                "bone_name": {"type": "string", "description": "特定骨骼名，需配合 armature_name"},
            },
        },
    },
    {
        "name": "blender_get_animation_data",
        "description": "获取动画数据：动作名、帧范围、关键帧数量。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "object_name": {"type": "string", "description": "目标对象，默认活动对象"},
            },
        },
    },
    {
        "name": "blender_inspect_vertex_weights",
        "description": "检查顶点组的权重数据。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "object_name": {"type": "string", "description": "网格对象名称"},
                "vertex_group_name": {"type": "string", "description": "顶点组名称，不指定则列出所有"},
            },
            "required": ["object_name"],
        },
    },
    # ── Modification (8) ──
    {
        "name": "blender_add_modifier",
        "description": "给对象添加修改器。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "object_name": {"type": "string", "description": "目标对象"},
                "modifier_type": {"type": "string", "description": "修改器类型，如 COLLISION, CLOTH, SUBSURF, SOLIDIFY"},
                "settings": {"type": "object", "description": "修改器设置键值对"},
            },
            "required": ["object_name", "modifier_type"],
        },
    },
    {
        "name": "blender_set_cloth_settings",
        "description": "修改布料修改器的设置。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "object_name": {"type": "string", "description": "有布料修改器的对象"},
                "settings": {"type": "object", "description": "要修改的设置键值对，如 {'collision_quality': 5, 'distance_min': 0.015}"},
            },
            "required": ["object_name", "settings"],
        },
    },
    {
        "name": "blender_set_collision_settings",
        "description": "修改碰撞修改器的设置。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "object_name": {"type": "string", "description": "有碰撞修改器的对象"},
                "settings": {"type": "object", "description": "碰撞设置键值对"},
            },
            "required": ["object_name", "settings"],
        },
    },
    {
        "name": "blender_set_bone_transform",
        "description": "设置骨骼的变换（位置/旋转/缩放）。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "armature_name": {"type": "string", "description": "骨骼对象名称"},
                "bone_name": {"type": "string", "description": "骨骼名"},
                "location": {"type": "array", "items": {"type": "number"}, "description": "[x, y, z]"},
                "rotation_euler": {"type": "array", "items": {"type": "number"}, "description": "[x, y, z] 弧度"},
                "rotation_quaternion": {"type": "array", "items": {"type": "number"}, "description": "[w, x, y, z]"},
                "scale": {"type": "array", "items": {"type": "number"}, "description": "[x, y, z]"},
            },
            "required": ["armature_name", "bone_name"],
        },
    },
    {
        "name": "blender_set_vertex_weights",
        "description": "设置顶点组的权重值。适合少量修改，大批量请用 blender_execute_python。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "object_name": {"type": "string", "description": "网格对象"},
                "vertex_group_name": {"type": "string", "description": "顶点组名（不存在则创建）"},
                "weights": {"description": "权重列表 [[顶点索引, 权重], ...] 或 {顶点索引: 权重, ...}"},
            },
            "required": ["object_name", "vertex_group_name", "weights"],
        },
    },
    {
        "name": "blender_add_bone_constraint",
        "description": "给骨骼添加约束。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "armature_name": {"type": "string"},
                "bone_name": {"type": "string"},
                "constraint_type": {"type": "string", "description": "约束类型，如 COPY_ROTATION, IK, LIMIT_ROTATION"},
                "settings": {"type": "object", "description": "约束设置"},
            },
            "required": ["armature_name", "bone_name", "constraint_type"],
        },
    },
    {
        "name": "blender_set_constraint_settings",
        "description": "修改已有约束的设置。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "armature_name": {"type": "string"},
                "bone_name": {"type": "string"},
                "constraint_name": {"type": "string"},
                "settings": {"type": "object", "description": "要修改的设置"},
            },
            "required": ["armature_name", "bone_name", "constraint_name", "settings"],
        },
    },
    {
        "name": "blender_create_collision_proxy",
        "description": "创建简化的碰撞体代理对象（盒子/球/圆柱/胶囊）。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "object_name": {"type": "string", "description": "参考对象（用于定位和自动尺寸）"},
                "proxy_type": {"type": "string", "default": "BOX", "enum": ["BOX", "SPHERE", "CYLINDER", "CAPSULE"]},
                "size": {"type": "array", "items": {"type": "number"}, "description": "手动指定 [x, y, z] 尺寸"},
                "offset": {"type": "array", "items": {"type": "number"}, "description": "[x, y, z] 偏移"},
            },
            "required": ["object_name"],
        },
    },
    # ── Animation (5) ──
    {
        "name": "blender_set_frame",
        "description": "跳转到指定帧。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "frame": {"type": "integer", "description": "目标帧号"},
            },
            "required": ["frame"],
        },
    },
    {
        "name": "blender_set_frame_range",
        "description": "设置时间轴帧范围。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "frame_start": {"type": "integer", "description": "起始帧"},
                "frame_end": {"type": "integer", "description": "结束帧"},
            },
            "required": ["frame_start", "frame_end"],
        },
    },
    {
        "name": "blender_bake_simulation",
        "description": "烘焙物理模拟（布料、软体等）。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "object_name": {"type": "string", "description": "有物理修改器的对象"},
                "frame_start": {"type": "integer", "description": "起始帧（默认场景起始）"},
                "frame_end": {"type": "integer", "description": "结束帧（默认场景结束）"},
            },
            "required": ["object_name"],
        },
    },
    {
        "name": "blender_bake_action",
        "description": "将对象动画烘焙为关键帧。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "object_name": {"type": "string", "description": "目标对象"},
                "frame_start": {"type": "integer"},
                "frame_end": {"type": "integer"},
            },
        },
    },
    {
        "name": "blender_play_animation",
        "description": "切换动画播放（开始/停止）。",
        "inputSchema": {"type": "object", "properties": {}, "required": []},
    },
    # ── Execution + Files (3) ──
    {
        "name": "blender_execute_python",
        "description": "在 Blender 中执行任意 Python 代码。万能工具，可访问 bpy/bmesh/mathutils。返回 stdout 输出。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "code": {"type": "string", "description": "要执行的 Python 代码"},
            },
            "required": ["code"],
        },
    },
    {
        "name": "blender_save_file",
        "description": "保存当前 .blend 文件。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "filepath": {"type": "string", "description": "保存路径，默认覆盖当前文件"},
            },
        },
    },
    {
        "name": "blender_undo",
        "description": "撤销上一步操作。",
        "inputSchema": {"type": "object", "properties": {}, "required": []},
    },
]

# Screenshot tools that trigger Qwen-VL translation
SCREENSHOT_TOOLS = {
    "blender_capture_viewport",
    "blender_capture_animation_frame",
    "blender_capture_multiview",
    "blender_capture_clipping_region",
}


# ═══════════════════════════════════════════════════════════════
#  MCP Bridge — main request handler
# ═══════════════════════════════════════════════════════════════

class MCPBridge:
    """Handles MCP JSON-RPC requests from stdin, forwards to Blender via WS."""

    def __init__(self):
        self.host = os.environ.get("BLENDER_MCP_HOST", "127.0.0.1")
        self.port = int(os.environ.get("BLENDER_MCP_PORT", "9876"))
        self._client = None
        self._translator = VisionTranslator()
        self._reconnect_attempts = 0
        self._max_reconnect = 3

    @property
    def client(self):
        """Lazy WS connection with auto-reconnect."""
        if self._client is None or not self._client.is_connected:
            self._connect()
        return self._client

    def _connect(self):
        """Connect (or reconnect) to Blender WebSocket."""
        for i in range(self._max_reconnect):
            try:
                if self._client:
                    self._client.close()
                self._client = BlenderWSClient(self.host, self.port)
                self._client.connect(timeout=5.0)
                self._reconnect_attempts = 0
                return
            except Exception as e:
                self._reconnect_attempts += 1
                if i < self._max_reconnect - 1:
                    time.sleep(0.5)
        raise ConnectionError(
            f"无法连接到 Blender (ws://{self.host}:{self.port})，"
            f"请确认 Blender 中的 MCP 服务器已启动"
        )

    def handle_request(self, raw_request):
        """Process one JSON-RPC request. Returns dict to send via stdout, or None."""
        try:
            request = json.loads(raw_request)
        except json.JSONDecodeError:
            return self._error(None, -32700, "Parse error")

        method = request.get("method", "")
        req_id = request.get("id")

        try:
            if method == "initialize":
                return self._handle_initialize(req_id, request.get("params", {}))
            elif method == "tools/list":
                return self._handle_tools_list(req_id)
            elif method == "tools/call":
                return self._handle_tools_call(req_id, request.get("params", {}))
            elif method == "notifications/initialized":
                return None  # No response for notifications
            else:
                return self._error(req_id, -32601, f"Method not found: {method}")
        except ConnectionError as e:
            return self._error(req_id, -32000, f"Blender 连接丢失: {e}")
        except Exception as e:
            return self._error(req_id, -32603, f"Internal error: {e}\n{traceback.format_exc()}")

    def _handle_initialize(self, req_id, params):
        return {
            "jsonrpc": "2.0",
            "id": req_id,
            "result": {
                "protocolVersion": "2024-11-05",
                "serverInfo": {
                    "name": "blender-mcp-bridge",
                    "version": "1.0.0",
                },
                "capabilities": {
                    "tools": {},
                },
            },
        }

    def _handle_tools_list(self, req_id):
        return {
            "jsonrpc": "2.0",
            "id": req_id,
            "result": {"tools": TOOLS},
        }

    def _handle_tools_call(self, req_id, params):
        tool_name = params.get("name", "")
        arguments = params.get("arguments", {})

        # Call Blender via WebSocket
        result = self.client.call(tool_name, arguments)

        # Build MCP content array
        content = []

        is_screenshot = tool_name in SCREENSHOT_TOOLS

        if is_screenshot and result.get("success"):
            # Translate screenshot(s) via Qwen-VL
            if "image_base64" in result:
                ctx = (f"视角: {result.get('camera_used', 'Unknown')}, "
                       f"帧: {result.get('frame', 'current')}")
                visual_text = self._translator.describe(result["image_base64"], ctx)
                content.append({"type": "text", "text": f"[视觉分析] {visual_text}"})
                content.append({"type": "image", "data": result["image_base64"],
                                "mimeType": "image/png"})
                content.append({"type": "text", "text": json.dumps({
                    k: v for k, v in result.items()
                    if k not in ("image_base64", "success") and v is not None
                }, indent=2, ensure_ascii=False)})
            elif "views" in result:
                # Multiview
                descriptions = []
                for view_name, img_b64 in result["views"].items():
                    vtext = self._translator.describe(
                        img_b64, f"视角: {view_name}, 帧: {result.get('frame', 'current')}")
                    descriptions.append(f"[{view_name}] {vtext}")
                    content.append({"type": "image", "data": img_b64,
                                    "mimeType": "image/png"})
                content.insert(0, {"type": "text",
                                   "text": "[视觉分析-多视图]\n" + "\n\n".join(descriptions)})
            else:
                content.append({"type": "text",
                                "text": json.dumps(result, indent=2, ensure_ascii=False)})
        else:
            content.append({"type": "text",
                            "text": json.dumps(result, indent=2, ensure_ascii=False)})

        return {
            "jsonrpc": "2.0",
            "id": req_id,
            "result": {"content": content},
        }

    @staticmethod
    def _error(req_id, code, message):
        return {
            "jsonrpc": "2.0",
            "id": req_id,
            "error": {
                "code": code,
                "message": message,
            },
        }


# ═══════════════════════════════════════════════════════════════
#  Main — stdio JSON-RPC loop
# ═══════════════════════════════════════════════════════════════

def main():
    """Read MCP requests from stdin, write responses to stdout."""
    # Ensure stdout is unbuffered for MCP protocol
    sys.stdout.reconfigure(line_buffering=True) if hasattr(sys.stdout, 'reconfigure') else None
    sys.stderr.reconfigure(line_buffering=True) if hasattr(sys.stderr, 'reconfigure') else None

    bridge = MCPBridge()

    # Log startup
    log = lambda msg: print(msg, file=sys.stderr, flush=True)
    log(f"[MCP Bridge] Starting on {bridge.host}:{bridge.port}")
    log(f"[MCP Bridge] Qwen-VL: {'enabled' if bridge._translator.api_key else 'DISABLED'}")

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue

        response = bridge.handle_request(line)
        if response is not None:
            sys.stdout.write(json.dumps(response, ensure_ascii=False) + "\n")
            sys.stdout.flush()

    log("[MCP Bridge] stdin closed, shutting down")
    if bridge._client:
        bridge._client.close()


if __name__ == "__main__":
    main()
