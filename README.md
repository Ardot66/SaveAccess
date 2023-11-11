# SaveAccess (C#)

SaveAccess is a system for easily saving data to text files in a JSON format. <br/> <br/>
Features Include: <br/>
 - Saving entire scene trees.
 - Automatic JSON serialization.
 - Support for recursive save structures.
 - ISaveable interface to allow modular save/load structures.

# Classes

This plugin consists of 3 central parts: (all are located under the Ardot.SaveSystems namespace) <br/>
- SaveAccess (used to save ISaveable nodes and SaveData to files) <br/>
- SaveData (used to store and move data to and from ISaveable objects) <br/>
- ISaveable (used by SaveAccess to interface with nodes and objects that need to be saved) <br/>

Together, these allow entire scenes of ISaveable nodes to be saved in just a few lines of code. <br/>

For more information, see the code documentation.

# Example

You have a node, 'Player'. You need to save its position. This is its code:

``` C#
public partial class Player : Node2D
{
    public override void _Process()
    {
        //Movement stuff here
    }
}
```

To make it save, you would need to set it up like this. (this is using the ISaveable default implementation template)

``` C#
using Ardot.SaveSystems;

public partial class Player : Node2D, ISaveable
{
    public override void _Process(double delta)
    {
        //Movement stuff here
    }
  
    public SaveData Save(params Variant[] parameters)
    {
        //Create and return a new SaveData with its key as GetLoadKey(), and its first value as GlobalPosition
        return new SaveData(GetLoadKey(), GlobalPosition); 
    }   
    
    public void Load(SaveData data, params Variant[] parameters)
    {
        //Checking if data is null, in case something went wrong.
        if(data == null)
          return;

        //Setting GlobalPosition to the first value of data, as a Vector2
        GlobalPosition = data[0].AsVector2();
    }
    
    public StringName GetLoadKey(params Variant[] parameters)
    {
        //Returning the LoadKey as 'Player'. It's important that this is unique, otherwise data can be confused
        //If there were going to be more than one player, we may want this key to include some other identifier, like the node's path
        return "Player";
    }
}
```

Now, all you would need to save the player would be to have some script that creates a SaveAccess and runs SaveTree().
 
``` C#
public partial class SceneRootNode : Node2D
{
    public void SaveScene()
    {
        SaveAccess saveAccess = SaveAccess.Open("user://Save.txt");

        saveAccess.SaveTree(this);
        saveAccess.Commit();
    }
}
```

# Advanced Example

Now, imagine that Player has an inventory. This inventory is a Resource with ISaveable implemented. It won't be automatically saved like nodes, because it isn't directly included in the scene tree. <br/>

What you can do is set up Player to save and load its inventory by including the inventory's SaveData in the player's SaveData.

``` C#
using Ardot.SaveSystems;

public partial class Player : Node2D, ISaveable
{
    public Inventory playerInventory;

    public override void _Process(double delta)
    {
        //Movement stuff here
    }
  
    public SaveData Save(params Variant[] parameters)
    {
        //Create and return a new SaveData with its key as GetLoadKey(), its first value as GlobalPosition,
        //and its second value as playerInventory's SaveData
        return new SaveData(GetLoadKey(), GlobalPosition, playerInventory.Save());
    }   
    
    public void Load(SaveData data, params Variant[] parameters)
    {
        //Checking if data is null, in case something went wrong.
        if(data == null)
          return;

        //Setting GlobalPosition to the first value of data, as a Vector2
        GlobalPosition = data[0].AsVector2();

        //Loading playerInventory with the second value of data, which is SaveData
        playerInventory.Load(data[1]);
    }
    
    public StringName GetLoadKey(params Variant[] parameters)
    {
        //Returning the LoadKey as 'Player'. It's important that this is unique, otherwise data can be confused
        //If there were going to be more than one player, we may want this key to include some other identifier, like the node's path
        return "Player";
    }
}
```
