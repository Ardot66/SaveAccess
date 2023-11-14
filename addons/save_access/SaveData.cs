using Godot;
using Godot.Collections;
using Microsoft.VisualBasic;
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
			switch (data.VariantType)
			{
				case Variant.Type.Object:
					GodotObject godotObject = data.AsGodotObject();

					if (godotObject is SaveData saveData)
						return saveData.ToDictionary();

					return data;

				case Variant.Type.Dictionary:
					Dictionary dictionary = data.AsGodotDictionary();

					foreach (Variant key in dictionary.Keys)
					{
						Variant serializedKey = SerializeData(key);
						Variant serializedValue = SerializeData(dictionary[key]);
						dictionary.Remove(key);
						dictionary.Add(serializedKey, serializedValue);
					}

					return dictionary;

				case Variant.Type.Array:
					Array array = data.AsGodotArray();

					for (int x = 0; x < array.Count; x++)
						array[x] = SerializeData(array[x]);

					return array;
				case Variant.Type.Vector2:
					Vector2 vector2 = data.AsVector2();
					return new Dictionary() { { data.VariantType.ToString(), new Array() { vector2.X, vector2.Y } } };
				case Variant.Type.Vector2I:
					Vector2I vector2I = data.AsVector2I();
					return new Dictionary() { { data.VariantType.ToString(), new Array() { vector2I.X, vector2I.Y } } };
				case Variant.Type.Vector3:
					Vector3 vector3 = data.AsVector3();
					return new Dictionary() { { data.VariantType.ToString(), new Array() { vector3.X, vector3.Y, vector3.Z } } };
				case Variant.Type.Vector3I:
					Vector3I vector3I = data.AsVector3I();
					return new Dictionary() { { data.VariantType.ToString(), new Array() { vector3I.X, vector3I.Y, vector3I.Z } } };
				case Variant.Type.Vector4:
					Vector4 vector4 = data.AsVector4();
					return new Dictionary() { { data.VariantType.ToString(), new Array() { vector4.X, vector4.Y, vector4.Z, vector4.W } } };
				case Variant.Type.Vector4I:
					Vector4I vector4I = data.AsVector4I();
					return new Dictionary() { { data.VariantType.ToString(), new Array() { vector4I.X, vector4I.Y, vector4I.Z, vector4I.W } } };
				case Variant.Type.Rect2:
					Rect2 rect2 = data.AsRect2();
					return new Dictionary() { { data.VariantType.ToString(), new Array() { rect2.Position.X, rect2.Position.Y, rect2.Size.X, rect2.Size.Y } } };
				case Variant.Type.Rect2I:
					Rect2I rect2I = data.AsRect2I();
					return new Dictionary() { { data.VariantType.ToString(), new Array() { rect2I.Position.X, rect2I.Position.Y, rect2I.Size.X, rect2I.Size.Y } } };
			}

			return data;
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
			switch (data.VariantType)
			{
				case Variant.Type.Array:
					Array dataArray = data.AsGodotArray();

					for (int x = 0; x < dataArray.Count; x++)
						dataArray[x] = DeserializeData(dataArray[x]);

					return dataArray;
				case Variant.Type.Dictionary:
					Dictionary dataDictionary = data.AsGodotDictionary();

					if (dataDictionary.Count == 1)
					{
						string valueTypeString = dataDictionary.Keys.First().AsString();
						Array values = dataDictionary[valueTypeString].AsGodotArray();

						if (values.Count != 0 && System.Enum.TryParse(typeof(Variant.Type), valueTypeString, false, out object parseResult))
						{
							Variant.Type valueType = (Variant.Type)parseResult;

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
								_ => data,
							};
						}
					}
					else if (FromDictionary(dataDictionary) is SaveData subSaveData)
						return subSaveData;

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

			return data;
		}
	}
}