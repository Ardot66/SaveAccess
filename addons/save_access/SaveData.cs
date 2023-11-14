using Godot;
using Godot.Collections;
using System.Collections;
using System.Linq;

namespace Ardot.SaveSystems;

///<summary>
///SaveData is the main class used to store and serialize data for use by SaveAccess.<para/>
///Generally, when saving GodotObjects like nodes and resources, implement ISaveable in their scripts so that they can be saved as SaveData This is because 
///SaveData does not directly support serialization of GodotObjects like nodes and resources.<para/>
///<b>Note:</b> To make syntax simpler, SaveData is accessed the same way as an array. e.g. mySaveData[0] will get the first item stored in mySaveData. <para/>
///<b>Note:</b> SaveData can be nested and will still be serialized, even in dictionaries and arrays.<para/>
///<b>Note:</b> SaveData does not support serializing GodotObjects, but it does support some variant compatible structs, like all vectors and rects.<para/>
///</summary>
public partial class SaveData : GodotObject, IEnumerable
{
	//These show up in save files to indicate SaveData
	private static readonly StringName
	JsonKeyName = "K", //stands for key 
	JsonDataName = "D"; //stand for data

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

	///<summary>Finds whether this SaveData is 'empty'. This means that it checks recursively if every value is null, or is an array containing only null values.</summary>
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
	///This also supports serialization of certain Structs like Vector2, vector3, vector4, and rect2 (as well as their integer representations).</summary>
	public string ToJson()
	{
		return Json.Stringify(ToDictionary(), "", false);
	}

	public IEnumerator GetEnumerator()
	{
		return _data.GetEnumerator();
	}

	public override bool Equals(object obj)
	{
		if (obj is not SaveData saveData)
			return false;

		return Key.Equals(saveData.Key);
	}

	public override int GetHashCode()
	{
		return Key.GetHashCode();
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
			{JsonDataName, SerializeData(new Array(_data))},
		};

		return dictionary;

		static Variant SerializeData(Variant data)
		{
			GodotObject godotObject = data.AsGodotObject();

			if (godotObject is SaveData saveData)
				return saveData.ToDictionary();

			Array array = data.AsGodotArray();

			if (array.Count > 0)
			{
				for (int x = 0; x < array.Count; x++)
					array[x] = SerializeData(array[x]);

				return array;
			}

			Dictionary dictionary = data.AsGodotDictionary();

			if (dictionary.Count > 0)
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

			return SerializeStruct(data);
		}

		static Variant SerializeStruct(Variant value)
		{
			Array outputValue;

			switch (value.VariantType)
			{
				case Variant.Type.Vector2:
					Vector2 vector2 = value.AsVector2();
					outputValue = new() { vector2.X, vector2.Y };
					break;
				case Variant.Type.Vector2I:
					Vector2I vector2I = value.AsVector2I();
					outputValue = new() { vector2I.X, vector2I.Y };
					break;
				case Variant.Type.Vector3:
					Vector3 vector3 = value.AsVector3();
					outputValue = new() { vector3.X, vector3.Y, vector3.Z };
					break;
				case Variant.Type.Vector3I:
					Vector3I vector3I = value.AsVector3I();
					outputValue = new() { vector3I.X, vector3I.Y, vector3I.Z };
					break;
				case Variant.Type.Vector4:
					Vector4 vector4 = value.AsVector4();
					outputValue = new() { vector4.X, vector4.Y, vector4.Z, vector4.W };
					break;
				case Variant.Type.Vector4I:
					Vector4I vector4I = value.AsVector4I();
					outputValue = new() { vector4I.X, vector4I.Y, vector4I.Z, vector4I.W };
					break;
				case Variant.Type.Rect2:
					Rect2 rect2 = value.AsRect2();
					outputValue = new() { rect2.Position.X, rect2.Position.Y, rect2.Size.X, rect2.Size.Y };
					break;
				case Variant.Type.Rect2I:
					Rect2I rect2I = value.AsRect2I();
					outputValue = new() { rect2I.Position.X, rect2I.Position.Y, rect2I.Size.X, rect2I.Size.Y };
					break;
				default:
					return value;
			}

			return new Dictionary() { { value.VariantType.ToString(), outputValue } };
		}
	}

	private static SaveData FromDictionary(Dictionary dictionary)
	{
		if (dictionary.Count != 2 || !dictionary.ContainsKey(JsonKeyName) || !dictionary.ContainsKey(JsonDataName))
			return null;

		SaveData saveData = new(dictionary[JsonKeyName].AsString(), dictionary[JsonDataName].AsGodotArray());

		for (int x = 0; x < saveData._data.Length; x++)
			saveData._data[x] = DeserializeData(saveData._data[x]);

		return saveData;

		static Variant DeserializeData(Variant data)
		{
			Dictionary dataDictionary = data.AsGodotDictionary();
			Variant deserialized = DeserializeStruct(dataDictionary, out bool successful);

			if (successful)
				return deserialized;
			else if (FromDictionary(dataDictionary) is SaveData subSaveData)
				return subSaveData;
			else if (dataDictionary.Count > 0)
			{
				foreach (Variant key in dataDictionary.Keys)
				{
					Variant deserializedValue = DeserializeData(dataDictionary[key]);
					string keyString = key.AsString();

					if (keyString.StartsWith('{') && keyString.EndsWith('}'))
					{
						Variant deserializedKey = DeserializeData(Json.ParseString(keyString));

						dataDictionary.Remove(key);
						dataDictionary.Add(deserializedKey, deserializedValue);
						continue;
					}

					dataDictionary[key] = deserializedValue;
				}

				return dataDictionary;
			}

			Array dataArray = data.AsGodotArray();

			if (dataArray.Count > 0)
			{
				for (int x = 0; x < dataArray.Count; x++)
					dataArray[x] = DeserializeData(dataArray[x]);

				return dataArray;
			}

			return data;
		}

		static Variant DeserializeStruct(Dictionary serializedValue, out bool successful)
		{
			successful = false;

			if (serializedValue.Count != 1)
				return serializedValue;

			string valueTypeString = serializedValue.Keys.First().AsString();
			Array values = serializedValue[valueTypeString].AsGodotArray();

			if (values.Count == 0 || !System.Enum.TryParse(typeof(Variant.Type), valueTypeString, false, out object parseResult))
				return serializedValue;

			Variant.Type valueType = (Variant.Type)parseResult;
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
}

