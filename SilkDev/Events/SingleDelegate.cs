using System;

namespace SilkDev.Events;

//Wrapper for singlecast delegates with custom equality
public sealed class SingleDelegate<A> where A : Delegate
{
	public A Handler { get; }

	public SingleDelegate(A Handler)
	{
		if(Handler==null)
			throw new ArgumentNullException(nameof(Handler));
		if(Handler.GetInvocationList().Length>1)
			throw new ArgumentException("Multicast delegates are not allowed.");
		this.Handler=Handler;
	}

	public override bool Equals(object Obj) =>
		Obj is SingleDelegate<A> Other && Handler.Target==Other.Handler.Target && Handler.Method==Other.Handler.Method;

	public override int GetHashCode() =>
		(Handler.Target?.GetHashCode() ?? 0)^Handler.Method.GetHashCode();

	public static implicit operator A(SingleDelegate<A> SD) => SD.Handler;
	public static implicit operator SingleDelegate<A>(A D) => D==null ? throw new ArgumentNullException() : new SingleDelegate<A>(D);
}