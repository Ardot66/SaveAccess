using Godot;
using Godot.Collections;
using System.Collections;
using System.Linq;

namespace Ardot.SaveSystems;

///<summary>
///<c>SaveData</c> is the main class used to store and serialize data for use by <c>SaveAccess</c> and other systems.<para/>
///Generally, when saving objects like nodes and resources, implement <c>ISaveable</c> in their scripts so that they can be saved as <c>SaveData</c>. 
///This is because <c>SaveData</c> does not directly support serialization of objects like nodes and resources.
///Also, <c>ISaveable</c> objects will not be automatically saved and loaded if entered into <c>SaveData</c> and must be saved and loaded manually.<para/>
///<b>Note:</b> To make syntax simpler, <c>SaveData</c> is accessed the same way as an array.
///e.g. <c>mySaveData[0]</c> will get the first item stored in <c>mySaveData</c>. <para/>
///<b>Note:</b> <c>SaveData</c> can be nested and will still be serialized, even in dictionaries and arrays. 
///It also supports serializing some variant compatible structs, like all vectors and rects.<para/>
///<b>Functions:</b><br/>
///- <c>IsEmpty()</c><br/>
///- <c>ToJson()</c> *always use this instead of <c>JSON.Stringify()</c><br/> 
///- <c>FromJson(string json)</c> *static *always use this instead of <c>JSON.ParseString()</c><br/>
///</summary>

public sealed partial class SaveData : GodotObject, IEnumerable
{
	//These show up in save files to indicate SaveData and other values (These can be modified)
	private const string
	JsonKeyName = "K", //stands for key 
	JsonDataName = "D", //stand for data
	JsonVector2Name = "Vector2",
	JsonVector2IName = "Vector2I",
	JsonVector3Name = "Vector3",
	JsonVector3IName = "Vector3I",
	JsonVector4Name = "Vector4",
	JsonVector4IName = "Vector4I",
	JsonRect2Name = "Rect2",
	JsonRect2IName = "Rect2I";

	private static readonly StringName
	JsonKeyStringName = JsonKeyName,
	JsonDataStringName = JsonDataName;

	private SaveData(StringName key, Array data)
	{
		Key = key;
		_data = data.ToArray();
	}

	///<summary>Creates a new <c>SaveData</c> with a key and data.</summary>
	public SaveData(StringName key, params Variant[] data)
	{
		Key = key;
		_data = data;
	}

	public StringName Key;
	private readonly Variant[] _data;

	///<summary>How many objects are stored in this <c>SaveData</c>.</summary>
	public int Length
	{
		get => _data.Length;
	}

	public Variant this[int index]
	{
		get => _data[index];
	}

	///<summary>Finds whether this <c>SaveData</c> is 'empty'. This means that it checks recursively if every value is <c>null</c>, 
	///or is an array containing only <c>null</c> values.</summary>
	///<returns>Whether this <c>SaveData</c> is 'empty'.</returns>
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

	///<summary>Converts this <c>SaveData</c> into its JSON representation. This supports recursion, so if this <c>SaveData</c> has another <c>SaveData<c> stored within it, that <c>SaveData</c> will be converted to JSON as well. 
	///This also supports serialization of some variant compatible structs, like all vectors and rects.</summary>
	public string ToJson()
	{
		return Json.Stringify(SerializeObject(this), "", false);
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

	///<summary>Creates a <c>SaveData</c> from its JSON representation.</summary>
	///<returns><c>null</c> if <c>json</c> cannot be parsed.</returns>
	public static SaveData FromJson(string json)
	{
		return DeserializeObject(Json.ParseString(json)).AsGodotObject() as SaveData;
	}

	public static implicit operator SaveData(Variant variant)
	{
		return variant.AsGodotObject() as SaveData;
	}

	public static implicit operator Variant(SaveData saveData)
	{
		return Variant.CreateFrom(saveData);
	}

	static Variant SerializeObject(Variant data)
	{
		switch (data.VariantType)
		{
			case Variant.Type.Object:
				GodotObject godotObject = data.AsGodotObject();

				if (godotObject is SaveData saveData)
				{
					return new Dictionary()
					{
						{JsonKeyStringName, saveData.Key},
						{JsonDataStringName, SerializeObject(new Array(saveData._data))},
					};
				}

				return data;
			case Variant.Type.Dictionary:
				Dictionary dictionary = data.AsGodotDictionary();

				foreach (Variant key in dictionary.Keys)
				{
					Variant serializedKey = SerializeObject(key);
					Variant serializedValue = SerializeObject(dictionary[key]);
					dictionary.Remove(key);
					dictionary.Add(serializedKey, serializedValue);
				}

				return dictionary;
			case Variant.Type.Array:
				Array array = data.AsGodotArray();

				for (int x = 0; x < array.Count; x++)
					array[x] = SerializeObject(array[x]);

				return array;
			case Variant.Type.String:
			case Variant.Type.StringName:
				string dataString = data.AsString();

				if (dataString.StartsWith('$'))
					return "$" + dataString;

				return dataString switch
				{
					JsonKeyName or JsonDataName or JsonVector2Name or JsonVector2IName or JsonVector3Name or JsonVector3IName or JsonVector4Name or JsonVector4IName or JsonRect2Name or JsonRect2IName => "$" + dataString,
					_ => dataString,
				};
			case Variant.Type.Vector2:
				Vector2 vector2 = data.AsVector2();
				return new Dictionary() { { JsonVector2Name, new Array() { vector2.X, vector2.Y } } };
			case Variant.Type.Vector2I:
				Vector2I vector2I = data.AsVector2I();
				return new Dictionary() { { JsonVector2IName, new Array() { vector2I.X, vector2I.Y } } };
			case Variant.Type.Vector3:
				Vector3 vector3 = data.AsVector3();
				return new Dictionary() { { JsonVector3Name, new Array() { vector3.X, vector3.Y, vector3.Z } } };
			case Variant.Type.Vector3I:
				Vector3I vector3I = data.AsVector3I();
				return new Dictionary() { { JsonVector3IName, new Array() { vector3I.X, vector3I.Y, vector3I.Z } } };
			case Variant.Type.Vector4:
				Vector4 vector4 = data.AsVector4();
				return new Dictionary() { { JsonVector4Name, new Array() { vector4.X, vector4.Y, vector4.Z, vector4.W } } };
			case Variant.Type.Vector4I:
				Vector4I vector4I = data.AsVector4I();
				return new Dictionary() { { JsonVector4IName, new Array() { vector4I.X, vector4I.Y, vector4I.Z, vector4I.W } } };
			case Variant.Type.Rect2:
				Rect2 rect2 = data.AsRect2();
				return new Dictionary() { { JsonRect2Name, new Array() { rect2.Position.X, rect2.Position.Y, rect2.Size.X, rect2.Size.Y } } };
			case Variant.Type.Rect2I:
				Rect2I rect2I = data.AsRect2I();
				return new Dictionary() { { JsonRect2IName, new Array() { rect2I.Position.X, rect2I.Position.Y, rect2I.Size.X, rect2I.Size.Y } } };
		}

		return data;
	}
	
	static Variant DeserializeObject(Variant data)
	{
		switch (data.VariantType)
		{
			case Variant.Type.Array:
				Array dataArray = data.AsGodotArray();

				for (int x = 0; x < dataArray.Count; x++)
					dataArray[x] = DeserializeObject(dataArray[x]);
					
				return dataArray;
			case Variant.Type.Dictionary:
				Dictionary dataDictionary = data.AsGodotDictionary();

				if (dataDictionary.Count == 1)
				{
					string valueTypeString = dataDictionary.Keys.First().AsString();
					Array values = dataDictionary[valueTypeString].AsGodotArray();

					Variant output = valueTypeString switch
					{
						JsonVector2Name => (Variant)new Vector2((float)values[0], (float)values[1]),
						JsonVector2IName => (Variant)new Vector2I(values[0].AsInt32(), values[1].AsInt32()),
						JsonVector3Name => (Variant)new Vector3((float)values[0], (float)values[1], (float)values[2]),
						JsonVector3IName => (Variant)new Vector3I(values[0].AsInt32(), values[1].AsInt32(), values[2].AsInt32()),
						JsonVector4Name => (Variant)new Vector4((float)values[0], (float)values[1], (float)values[2], (float)values[3]),
						JsonVector4IName => (Variant)new Vector4I(values[0].AsInt32(), values[1].AsInt32(), values[2].AsInt32(), values[3].AsInt32()),
						JsonRect2Name => (Variant)new Rect2((float)values[0], (float)values[1], (float)values[2], (float)values[3]),
						JsonRect2IName => (Variant)new Rect2I(values[0].AsInt32(), values[1].AsInt32(), values[2].AsInt32(), values[3].AsInt32()),
						_ => 0,
					};

					if (output.VariantType != Variant.Type.Int)
						return output;
				}
				else if (dataDictionary.Count == 2 && dataDictionary.ContainsKey(JsonKeyStringName) && dataDictionary.ContainsKey(JsonDataStringName))
				{
					SaveData saveData = new(dataDictionary[JsonKeyStringName].AsString(), dataDictionary[JsonDataStringName].AsGodotArray());

					for (int x = 0; x < saveData._data.Length; x++)
						saveData._data[x] = DeserializeObject(saveData._data[x]);

					return saveData;
				}

				foreach (Variant key in dataDictionary.Keys)
				{
					Variant deserializedValue = DeserializeObject(dataDictionary[key]);
					Variant deserializedKey;
					string keyString = key.AsString();

					if (keyString.StartsWith('{') && keyString.EndsWith('}'))
						deserializedKey = DeserializeObject(Json.ParseString(keyString));				
					else
						deserializedKey = DeserializeObject(keyString);

					dataDictionary.Remove(key);
					dataDictionary.Add(deserializedKey, deserializedValue);
				}

				return dataDictionary;
			case Variant.Type.StringName:
			case Variant.Type.String:
				string dataString = data.AsString();

				if (dataString.StartsWith('$'))
					return dataString[1..];

				break;
		}

		return data;
	}
}

