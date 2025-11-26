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
	public bool AllowDuplicates=false;

	protected readonly List<(int Priority, T Value)> Values=[];
	public ReadOnlyCollection<(int Priority, T Value)> GetValues => new(Values);
	public bool HasAny => Values.Count>0;

	public static TSelf operator +(PrioritizedValues<TSelf, T> e, (T Val, int Pri) v) => e.Add(v.Val, v.Pri);
	public static TSelf operator +(PrioritizedValues<TSelf, T> e, T Value			) => e.Add(Value, 0);
	public static TSelf operator -(PrioritizedValues<TSelf, T> e, T Value			) => e.Sub(Value);
	public IEnumerable<T> Sorted()				 => Values.OrderBy			(static v => v.Priority).Select(static v => v.Value);
	public IEnumerable<T> SortedDescending()	 => Values.OrderByDescending(static v => v.Priority).Select(static v => v.Value);
	protected virtual bool GetEquality(T a, T b) => a.Equals(b);

	public TSelf Add(T Value, int Priority=0)
	{
		if(GetValueIndex(Value)!=-1) {
			Log.Info($"Duplicate found and {(AllowDuplicates ? "Allowed" : "Denied")}: {Value}");
			if(!AllowDuplicates)
				return (TSelf)this;
		}

		Values.Add((Priority, Value));
		return (TSelf)this;
	}

	public TSelf Sub(T Value)
	{
		int FoundIndex;
		if((FoundIndex=GetValueIndex(Value))!=-1)
			Values.RemoveAt(FoundIndex);
		return (TSelf)this;
	}

	public TSelf Toggle(T Value, bool IsAdd, int Priority=0) =>
		IsAdd ? Add(Value, Priority) : Sub(Value);

	private int GetValueIndex(T Value) =>
		Values.FindLastIndex(V => GetEquality(V.Value, Value));
}

//Prioritized event list (higher priority runs first)
public class PrioritizedEvents<A>(string Name) : PrioritizedValues<PrioritizedEvents<A>, SingleDelegate<A>> where A : Delegate
{
	public void Run(					) => Catcher.RunList(Name, SortedDescending().Select(static D => D.Handler)				); //No parameters passed
	public void Run(Action<A> RunEvents	) => Catcher.RunList(Name, SortedDescending().Select(static D => D.Handler), RunEvents	); //Parameters passed
	public IEnumerable<SingleDelegate<A>> Handlers			=>	   SortedDescending();
	public IEnumerable<SingleDelegate<A>> UnsortedHandlers	=>				   Values.Select(static V => V.Value  );

	//Convenience overloads to accept raw A (wrap internally)
	public PrioritizedEvents<A> Toggle	(A Handler, bool IsAdd, int Priority=0	) => base.Toggle(new SingleDelegate<A>(Handler), IsAdd, Priority);
	public PrioritizedEvents<A> Add		(A Handler,				int Priority=0	) => base.Add	(new SingleDelegate<A>(Handler),		Priority);
	public PrioritizedEvents<A> Sub		(A Handler								) => base.Sub	(new SingleDelegate<A>(Handler)					);

	//Operator overloads for raw A
	public static PrioritizedEvents<A> operator +(PrioritizedEvents<A> e, (A Val, int Pri) v) => e.Add(v.Val, v.Pri	);
	public static PrioritizedEvents<A> operator +(PrioritizedEvents<A> e, A Value			) => e.Add(Value, 0		);
	public static PrioritizedEvents<A> operator -(PrioritizedEvents<A> e, A Value			) => e.Sub(Value		);
}