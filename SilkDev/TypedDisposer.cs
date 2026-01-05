using System;

namespace SilkDev;

//A generic IDisposable class where the dispose action is set in Disposal
public sealed class TypedDisposer<T>(T Target, Action<T> Disposal) : IDisposable
{
	public T Target { get; } = Target ?? throw new ArgumentNullException(nameof(Target));
	private readonly Action<T> Disposal=Disposal ?? throw new ArgumentNullException(nameof(Disposal));
	public bool Disposed { get; private set; } = false;

	public void Dispose()
	{
		if(!Disposed)
			try { Disposal(Target); }
			finally { Disposed=true; }
	}

	public T Detach() => Misc.PassThru( //Will not dispose
		Disposed=true,
		Target
	);
}