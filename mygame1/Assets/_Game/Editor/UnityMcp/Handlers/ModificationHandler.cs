using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace _Game.Editor.UnityMcp
{
    /// <summary>
    /// 场景修改 Handler — 创建/删除/移动 GameObject，修改组件属性。
    /// 所有修改操作都记录 Undo，可撤销。
    /// </summary>
    public static class ModificationHandler
    {
        private const string CREATE_OBJECT = "unity_create_object";
        private const string DELETE_OBJECT = "unity_delete_object";
        private const string SET_TRANSFORM = "unity_set_transform";
        private const string SET_COMPONENT_FIELD = "unity_set_component_field";
        private const string DUPLICATE_OBJECT = "unity_duplicate_object";
        private const string SET_PARENT = "unity_set_parent";

        public static void Register()
        {
            UnityMcpServer.RegisterHandler(CREATE_OBJECT, HandleCreateObject);
            UnityMcpServer.RegisterHandler(DELETE_OBJECT, HandleDeleteObject);
            UnityMcpServer.RegisterHandler(SET_TRANSFORM, HandleSetTransform);
            UnityMcpServer.RegisterHandler(SET_COMPONENT_FIELD, HandleSetComponentField);
            UnityMcpServer.RegisterHandler(DUPLICATE_OBJECT, HandleDuplicateObject);
            UnityMcpServer.RegisterHandler(SET_PARENT, HandleSetParent);
        }

        // ── Create empty GameObject ──

        static string HandleCreateObject(string paramsJson)
        {
            string name = GetStringParam(paramsJson, "name");
            if (string.IsNullOrEmpty(name)) name = "NewObject";

            float px = GetFloatParam(paramsJson, "x", 0f);
            float py = GetFloatParam(paramsJson, "y", 0f);
            float pz = GetFloatParam(paramsJson, "z", 0f);

            var go = new GameObject(name);
            go.transform.position = new Vector3(px, py, pz);

            Undo.RegisterCreatedObjectUndo(go, "Create " + name);

            return $@"{{
                ""success"": true,
                ""name"": ""{SimpleJson.Escape(name)}"",
                ""instance_id"": {go.GetInstanceID()},
                ""position"": ""({px}, {py}, {pz})""
            }}";
        }

        // ── Delete GameObject ──

        static string HandleDeleteObject(string paramsJson)
        {
            string name = GetStringParam(paramsJson, "name");
            if (string.IsNullOrEmpty(name))
                return @"{""success"": false, ""error"": ""Missing parameter: name""}";

            var go = GameObject.Find(name);
            if (go == null)
                return @$"{{""success"": false, ""error"": ""GameObject '{SimpleJson.Escape(name)}' not found""}}";

            Undo.DestroyObjectImmediate(go);
            return $@"{{""success"": true, ""deleted"": ""{SimpleJson.Escape(name)}""}}";
        }

        // ── Set Transform ──

        static string HandleSetTransform(string paramsJson)
        {
            string name = GetStringParam(paramsJson, "name");
            if (string.IsNullOrEmpty(name))
                return @"{""success"": false, ""error"": ""Missing parameter: name""}";

            var go = GameObject.Find(name);
            if (go == null)
                return @$"{{""success"": false, ""error"": ""GameObject '{SimpleJson.Escape(name)}' not found""}}";

            var t = go.transform;

            if (HasParam(paramsJson, "px") || HasParam(paramsJson, "py") || HasParam(paramsJson, "pz"))
            {
                Vector3 pos = t.position;
                if (HasParam(paramsJson, "px")) pos.x = GetFloatParam(paramsJson, "px", pos.x);
                if (HasParam(paramsJson, "py")) pos.y = GetFloatParam(paramsJson, "py", pos.y);
                if (HasParam(paramsJson, "pz")) pos.z = GetFloatParam(paramsJson, "pz", pos.z);
                Undo.RecordObject(t, "Move " + name);
                t.position = pos;
            }

            if (HasParam(paramsJson, "rx") || HasParam(paramsJson, "ry") || HasParam(paramsJson, "rz"))
            {
                Vector3 rot = t.rotation.eulerAngles;
                if (HasParam(paramsJson, "rx")) rot.x = GetFloatParam(paramsJson, "rx", rot.x);
                if (HasParam(paramsJson, "ry")) rot.y = GetFloatParam(paramsJson, "ry", rot.y);
                if (HasParam(paramsJson, "rz")) rot.z = GetFloatParam(paramsJson, "rz", rot.z);
                Undo.RecordObject(t, "Rotate " + name);
                t.rotation = Quaternion.Euler(rot);
            }

            if (HasParam(paramsJson, "sx") || HasParam(paramsJson, "sy") || HasParam(paramsJson, "sz"))
            {
                Vector3 scale = t.localScale;
                if (HasParam(paramsJson, "sx")) scale.x = GetFloatParam(paramsJson, "sx", scale.x);
                if (HasParam(paramsJson, "sy")) scale.y = GetFloatParam(paramsJson, "sy", scale.y);
                if (HasParam(paramsJson, "sz")) scale.z = GetFloatParam(paramsJson, "sz", scale.z);
                Undo.RecordObject(t, "Scale " + name);
                t.localScale = scale;
            }

            return $@"{{
                ""success"": true,
                ""name"": ""{SimpleJson.Escape(name)}"",
                ""position"": ""({t.position.x:F2}, {t.position.y:F2}, {t.position.z:F2})"",
                ""rotation"": ""({t.rotation.eulerAngles.x:F1}, {t.rotation.eulerAngles.y:F1}, {t.rotation.eulerAngles.z:F1})"",
                ""scale"": ""({t.localScale.x:F2}, {t.localScale.y:F2}, {t.localScale.z:F2})""
            }}";
        }

        // ── Set component field ──

        static string HandleSetComponentField(string paramsJson)
        {
            string objName = GetStringParam(paramsJson, "object");
            string compType = GetStringParam(paramsJson, "component");
            string fieldName = GetStringParam(paramsJson, "field");
            string value = GetStringParam(paramsJson, "value");

            if (string.IsNullOrEmpty(objName) || string.IsNullOrEmpty(compType) ||
                string.IsNullOrEmpty(fieldName) || string.IsNullOrEmpty(value))
            {
                return @"{""success"": false, ""error"": ""Missing parameters: object, component, field, value""}";
            }

            var go = GameObject.Find(objName);
            if (go == null)
                return @$"{{""success"": false, ""error"": ""GameObject not found: {SimpleJson.Escape(objName)}""}}";

            var type = Type.GetType(compType + ", Assembly-CSharp");
            if (type == null)
                type = Type.GetType(compType); // Try full qualified
            if (type == null)
            {
                // Try search among components
                foreach (var c in go.GetComponents<Component>())
                {
                    if (c != null && (c.GetType().FullName == compType || c.GetType().Name == compType))
                    {
                        type = c.GetType();
                        break;
                    }
                }
            }

            if (type == null)
                return @$"{{""success"": false, ""error"": ""Component type not found: {SimpleJson.Escape(compType)}""}}";

            var comp = go.GetComponent(type);
            if (comp == null)
                return @$"{{""success"": false, ""error"": ""Component not on object""}}";

            var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                return @$"{{""success"": false, ""error"": ""Field not found: {SimpleJson.Escape(fieldName)}""}}";

            Undo.RecordObject(comp, $"Set {fieldName}");

            try
            {
                object typedValue = ConvertValue(value, field.FieldType);
                field.SetValue(comp, typedValue);
                EditorUtility.SetDirty(comp);
            }
            catch (Exception ex)
            {
                return $@"{{""success"": false, ""error"": ""{SimpleJson.Escape(ex.Message)}""}}";
            }

            return $@"{{""success"": true, ""object"": ""{SimpleJson.Escape(objName)}"", ""field"": ""{SimpleJson.Escape(fieldName)}"", ""value"": ""{SimpleJson.Escape(value)}""}}";
        }

        // ── Duplicate ──

        static string HandleDuplicateObject(string paramsJson)
        {
            string name = GetStringParam(paramsJson, "name");
            if (string.IsNullOrEmpty(name))
                return @"{""success"": false, ""error"": ""Missing parameter: name""}";

            var go = GameObject.Find(name);
            if (go == null)
                return @$"{{""success"": false, ""error"": ""GameObject not found""}}";

            var dup = GameObject.Instantiate(go, go.transform.position, go.transform.rotation);
            dup.name = go.name + "_copy";
            Undo.RegisterCreatedObjectUndo(dup, "Duplicate " + name);

            return $@"{{""success"": true, ""original"": ""{SimpleJson.Escape(name)}"", ""duplicate"": ""{SimpleJson.Escape(dup.name)}""}}";
        }

        // ── Set Parent ──

        static string HandleSetParent(string paramsJson)
        {
            string childName = GetStringParam(paramsJson, "child");
            string parentName = GetStringParam(paramsJson, "parent");

            var child = GameObject.Find(childName);
            if (child == null)
                return @$"{{""success"": false, ""error"": ""Child not found: {SimpleJson.Escape(childName)}""}}";

            if (string.IsNullOrEmpty(parentName) || parentName == "null" || parentName == "root")
            {
                Undo.SetTransformParent(child.transform, null, "Unparent");
                return $@"{{""success"": true, ""child"": ""{SimpleJson.Escape(childName)}"", ""parent"": ""null""}}";
            }

            var parent = GameObject.Find(parentName);
            if (parent == null)
                return @$"{{""success"": false, ""error"": ""Parent not found: {SimpleJson.Escape(parentName)}""}}";

            Undo.SetTransformParent(child.transform, parent.transform, "SetParent");
            return $@"{{""success"": true, ""child"": ""{SimpleJson.Escape(childName)}"", ""parent"": ""{SimpleJson.Escape(parentName)}""}}";
        }

        // ═══ Helpers ═══

        static object ConvertValue(string value, Type targetType)
        {
            if (targetType == typeof(float)) return float.Parse(value);
            if (targetType == typeof(int)) return int.Parse(value);
            if (targetType == typeof(bool)) return bool.Parse(value);
            if (targetType == typeof(string)) return value;
            if (targetType == typeof(Vector3))
            {
                // Parse "(x, y, z)"
                value = value.Trim('(', ')');
                var parts = value.Split(',');
                return new Vector3(
                    float.Parse(parts[0].Trim()),
                    float.Parse(parts[1].Trim()),
                    float.Parse(parts[2].Trim()));
            }
            if (targetType == typeof(Color))
            {
                // Parse "(r, g, b, a)"
                value = value.Trim('(', ')');
                var parts = value.Split(',');
                return new Color(
                    float.Parse(parts[0].Trim()),
                    float.Parse(parts[1].Trim()),
                    float.Parse(parts[2].Trim()),
                    parts.Length > 3 ? float.Parse(parts[3].Trim()) : 1f);
            }
            return value;
        }

        static bool HasParam(string json, string key)
        {
            return json.IndexOf($"\"{key}\"", StringComparison.Ordinal) >= 0;
        }

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
            int end = start + 1;
            while (end < json.Length)
            {
                if (json[end] == '"' && json[end - 1] != '\\') break;
                end++;
            }
            // Unescape
            string raw = json.Substring(start + 1, end - start - 1);
            return raw.Replace("\\\"", "\"").Replace("\\\\", "\\")
                      .Replace("\\n", "\n").Replace("\\t", "\t");
        }

        static float GetFloatParam(string json, string key, float defaultValue)
        {
            string pattern = $"\"{key}\"";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return defaultValue;
            int colon = json.IndexOf(':', idx + pattern.Length);
            if (colon < 0) return defaultValue;
            int start = colon + 1;
            while (start < json.Length && (json[start] == ' ' || json[start] == '\t')) start++;
            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-' || json[end] == '.')) end++;
            if (end > start && float.TryParse(
                json.Substring(start, end - start),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float val))
                return val;
            return defaultValue;
        }
    }
}
