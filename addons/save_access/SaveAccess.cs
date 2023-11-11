using Godot;
using System;
using System.Collections.Generic;

namespace Ardot.SaveSystems;

///<summary>
///This class can be used to save and load the state of a game, and is designed to be easier to use than FileAccess. <para/>
///<b>Note:</b> Due to storing the entire save file in memory, this class is not intended exist for long periods of time.<para/>
///Core functions include: <br/>
/// - Open(string filePath) *and all its variants<br/>
/// - Commit()<br/>
/// - SaveTree(Node root)<br/>
/// - SaveObject(ISaveable saveObject)<br/>
/// - SaveData(SaveData saveData)<br/>
/// - LoadTree(Node root)<br/>
/// - LoadObject(ISaveable loadObject)<br/>
/// - LoadData(StringName key)<br/>
/// - RemoveData(StringName key)<br/>
/// - Clear()<br/>
///</summary>
public partial class SaveAccess
{
	private readonly SaveData[] _fileSaveData;
	private readonly List<SaveData> _fileAppendedSaveData = new();
	private Func<FileAccess> _initFileAccess;

	protected SaveAccess(FileAccess readAccess)
	{
		if (readAccess != null)
			_fileSaveData = GetFileData(readAccess);
		else
			_fileSaveData = Array.Empty<SaveData>();
	}

	///<summary>Opens a SaveAccess to filePath.</summary>
	public static SaveAccess Open(string filePath)
	{
		FileAccess readAccess = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);

		SaveAccess saveAccess = new(readAccess);
		readAccess?.Dispose();

		saveAccess._initFileAccess = () => FileAccess.Open(filePath, FileAccess.ModeFlags.WriteRead);

		return saveAccess;
	}

	///<summary>Opens a SaveAccess to filePath that reads and writes a compressed file.</summary>
	public static SaveAccess OpenCompressed(string filePath, FileAccess.CompressionMode compressionMode = FileAccess.CompressionMode.Fastlz)
	{
		FileAccess readAccess = FileAccess.OpenCompressed(filePath, FileAccess.ModeFlags.Read, compressionMode);

		SaveAccess saveAccess = new(readAccess);
		readAccess?.Dispose();

		saveAccess._initFileAccess = () => FileAccess.OpenCompressed(filePath, FileAccess.ModeFlags.WriteRead, compressionMode);

		return saveAccess;
	}

	///<summary>
	///Opens a SaveAccess to filePath that reads and writes to an encrypted file using a binary key.<para/>
	///Note: The provided key must be 32 bytes long.<para/>
	///</summary>
	public static SaveAccess OpenEncrypted(string filePath, byte[] key)
	{
		FileAccess readAccess = FileAccess.OpenEncrypted(filePath, FileAccess.ModeFlags.Read, key);

		SaveAccess saveAccess = new(readAccess);
		readAccess?.Dispose();

		saveAccess._initFileAccess = () => FileAccess.OpenEncrypted(filePath, FileAccess.ModeFlags.WriteRead, key);

		return saveAccess;
	}

	///<summary>Opens a SaveAccess to filePath that reads and writes to an encrypted file using a string password.</summary>
	public static SaveAccess OpenEncryptedWithPass(string filePath, string pass)
	{
		FileAccess readAccess = FileAccess.OpenEncryptedWithPass(filePath, FileAccess.ModeFlags.Read, pass);

		SaveAccess saveAccess = new(readAccess);
		readAccess?.Dispose();

		saveAccess._initFileAccess = () => FileAccess.OpenEncryptedWithPass(filePath, FileAccess.ModeFlags.WriteRead, pass);

		return saveAccess;
	}

	///<summary>Queues all ISaveable children of root (recursively) to be saved when Commit() is called.</summary>
	public void SaveTree(Node root)
	{
		RunInChildrenRecursive(root, (ISaveable node) => { SaveObject(node); });
	}

	///<summary>Queues an ISaveable object to be saved when Commit() is called.</summary>
	public void SaveObject(ISaveable saveObject)
	{
		SaveData(saveObject.Save());
	}

	///<summary>Queues saveData to be saved when Commit() is called.</summary>
	public void SaveData(SaveData saveData)
	{
		SaveData oldSaveData = LoadData(saveData.Key, out int dataIndex);

		if (oldSaveData != null)
		{
			if (dataIndex < _fileSaveData.Length)
			{
				_fileSaveData[dataIndex] = saveData;
				return;
			}
			else
			{
				_fileSaveData[dataIndex - _fileSaveData.Length] = saveData;
				return;
			}
		}

		_fileAppendedSaveData.Add(saveData);
	}

	///<summary>Loads all ISaveable children of root (recursively).</summary>
	public void LoadTree(Node root)
	{
		RunInChildrenRecursive(root, (ISaveable node) => { LoadObject(node); });
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

		if (data != null)
		{
			if (index < _fileSaveData.Length)
			{
				_fileSaveData[index] = null;
				return;
			}
			else
			{
				_fileSaveData[index - _fileSaveData.Length] = null;
				return;
			}
		}
	}

	///<summary>Queues all SaveData to be removed when Commit() is called.</summary>
	public void Clear()
	{
		Array.Clear(_fileSaveData);
		_fileAppendedSaveData.Clear();
	}

	///<summary>
	///Commits all changes to the file.<para/>
	///<b>Note:</b> Only call commit when you actually need it, there could be a significant performance impact from repeated commits.<para/>
	///</summary>
	public void Commit()
	{
		FileAccess fileAccess = _initFileAccess.Invoke();

		foreach (SaveData data in _fileSaveData)
			if (data != null)
				fileAccess.StoreLine(data.ToJson());

		foreach (SaveData data in _fileAppendedSaveData)
			if (data != null)
				fileAccess.StoreLine(data.ToJson());

		fileAccess.Close();
		fileAccess.Dispose();
	}

	private SaveData LoadData(StringName key, out int index)
	{
		index = 0;

		foreach (SaveData data in _fileSaveData)
		{
			if (data.Key == key)
				return data;
			index++;
		}

		foreach (SaveData data in _fileAppendedSaveData)
		{
			if (data.Key == key)
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

		foreach (string data in stringFileData)
		{
			fileData[x] = SaveSystems.SaveData.FromJson(data);
			x++;
		}

		return fileData;
	}

	private static void RunInChildrenRecursive<T>(Node parent, Action<T> action)
	{
		RunInChildrenRecursive(parent, (Node node) => { if (node is T t) action.Invoke(t); });
	}

	private static void RunInChildrenRecursive(Node parent, Action<Node> action)
	{
		RunInChildren(parent);

		void RunInChildren(Node parentNode)
		{
			foreach (Node node in parentNode.GetChildren())
			{
				if (node.GetChildCount() > 0)
					RunInChildren(node);

				action.Invoke(node);
			}
		}
	}
}