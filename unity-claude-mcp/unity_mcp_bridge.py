#!/usr/bin/env python3
"""unity_mcp_bridge.py — MCP Bridge between Claude Code and Unity Editor.

Architecture:
  Claude Code (MCP Client) ←→ stdin/stdout JSON-RPC ←→ this bridge
    ←→ WebSocket client ←→ Unity Editor McpServer (127.0.0.1:9877)
    ←→ Qwen-VL-Max API (visual translation layer)

Key behavior:
  - When a tool call returns image_base64, the bridge automatically calls
    Qwen-VL-Max to generate a text description. DeepSeek (text-only) reads
    this description to "see" the Unity viewport.
  - Connection to Unity is lazy (connects on first tool call) and
    auto-reconnects on failure.
  - All MCP tools are defined inline with JSON Schema parameters.
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

class UnityWSClient:
    """Synchronous RFC 6455 WebSocket client to Unity Editor McpServer."""

    def __init__(self, host="127.0.0.1", port=9877):
        self.host = host
        self.port = port
        self.sock = None
        self._connected = False

    def connect(self, timeout=5.0):
        """Connect to Unity WebSocket server with HTTP upgrade."""
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
        # Unity returns the result directly; if there IS a "result" wrapper, unwrap it
        if "result" in resp and len(resp) <= 3:  # JSON-RPC style response
            return resp["result"]
        return resp

    def close(self):
        """Close the WebSocket connection."""
        if self.sock:
            try:
                frame = bytearray([0x88, 0x00])
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
        frame = bytearray([0x80 | opcode])
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
        # Temporarily increase timeout for large payloads
        old_timeout = self.sock.gettimeout()
        if n > 10000:
            self.sock.settimeout(max(old_timeout or 5, 30.0))
        try:
            while len(data) < n:
                chunk = self.sock.recv(min(n - len(data), 65536))
                if not chunk:
                    raise ConnectionError("Connection closed")
                data += chunk
        finally:
            if n > 10000:
                self.sock.settimeout(old_timeout)
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
            f"请详细描述这张Unity编辑器或游戏画面的截图内容。{context}"
            f"注意以下细节：UI布局、元素位置、颜色搭配、文字内容、"
            f"3D场景中的物体位置和姿态、Canvas元素、字体大小、"
            f"间距对齐问题、任何明显的视觉问题。"
            f"用中文回答，尽量具体，列出所有可识别的UI元素和位置。"
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
            "max_tokens": 1000,
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
#  Tool definitions
# ═══════════════════════════════════════════════════════════════

TOOLS = [
    # ── Connection ──
    {
        "name": "unity_get_status",
        "description": "获取 Unity 编辑器连接状态。返回 Play/Edit 模式、场景名、对象数量等信息。验证 Bridge ↔ Unity 是否正常通信。",
        "inputSchema": {"type": "object", "properties": {}, "required": []},
    },
    # ── Capture (4) ──
    {
        "name": "unity_capture_editor",
        "description": "【推荐】截取 Unity 当前画面。Play 模式截图 Game View，Edit 模式截图 Scene View。截图自动经过 Qwen-VL 翻译为文字描述。这是最重要的工具——每次修改前后都应该截图确认效果。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "width": {"type": "integer", "default": 1920, "description": "截图宽度"},
                "height": {"type": "integer", "default": 1080, "description": "截图高度"},
            },
        },
    },
    {
        "name": "unity_capture_game",
        "description": "截取 Game View（仅 Play 模式可用）。含 UI 叠加层。自动经过 Qwen-VL 翻译。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "width": {"type": "integer", "default": 1920},
                "height": {"type": "integer", "default": 1080},
            },
        },
    },
    {
        "name": "unity_capture_scene",
        "description": "截取 Scene View（Edit/Play 均可）。Scene 视口相机视角。自动经过 Qwen-VL 翻译。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "width": {"type": "integer", "default": 1920},
                "height": {"type": "integer", "default": 1080},
            },
        },
    },
    {
        "name": "unity_capture_to_file",
        "description": "【大图专用】截图直接保存到文件（绕过 base64 WebSocket 传输限制）。支持任意分辨率。返回文件路径。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "width": {"type": "integer", "default": 1920, "description": "截图宽度"},
                "height": {"type": "integer", "default": 1080, "description": "截图高度"},
                "path": {"type": "string", "description": "保存路径（可选，默认临时目录）"},
            },
        },
    },
    # ── Inspection (4) ──
    {
        "name": "unity_list_objects",
        "description": "列出场景中的顶层 GameObject（根对象）。返回名称、激活状态、子对象数、组件数。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "max_count": {"type": "integer", "default": 100, "description": "最大返回数量"},
            },
        },
    },
    {
        "name": "unity_get_object_info",
        "description": "获取指定 GameObject 的详细信息：Transform（位置/旋转/缩放）、组件列表及公开字段值。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "name": {"type": "string", "description": "GameObject 名称"},
            },
            "required": ["name"],
        },
    },
    {
        "name": "unity_list_components",
        "description": "列出指定 GameObject 上的所有组件及其启用状态。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "name": {"type": "string", "description": "GameObject 名称"},
            },
            "required": ["name"],
        },
    },
    {
        "name": "unity_find_objects",
        "description": "按名称模式或组件类型搜索 GameObject。返回匹配对象的路径。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "pattern": {"type": "string", "description": "名称搜索模式（支持部分匹配）"},
                "type": {"type": "string", "description": "组件类型名称（可选）"},
                "max_count": {"type": "integer", "default": 50},
            },
        },
    },
    # ── Modification (6) ──
    {
        "name": "unity_create_object",
        "description": "创建空 GameObject。返回实例 ID。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "name": {"type": "string", "description": "GameObject 名称"},
                "x": {"type": "number", "default": 0},
                "y": {"type": "number", "default": 0},
                "z": {"type": "number", "default": 0},
            },
            "required": ["name"],
        },
    },
    {
        "name": "unity_delete_object",
        "description": "删除指定 GameObject（可通过 Undo 恢复）。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "name": {"type": "string", "description": "GameObject 名称"},
            },
            "required": ["name"],
        },
    },
    {
        "name": "unity_set_transform",
        "description": "设置 GameObject 的 Transform。仅传需要修改的坐标/旋转/缩放，其他保持不变。参数命名: px/py/pz=位置, rx/ry/rz=旋转(欧拉角), sx/sy/sz=缩放。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "name": {"type": "string", "description": "GameObject 名称"},
                "px": {"type": "number"}, "py": {"type": "number"}, "pz": {"type": "number"},
                "rx": {"type": "number"}, "ry": {"type": "number"}, "rz": {"type": "number"},
                "sx": {"type": "number"}, "sy": {"type": "number"}, "sz": {"type": "number"},
            },
            "required": ["name"],
        },
    },
    {
        "name": "unity_set_component_field",
        "description": "设置 GameObject 上指定组件的字段值。支持 float/int/bool/string/Vector3/Color 类型自动转换。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "object": {"type": "string", "description": "GameObject 名称"},
                "component": {"type": "string", "description": "组件类型全名，如 _Game.Systems.Weapon.WeaponAiming"},
                "field": {"type": "string", "description": "字段名"},
                "value": {"type": "string", "description": "值（字符串形式，自动转换类型）"},
            },
            "required": ["object", "component", "field", "value"],
        },
    },
    {
        "name": "unity_duplicate_object",
        "description": "复制 GameObject。副本在相同位置，命名为 name_copy。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "name": {"type": "string", "description": "源 GameObject 名称"},
            },
            "required": ["name"],
        },
    },
    {
        "name": "unity_set_parent",
        "description": "设置父对象。传 parent=\"null\" 或 \"root\" 取消父级。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "child": {"type": "string", "description": "子对象名称"},
                "parent": {"type": "string", "description": "父对象名称，或 \"null\" / \"root\" 取消父级"},
            },
            "required": ["child", "parent"],
        },
    },
    # ── Execution (2) ──
    {
        "name": "unity_execute_code",
        "description": "在 Unity Editor 上下文中编译并执行 C# 代码。可引用 UnityEngine/UnityEditor 及项目程序集。用于批量操作或复杂修改。返回执行结果或编译错误。",
        "inputSchema": {
            "type": "object",
            "properties": {
                "code": {"type": "string", "description": "要执行的 C# 代码。不要包含 class/method 包装，直接写语句体。"},
            },
            "required": ["code"],
        },
    },
    {
        "name": "unity_undo",
        "description": "执行一次 Undo 操作。",
        "inputSchema": {"type": "object", "properties": {}, "required": []},
    },
]

SCREENSHOT_TOOLS = {
    "unity_capture_editor",
    "unity_capture_game",
    "unity_capture_scene",
}


# ═══════════════════════════════════════════════════════════════
#  MCP Bridge
# ═══════════════════════════════════════════════════════════════

class MCPBridge:
    """MCP protocol handler. Manages Unity WebSocket connection + Qwen-VL."""

    def __init__(self, host="127.0.0.1", port=9877):
        self.host = host
        self.port = port
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
        """Connect (or reconnect) to Unity WebSocket."""
        for i in range(self._max_reconnect):
            try:
                if self._client:
                    self._client.close()
                self._client = UnityWSClient(self.host, self.port)
                self._client.connect(timeout=5.0)
                self._reconnect_attempts = 0
                return
            except Exception as e:
                self._reconnect_attempts += 1
                if i < self._max_reconnect - 1:
                    time.sleep(0.5)
        raise ConnectionError(
            f"无法连接到 Unity (ws://{self.host}:{self.port})，"
            f"请确认 Unity 编辑器已打开且 MCP Server 正在运行"
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
            return self._error(req_id, -32000, f"Unity 连接丢失: {e}\n请确认 Unity 编辑器已打开")
        except Exception as e:
            return self._error(req_id, -32603, f"Internal error: {e}\n{traceback.format_exc()}")

    def _handle_initialize(self, req_id, params):
        return {
            "jsonrpc": "2.0",
            "id": req_id,
            "result": {
                "protocolVersion": "2024-11-05",
                "serverInfo": {
                    "name": "unity-mcp-bridge",
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

        # Call Unity via WebSocket
        result = self.client.call(tool_name, arguments)

        # Build MCP content array
        content = []

        is_screenshot = tool_name in SCREENSHOT_TOOLS

        if is_screenshot and result.get("success"):
            # Translate screenshot(s) via Qwen-VL
            if "image_base64" in result:
                ctx = (f"视角: {result.get('view', 'Unknown')}, "
                       f"Play模式: {result.get('play_mode', False)}, "
                       f"分辨率: {result.get('width', '?')}x{result.get('height', '?')}")
                visual_text = self._translator.describe(result["image_base64"], ctx)
                content.append({"type": "text", "text": f"[Unity 视觉分析] {visual_text}"})
                content.append({"type": "image", "data": result["image_base64"],
                                "mimeType": "image/png"})
                # Also include metadata
                content.append({"type": "text", "text": json.dumps({
                    k: v for k, v in result.items()
                    if k not in ("image_base64", "success") and v is not None
                }, indent=2, ensure_ascii=False)})
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
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(line_buffering=True)
    if hasattr(sys.stderr, 'reconfigure'):
        sys.stderr.reconfigure(line_buffering=True)

    bridge = MCPBridge()

    log = lambda msg: print(msg, file=sys.stderr, flush=True)
    log(f"[Unity MCP Bridge] Starting on {bridge.host}:{bridge.port}")
    log(f"[Unity MCP Bridge] Qwen-VL: {'enabled' if bridge._translator.api_key else 'DISABLED'}")

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue

        response = bridge.handle_request(line)
        if response is not None:
            sys.stdout.write(json.dumps(response, ensure_ascii=False) + "\n")
            sys.stdout.flush()

    log("[Unity MCP Bridge] stdin closed, shutting down")
    if bridge._client:
        bridge._client.close()


if __name__ == "__main__":
    main()
