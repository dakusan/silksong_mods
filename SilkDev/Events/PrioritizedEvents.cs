using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SilkDev.Events;

//Generic class to store values with a priority (collisions allowed).
//+ and - are done in place on the class (no new instance). Only 1 value will be removed on -.
public class PrioritizedValues<TSelf, T>
	where TSelf : PrioritizedValues<TSelf, T>
	where T : class
{
	protected readonly List<(int Priority, T Value)> Values=[];
	public ReadOnlyCollection<(int Priority, T Value)> GetValues => new(Values);

	public static TSelf operator +(PrioritizedValues<TSelf, T> e, (T Val, int Pri) v) => e.Add(v.Val, v.Pri);
	public static TSelf operator +(PrioritizedValues<TSelf, T> e, T Value			) => e.Add(Value, 0);
	public static TSelf operator -(PrioritizedValues<TSelf, T> e, T Value			) => e.Sub(Value);
	public IEnumerable<T> Sorted()				 => Values.OrderBy			(static v => v.Priority).Select(static v => v.Value);
	public IEnumerable<T> SortedDescending()	 => Values.OrderByDescending(static v => v.Priority).Select(static v => v.Value);
	protected virtual bool GetEquality(T a, T b) => a.Equals(b);

	public TSelf Add(T Value, int Priority)
	{
		Values.Add((Priority, Value));
		return (TSelf)this;
	}

	public TSelf Sub(T Value)
	{
		int FoundIndex;
		if((FoundIndex=Values.FindLastIndex(V => GetEquality(V.Value, Value)))!=-1)
			Values.RemoveAt(FoundIndex);
		return (TSelf)this;
	}

	public TSelf Toggle(T Value, bool IsAdd, int Priority) =>
		IsAdd ? Add(Value, Priority) : Sub(Value);
}

//Prioritized event list (higher priority runs first)
public class PrioritizedEvents<A>(string Name) : PrioritizedValues<PrioritizedEvents<A>, SingleDelegate<A>> where A : Delegate
{
	public void Run(					) => Catcher.RunList(Name, SortedDescending().Select(static D => D.Handler)				); //No parameters passed
	public void Run(Action<A> RunEvents	) => Catcher.RunList(Name, SortedDescending().Select(static D => D.Handler), RunEvents	); //Parameters passed

	//Convenience overloads to accept raw A (wrap internally)
	public PrioritizedEvents<A> Add(A handler, int priority=0	) => base.Add(new SingleDelegate<A>(handler), priority	);
	public PrioritizedEvents<A> Sub(A handler					) => base.Sub(new SingleDelegate<A>(handler)			);

	//Operator overloads for raw A
	public static PrioritizedEvents<A> operator +(PrioritizedEvents<A> e, (A Val, int Pri) v) => e.Add(v.Val, v.Pri	);
	public static PrioritizedEvents<A> operator +(PrioritizedEvents<A> e, A Value			) => e.Add(Value, 0		);
	public static PrioritizedEvents<A> operator -(PrioritizedEvents<A> e, A Value			) => e.Sub(Value		);
}