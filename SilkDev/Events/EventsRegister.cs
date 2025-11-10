using System;
using System.Collections.Generic;
using System.Linq;

namespace SilkDev.Events;

//Register an event with a key. Uses a Set so action instance can only be registered once
public class EventRegister<KeyType, ValueType>(string Name) where ValueType : Delegate
{
	private readonly string Name=Name;
	private readonly Dictionary<KeyType, HashSet<SingleDelegate<ValueType>>> DictList=[];
	public void Add(KeyType Key, ValueType Value)
	{
		if(!DictList.TryGetValue(Key, out var KeyList))
			DictList.Add(Key, KeyList=[]);
		_=KeyList.Add(Value);
	}
	public bool Remove(KeyType Key, ValueType Value) =>
		DictList.Get(Key)?.Remove(Value) ?? false;
	public bool Run(KeyType Key, Action<ValueType>? CallWrapper=null)
	{
		if(!DictList.TryGetValue(Key, out var ActionList))
			return false;
		Catcher.RunList($"{Name} for {Key!}", ActionList.AsEnumerable().Select(D => D.Handler), CallWrapper);
		return true;
	}
}