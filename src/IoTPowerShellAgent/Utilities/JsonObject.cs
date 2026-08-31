using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace IoTPowerShellAgent.Utilities
{



    public static class JsonObject
    {
        public static object ConvertFromJson(string input, out ErrorRecord error)
        {
            return JsonObject.ConvertFromJson(input, false, out error);
        }

        public static object ConvertFromJson(string input, bool returnHashtable, out ErrorRecord error)
        {
            return JsonObject.ConvertFromJson(input, returnHashtable, new int?(1024), out error);
        }

        public static object ConvertFromJson(string input, bool returnHashtable, int? maxDepth, out ErrorRecord error)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            error = null!;
            object? obj2 = null!;
            try
            {
                if (Regex.Match(input, "^\\s*\\[").Success)
                {
                    JArray.Parse(input);
                }

                object? obj = JsonConvert.DeserializeObject(input, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                    MaxDepth = maxDepth
                });
                JObject? jobject = obj as JObject;
                if (jobject == null)
                {
                    JArray? jarray = obj as JArray;
                    if (jarray == null)
                    {
                        obj2 = obj;
                    }
                    else
                    {
                        obj2 = (returnHashtable
                            ? JsonObject.PopulateHashTableFromJArray(jarray, out ErrorRecord? error2)
                            : JsonObject.PopulateFromJArray(jarray, out error));
                    }
                }
                else if (returnHashtable)
                {
                    obj2 = JsonObject.PopulateHashTableFromJDictionary(jobject, out error);
                }
                else
                {
                    obj2 = JsonObject.PopulateFromJDictionary(jobject,
                        new JsonObject.DuplicateMemberHashSet(jobject.Count), out error);
                }
            }
            catch (JsonException ex)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, "Json Deserialisation Failed",
                        new object[] { ex.Message }), ex);
            }

            return obj2!;
        }

        private static PSObject? PopulateFromJDictionary(JObject entries,
            JsonObject.DuplicateMemberHashSet memberHashTracker, out ErrorRecord? error)
        {
            error = null;
            PSObject psobject = new PSObject(entries.Count);
            foreach (KeyValuePair<string, JToken> keyValuePair in entries)
            {
                if (string.IsNullOrEmpty(keyValuePair.Key))
                {
                    string text = string.Format(CultureInfo.CurrentCulture, "Empty key in JSON string", new object[0]);
                    error = new ErrorRecord(new InvalidOperationException(text), "EmptyKeyInJsonString",
                        ErrorCategory.InvalidData, null);
                    return null!;
                }

                string text2;
                if (memberHashTracker.TryGetValue(keyValuePair.Key, out text2) &&
                    string.Equals(keyValuePair.Key, text2, StringComparison.Ordinal))
                {
                    string text3 = string.Format(CultureInfo.CurrentCulture, "Duplicate keys in Json string",
                        new object[] { keyValuePair.Key });
                    error = new ErrorRecord(new InvalidOperationException(text3), "DuplicateKeysInJsonString",
                        ErrorCategory.InvalidData, null);
                    return null!;
                }

                string text4;
                if (memberHashTracker.TryGetValue(keyValuePair.Key, out text4))
                {
                    string text5 = string.Format(CultureInfo.CurrentCulture, "KeysWithDifferentCasingInJsonString",
                        new object[] { text4, keyValuePair.Key });
                    error = new ErrorRecord(new InvalidOperationException(text5), "KeysWithDifferentCasingInJsonString",
                        ErrorCategory.InvalidData, null);
                    return null!;
                }

                memberHashTracker.Add(keyValuePair.Key);
                JToken value = keyValuePair.Value;
                object? obj = JsonObject.PopulateFromJToken(value, out error);
                if (error != null)
                {
                    return null!;
                }

                psobject.Properties.Add(new PSNoteProperty(keyValuePair.Key, obj!));
            }

            return psobject;
        }

        private static object? PopulateFromJToken(JToken token, out ErrorRecord? error)
        {
            error = null;
            JValue jvalue = token as JValue;
            if (jvalue != null)
            {
                return jvalue.Value;
            }

            JObject jobject = token as JObject;
            if (jobject != null)
            {
                return JsonObject.PopulateFromJDictionary(jobject,
                    new JsonObject.DuplicateMemberHashSet(jobject.Count), out error);
            }

            JArray jarray = token as JArray;
            if (jarray != null)
            {
                return JsonObject.PopulateFromJArray(jarray, out error);
            }

            return null!;
        }

        private static object? PopulateFromJArray(JArray array, out ErrorRecord? error)
        {
            error = null;
            object?[] array2 = new object?[array.Count];
            int num = 0;
            foreach (JToken token in array)
            {
                object? obj = JsonObject.PopulateFromJToken(token, out error);
                if (error != null)
                {
                    return null!;
                }

                array2[num++] = obj;
            }

            return array2;
        }

        private static Hashtable? PopulateHashTableFromJDictionary(JObject entries, out ErrorRecord? error)
        {
            error = null;
            Hashtable hashtable = new Hashtable(entries.Count);
            foreach (KeyValuePair<string, JToken> keyValuePair in entries)
            {
                object? obj = JsonObject.PopulateFromJToken(keyValuePair.Value, out error);
                if (error != null)
                {
                    return null!;
                }

                hashtable.Add(keyValuePair.Key, obj);
            }

            return hashtable;
        }

        private static ArrayList PopulateHashTableFromJArray(JArray array, out ErrorRecord? error)
        {
            error = null;
            ArrayList arrayList = new ArrayList(array.Count);
            foreach (JToken token in array)
            {
                object obj = JsonObject.PopulateFromJToken(token, out ErrorRecord? error2);
                if (error2 != null)
                {
                    error = error2;
                    return null!;
                }

                arrayList.Add(obj);
            }

            return arrayList;
        }

        private class DuplicateMemberHashSet : HashSet<string>
        {
            public DuplicateMemberHashSet(int capacity) : base(StringComparer.OrdinalIgnoreCase)
            {
            }
        }

        private static bool _maxDepthWarningWritten = false;

        public static string ConvertToJson(object objectToProcess, in ConvertToJsonContext context)
        {
            string text;
            try
            {
                JsonObject._maxDepthWarningWritten = false;
                object obj = JsonObject.ProcessValue(objectToProcess, 0, context);
                JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    MaxDepth = new int?(1024),
                    StringEscapeHandling = context.StringEscapeHandling
                };
                if (context.EnumsAsStrings)
                {
                    jsonSerializerSettings.Converters.Add(new StringEnumConverter());
                }

                if (!context.CompressOutput)
                {
                    jsonSerializerSettings.Formatting = Formatting.Indented;
                }

                text = JsonConvert.SerializeObject(obj, jsonSerializerSettings);
            }
            catch (OperationCanceledException)
            {
                text = null!;
            }

            return text!;
        }

        private static object ProcessValue(object obj, int currentDepth, in ConvertToJsonContext context)
        {
            if (obj == null || obj == AutomationNull.Value)
            {
                return null;
            }

            PSObject psobject = obj as PSObject;
            if (psobject != null)
            {
                obj = psobject.BaseObject;
            }

            bool flag = false;
            bool flag2 = false;
            Type type = obj.GetType();
            if (type == typeof(object))
            {
                flag = true;
            }

            if (psobject != null && psobject.TypeNames.Count > 0 && psobject.TypeNames[0] == "System.Management.Automation.PSCustomObject")
            {
                flag2 = true;
            }

            if (currentDepth >= context.MaxDepth)
            {
                if (!JsonObject._maxDepthWarningWritten)
                {
                    JsonObject._maxDepthWarningWritten = true;
                }

                return string.Format(CultureInfo.CurrentCulture, "<MaxDepthExceeded>", new object[0]);
            }

            IDictionary dictionary = obj as IDictionary;
            if (dictionary != null)
            {
                return JsonObject.AddPsProperties(psobject, JsonObject.ProcessDictionary(dictionary, currentDepth, context), currentDepth, flag, flag2, context);
            }

            IEnumerable enumerable = obj as IEnumerable;
            if (enumerable != null && !(obj is string))
            {
                return JsonObject.AddPsProperties(psobject, JsonObject.ProcessEnumerable(enumerable, currentDepth, context), currentDepth, flag, flag2, context);
            }

            if (obj is DateTime dateTime)
            {
                return dateTime.ToString("o", CultureInfo.InvariantCulture);
            }

            if (obj is Enum)
            {
                return context.EnumsAsStrings ? obj.ToString() : Convert.ToInt32(obj, CultureInfo.InvariantCulture);
            }

            if (type.IsPrimitive || obj is string || obj is decimal || obj is DateTime || obj is DateTimeOffset || obj is Guid || obj is Uri || obj is TimeSpan)
            {
                return obj;
            }

            return JsonObject.AddPsProperties(psobject, JsonObject.ProcessCustomObject<System.Text.Json.Serialization.JsonIgnoreAttribute>(obj, currentDepth, context), currentDepth, flag, flag2, context);
        }

        private static object AddPsProperties(object psObj, object obj, int depth, bool isPurePSObj, bool isCustomObj,
            in ConvertToJsonContext context)
        {
            PSObject psobject = psObj as PSObject;
            if (psobject == null)
            {
                return obj;
            }

            if (isPurePSObj)
            {
                return obj;
            }

            bool flag = true;
            IDictionary dictionary = obj as IDictionary;
            if (dictionary == null)
            {
                flag = false;
                dictionary = new Dictionary<string, object>();
                dictionary.Add("value", obj);
            }

            JsonObject.AppendPsProperties(psobject, dictionary, depth, isCustomObj, context);
            if (!flag && dictionary.Count == 1)
            {
                return obj;
            }

            return dictionary;
        }

        private static void AppendPsProperties(PSObject psObj, IDictionary receiver, int depth, bool isCustomObject,
            in ConvertToJsonContext context)
        {
            if (psObj.BaseObject is string || psObj.BaseObject is DateTime)
            {
                return;
            }

            foreach (PSPropertyInfo pspropertyInfo in psObj.Properties)
            {
                object obj = null;
                try
                {
                    obj = pspropertyInfo.Value;
                }
                catch (Exception)
                {
                }

                if (!receiver.Contains(pspropertyInfo.Name))
                {
                    receiver[pspropertyInfo.Name] = JsonObject.ProcessValue(obj, depth + 1, context);
                }
            }
        }

        private static object ProcessDictionary(IDictionary dict, int depth, in ConvertToJsonContext context)
        {
            Dictionary<string, object> dictionary = new Dictionary<string, object>(dict.Count);
            foreach (object obj in dict)
            {
                DictionaryEntry dictionaryEntry = (DictionaryEntry)obj;
                string text = dictionaryEntry.Key as string;
                if (text == null)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                        "NonStringKeyInDictionary", new object[] { dict.GetType().FullName }));
                }

                dictionary.Add(text, JsonObject.ProcessValue(dictionaryEntry.Value, depth + 1, context));
            }

            return dictionary;
        }

        private static object ProcessEnumerable(IEnumerable enumerable, int depth, in ConvertToJsonContext context)
        {
            List<object> list = new List<object>();
            foreach (object obj in enumerable)
            {
                list.Add(JsonObject.ProcessValue(obj, depth + 1, context));
            }

            return list;
        }

        private static object ProcessCustomObject<T>(object o, int depth, in ConvertToJsonContext context)
        {
            Dictionary<string, object> dictionary = new Dictionary<string, object>();
            Type type = o.GetType();
            foreach (FieldInfo fieldInfo in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!fieldInfo.IsDefined(typeof(T), true))
                {
                    object obj;
                    try
                    {
                        obj = fieldInfo.GetValue(o);
                    }
                    catch (Exception)
                    {
                        obj = null;
                    }

                    dictionary.Add(fieldInfo.Name, JsonObject.ProcessValue(obj, depth + 1, context));
                }
            }

            foreach (PropertyInfo propertyInfo in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!propertyInfo.IsDefined(typeof(T), true) && propertyInfo.GetIndexParameters().Length == 0)
                {
                    object obj2;
                    try
                    {
                        obj2 = propertyInfo.GetValue(o);
                    }
                    catch (Exception)
                    {
                        obj2 = null;
                    }

                    dictionary.Add(propertyInfo.Name, JsonObject.ProcessValue(obj2, depth + 1, context));
                }
            }

            return dictionary;
        }
    }
}
