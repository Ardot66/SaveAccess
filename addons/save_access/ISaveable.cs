using Godot;

namespace Ardot.SaveSystems;

///<summary>
///An interface that allows objects to be saved and serialized as <c>SaveData</c>. <para/>
///<b>Note:</b> The 'parameters' field on all functions is only intended to be used for complex applications where extra data is needed for saving.
///It is not used by <c>SaveAccess</c> or <c>SaveData</c> by default.<para/>
///<example>
///<b>Example Implementation:</b> (Lots of characters are prefixed by <c>\</c> due to a bug, to get a clean version for copy and paste, see the the source code)
///<code>
///		public SaveData Save(params Variant[] parameters)
///		{
///			return new SaveData(GetLoadKey(), null); //Put stuff that needs saving where null is. 
///		}   
///
///		public void Load(SaveData data, params Variant[] parameters)
///		{
///			//Load things here, e.g. MyValue = data[0].AsInt32();
///		}
///
///		public StringName GetLoadKey(params Variant[] parameters)
///		{
///			return ""; //For nodes use 'Name' or 'GetPath()', for Resources use 'ResourcePath'.
///		}
///</code>
///</example>
///</summary>
public interface ISaveable
{
	///<summary>Called when the object is being saved.</summary>
	public SaveData Save(params Variant[] parameters);
	///<summary>Called when the object is being loaded, set up your object here.</summary>
	public void Load(SaveData data, params Variant[] parameters);
	///<summary>Called when the object is being loaded to properly identify what data to load it with.</summary>
	public StringName GetLoadKey(params Variant[] parameters);
}
