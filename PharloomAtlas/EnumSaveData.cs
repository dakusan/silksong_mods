using SilkDev;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PharloomAtlas;

public static class EnumSaveData
{
	//The data on persistent objects
	public abstract class PerObject(GameObject GO, PerObject.POType Type)
	{
		public enum POType { PermBool, PermInt }
		public readonly GameObject GO=GO;
		public readonly string ObjName=GO.name;
		public readonly POType Type=Type;
		internal object _LastValue=null!;
		public abstract object CurrentObjValue { get; }
	}
	public class PerObjectI(PersistentIntItem GO) : PerObject(GO.gameObject, POType.PermInt)
	{
		public readonly Reflectors.RField<PersistentIntItem, PersistentItemData<int>> GetField=new(GO, "itemData");
		public int LastValue						=> (int)_LastValue;
		public int CurrentValue						=> GetField.Get().Value;
		public override object CurrentObjValue		=> CurrentValue;
	}
	public class PerObjectB(PersistentBoolItem GO) : PerObject(GO.gameObject, POType.PermBool)
	{
		public readonly Reflectors.RField<PersistentBoolItem, PersistentItemData<bool>> GetField=new(GO, "itemData");
		public bool LastValue						=> (bool)_LastValue;
		public bool CurrentValue					=> GetField.Get().Value;
		public override object CurrentObjValue		=> CurrentValue;
	}

	//Get the persistent variable value
	public static PerObject? GetPersistObject(GameObject Obj)
	{
		PersistentBoolItem? PBI;
		PersistentIntItem? PII;
		if(Obj==null)
			return null;
		else if((PBI=Obj.GetComponent<PersistentBoolItem>())!=null)
			return new PerObjectB(PBI);
		else if((PII=Obj.GetComponent<PersistentIntItem>())!=null)
			return new PerObjectI(PII);
		return null;
	}

	//Find all persistent objects in a scene
	public static List<PerObject> FindPersistentObjectsInScene(Scene TheScene)
	{
		List<PerObject> TheList=[];
		foreach(GameObject Parent in TheScene.GetRootGameObjects())
			FindPersistentObjectsRecurse(TheList, Parent.transform);
		return TheList;
	}

	//Helper for the above function
	private static void FindPersistentObjectsRecurse(List<PerObject> ObjList, Transform Parent)
	{
		try {
			PerObject? NewVal=GetPersistObject(Parent.gameObject);
			if(NewVal!=null)
				ObjList.Add(NewVal);
		} catch(Exception e) {
			Log.Error($"GetPersistData on {Parent.gameObject.scene.name}.{Parent.name}: {e.Message}");
		}

		foreach(Transform Child in Parent)
			FindPersistentObjectsRecurse(ObjList, Child);
	}

	//Get all internal player data values
	public class PlayerDataValue(string Name, PlayerDataValue.PDType Type, FieldInfo FI)
	{
		public enum PDType { PDBool, PDInt, PDString, PDHashSet }
		public readonly string Name=Name;
		public readonly PDType Type=Type;
		private readonly FieldInfo FI=FI;
		public object LastValue { get; internal set; } = null!;
		public object CurrentValue(PlayerData PD) => FI.GetValue(PD);
	}
	public static PlayerDataValue[] PlayerDataValues { get
	{
		Dictionary<Type, PlayerDataValue.PDType> TypeMap=new() {
			{ typeof(bool			), PlayerDataValue.PDType.PDBool	},
			{ typeof(int			), PlayerDataValue.PDType.PDInt		},
			{ typeof(string			), PlayerDataValue.PDType.PDString	},
			{ typeof(HashSet<string>), PlayerDataValue.PDType.PDHashSet },
		};
		List<PlayerDataValue> RetData=[];

		foreach(FieldInfo FI in typeof(PlayerData).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			if(TypeMap.TryGetValue(FI.FieldType, out PlayerDataValue.PDType ThePDType) && !FI.IsNotSerialized)
				RetData.Add(new PlayerDataValue(FI.Name, ThePDType, FI));

		//Move ProfileID to the beginning of the list
		PlayerDataValue Item=RetData.FirstOrDefault(static V => V.Name==MonitorSaveValues.ProfileIDStr);
		if(Item is not null) {
			_=RetData.Remove(Item);
			RetData.Insert(0, Item);
		}

		return [.. RetData];
	} }
}