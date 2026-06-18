using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

namespace _Game.Editor.UnityMcp
{
    /// <summary>
    /// RFC 6455 WebSocket Server — 纯 C# socket 实现，零外部依赖。
    /// 后台线程处理 I/O，主线程通过 EditorApplication.update 排空队列。
    /// 协议与 Blender MCP Addon 完全兼容。
    /// </summary>
    public class UnityWebSocketServer
    {
        private const int OP_TEXT = 0x01;
        private const int OP_CLOSE = 0x08;
        private const int OP_PING = 0x09;
        private const int OP_PONG = 0x0A;
        private const string WS_MAGIC = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        private readonly string _host;
        private readonly int _port;
        private TcpListener _listener;
        private TcpClient _client;
        private NetworkStream _clientStream;
        private Thread _thread;
        private volatile bool _running;

        /// <summary> 主线程从此队列读取请求 </summary>
        public ConcurrentQueue<(string reqId, string method, string paramsJson)> RequestQueue { get; }
            = new ConcurrentQueue<(string, string, string)>();

        /// <summary> 请求 ID → 响应（后台线程等待） </summary>
        private readonly ConcurrentDictionary<string, string> _responses
            = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, ManualResetEventSlim> _waitHandles
            = new ConcurrentDictionary<string, ManualResetEventSlim>();
        private readonly object _sendLock = new object();

        public bool IsRunning => _running;

        public UnityWebSocketServer(string host = "127.0.0.1", int port = 9877)
        {
            _host = host;
            _port = port;
        }

        // ═══ Public API ═══

        public void Start()
        {
            if (_running) return;
            _running = true;
            _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "UnityMcp-WS" };
            _thread.Start();
            Debug.Log($"[UnityMcp] WebSocket server started on {_host}:{_port}");
        }

        public void Stop()
        {
            _running = false;
            // Wake blocked accept
            try { new TcpClient(_host, _port).Close(); } catch { }
            _clientStream?.Close();
            _client?.Close();
            _listener?.Stop();
            _thread?.Join(2000);
            Debug.Log("[UnityMcp] WebSocket server stopped");
        }

        /// <summary> 从主线程调用，发送响应给等待的后台线程 </summary>
        public void SendResponse(string reqId, string resultJson)
        {
            _responses[reqId] = resultJson;
            if (_waitHandles.TryGetValue(reqId, out var handle))
                handle.Set();
        }

        // ═══ Background Thread ═══

        private void AcceptLoop()
        {
            _listener = new TcpListener(IPAddress.Parse(_host), _port);
            _listener.Start();

            while (_running)
            {
                try
                {
                    _listener.Server.ReceiveTimeout = 1000;
                    var client = _listener.AcceptTcpClient();
                    if (!_running) { client.Close(); return; }

                    _client = client;
                    _clientStream = client.GetStream();
                    _clientStream.ReadTimeout = 1000;

                    Handshake(_clientStream);
                    Serve(_clientStream);
                }
                catch (SocketException)
                {
                    if (_running) Thread.Sleep(100);
                }
                catch (IOException)
                {
                    if (_running) Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    if (_running)
                        Debug.LogError($"[UnityMcp] Accept error: {ex.Message}");
                }
            }
        }

        // ═══ Handshake ═══

        private void Handshake(NetworkStream stream)
        {
            byte[] buffer = new byte[8192];
            int total = 0;
            string data = "";
            while (!data.Contains("\r\n\r\n") && total < buffer.Length)
            {
                int read = stream.Read(buffer, total, buffer.Length - total);
                if (read <= 0) throw new IOException("No handshake data");
                total += read;
                data = Encoding.UTF8.GetString(buffer, 0, total);
            }

            string key = null;
            foreach (var line in data.Split(new[] { "\r\n" }, StringSplitOptions.None))
            {
                if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                {
                    key = line.Substring(line.IndexOf(':') + 1).Trim();
                    break;
                }
            }
            if (key == null) throw new IOException("Missing Sec-WebSocket-Key");

            string acceptKey = Convert.ToBase64String(
                SHA1.Create().ComputeHash(
                    Encoding.UTF8.GetBytes(key + WS_MAGIC)));

            string response = "HTTP/1.1 101 Switching Protocols\r\n" +
                              "Upgrade: websocket\r\n" +
                              "Connection: Upgrade\r\n" +
                              $"Sec-WebSocket-Accept: {acceptKey}\r\n" +
                              "\r\n";
            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            stream.Write(responseBytes, 0, responseBytes.Length);
            stream.Flush();
        }

        // ═══ Serve ═══

        private void Serve(NetworkStream stream)
        {
            while (_running)
            {
                try
                {
                    var (opcode, payload) = ReadFrame(stream);

                    if (opcode == OP_CLOSE)
                    {
                        SendFrame(stream, OP_CLOSE, Array.Empty<byte>());
                        break;
                    }
                    else if (opcode == OP_PING)
                    {
                        SendFrame(stream, OP_PONG, payload);
                    }
                    else if (opcode == OP_TEXT)
                    {
                        HandleMessage(stream, payload);
                    }
                }
                catch (IOException)
                {
                    if (_running) break;
                }
                catch (Exception ex)
                {
                    if (_running)
                        Debug.LogError($"[UnityMcp] Serve error: {ex.Message}");
                    break;
                }
            }
            _clientStream = null;
            _client = null;
        }

        private void HandleMessage(NetworkStream stream, byte[] payload)
        {
            string json = Encoding.UTF8.GetString(payload);
            SimpleJson.GetStringValue(json, "id", out string reqId);
            SimpleJson.GetStringValue(json, "method", out string method);

            // Extract params as raw JSON string
            string paramsJson = "{}";
            int pIdx = json.IndexOf("\"params\"");
            if (pIdx >= 0)
            {
                int colon = json.IndexOf(':', pIdx);
                if (colon >= 0)
                {
                    int brace = json.IndexOf('{', colon);
                    if (brace >= 0)
                    {
                        int depth = 0, end = brace;
                        for (int i = brace; i < json.Length; i++)
                        {
                            if (json[i] == '{') depth++;
                            else if (json[i] == '}')
                            {
                                depth--;
                                if (depth == 0) { end = i; break; }
                            }
                        }
                        paramsJson = json.Substring(brace, end - brace + 1);
                    }
                }
            }

            if (string.IsNullOrEmpty(reqId)) return; // notification

            var waitHandle = new ManualResetEventSlim(false);
            _waitHandles[reqId] = waitHandle;

            // Queue to main thread
            RequestQueue.Enqueue((reqId, method, paramsJson));

            // Wait for main thread response (30s timeout)
            if (!waitHandle.Wait(30000))
            {
                _waitHandles.TryRemove(reqId, out _);
                _responses.TryRemove(reqId, out _);
                string errorResp = "{\"id\":\"" + reqId + "\",\"error\":{\"code\":-32000,\"message\":\"Request timed out\"}}";
                lock (_sendLock)
                    SendFrame(stream, OP_TEXT, Encoding.UTF8.GetBytes(errorResp));
                return;
            }

            _waitHandles.TryRemove(reqId, out _);
            _responses.TryRemove(reqId, out string result);
            if (result == null) result = "{\"id\":\"" + reqId + "\",\"result\":{}}";

            lock (_sendLock)
                SendFrame(stream, OP_TEXT, Encoding.UTF8.GetBytes(result));
        }

        // ═══ Frame I/O ═══

        private (int opcode, byte[] payload) ReadFrame(NetworkStream stream)
        {
            byte[] header = ReadExact(stream, 2);
            int opcode = header[0] & 0x0F;
            bool masked = (header[1] & 0x80) != 0;
            long length = (uint)(header[1] & 0x7F);

            if (length == 126)
            {
                byte[] ext2 = ReadExact(stream, 2);
                length = (uint)((ext2[0] << 8) | ext2[1]);
            }
            else if (length == 127)
            {
                byte[] ext = ReadExact(stream, 8);
                length = (long)((ulong)ext[0] << 56 | (ulong)ext[1] << 48 | (ulong)ext[2] << 40 |
                                (ulong)ext[3] << 32 | (ulong)ext[4] << 24 | (ulong)ext[5] << 16 |
                                (ulong)ext[6] << 8 | ext[7]);
            }

            byte[] maskKey = masked ? ReadExact(stream, 4) : null;
            byte[] payload = ReadExact(stream, (int)length);

            if (masked && maskKey != null)
                for (int i = 0; i < payload.Length; i++)
                    payload[i] ^= maskKey[i % 4];

            return (opcode, payload);
        }

        private static void SendFrame(NetworkStream stream, int opcode, byte[] payload)
        {
            var frame = new System.Collections.Generic.List<byte>();
            frame.Add((byte)(0x80 | opcode));

            int plen = payload.Length;
            if (plen < 126)
                frame.Add((byte)plen);
            else if (plen < 65536)
            {
                frame.Add(126);
                frame.Add((byte)(plen >> 8));
                frame.Add((byte)(plen & 0xFF));
            }
            else
            {
                frame.Add(127);
                for (int i = 7; i >= 0; i--)
                    frame.Add((byte)((plen >> (i * 8)) & 0xFF));
            }

            frame.AddRange(payload);
            stream.Write(frame.ToArray(), 0, frame.Count);
            stream.Flush();
        }

        private static byte[] ReadExact(NetworkStream stream, int n)
        {
            byte[] buf = new byte[n];
            int offset = 0;
            while (offset < n)
            {
                int read = stream.Read(buf, offset, n - offset);
                if (read <= 0) throw new IOException("Connection closed");
                offset += read;
            }
            return buf;
        }
    }

    /// <summary>
    /// 极简 JSON 工具 — 避免依赖 Newtonsoft.Json / Unity.JsonUtility。
    /// 只做我们需要的字段提取和基础构建。
    /// </summary>
    public static class SimpleJson
    {
        public static bool GetStringValue(string json, string key, out string value)
        {
            value = null;
            string pattern = $"\"{key}\"";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return false;

            int colon = json.IndexOf(':', idx + pattern.Length);
            if (colon < 0) return false;

            // Skip whitespace
            int start = colon + 1;
            while (start < json.Length && (json[start] == ' ' || json[start] == '\t'))
                start++;

            if (start >= json.Length) return false;

            if (json[start] == '"')
            {
                // String value
                int end = start + 1;
                while (end < json.Length)
                {
                    if (json[end] == '"' && json[end - 1] != '\\')
                        break;
                    end++;
                }
                value = json.Substring(start + 1, end - start - 1);
                return true;
            }

            return false;
        }

        public static string BuildResponse(string reqId, string resultJson)
        {
            return $"{{\"id\":\"{reqId}\",\"result\":{resultJson}}}";
        }

        public static string BuildError(string reqId, int code, string message)
        {
            return $"{{\"id\":\"{reqId}\",\"error\":{{\"code\":{code},\"message\":\"{Escape(message)}\"}}}}";
        }

        public static string BuildResultString(string key, string value)
        {
            return $"{{\"success\":true,\"{key}\":\"{Escape(value)}\"}}";
        }

        public static string Escape(string s)
        {
            return s?.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }
    }
}
