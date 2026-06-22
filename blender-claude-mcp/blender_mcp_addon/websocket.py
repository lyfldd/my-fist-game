"""RFC 6455 WebSocket Server — pure Python stdlib, zero external dependencies.

Runs in a background thread. Communicates with main thread via:
  - `request_queue`: WebSocket → Main, carries (request_id, method, params)
  - `send_response()`: called from Main thread, sends back via WebSocket
"""

import socket
import struct
import hashlib
import base64
import json
import threading
import queue

WS_MAGIC = b"258EAFA5-E914-47DA-95CA-C5AB0DC85B11"

OP_TEXT = 0x01
OP_CLOSE = 0x08
OP_PING = 0x09
OP_PONG = 0x0A


class WebSocketServer:
    """Minimal single-client RFC 6455 WebSocket server.

    Only listens on 127.0.0.1; designed for one MCP Bridge client at a time.
    """

    def __init__(self, host="127.0.0.1", port=9876):
        self.host = host
        self.port = port
        self._server_sock = None
        self._client_sock = None
        self._running = False
        self._thread = None

        # Thread-safe queue: WebSocket thread → Main thread
        self.request_queue = queue.Queue()

        # Per-request response storage (accessed only from background thread after set)
        self._pending = {}       # request_id → None (waiting) / result
        self._pending_events = {}  # request_id → threading.Event
        self._lock = threading.Lock()

    # ── public API ─────────────────────────────────────────────

    def start(self):
        """Start the WebSocket server in a daemon background thread."""
        self._server_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self._server_sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self._server_sock.bind((self.host, self.port))
        self._server_sock.listen(1)
        self._server_sock.settimeout(1.0)
        self._running = True
        self._thread = threading.Thread(target=self._accept_loop, daemon=True)
        self._thread.start()

    def stop(self):
        """Stop the server and close all connections."""
        self._running = False
        # Wake any blocked recv
        try:
            s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            s.connect((self.host, self.port))
            s.close()
        except Exception:
            pass
        if self._client_sock:
            try:
                self._client_sock.close()
            except Exception:
                pass
        if self._server_sock:
            try:
                self._server_sock.close()
            except Exception:
                pass
        if self._thread and self._thread.is_alive():
            self._thread.join(timeout=2)

    def get_request(self):
        """Non-blocking. Returns (request_id, method, params) or None."""
        try:
            return self.request_queue.get_nowait()
        except queue.Empty:
            return None

    def send_response(self, request_id, result):
        """Called from main thread to complete a pending request."""
        with self._lock:
            self._pending[request_id] = result
            ev = self._pending_events.get(request_id)
        if ev:
            ev.set()

    # ── internal ───────────────────────────────────────────────

    def _accept_loop(self):
        """Background thread: accept connection, handshake, serve."""
        while self._running:
            try:
                client, addr = self._server_sock.accept()
                if not self._running:
                    client.close()
                    return
                self._client_sock = client
                self._handshake(client)
                self._serve(client)
            except socket.timeout:
                continue
            except OSError:
                if self._running:
                    raise
            except Exception:
                if self._running:
                    import traceback
                    traceback.print_exc()

    def _handshake(self, conn):
        """Perform HTTP Upgrade → WebSocket handshake."""
        data = b""
        while b"\r\n\r\n" not in data and len(data) < 8192:
            chunk = conn.recv(4096)
            if not chunk:
                raise ConnectionError("No handshake data")
            data += chunk

        headers_text = data.decode("utf-8", errors="replace")
        key = None
        for line in headers_text.split("\r\n"):
            if line.lower().startswith("sec-websocket-key:"):
                key = line.split(":", 1)[1].strip()
                break

        if not key:
            raise ValueError("Missing Sec-WebSocket-Key header")

        accept = base64.b64encode(
            hashlib.sha1((key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").encode()).digest()
        ).decode()

        response = (
            "HTTP/1.1 101 Switching Protocols\r\n"
            "Upgrade: websocket\r\n"
            "Connection: Upgrade\r\n"
            f"Sec-WebSocket-Accept: {accept}\r\n"
            "\r\n"
        )
        conn.sendall(response.encode())

    def _serve(self, conn):
        """Read frames, dispatch messages, send responses."""
        while self._running:
            try:
                conn.settimeout(1.0)
                opcode, payload = self._read_frame(conn)

                if opcode == OP_CLOSE:
                    self._send_frame(conn, OP_CLOSE, b"")
                    break
                elif opcode == OP_PING:
                    self._send_frame(conn, OP_PONG, payload)
                elif opcode == OP_TEXT:
                    self._handle_message(conn, payload)
            except socket.timeout:
                continue
            except (ConnectionError, OSError):
                break
            except Exception:
                if self._running:
                    import traceback
                    traceback.print_exc()
                break
        self._client_sock = None

    def _handle_message(self, conn, payload):
        """Parse JSON-RPC-like message, queue it, wait for main-thread response."""
        try:
            msg = json.loads(payload.decode("utf-8"))
        except json.JSONDecodeError:
            self._send_frame(conn, OP_TEXT,
                             json.dumps({"error": "Invalid JSON"}).encode())
            return

        req_id = msg.get("id")
        method = msg.get("method", "")
        params = msg.get("params", {})

        if req_id is None:
            return  # notification, no response needed

        ev = threading.Event()
        with self._lock:
            self._pending[req_id] = None
            self._pending_events[req_id] = ev

        # Queue to main thread
        self.request_queue.put((req_id, method, params))

        # Wait for main thread to process
        if not ev.wait(timeout=30.0):
            with self._lock:
                self._pending_events.pop(req_id, None)
                self._pending.pop(req_id, None)
            error_resp = json.dumps({
                "id": req_id,
                "error": {"code": -32000, "message": "Request timed out"}
            })
            self._send_frame(conn, OP_TEXT, error_resp.encode())
            return

        # Retrieve result
        with self._lock:
            result = self._pending.pop(req_id, None)
            self._pending_events.pop(req_id, None)

        response = json.dumps({"id": req_id, "result": result})
        self._send_frame(conn, OP_TEXT, response.encode())

    # ── frame I/O ─────────────────────────────────────────────

    def _read_frame(self, conn):
        """Read one WebSocket frame. Returns (opcode, payload_bytes)."""
        header = self._recv_exact(conn, 2)
        byte1, byte2 = header[0], header[1]

        opcode = byte1 & 0x0F
        masked = (byte2 & 0x80) != 0
        length = byte2 & 0x7F

        if length == 126:
            length = struct.unpack(">H", self._recv_exact(conn, 2))[0]
        elif length == 127:
            length = struct.unpack(">Q", self._recv_exact(conn, 8))[0]

        mask_key = self._recv_exact(conn, 4) if masked else b""

        payload = self._recv_exact(conn, length)
        if masked:
            payload = bytes(b ^ mask_key[i % 4] for i, b in enumerate(payload))

        return opcode, payload

    def _send_frame(self, conn, opcode, payload):
        """Send one WebSocket frame (server → client, never masked)."""
        frame = bytearray()
        frame.append(0x80 | opcode)  # FIN + opcode
        plen = len(payload)
        if plen < 126:
            frame.append(plen)
        elif plen < 65536:
            frame.append(126)
            frame.extend(struct.pack(">H", plen))
        else:
            frame.append(127)
            frame.extend(struct.pack(">Q", plen))
        frame.extend(payload)
        conn.sendall(bytes(frame))

    @staticmethod
    def _recv_exact(conn, n):
        """Read exactly n bytes from a socket."""
        data = b""
        while len(data) < n:
            chunk = conn.recv(n - len(data))
            if not chunk:
                raise ConnectionError("Connection closed")
            data += chunk
        return data
