using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace _Game.Editor.UnityMcp
{
    /// <summary>
    /// C# 代码执行 Handler — 在 Unity Editor 上下文中编译并执行 C# 片段。
    /// 对标 Blender 的 blender_execute_python。
    /// </summary>
    public static class ExecutionHandler
    {
        private const string EXECUTE_CODE = "unity_execute_code";
        private const string UNDO = "unity_undo";

        // Cache compiled assemblies to avoid recompile overhead
        private static readonly Dictionary<string, MethodInfo> _methodCache
            = new Dictionary<string, MethodInfo>();

        public static void Register()
        {
            UnityMcpServer.RegisterHandler(EXECUTE_CODE, HandleExecuteCode);
            UnityMcpServer.RegisterHandler(UNDO, HandleUndo);
        }

        // ── Execute C# code ──

        static string HandleExecuteCode(string paramsJson)
        {
            string code = GetStringParam(paramsJson, "code");
            if (string.IsNullOrEmpty(code))
                return @"{""success"": false, ""error"": ""Missing parameter: code""}";

            try
            {
                // 编译并执行
                string result = CompileAndRun(code);
                return $@"{{
                    ""success"": true,
                    ""result"": ""{SimpleJson.Escape(result)}"",
                    ""code_length"": {code.Length}
                }}";
            }
            catch (Exception ex)
            {
                return $@"{{
                    ""success"": false,
                    ""error"": ""{SimpleJson.Escape(ex.Message)}""
                }}";
            }
        }

        // ── Undo ──

        static string HandleUndo(string paramsJson)
        {
            Undo.PerformUndo();
            return @"{""success"": true, ""message"": ""Undo performed""}";
        }

        // ═══ Code Compilation ═══

        /// <summary>
        /// 编译 C# 代码为方法并执行。使用 CSharpCodeProvider (Mono/.NET)。
        /// 每段代码按 hash 缓存编译结果，避免重复编译。
        /// </summary>
        static string CompileAndRun(string code)
        {
            string codeHash = Convert.ToBase64String(
                System.Security.Cryptography.SHA1.Create()
                    .ComputeHash(System.Text.Encoding.UTF8.GetBytes(code)));

            if (_methodCache.TryGetValue(codeHash, out var cached))
            {
                return InvokeMethod(cached);
            }

            // 包裹为完整类
            string wrappedCode = $@"
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace _UnityMcp_Dynamic
{{
    public static class Exec_{codeHash.Replace("+", "")
        .Replace("/", "_").Replace("=", "")}
    {{
        public static string Run()
        {{
            try
            {{
                {code}
                return ""OK"";
            }}
            catch (Exception ex)
            {{
                return ""Error: "" + ex.Message + ""\\n"" + ex.StackTrace;
            }}
        }}
    }}
}}";

            // 编译
            var provider = CodeDomProvider.CreateProvider("CSharp");
            var options = new CompilerParameters();
            options.GenerateInMemory = true;
            options.GenerateExecutable = false;
            options.IncludeDebugInformation = false;

            // 只引用核心程序集，避免重复类型定义导致 CS0436
            var coreAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "mscorlib", "System", "System.Core",
                "UnityEngine", "UnityEngine.CoreModule", "UnityEngine.UIModule",
                "UnityEngine.PhysicsModule", "UnityEngine.AnimationModule",
                "UnityEngine.AudioModule", "UnityEngine.ImageConversionModule",
                "UnityEngine.TextRenderingModule", "UnityEngine.IMGUIModule",
                "UnityEngine.InputLegacyModule", "UnityEngine.UnityWebRequestModule",
                "UnityEngine.UI", "UnityEditor", "UnityEditor.CoreModule",
                "Assembly-CSharp", "Assembly-CSharp-Editor",
            };
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (!asm.IsDynamic && !string.IsNullOrEmpty(asm.Location)
                        && coreAssemblies.Contains(asm.GetName().Name))
                        options.ReferencedAssemblies.Add(asm.Location);
                }
                catch { }
            }
            // 也引用项目自定义程序集（_Game 等）
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var name = asm.GetName().Name;
                    if (!asm.IsDynamic && !string.IsNullOrEmpty(asm.Location)
                        && !coreAssemblies.Contains(name)
                        && name.StartsWith("_Game", StringComparison.OrdinalIgnoreCase))
                        options.ReferencedAssemblies.Add(asm.Location);
                }
                catch { }
            }

            var results = provider.CompileAssemblyFromSource(options, wrappedCode);

            if (results.Errors.HasErrors)
            {
                var errors = new List<string>();
                foreach (CompilerError err in results.Errors)
                {
                    if (!err.IsWarning)
                        errors.Add($"[Line {err.Line}] {err.ErrorText}");
                }
                throw new Exception("Compilation failed:\n" + string.Join("\n", errors));
            }

            var asmResult = results.CompiledAssembly;
            var type = asmResult.GetType("_UnityMcp_Dynamic.Exec_" +
                codeHash.Replace("+", "").Replace("/", "_").Replace("=", ""));
            var method = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);

            _methodCache[codeHash] = method;

            // 清理缓存（超过 50 个时清空，避免内存泄漏）
            if (_methodCache.Count > 50)
                _methodCache.Clear();

            return InvokeMethod(method);
        }

        static string InvokeMethod(MethodInfo method)
        {
            var result = method.Invoke(null, null);
            return result?.ToString() ?? "null";
        }

        // ═══ Helpers ═══

        static string GetStringParam(string json, string key)
        {
            string pattern = $"\"{key}\"";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx + pattern.Length);
            if (colon < 0) return null;
            int start = colon + 1;
            while (start < json.Length && (json[start] == ' ' || json[start] == '\t')) start++;
            if (start >= json.Length || json[start] != '"') return null;

            // Find end (handle escaped quotes)
            var sb = new System.Text.StringBuilder();
            int pos = start + 1;
            while (pos < json.Length)
            {
                if (json[pos] == '\\' && pos + 1 < json.Length)
                {
                    char next = json[pos + 1];
                    if (next == '"') { sb.Append('"'); pos += 2; continue; }
                    if (next == '\\') { sb.Append('\\'); pos += 2; continue; }
                    if (next == 'n') { sb.Append('\n'); pos += 2; continue; }
                    if (next == 't') { sb.Append('\t'); pos += 2; continue; }
                    if (next == 'r') { sb.Append('\r'); pos += 2; continue; }
                }
                if (json[pos] == '"') break;
                sb.Append(json[pos]);
                pos++;
            }
            return sb.ToString();
        }
    }
}
