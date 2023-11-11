# SaveAccess (C#)

SaveAccess is a system for easily saving data to text files in a JSON format. It is designed to search through trees and save saveable nodes, which means you won't have to code any special cases, and any nodes added at runtime will be saved properly with no extra work.<br/>

# Functionality

This plugin consists of 3 central parts: (all classes are located under the Ardot.SaveSystems namespace) <br/>
- SaveAccess (used to save ISaveable nodes and SaveData to files) <br/>
- SaveData (used to store and move data to and from ISaveable objects) <br/>
- ISaveable (used by SaveAccess to interface with nodes and objects that need to be saved) <br/>

Together, these allow entire scenes of ISaveable nodes to be saved in just a few lines of code. <br/>

For more information, see the code documentation.

# Example

You have a node, 'Player'. You need to save its position. This is its code:

```
public class Player : Node2D
{
    public override void _Process()
    {
        //Movement stuff here
    }
}
```

To make it save, you would need to set it up like this. (this is using the ISaveable default implementation template)

```
using Ardot.SaveSystems;

public class Player : Node2D, ISaveable
{
    public override void _Process()
    {
        //Movement stuff here
    }
  
    public SaveData Save(params Variant[] parameters)
    {
        return new SaveData(GetLoadKey(), GlobalPosition);
    }   
    
    public void Load(SaveData data, params Variant[] parameters)
    {
        //Checking if data is null, in case something went wrong.
        if(data == null)
          return;
      
        GlobalPosition = data[0].AsVector2();
    }
    
    public StringName GetLoadKey(params Variant[] parameters)
    {
        return "Player";.
    }
}
```

Now, all you would need to save the player would be to have some script that creates a SaveAccess and runs SaveTree().

```
public class SceneRootNode : Node2D
{
    public void SaveScene()
    {
        SaveAccess saveAccess = SaveAccess.Open("user://Save.txt")

        saveAccess.SaveTree(this)
        saveAccess.Commit()
    }
}
```
