using Godot;

namespace Ardot.SaveSystems;

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