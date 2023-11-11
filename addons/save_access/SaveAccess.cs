using Godot;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace Ardot.SaveSystems;

///<summary>
///This class can be used to save and load the state of a game, and is designed to be easier to use than FileAccess. <para/>
///<b>Note:</b> Due to storing the entire save file in memory, this class is not intended exist for long periods of time.<para/>
///Core functions include: <br/>
/// - Open(string filePath) *and all its variants<br/>
/// - SaveTree(Node root)<br/>
/// - SaveObject(ISaveable saveObject)<br/>
/// - SaveData(SaveData saveData)<br/>
/// - LoadTree(Node root)<br/>
/// - LoadObject(ISaveable loadObject)<br/>
/// - LoadData(StringName key)<br/>
/// - RemoveData(StringName key)<br/>
/// - Clear()<br/>
/// - Commit()
///</summary>

public partial class SaveAccess
{
	private readonly SaveData[] fileSaveData;
	private readonly List<SaveData> fileAppendedSaveData = new ();
	private Func<FileAccess> initFileAccess;

	protected SaveAccess (FileAccess readAccess)
	{ 
		if(readAccess != null)
			fileSaveData = GetFileData(readAccess);
		else
			fileSaveData = Array.Empty<SaveData>();
	}

	///<summary>Opens a SaveAccess to filePath.</summary>
	public static SaveAccess Open(string filePath)
	{
		FileAccess readAccess = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);  
		
		SaveAccess saveAccess = new (readAccess);
		readAccess?.Dispose();

		saveAccess.initFileAccess = () => FileAccess.Open(filePath, FileAccess.ModeFlags.WriteRead);

		return saveAccess;
	}

	///<summary>Opens a SaveAccess to filePath that reads and writes a compressed file.</summary>
	public static SaveAccess OpenCompressed(string filePath, FileAccess.CompressionMode compressionMode = FileAccess.CompressionMode.Fastlz)
	{
		FileAccess readAccess = FileAccess.OpenCompressed(filePath, FileAccess.ModeFlags.Read, compressionMode);  
		
		SaveAccess saveAccess = new (readAccess);
		readAccess?.Dispose();

		saveAccess.initFileAccess = () =>  FileAccess.OpenCompressed(filePath, FileAccess.ModeFlags.WriteRead, compressionMode);

		return saveAccess;
	}

	///<summary>
	///Opens a SaveAccess to filePath that reads and writes to an encrypted file using a binary key.<para/>
	///Note: The provided key must be 32 bytes long.<para/>
	///</summary>
	public static SaveAccess OpenEncrypted(string filePath, byte[] key)
	{
		FileAccess readAccess = FileAccess.OpenEncrypted(filePath, FileAccess.ModeFlags.Read, key);  
		
		SaveAccess saveAccess = new (readAccess);
		readAccess?.Dispose();

		saveAccess.initFileAccess = () =>  FileAccess.OpenEncrypted(filePath, FileAccess.ModeFlags.WriteRead, key);

		return saveAccess;
	}

	///<summary>Opens a SaveAccess to filePath that reads and writes to an encrypted file using a string password.</summary>
	public static SaveAccess OpenEncryptedWithPass(string filePath, string pass)
	{
		FileAccess readAccess = FileAccess.OpenEncryptedWithPass(filePath, FileAccess.ModeFlags.Read, pass);  
		
		SaveAccess saveAccess = new (readAccess);
		readAccess?.Dispose();

		saveAccess.initFileAccess = () => FileAccess.OpenEncryptedWithPass(filePath, FileAccess.ModeFlags.WriteRead, pass);

		return saveAccess;
	}
	
	///<summary>Queues all ISaveable children of root (recursively) to be saved when Commit() is called.</summary>
	public void SaveTree(Node root)
	{
		RunInChildrenRecursive(root, (ISaveable node) => {SaveObject(node);});
	}

	///<summary>Queues an ISaveable object to be saved when Commit() is called.</summary>
	public void SaveObject(ISaveable saveObject)
	{
		SaveData(saveObject.Save());
	}

	///<summary>Queues saveData to be saved when Commit() is called.</summary>
	public void SaveData(SaveData saveData)
	{
		SaveData oldSaveData = LoadData(saveData.key, out int dataIndex);

		if(oldSaveData != null)
		{
			if(dataIndex < fileSaveData.Length)
			{
				fileSaveData[dataIndex] = saveData;
				return;
			}
			else
			{
				fileSaveData[dataIndex - fileSaveData.Length] = saveData;
				return;
			}
		}

		fileAppendedSaveData.Add(saveData);
	}

	///<summary>Loads all ISaveable children of root (recursively).</summary>
	public void LoadTree(Node root)
	{
		RunInChildrenRecursive(root, (ISaveable node) => {LoadObject(node);});
	}

	///<summary>Loads an ISaveable object.</summary>
	public void LoadObject(ISaveable loadObject)
	{
		loadObject.Load(LoadData(loadObject.GetLoadKey()));
	}

	///<summary>Returns the SaveData with the specified key.</summary>
	public SaveData LoadData(StringName key)
	{
		return LoadData(key, out _);
	}

	///<summary>Queues the SaveData with the specified key to be removed when Commit() is called.</summary>
	public void RemoveData(StringName key)
	{
		SaveData data = LoadData(key, out int index);

		if(data != null)
		{
			if(index < fileSaveData.Length)
			{
				fileSaveData[index] = null;
				return;
			}
			else
			{
				fileSaveData[index - fileSaveData.Length] = null;
				return;
			}
		}
	}

	///<summary>Queues all SaveData to be removed when Commit() is called.</summary>
	public void Clear()
	{
		Array.Clear(fileSaveData);
		fileAppendedSaveData.Clear();
	}

	///<summary>
	///Commits all changes to the file.<para/>
	///<b>Note:</b> Only call commit when you actually need it, there could be a significant performance impact from repeated commits.<para/>
	///</summary>
	public void Commit()
	{
		FileAccess fileAccess = initFileAccess.Invoke();

		foreach(SaveData data in fileSaveData)
			if(data != null)
				fileAccess.StoreLine(data.ToJson());

		foreach(SaveData data in fileAppendedSaveData)
			if(data != null)
				fileAccess.StoreLine(data.ToJson());
		
		fileAccess.Close();
		fileAccess.Dispose();
	}

	private SaveData LoadData(StringName key, out int index)
	{
		index = 0;

		foreach(SaveData data in fileSaveData)
		{
			if(data.key == key)
				return data;
			index++;
		}

		foreach(SaveData data in fileAppendedSaveData)
		{
			if(data.key == key)
				return data;
			index++;
		}

		index = -1;
		return null;
	}

	private static SaveData[] GetFileData(FileAccess fileAccess)
	{
		string[] stringFileData = fileAccess.GetAsText().Split('\n', StringSplitOptions.RemoveEmptyEntries);
		SaveData[] fileData = new SaveData[stringFileData.Length];
		int x = 0;

		foreach(string data in stringFileData) 
		{
			fileData[x] = SaveSystems.SaveData.FromJson(data);
			x++;
		}

		return fileData;
	}

	private static void RunInChildrenRecursive<T>(Node parent, Action<T> action)
	{
		RunInChildrenRecursive(parent, (Node node) => {if(node is T t) action.Invoke(t);});
	}

	private static void RunInChildrenRecursive(Node parent, Action<Node> action)
	{
		RunInChildren(parent);

		void RunInChildren(Node parentNode)
		{
			foreach(Node node in parentNode.GetChildren())
			{
				if(node.GetChildCount() > 0)
					RunInChildren(node);

				action.Invoke(node);
			}
		}
	}
}   

///<summary>
///An interface that allows objects to be saved and serialized as SaveData. <para/>
///This interface comes with a default implementation. (see the source code) <para/>
///<b>Note:</b> The 'parameters' field on all functions is only intended to be used for complex applications where extra data is needed for saving.
///It is not used by SaveAccess or SaveData.<para/>
///</summary>
public interface ISaveable
{
	///<summary>Called when the object is being saved.</summary>
	public SaveData Save(params Variant[] parameters);
	///<summary>Called when the object is being loaded, set up your object here.</summary>
	public void Load(SaveData data, params Variant[] parameters);
	///<summary>Called when the object is being loaded to properly identify what data to load it with.</summary>
	public StringName GetLoadKey(params Variant[] parameters);
	
	//default implementation:

	/*
	public SaveData Save(params Variant[] parameters)
	{
		return new SaveData(GetLoadKey(), null); //Put stuff that needs saving where null is. 
	}   

	public void Load(SaveData data, params Variant[] parameters)
	{
		//Checking if data is null, in case something went wrong.
		if(data == null)
			return;

		//Load things here
	}

	public StringName GetLoadKey(params Variant[] parameters)
	{
		return ""; //For nodes use 'Name' or GetPath(), for Resources use 'ResourcePath'.
	}
	*/
}

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
	jsonKeyName = "K", //stands for key 
	jsonDataName = "V"; //stand for data (can't use D because otherwise data comes first in the file, making it very difficult to read)

	private SaveData(StringName key, Godot.Collections.Array data)
	{
		this.key = key;
		this.data = data.ToArray();
	}

	///<summary>Creates a new SaveData with a key and data.</summary>
	public SaveData(StringName key, params Variant[] data)
	{
		this.key = key;
		this.data = data;
	}

	public StringName key;
	
	///<summary>How many objects are stored in this SaveData</summary>
	public int Length
	{
		get => data.Length;
	}

	public Variant this[int index]
	{
		get => data[index];
	}

	private readonly Variant[] data;

	///<summary>Finds whether this SaveData is 'empty'. This means that it checks recursively if every value is null, or is an empty array containing only null values.</summary>
	public bool IsEmpty()
	{
		return ArrayEmpty(data);

		static bool ArrayEmpty(IEnumerable array)
		{
			bool empty = true;

			foreach(object obj in array)
			{
				if(obj is Variant variant && variant.Obj != null)
					empty &= variant.Obj is IEnumerable i && ArrayEmpty(i);
				else if(obj != null)
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
		return data.GetEnumerator();
	}

	///<summary>Creates a SaveData from a JSON representation.</summary>
	public static SaveData FromJson(string json)
	{
		Godot.Collections.Dictionary dictionary = Json.ParseString(json).AsGodotDictionary();

		if(dictionary == null)
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

	private Godot.Collections.Dictionary ToDictionary()
	{
		Godot.Collections.Dictionary dictionary = new()
		{
			{jsonKeyName, key},
			{jsonDataName, SerializeData(data)},
		};

		return dictionary;

		static Variant SerializeData(object data)
		{
			if(data is Variant variant)
			{
				GodotObject godotObject = variant.AsGodotObject();

				if(godotObject is SaveData saveData)
					return saveData.ToDictionary();
				else if(variant.AsGodotArray() is Godot.Collections.Array array && array.Count > 0)
				{
					for(int x = 0; x < array.Count; x++)
						array[x] = SerializeData(array[x]);
					
					return array;
				}
				else if(variant.AsGodotDictionary() is Godot.Collections.Dictionary dictionary && dictionary.Count > 0)
				{
					foreach(Variant key in dictionary.Keys)
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
			else if(data is Variant[] array)
			{
				for(int x = 0; x < array.Length; x++)
					array[x] = SerializeData(array[x]);
				
				return new Godot.Collections.Array(array);
			}

			return default;
		}
	}

	private static SaveData FromDictionary(Godot.Collections.Dictionary dictionary)
	{
		if(!dictionary.ContainsKey(jsonKeyName) || !dictionary.ContainsKey(jsonDataName))
			return null;

		string key = dictionary[jsonKeyName].AsString();
		Godot.Collections.Array data = dictionary[jsonDataName].AsGodotArray();

		if(key == null || data == null)
			return null;

		SaveData saveData = new (key, data);

		for(int x = 0; x < saveData.data.Length; x++)
			saveData.data[x] = DeserializeData(saveData.data[x]);

		return saveData;

		static Variant DeserializeData(object data)
		{
			if(data is Variant variant)
			{
				Variant deserialized = DeserializeStruct(variant, out bool successful);

				if(successful)
					return deserialized;
				else if(variant.AsGodotDictionary() is Godot.Collections.Dictionary subDictionary && FromDictionary(subDictionary) is SaveData subSaveData)
					return subSaveData;
				else if(variant.AsString() is string resourcePath && resourcePath.StartsWith("res://"))
					return ResourceLoader.Load(resourcePath);
				else if(variant.AsGodotArray() is Godot.Collections.Array array && array.Count > 0)
				{
					for(int x = 0; x < array.Count; x++)
						array[x] = DeserializeData(array[x]);
					
					return array;
				}
				else if(variant.AsGodotDictionary() is Godot.Collections.Dictionary dictionary && dictionary.Count > 0)
				{
					foreach(Variant key in dictionary.Keys)
					{
						Variant deserializedValue = DeserializeData(dictionary[key]);

						if(key.AsString() is string str && str.StartsWith('{') && str.EndsWith('}'))
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
			else if(data is Variant[] array)
			{
				for(int x = 0; x < array.Length; x++)
					array[x] = DeserializeData(array[x]);
				
				return new Godot.Collections.Array(array);
			}

			return default;
		}
	}

	private static Variant SerializeStruct(Variant value)
	{
		string valueType = value.VariantType.ToString();
		Godot.Collections.Dictionary output = new();

		switch (value.VariantType)
		{
			case Variant.Type.Vector2 : 
			Vector2 vector2 = value.AsVector2();
			output.Add(valueType, new Godot.Collections.Array() {vector2.X, vector2.Y});
			break;
			case Variant.Type.Vector2I : 
			Vector2I vector2I = value.AsVector2I();
			output.Add(valueType, new Godot.Collections.Array() {vector2I.X, vector2I.Y});
			break;
			case Variant.Type.Vector3 : 
			Vector3 vector3 = value.AsVector3();
			output.Add(valueType, new Godot.Collections.Array() {vector3.X, vector3.Y, vector3.Z});
			break;
			case Variant.Type.Vector3I : 
			Vector3I vector3I = value.AsVector3I();
			output.Add(valueType, new Godot.Collections.Array() {vector3I.X, vector3I.Y, vector3I.Z});
			break;
			case Variant.Type.Vector4 : 
			Vector4 vector4 = value.AsVector4();
			output.Add(valueType, new Godot.Collections.Array() {vector4.X, vector4.Y, vector4.Z, vector4.W});
			break;
			case Variant.Type.Vector4I : 
			Vector4I vector4I = value.AsVector4I();
			output.Add(valueType, new Godot.Collections.Array() {vector4I.X, vector4I.Y, vector4I.Z, vector4I.W});
			break;
			case Variant.Type.Rect2 : 
			Rect2 rect2 = value.AsRect2();
			output.Add(valueType, new Godot.Collections.Array() {rect2.Position.X, rect2.Position.Y, rect2.Size.X, rect2.Size.Y});
			break;
			case Variant.Type.Rect2I : 
			Rect2I rect2I = value.AsRect2I();
			output.Add(valueType, new Godot.Collections.Array() {rect2I.Position.X, rect2I.Position.Y, rect2I.Size.X, rect2I.Size.Y});
			break;
			default : 
			return value;
		}

		return output;
	}

	private static Variant DeserializeStruct(Variant serializedValue, out bool successful)
	{
		Godot.Collections.Dictionary input = serializedValue.AsGodotDictionary();
		successful = false;

		if(input.Count != 1)
			return serializedValue;

		string valueTypeString = input.Keys.First().AsString();

		if(!Enum.TryParse(typeof(Variant.Type), valueTypeString, false, out object parseResult))
			return serializedValue;
		
		Variant.Type valueType = (Variant.Type)parseResult;
		Godot.Collections.Array values = input[valueTypeString].AsGodotArray();

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

