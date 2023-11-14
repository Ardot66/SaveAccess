using Godot;
using System;
using System.Collections.Generic;

namespace Ardot.SaveSystems;

///<summary>
///This class can be used to save and load the state of a game, and is designed to be easier to use than FileAccess. <para/>
///<b>Note:</b> This class will completely deserialize and store in memory any file that is opened with it, even if it is never used.
///For this reason, try to avoid opening many SaveAccesses, and opt to use one instance to save entire trees or data structures.<para/>
///Core functions include: <br/>
/// - Open(string filePath) *and all its variants *static<br/>
/// - Commit()<br/>
/// - SaveTree(Node root)<br/>
/// - SaveObject(ISaveable saveObject)<br/>
/// - SaveData(SaveData saveData)<br/>
/// - SaveTreeToSaveData(Node root) *static<br/>
/// - LoadTree(Node root)<br/>
/// - LoadObject(ISaveable loadObject)<br/>
/// - LoadData(StringName key)<br/>
/// - LoadTreeFromSaveData(Node root, HashSet&lt;SaveData‎&gt; saveData) *static<br/>
/// - RemoveData(StringName key)<br/>
/// - Clear()<br/>
///</summary>

public sealed partial class SaveAccess
{
	private readonly HashSet<SaveData> _fileData;
	private Func<FileAccess> _initFileAccess;

	private SaveAccess(FileAccess readAccess)
	{
		if (readAccess != null)
			_fileData = GetFileData(readAccess);
		else
			_fileData = new();
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
		if (_fileData.Contains(saveData))
		{
			_fileData.Remove(saveData);
			_fileData.Add(saveData);
		}

		_fileData.Add(saveData);
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
		if (_fileData.TryGetValue(new SaveData(key), out SaveData data))
			return data;

		return null;
	}

	///<summary>Queues the SaveData with the specified key to be removed when Commit() is called.</summary>
	public void RemoveData(StringName key)
	{
		_fileData.Remove(new SaveData(key));
	}

	///<summary>Queues all SaveData to be removed when Commit() is called.</summary>
	public void Clear()
	{
		_fileData.Clear();
	}

	///<summary>
	///Commits all changes to the file.<para/>
	///<b>Note:</b> Only call commit when you actually need it, there could be a significant performance impact from repeated commits.<para/>
	///</summary>
	public void Commit()
	{
		FileAccess fileAccess = _initFileAccess.Invoke();

		foreach (SaveData data in _fileData)
			if (data != null)
				fileAccess.StoreLine(data.ToJson());

		fileAccess.Dispose();
	}

	///<summary>Saves a tree, but instead of adding it to a file, returns it as a HashSet of SaveData.</summary>
	public static HashSet<SaveData> SaveTreeToSaveData(Node root)
	{
		HashSet<SaveData> saveData = new();

		RunInChildrenRecursive(root, (ISaveable node) => saveData.Add(node.Save()));

		return saveData;
	}

	///<summary>Loads a tree, but instead of getting data from a file, loads from a HashSet of SaveData.</summary>
	public static void LoadTreeFromSaveData(Node root, HashSet<SaveData> saveData)
	{
		RunInChildrenRecursive(root, (ISaveable node) =>
		{
			if (saveData.TryGetValue(new SaveData(node.GetLoadKey()), out SaveData data))
				node.Load(data);

			node.Load(null);
		});
	}

	private static HashSet<SaveData> GetFileData(FileAccess fileAccess)
	{
		HashSet<SaveData> fileData = new();

		while (!fileAccess.EofReached())
		{
			string line = fileAccess.GetLine();

			if (string.IsNullOrEmpty(line))
				continue;

			fileData.Add(SaveSystems.SaveData.FromJson(line));
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
