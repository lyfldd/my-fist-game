using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Game.Editor.UnityMcp
{
    /// <summary>
    /// 场景查询 Handler — 列出 GameObject、查询组件、获取 Transform 信息。
    /// </summary>
    public static class InspectionHandler
    {
        private const string LIST_OBJECTS = "unity_list_objects";
        private const string GET_OBJECT_INFO = "unity_get_object_info";
        private const string LIST_COMPONENTS = "unity_list_components";
        private const string FIND_OBJECTS = "unity_find_objects";

        public static void Register()
        {
            UnityMcpServer.RegisterHandler(LIST_OBJECTS, HandleListObjects);
            UnityMcpServer.RegisterHandler(GET_OBJECT_INFO, HandleGetObjectInfo);
            UnityMcpServer.RegisterHandler(LIST_COMPONENTS, HandleListComponents);
            UnityMcpServer.RegisterHandler(FIND_OBJECTS, HandleFindObjects);
        }

        // ── List all root GameObjects ──

        static string HandleListObjects(string paramsJson)
        {
            int maxCount = GetIntParam(paramsJson, "max_count", 100);

            var roots = new List<GameObject>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                roots.AddRange(scene.GetRootGameObjects());
            }

            // Also include DontDestroyOnLoad
            var ddol = GameObject.FindObjectsOfType<GameObject>(true)
                .Where(g => g.scene.name == null || g.scene.name == "DontDestroyOnLoad")
                .Where(g => g.transform.parent == null)
                .Distinct()
                .ToList();

            var result = new List<string>();
            int count = 0;
            foreach (var go in roots.Concat(ddol).Take(maxCount))
            {
                if (go == null) continue;
                int childCount = go.transform.childCount;
                int compCount = go.GetComponents<Component>().Length;
                bool active = go.activeSelf;
                string layer = LayerMask.LayerToName(go.layer);

                result.Add($@"{{
                    ""name"": ""{SimpleJson.Escape(go.name)}"",
                    ""active"": {active.ToString().ToLower()},
                    ""layer"": ""{layer}"",
                    ""tag"": ""{go.tag}"",
                    ""children"": {childCount},
                    ""components"": {compCount},
                    ""scene"": ""{SimpleJson.Escape(go.scene.name ?? "DontDestroyOnLoad")}""
                }}");
                count++;
            }

            return $@"{{
                ""success"": true,
                ""total_roots"": {roots.Count + ddol.Count},
                ""returned"": {count},
                ""objects"": [{string.Join(",", result)}]
            }}";
        }

        // ── Get detailed info about one object ──

        static string HandleGetObjectInfo(string paramsJson)
        {
            string objName = GetStringParam(paramsJson, "name");
            if (string.IsNullOrEmpty(objName))
                return @"{""success"": false, ""error"": ""Missing parameter: name""}";

            var go = GameObject.Find(objName);
            if (go == null)
                return @$"{{""success"": false, ""error"": ""GameObject '{SimpleJson.Escape(objName)}' not found""}}";

            return BuildObjectDetail(go);
        }

        // ── List components on an object ──

        static string HandleListComponents(string paramsJson)
        {
            string objName = GetStringParam(paramsJson, "name");
            if (string.IsNullOrEmpty(objName))
                return @"{""success"": false, ""error"": ""Missing parameter: name""}";

            var go = GameObject.Find(objName);
            if (go == null)
                return @$"{{""success"": false, ""error"": ""GameObject '{SimpleJson.Escape(objName)}' not found""}}";

            var comps = new List<string>();
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                comps.Add($@"{{
                    ""type"": ""{SimpleJson.Escape(c.GetType().FullName)}"",
                    ""enabled"": {(c is Behaviour b ? b.enabled.ToString().ToLower() : "true")}
                }}");
            }

            return $@"{{
                ""success"": true,
                ""object"": ""{SimpleJson.Escape(objName)}"",
                ""components"": [{string.Join(",", comps)}]
            }}";
        }

        // ── Find objects by name/type ──

        static string HandleFindObjects(string paramsJson)
        {
            string pattern = GetStringParam(paramsJson, "pattern");
            string typeName = GetStringParam(paramsJson, "type");
            int maxCount = GetIntParam(paramsJson, "max_count", 50);

            var allObjects = GameObject.FindObjectsOfType<GameObject>(true);
            var matched = new List<GameObject>();

            foreach (var go in allObjects)
            {
                if (go == null) continue;
                bool nameMatch = string.IsNullOrEmpty(pattern) ||
                    go.name.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
                bool typeMatch = string.IsNullOrEmpty(typeName) ||
                    go.GetComponent(typeName) != null;

                if (nameMatch && typeMatch)
                    matched.Add(go);
            }

            var result = new List<string>();
            foreach (var go in matched.Take(maxCount))
            {
                result.Add($@"{{
                    ""name"": ""{SimpleJson.Escape(go.name)}"",
                    ""path"": ""{SimpleJson.Escape(GetGameObjectPath(go))}"",
                    ""active"": {go.activeSelf.ToString().ToLower()}
                }}");
            }

            return $@"{{
                ""success"": true,
                ""total_matches"": {matched.Count},
                ""returned"": {result.Count},
                ""objects"": [{string.Join(",", result)}]
            }}";
        }

        // ═══ Helpers ═══

        static string BuildObjectDetail(GameObject go)
        {
            var t = go.transform;
            string pos = $"({t.position.x:F2}, {t.position.y:F2}, {t.position.z:F2})";
            string rot = $"({t.rotation.eulerAngles.x:F1}, {t.rotation.eulerAngles.y:F1}, {t.rotation.eulerAngles.z:F1})";
            string scale = $"({t.localScale.x:F2}, {t.localScale.y:F2}, {t.localScale.z:F2})";

            var comps = new List<string>();
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                var type = c.GetType();
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(f => f.FieldType.IsValueType || f.FieldType == typeof(string))
                    .Take(10);
                var fieldList = new List<string>();
                foreach (var f in fields)
                {
                    try
                    {
                        var val = f.GetValue(c);
                        if (val != null)
                            fieldList.Add($"\"{f.Name}\": \"{SimpleJson.Escape(val.ToString())}\"");
                    }
                    catch { }
                }

                comps.Add($@"{{
                    ""type"": ""{SimpleJson.Escape(type.FullName)}"",
                    ""fields"": {{{string.Join(",", fieldList)}}}
                }}");
            }

            return $@"{{
                ""success"": true,
                ""name"": ""{SimpleJson.Escape(go.name)}"",
                ""active"": {go.activeSelf.ToString().ToLower()},
                ""layer"": ""{LayerMask.LayerToName(go.layer)}"",
                ""tag"": ""{go.tag}"",
                ""scene"": ""{SimpleJson.Escape(go.scene.name ?? "DontDestroyOnLoad")}"",
                ""position"": ""{pos}"",
                ""rotation"": ""{rot}"",
                ""scale"": ""{scale}"",
                ""child_count"": {t.childCount},
                ""components"": [{string.Join(",", comps)}]
            }}";
        }

        static string GetGameObjectPath(GameObject go)
        {
            string path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        static string GetStringParam(string json, string key)
        {
            string pattern = $"\"{key}\"";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return null;

            int colon = json.IndexOf(':', idx + pattern.Length);
            if (colon < 0) return null;

            int start = colon + 1;
            while (start < json.Length && (json[start] == ' ' || json[start] == '\t'))
                start++;

            if (start >= json.Length || json[start] != '"') return null;

            int end = start + 1;
            while (end < json.Length)
            {
                if (json[end] == '"' && json[end - 1] != '\\')
                    break;
                end++;
            }
            return json.Substring(start + 1, end - start - 1);
        }

        static int GetIntParam(string json, string key, int defaultValue)
        {
            string pattern = $"\"{key}\"";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return defaultValue;

            int colon = json.IndexOf(':', idx + pattern.Length);
            if (colon < 0) return defaultValue;

            int start = colon + 1;
            while (start < json.Length && (json[start] == ' ' || json[start] == '\t'))
                start++;

            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'))
                end++;

            if (end > start && int.TryParse(json.Substring(start, end - start), out int val))
                return val;

            return defaultValue;
        }
    }
}
