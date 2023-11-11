using Godot;
using Godot.Collections;
using System.Collections;
using System.Linq;

namespace Ardot.SaveSystems;

///<summary>
///SaveData is the main class used to store and serialize data for use by SaveAccess.<para/>
///Generally, when saving GototObjects like nodes and resources, implement ISaveable in their scripts so that they can be saved as SaveData. This is because 
///SaveData does not directly support serialization of GototObjects like nodes and resources.<para/>
///<b>Note:</b> To make syntax simpler, saved data is accessed like with an array. e.g. mySaveData[1]; <para/>
///<b>Note:</b> SaveData can be nested and serialized, even in dictionaries and arrays.<para/>
///<b>Note:</b> While SaveData does not support serializing GodotObjects, it does support some variant compatible structs, like all vectors and rects.<para/>
///</summary>
public partial class SaveData : GodotObject, IEnumerable
{
	private static readonly StringName
	JsonKeyName = "K", //stands for key 
	JsonDataName = "V"; //stand for data (can't use D because otherwise data comes first in the file, making it very difficult to read)

	private SaveData(StringName key, Array data)
	{
		Key = key;
		_data = data.ToArray();
	}

	///<summary>Creates a new SaveData with a key and data.</summary>
	public SaveData(StringName key, params Variant[] data)
	{
		Key = key;
		_data = data;
	}

	public StringName Key;
	private readonly Variant[] _data;

	///<summary>How many objects are stored in this SaveData</summary>
	public int Length
	{
		get => _data.Length;
	}

	public Variant this[int index]
	{
		get => _data[index];
	}

	///<summary>Finds whether this SaveData is 'empty'. This means that it checks recursively if every value is null, or is an empty array containing only null values.</summary>
	public bool IsEmpty()
	{
		return ArrayEmpty(_data);

		static bool ArrayEmpty(IEnumerable array)
		{
			bool empty = true;

			foreach (object obj in array)
			{
				if (obj is Variant variant && variant.Obj != null)
					empty &= variant.Obj is IEnumerable i && ArrayEmpty(i);
				else if (obj != null)
					empty &= obj is IEnumerable i && ArrayEmpty(i);
			}

			return empty;
		}
	}

	///<summary>Converts this SaveData into its JSON representation. This supports recursion, so if this SaveData has another SaveData stored within it, that SaveData will be converted to JSON as well. 
	///This also supports serialization of certain Structs like Vector2, vector3, vector4, and rect2 (as well as their integer represenations).</summary>
	public string ToJson()
	{
		return Json.Stringify(ToDictionary());
	}

	public IEnumerator GetEnumerator()
	{
		return _data.GetEnumerator();
	}

	///<summary>Creates a SaveData from a JSON representation.</summary>
	public static SaveData FromJson(string json)
	{
		Dictionary dictionary = Json.ParseString(json).AsGodotDictionary();

		if (dictionary == null)
			return null;

		SaveData saveData = FromDictionary(dictionary);

		return saveData;
	}

	public static implicit operator SaveData(Variant variant)
	{
		return variant.AsGodotObject() as SaveData;
	}

	public static implicit operator Variant(SaveData saveData)
	{
		return Variant.CreateFrom(saveData);
	}

	private Dictionary ToDictionary()
	{
		Dictionary dictionary = new()
		{
			{JsonKeyName, Key},
			{JsonDataName, SerializeData(_data)},
		};

		return dictionary;

		static Variant SerializeData(object data)
		{
			if (data is Variant variant)
			{
				GodotObject godotObject = variant.AsGodotObject();

				if (godotObject is SaveData saveData)
					return saveData.ToDictionary();
				else if (variant.AsGodotArray() is Array array && array.Count > 0)
				{
					for (int x = 0; x < array.Count; x++)
						array[x] = SerializeData(array[x]);

					return array;
				}
				else if (variant.AsGodotDictionary() is Dictionary dictionary && dictionary.Count > 0)
				{
					foreach (Variant key in dictionary.Keys)
					{
						Variant serializedKey = SerializeData(key);
						Variant serializedValue = SerializeData(dictionary[key]);
						dictionary.Remove(key);
						dictionary.Add(serializedKey, serializedValue);
					}

					return dictionary;
				}

				return SerializeStruct(variant);
			}
			else if (data is Variant[] array)
			{
				for (int x = 0; x < array.Length; x++)
					array[x] = SerializeData(array[x]);

				return new Array(array);
			}

			return default;
		}
	}

	private static SaveData FromDictionary(Dictionary dictionary)
	{
		if (!dictionary.ContainsKey(JsonKeyName) || !dictionary.ContainsKey(JsonDataName))
			return null;

		string key = dictionary[JsonKeyName].AsString();
		Array data = dictionary[JsonDataName].AsGodotArray();

		if (key == null || data == null)
			return null;

		SaveData saveData = new(key, data);

		for (int x = 0; x < saveData._data.Length; x++)
			saveData._data[x] = DeserializeData(saveData._data[x]);

		return saveData;

		static Variant DeserializeData(object data)
		{
			if (data is Variant variant)
			{
				Variant deserialized = DeserializeStruct(variant, out bool successful);

				if (successful)
					return deserialized;
				else if (variant.AsGodotDictionary() is Dictionary subDictionary && FromDictionary(subDictionary) is SaveData subSaveData)
					return subSaveData;
				else if (variant.AsString() is string resourcePath && resourcePath.StartsWith("res://"))
					return ResourceLoader.Load(resourcePath);
				else if (variant.AsGodotArray() is Array array && array.Count > 0)
				{
					for (int x = 0; x < array.Count; x++)
						array[x] = DeserializeData(array[x]);

					return array;
				}
				else if (variant.AsGodotDictionary() is Dictionary dictionary && dictionary.Count > 0)
				{
					foreach (Variant key in dictionary.Keys)
					{
						Variant deserializedValue = DeserializeData(dictionary[key]);

						if (key.AsString() is string str && str.StartsWith('{') && str.EndsWith('}'))
						{
							Variant deserializedKey = DeserializeData(Json.ParseString(str));

							dictionary.Remove(key);
							dictionary.Add(deserializedKey, deserializedValue);
							continue;
						}

						dictionary[key] = deserializedValue;
					}

					return dictionary;
				}

				return variant;
			}
			else if (data is Variant[] array)
			{
				for (int x = 0; x < array.Length; x++)
					array[x] = DeserializeData(array[x]);

				return new Array(array);
			}

			return default;
		}
	}

	private static Variant SerializeStruct(Variant value)
	{
		string valueType = value.VariantType.ToString();
		Dictionary output = new();

		switch (value.VariantType)
		{
			case Variant.Type.Vector2:
				Vector2 vector2 = value.AsVector2();
				output.Add(valueType, new Array() { vector2.X, vector2.Y });
				break;
			case Variant.Type.Vector2I:
				Vector2I vector2I = value.AsVector2I();
				output.Add(valueType, new Array() { vector2I.X, vector2I.Y });
				break;
			case Variant.Type.Vector3:
				Vector3 vector3 = value.AsVector3();
				output.Add(valueType, new Array() { vector3.X, vector3.Y, vector3.Z });
				break;
			case Variant.Type.Vector3I:
				Vector3I vector3I = value.AsVector3I();
				output.Add(valueType, new Array() { vector3I.X, vector3I.Y, vector3I.Z });
				break;
			case Variant.Type.Vector4:
				Vector4 vector4 = value.AsVector4();
				output.Add(valueType, new Array() { vector4.X, vector4.Y, vector4.Z, vector4.W });
				break;
			case Variant.Type.Vector4I:
				Vector4I vector4I = value.AsVector4I();
				output.Add(valueType, new Array() { vector4I.X, vector4I.Y, vector4I.Z, vector4I.W });
				break;
			case Variant.Type.Rect2:
				Rect2 rect2 = value.AsRect2();
				output.Add(valueType, new Array() { rect2.Position.X, rect2.Position.Y, rect2.Size.X, rect2.Size.Y });
				break;
			case Variant.Type.Rect2I:
				Rect2I rect2I = value.AsRect2I();
				output.Add(valueType, new Array() { rect2I.Position.X, rect2I.Position.Y, rect2I.Size.X, rect2I.Size.Y });
				break;
			default:
				return value;
		}

		return output;
	}

	private static Variant DeserializeStruct(Variant serializedValue, out bool successful)
	{
		Dictionary input = serializedValue.AsGodotDictionary();
		successful = false;

		if (input.Count != 1)
			return serializedValue;

		string valueTypeString = input.Keys.First().AsString();

		if (!System.Enum.TryParse(typeof(Variant.Type), valueTypeString, false, out object parseResult))
			return serializedValue;

		Variant.Type valueType = (Variant.Type)parseResult;
		Array values = input[valueTypeString].AsGodotArray();

		successful = true;

		return valueType switch
		{
			Variant.Type.Vector2 => (Variant)new Vector2((float)values[0], (float)values[1]),
			Variant.Type.Vector2I => (Variant)new Vector2I(values[0].AsInt32(), values[1].AsInt32()),
			Variant.Type.Vector3 => (Variant)new Vector3((float)values[0], (float)values[1], (float)values[2]),
			Variant.Type.Vector3I => (Variant)new Vector3I(values[0].AsInt32(), values[1].AsInt32(), values[2].AsInt32()),
			Variant.Type.Vector4 => (Variant)new Vector4((float)values[0], (float)values[1], (float)values[2], (float)values[3]),
			Variant.Type.Vector4I => (Variant)new Vector4I(values[0].AsInt32(), values[1].AsInt32(), values[2].AsInt32(), values[3].AsInt32()),
			Variant.Type.Rect2 => (Variant)new Rect2((float)values[0], (float)values[1], (float)values[2], (float)values[3]),
			Variant.Type.Rect2I => (Variant)new Rect2I(values[0].AsInt32(), values[1].AsInt32(), values[2].AsInt32(), values[3].AsInt32()),
			_ => serializedValue,
		};
	}
}

