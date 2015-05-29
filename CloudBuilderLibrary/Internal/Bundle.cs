﻿using System;
using System.Collections.Generic;
using System.Text;
using LitJson;
using System.Collections;

namespace CloudBuilderLibrary
{
	/**
	 * The bundle is a main concept of the CloudBuilder SDK. It is basically the equivalent of a JSON object, behaving
	 * like a C# dictionary, but with inferred typing and more safety.
	 *
	 * You need bundles in many calls, either to decode generic data received by the server (when such data can be
	 * enriched by hooks typically) or to pass generic user parameters, such as when creating a match.
	 * 
	 * Bundles are instantiated through their factory methods: Bundle.CreateObject or Bundle.CreateArray. The type of
	 * the resulting object can be inspected via the Type member. While objects and arrays are containers for further
	 * objects, a bundle is a simple node in the graph and may as such contain a value, such as a string.
	 * 
	 * Sub-objects are fetched using the index operator (brackets), either with a numeric argument for arrays or a
	 * string argument for dictionaries. Conversions are performed automatically based. Let's consider the following
	 * example:
	 * 
	 * ```Bundle b = Bundle.CreateObject();
	 * b["hello"] = "world";```
	 * 
	 * The bundle object, considered as dictionary will have its key "hello" set with a new Bundle node of type string
	 * (implicitly created from the string). So later on, you might fetch this value as well:
	 * 
	 * ```string value = b["hello"];```
	 * 
	 * What happens here is that the bundle under the key "hello" is fetched, and then implicitly converted to a string.
	 * 
	 * Bundle provides a safe way to browse a predefined graph, as when a key doesn't exist it returns the
	 * `Bundle.Empty` (no null value). This special bundle allows to fetch sub-objects but they will always translate to
	 * `Bundle.Empty` as well. If the values are to be converted to a primitive type, the result will be the default
	 * value for this type (null for a string, 0 for an int, etc.). As such, you may do this:
	 * 
	 * ```int value = bundle["nonexisting"]["key"];```
	 * 
	 * Since the "nonexisting" key is not found on bundle, `Bundle.Empty` is returned. Further fetching "key" will return
	 * an empty bundle as well. Which will be converted implicitly to an integer as 0. `Bundle.Empty` is a constant value
	 * that always refers to an empty bundle, and attempting to modify it will result in an exception.
	 * 
	 * ```Bundle b = Bundle.Empty;
	 * b["value"] = "something"; // Exception```
	 * 
	 * The bundle hierarchy doesn't accept null values (it just rejects them). You should avoid manipulating null Bundles
	 * and use `Bundle.Empty` wherever possible, however you may assign a null bundle to a key, which will have no effect.
	 * This can be useful for optional arguments. For instance, the following snippet will not affect the bundle.
	 * 
	 * ```string value = null;
	 * bundle["key"] = value;```
	 * 
	 * If you need a special value for keys that do not match the expected type or are not found in the hierarchy, you
	 * may as well use the .As* methods. For instance, the previous snippet could be written as follows to have a default
	 * value of one:
	 * 
	 * ```int value = bundle["nonexisting"]["key"].AsInt(1);```
	 * 
	 * It is also possible to inspect the Type property of the Bundle in order to ensure that the value was provided as
	 * expected.
	 * 
	 * A bundle may be pre-filled at creation by passing arguments to Bundle.CreateObject and Bundle.CreateArray. For
	 * instance:
	 * 
	 * ```Bundle b = Bundle.CreateObject("key1", "value1", "key2", "value2");```
	 * 
	 * Is equivalent to writing:
	 * 
	 * ```Bundle b = Bundle.CreateObject();
	 * b["key1"] = "value1";
	 * b["key2"] = "value2";```
	 * 
	 * A bundle can quickly be transformed from/to JSON using ToJson and Bundle.FromJson methods. One can also check
	 * for the presence of keys and remove them with the .Has respectively .Remove methods.
	 * 
	 * Iterating a JSON object is made using the explicit .As* methods. For instance, here how you iterate over an
	 * array bundle (no harm will happen if the key doesn't exist or is not an array, since an empty array is returned):
	 * 
	 * ```Bundle b;
	 * foreach (Bundle value in b) { ... }```
	 * 
	 * For an object, use AsDictionary().
	 *
	 * ```Bundle b;
	 * foreach (KeyValuePair<string, Bundle> pair in b["key"].AsDictionary()) { ... }```
	 * 
	 * This loop is safe as well even if the bundle doesn't contain a "key" entry or the "key" entry is not an object.
	 * 
	 * Null bundles should be avoided! Use Bundle. Empty everytime you need a "void", non mutable bundle value.
	 * Converting from a null bundle will result in an exception.
	 * 
	 * ```Bundle b = null;
	 * string value = b; // Null pointer exception!```
	 * 
	 * That's all what there is to know about bundles. In general they should make any code interacting with generic
	 * objects simple and safe.
	 */
	public class Bundle {
		public enum DataType {
			None, Boolean, Integer, Double, String, Array, Object
		}
		public static readonly EmptyBundle Empty = new EmptyBundle();
		private DataType type;
		private double doubleValue;
		private long longValue;
		private string stringValue;
		private Dictionary<string, Bundle> objectValue;
		private List<Bundle> arrayValue;

		// Explicit constructors
		public static Bundle CreateArray() {
			return new Bundle(DataType.Array);
		}
		public static Bundle CreateObject() {
			return new Bundle(DataType.Object);
		}
		public static Bundle CreateObject(string onlyKey, Bundle onlyValue) {
			Bundle result = new Bundle(DataType.Object);
			result[onlyKey] = onlyValue;
			return result;
		}
		public static Bundle CreateObject(string key1, Bundle value1, string key2, Bundle value2) {
			Bundle result = new Bundle(DataType.Object);
			result[key1] = value1;
			result[key2] = value2;
			return result;
		}
		public static Bundle CreateObject(string key1, Bundle value1, string key2, Bundle value2, string key3, Bundle value3) {
			Bundle result = new Bundle(DataType.Object);
			result[key1] = value1;
			result[key2] = value2;
			result[key3] = value3;
			return result;
		}
		public static Bundle CreateArray(params Bundle[] values) {
			Bundle result = new Bundle(DataType.Array);
			foreach (Bundle b in values) result.Add(b);
			return result;
		}

		// Construction (internal)
		protected Bundle(DataType dataType) {
			type = dataType;
			if (dataType == DataType.Object) objectValue = new Dictionary<string, Bundle>();
			else if (dataType == DataType.Array) arrayValue = new List<Bundle>();
		}
		public Bundle(bool value) { type = DataType.Boolean; longValue = value ? 1 : 0; }
		public Bundle(long value) { type = DataType.Integer; longValue = value; }
		public Bundle(double value) { type = DataType.Double; doubleValue = value; }
		public Bundle(string value) { type = DataType.String; stringValue = value; }
		public static implicit operator Bundle(bool value) { return new Bundle(value); }
		public static implicit operator Bundle(long value) { return new Bundle(value); }
		public static implicit operator Bundle(double value) { return new Bundle(value); }
		public static implicit operator Bundle(string value) { return value != null ? new Bundle(value) : Empty; }
		public static implicit operator bool(Bundle b) { return b.AsBool(); }
		public static implicit operator int(Bundle b) { return b.AsInt(); }
		public static implicit operator long(Bundle b) { return b.AsLong(); }
		public static implicit operator double(Bundle b) { return b.AsDouble(); }
		public static implicit operator string(Bundle b) { return b.AsString(); }

		public virtual Bundle this[string key] {
			get { return Has(key) ? Dictionary[key] : Empty; }
			set { if (value != null && value != Empty) Dictionary[key] = value; }
		}
		public virtual Bundle this[int index] {
			get { return Array[index]; }
			set {
				if (value == null || value == Empty) return;
				if (index != -1)
					Array[index] = value;
				else
					Array.Add(value);
			}
		}
		public void Add(Bundle value) {
			Array.Add (value);
		}

		// Dictionary getters
		public bool GetBool(string key, bool defaultValue = false) {
			return Has(key) ? Dictionary[key].AsBool(defaultValue) : defaultValue;
		}
		public int GetInt(string key, int defaultValue = 0) {
			return Has(key) ? Dictionary[key].AsInt(defaultValue) : defaultValue;
		}
		public long GetLong(string key, long defaultValue = 0) {
			return Has(key) ? Dictionary[key].AsLong(defaultValue) : defaultValue;
		}
		public double GetDouble(string key, double defaultValue = 0) {
			return Has(key) ? Dictionary[key].AsDouble(defaultValue) : defaultValue;
		}
		public string GetString(string key, string defaultValue = null) {
			return Has(key) ? Dictionary[key].AsString(defaultValue) : defaultValue;
		}

		// Array getters
		public bool GetBool(int index) {
			return Array[index].AsBool();
		}
		public int GetInt(int index) {
			return Array[index].AsInt();
		}
		public long GetLong(int index) {
			return Array[index].AsLong();
		}
		public double GetDouble(int index) {
			return Array[index].AsDouble();
		}
		public string GetString(int index) {
			return Array[index].AsString();
		}

		// Key management
		public bool Has(string key) {
			return Dictionary.ContainsKey(key);
		}
		public bool IsEmpty {
			get {
				switch (type) {
					case DataType.Object: return Dictionary.Count == 0;
					case DataType.Array: return Array.Count == 0;
					case DataType.None: return true;
					default: return false;
				}
			}
		}
		public void Remove(string key) {
			Dictionary.Remove(key);
		}

		// Representations
		public DataType Type {
			get { return type; }
		}
		public bool AsBool(bool defaultValue = false) {
			switch (Type) {
				case DataType.Boolean: return longValue != 0;
				case DataType.Integer: return longValue != 0;
				case DataType.Double: return doubleValue != 0;
				case DataType.String: return String.Compare(stringValue, "true", true) == 0;
			}
			return defaultValue;
		}
		public int AsInt(int defaultValue = 0) {
			int result = defaultValue;
			switch (Type) {
				case DataType.Boolean: return (int)longValue;
				case DataType.Integer: return (int)longValue;
				case DataType.Double: return (int)doubleValue;
				case DataType.String: int.TryParse(stringValue, out result); return result;
			}
			return defaultValue;
		}
		public long AsLong(long defaultValue = 0) {
			long result = defaultValue;
			switch (Type) {
				case DataType.Boolean: return (int)longValue;
				case DataType.Integer: return (int)longValue;
				case DataType.Double: return (int)doubleValue;
				case DataType.String: long.TryParse(stringValue, out result); return result;
			}
			return defaultValue;
		}
		public double AsDouble(double defaultValue = 0) {
			double result = defaultValue;
			switch (Type) {
				case DataType.Boolean: return (int)longValue;
				case DataType.Integer: return (int)longValue;
				case DataType.Double: return (int)doubleValue;
				case DataType.String: double.TryParse(stringValue, out result); return result;
			}
			return defaultValue;
		}
		public string AsString(string defaultValue = null) {
			switch (Type) {
				case DataType.Boolean: return longValue != 0 ? "true" : "false";
				case DataType.Integer: return longValue.ToString();
				case DataType.Double: return doubleValue.ToString();
				case DataType.String: return stringValue;
			}
			return defaultValue;
		}
		public List<Bundle> AsArray() {
			return Array;
		}
		public Dictionary<string, Bundle> AsDictionary() {
			return Dictionary;
		}

		// Json methods
		public static Bundle FromJson(string json) {
			if (json == null) return null;
			return FromJson(JsonMapper.ToObject(json));
		}

		public string ToJson() {
			// IDEA One day we could probably implement the JSON by ourselves, without requiring LitJson :)
			return JsonMapper.ToJson(ToJson(this));
		}

		private static Bundle FromJson(JsonData data) {
			if (data.IsBoolean) return ((IJsonWrapper) data).GetBoolean();
			if (data.IsDouble) return ((IJsonWrapper) data).GetDouble();
			if (data.IsInt) return ((IJsonWrapper) data).GetInt();
			if (data.IsLong) return ((IJsonWrapper) data).GetLong();
			if (data.IsString) return ((IJsonWrapper) data).GetString();
			if (data.IsObject) {
				Bundle subBundle = Bundle.CreateObject();
				IDictionary dict = (IDictionary) data;
				foreach (string key in dict.Keys) {
					JsonData item = (JsonData)dict[key];
					if (item != null) subBundle[key] = FromJson(item);
				}
				return subBundle;
			}
			if (data.IsArray) {
				Bundle subBundle = Bundle.CreateArray();
				IList dict = (IList) data;
				foreach (JsonData value in dict) {
					if (value != null) subBundle.Add(FromJson(value));
				}
				return subBundle;
			}
			return null;
		}

		private JsonData ToJson(Bundle bundle) {
			JsonData target = new JsonData();
			if (bundle.Type == DataType.Object) {
				target.SetJsonType(JsonType.Object);
				foreach (KeyValuePair<string, Bundle> entry in bundle.Dictionary) {
					switch (entry.Value.Type) {
					case DataType.Boolean: target[entry.Key] = entry.Value.AsBool(); break;
					case DataType.Integer: target[entry.Key] = entry.Value.AsLong(); break;
					case DataType.Double: target[entry.Key] = entry.Value.AsDouble(); break;
					case DataType.String: target[entry.Key] = entry.Value.AsString(); break;
					default: target[entry.Key] = ToJson(entry.Value); break;
					}
				}
			}
			else {
				target.SetJsonType(JsonType.Array);
				foreach (Bundle entry in bundle.Array) {
					switch (entry.Type) {
					case DataType.Boolean: target.Add(entry.AsBool()); break;
					case DataType.Integer: target.Add(entry.AsInt()); break;
					case DataType.Double: target.Add(entry.AsDouble()); break;
					case DataType.String: target.Add(entry.AsString()); break;
					default: target.Add(ToJson(entry)); break;
					}
				}
			}
			return target;
		}

		// Private
		private List<Bundle> Array {
			get { return arrayValue ?? new List<Bundle>(); }
		}
		private Dictionary<string, Bundle> Dictionary {
			get { return objectValue ?? new Dictionary<string, Bundle>(); }
		}
	}

	/**
	 * Never instantiate this class. Use Bundle.Empty instead. Pass that everywhere an explicit configuration is not wanted.
	 */
	public class EmptyBundle : Bundle
	{
		internal EmptyBundle() : base(Bundle.DataType.None) { }

		public override Bundle this[string key]
		{
			get { return Bundle.Empty; }
			set { throw new ArgumentException("Trying to assign to non-existing bundle node. Make sure you create the appropriate hierarchy."); }
		}
		public override Bundle this[int index]
		{
			get { return Bundle.Empty; }
			set { throw new ArgumentException("Trying to assign to non-existing bundle node. Make sure you create the appropriate hierarchy."); }
		}
	}
}
