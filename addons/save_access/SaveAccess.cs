using Godot;
using System;
using System.Collections.Generic;

namespace Ardot.SaveSystems;

///<summary>
///This class can be used to save and load the state of a game, and is designed to be easier to use than FileAccess. <para/>
///<b>Note:</b> This class will completely deserialize and store any file that is opened with it, even if it is never used.
///For this reason, try to avoid opening several <c>SaveAccess</c>, and opt instead to use one instance to save entire trees or data structures.<para/>
///<b>Functions:</b> <br/>
///- <c>Open(string filePath)</c> *and all its variants *static<br/>
///- <c>Commit()</c><br/>
///- <c>SaveTree(Node root)</c><br/>
///- <c>SaveObject(ISaveable saveObject)</c><br/>
///- <c>SaveData(SaveData saveData)</c><br/>
///- <c>SaveTreeToSaveData(Node root)</c> *static<br/>
///- <c>LoadTree(Node root)</c><br/>
///- <c>LoadObject(ISaveable loadObject)</c><br/>
///- <c>LoadData(StringName key)</c><br/>
///- <c>LoadTreeFromSaveData(Node root, HashSet&lt;SaveData‎&gt; saveData)</c> *static<br/>
///- <c>RemoveData(StringName key)</c><br/>
///- <c>Clear()</c><br/>
/// </summary>

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

	///<summary>Opens a <c>SaveAccess</c> to a file. <para/> <b>Note:</b> This will always successfully return a SaveAccess, even if the file does not exist (in that case, a new file will be created when <c>Commit()</c> is called)</summary>
	public static SaveAccess Open(string filePath)
	{
		FileAccess readAccess = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);

		SaveAccess saveAccess = new(readAccess);
		readAccess?.Dispose();

		saveAccess._initFileAccess = () => FileAccess.Open(filePath, FileAccess.ModeFlags.WriteRead);

		return saveAccess;
	}

	///<summary>Opens a <c>SaveAccess</c> to a file that reads and writes a compressed file. <para/> <b>Note:</b> This will always successfully return a SaveAccess, even if the file does not exist (in that case, a new file will be created when <c>Commit()</c> is called)</summary>
	public static SaveAccess OpenCompressed(string filePath, FileAccess.CompressionMode compressionMode = FileAccess.CompressionMode.Fastlz)
	{
		FileAccess readAccess = FileAccess.OpenCompressed(filePath, FileAccess.ModeFlags.Read, compressionMode);

		SaveAccess saveAccess = new(readAccess);
		readAccess?.Dispose();

		saveAccess._initFileAccess = () => FileAccess.OpenCompressed(filePath, FileAccess.ModeFlags.WriteRead, compressionMode);

		return saveAccess;
	}

	///<summary>
	///Opens a <c>SaveAccess</c> to a file that reads and writes to an encrypted file using a binary key.<para/>
	///<b>Note:</b> The provided key must be 32 bytes long.<para/>
	///<b>Note:</b> This will always successfully return a SaveAccess, even if the file does not exist (in that case, a new file will be created when <c>Commit()</c> is called)
	///</summary>
	public static SaveAccess OpenEncrypted(string filePath, byte[] key)
	{
		FileAccess readAccess = FileAccess.OpenEncrypted(filePath, FileAccess.ModeFlags.Read, key);

		SaveAccess saveAccess = new(readAccess);
		readAccess?.Dispose();

		saveAccess._initFileAccess = () => FileAccess.OpenEncrypted(filePath, FileAccess.ModeFlags.WriteRead, key);

		return saveAccess;
	}

	///<summary>Opens a <c>SaveAccess</c> to a file that reads and writes to an encrypted file using a string password. <para/> <b>Note:</b> This will always successfully return a SaveAccess, even if the file does not exist (in that case, a new file will be created when <c>Commit()</c> is called)</summary>
	public static SaveAccess OpenEncryptedWithPass(string filePath, string pass)
	{
		FileAccess readAccess = FileAccess.OpenEncryptedWithPass(filePath, FileAccess.ModeFlags.Read, pass);

		SaveAccess saveAccess = new(readAccess);
		readAccess?.Dispose();

		saveAccess._initFileAccess = () => FileAccess.OpenEncryptedWithPass(filePath, FileAccess.ModeFlags.WriteRead, pass);

		return saveAccess;
	}

	///<summary>Saves all <c>ISaveable</c> children of root (recursively). Make sure to call <c>Commit()</c> for data to be stored in the file.</summary>
	public void SaveTree(Node root)
	{
		RunInChildrenRecursive(root, (ISaveable node) => { SaveObject(node); });
	}

	///<summary>Saves an <c>ISaveable</c> object. Make sure to call <c>Commit()</c> for data to be stored in the file.</summary>
	public void SaveObject(ISaveable saveObject)
	{
		SaveData(saveObject.Save());
	}

	///<summary>Saves a <c>SaveData</c>. Make sure to call <c>Commit()</c> for data to be stored in the file.</summary>
	public void SaveData(SaveData saveData)
	{
		if (_fileData.Contains(saveData))
		{
			_fileData.Remove(saveData);
			_fileData.Add(saveData);
		}

		_fileData.Add(saveData);
	}

	///<summary>Loads all <c>ISaveable</c> children of root (recursively), if data cannot be found for an object, it will not be loaded.</summary>
	public void LoadTree(Node root)
	{
		RunInChildrenRecursive(root, (ISaveable node) => { LoadObject(node); });
	}

	///<summary>Loads an <c>ISaveable</c> object, unless no data exists for the object.</summary>
	public void LoadObject(ISaveable loadObject)
	{
		if(loadObject == null)
			return;

		SaveData loadedData = LoadData(loadObject.GetLoadKey());

		if(loadedData != null)
			loadObject.Load(loadedData);
	}

	///<summary>Returns the <c>SaveData</c> with the specified key. Returns null if no data with the given key exists.</summary>
	public SaveData LoadData(StringName key)
	{
		if (_fileData.TryGetValue(new SaveData(key), out SaveData data))
			return data;

		return null;
	}

	///<summary>Deletes the <c>SaveData</c> with the specified key.</summary>
	///<returns>Whether the <c>SaveData</c> was successfully removed.</returns>
	public bool RemoveData(StringName key)
	{
		return _fileData.Remove(new SaveData(key));
	}

	///<summary>Deletes all stored <c>SaveData</c>.</summary>
	public void Clear()
	{
		_fileData.Clear();
	}

	///<summary>
	///Commits all changes to the file.<para/>
	///<b>Note:</b> Only call <c>Commit()</c> when you actually need it, there could be a significant performance impact from repeated commits.<para/>
	///</summary>
	public void Commit()
	{
		FileAccess fileAccess = _initFileAccess.Invoke();

		foreach (SaveData data in _fileData)
			if (data != null)
				fileAccess.StoreLine(data.ToJson());

		fileAccess.Dispose();
	}

	///<summary>Saves a tree, but instead of adding it to a file, returns it as a <c>HashSet</c> of <c>SaveData</c>.</summary>
	public static HashSet<SaveData> SaveTreeToSaveData(Node root)
	{
		HashSet<SaveData> saveData = new();

		RunInChildrenRecursive(root, (ISaveable node) => saveData.Add(node.Save()));

		return saveData;
	}

	///<summary>Loads a tree, but instead of getting data from a file, loads from a <c>HashSet</c> of <c>SaveData</c>.</summary>
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
