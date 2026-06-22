"""
修复 Player_Rigged.blend 模型原点：将脚底对齐到 Z=0（地面）
通过 Blender MCP WebSocket 直连执行
"""
import socket
import struct
import hashlib
import base64
import os
import json

HOST = "127.0.0.1"
PORT = 9876
BLEND_PATH = r"C:\Users\Administrator\Desktop\Unity学习一\mygame\Player_Rigged.blend"
FBX_OUT_PATH = r"C:\Users\Administrator\Desktop\Unity学习一\mygame\Player_Rigged.fbx"


def ws_send(sock, message):
    """Send a WebSocket text frame WITH MASK (client must mask)"""
    data = message.encode("utf-8")
    frame = bytearray()
    frame.append(0x81)  # FIN + text opcode
    length = len(data)
    if length < 126:
        frame.append(0x80 | length)  # MASK bit set
    elif length < 65536:
        frame.append(0x80 | 126)
        frame.extend(struct.pack(">H", length))
    else:
        frame.append(0x80 | 127)
        frame.extend(struct.pack(">Q", length))

    mask_key = os.urandom(4)
    frame.extend(mask_key)

    masked = bytearray(length)
    for i in range(length):
        masked[i] = data[i] ^ mask_key[i % 4]
    frame.extend(masked)

    sock.sendall(bytes(frame))


def ws_recv(sock):
    """Receive a WebSocket frame (server frames are unmasked)"""
    # Read first 2 bytes
    header = b""
    while len(header) < 2:
        b = sock.recv(1)
        if not b:
            return None
        header += b

    byte1, byte2 = header[0], header[1]
    opcode = byte1 & 0x0F

    if opcode == 0x09:  # ping
        # Send pong with same payload
        length = byte2 & 0x7F
        payload = b""
        if length > 0:
            while len(payload) < length:
                payload += sock.recv(length - len(payload))
        pong = bytearray([0x8A, length])
        pong.extend(payload)
        sock.sendall(bytes(pong))
        return ws_recv(sock)  # retry

    if opcode == 0x08:  # close
        return None

    length = byte2 & 0x7F
    if length == 126:
        length = struct.unpack(">H", recv_exact(sock, 2))[0]
    elif length == 127:
        length = struct.unpack(">Q", recv_exact(sock, 8))[0]

    payload = recv_exact(sock, length)
    return bytes(payload).decode("utf-8", errors="replace")


def recv_exact(sock, n):
    data = b""
    while len(data) < n:
        chunk = sock.recv(n - len(data))
        if not chunk:
            raise ConnectionError("Connection closed")
        data += chunk
    return data


def call_blender_tool(sock, method, params=None):
    """Call a Blender MCP tool via WebSocket"""
    msg = {
        "id": 1,
        "method": method,
        "params": params or {},
    }
    ws_send(sock, json.dumps(msg))
    response = ws_recv(sock)
    if response:
        return json.loads(response)
    return None


# ── Blender Python 修复脚本 ──

FIX_SCRIPT = r'''
import bpy

print("=== 修复模型原点：脚底对齐 Z=0 ===")

blend_path = r"C:\Users\Administrator\Desktop\Unity学习一\mygame\Player_Rigged.blend"
fbx_out = r"C:\Users\Administrator\Desktop\Unity学习一\mygame\Player_Rigged.fbx"

# 打开文件
bpy.ops.wm.open_mainfile(filepath=blend_path)
print(f"已打开: {blend_path}")

# 找 Armature 和 Mesh
arm_obj = mesh_obj = None
for obj in bpy.data.objects:
    if obj.type == 'ARMATURE':
        arm_obj = obj
    elif obj.type == 'MESH':
        mesh_obj = obj

if not arm_obj:
    print("ERROR: 找不到 Armature!")
elif not mesh_obj:
    print("ERROR: 找不到 Mesh!")
else:
    print(f"Armature: {arm_obj.name}, Mesh: {mesh_obj.name}")

    # 计算 mesh 世界空间最低点
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    eval_obj = mesh_obj.evaluated_get(depsgraph)
    me = eval_obj.to_mesh()
    min_z = min(v.co.z for v in me.vertices)
    eval_obj.to_mesh_clear()
    print(f"Mesh 最低点 Z = {min_z:.4f} (世界空间)")

    # 下移到脚底在 Z=0
    offset = -min_z
    print(f"偏移量: {offset:.4f}")

    bpy.ops.object.mode_set(mode='OBJECT')

    # 移动 Armature
    arm_obj.location.z += offset
    bpy.context.view_layer.update()

    # 应用变换
    bpy.ops.object.select_all(action='DESELECT')
    arm_obj.select_set(True)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # 保存 .blend
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    print("OK: .blend saved")

    # 导出 FBX
    bpy.ops.object.select_all(action='DESELECT')
    mesh_obj.select_set(True)
    arm_obj.select_set(True)
    bpy.context.view_layer.objects.active = arm_obj

    bpy.ops.export_scene.fbx(
        filepath=fbx_out,
        use_selection=True,
        object_types={'ARMATURE', 'MESH'},
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
    )
    print(f"OK: FBX exported -> {fbx_out}")

print("=== DONE ===")
'''

print("连接 Blender WebSocket...")
sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
sock.settimeout(60)
sock.connect((HOST, PORT))

# HTTP Upgrade handshake
key = base64.b64encode(os.urandom(16)).decode()
request = (
    f"GET / HTTP/1.1\r\n"
    f"Host: {HOST}:{PORT}\r\n"
    f"Upgrade: websocket\r\n"
    f"Connection: Upgrade\r\n"
    f"Sec-WebSocket-Key: {key}\r\n"
    f"Sec-WebSocket-Version: 13\r\n"
    f"\r\n"
)
sock.sendall(request.encode())
response_data = b""
while b"\r\n\r\n" not in response_data:
    chunk = sock.recv(4096)
    if not chunk:
        break
    response_data += chunk

if b"101" not in response_data:
    print(f"Handshake FAILED: {response_data[:200]}")
    sock.close()
    exit(1)

print("WebSocket connected!")

# 执行修复
result = call_blender_tool(sock, "blender_execute_python", {"code": FIX_SCRIPT})
print(json.dumps(result, indent=2, ensure_ascii=False))

sock.close()
print("\nDone! Check Player_Rigged.fbx in Unity.")
