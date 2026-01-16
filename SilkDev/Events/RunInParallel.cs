using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Task = System.Threading.Tasks.Task;
using AtomicInt = SilkDev.Misc.AtomicInt;

namespace SilkDev.Events;

//Runs Source items through CallInThread on a max number of worker threads specified in Exec.
//CallOnComplete always runs on the thread that called Exec (the coordinator thread), only when all threads are busy.
//This is a bounded worker-pool with a single coordinator that:
// - feeds work items into WorkQueue
// - receives results from ThreadResultQueue
public class RunListInParallel<T, TResult>(Func<T, int, TResult> CallInThread, Action<T, int, TResult?, Exception?> CallOnComplete) where TResult: class
{
	private readonly Func  <T, int, TResult				> CallInThread	= CallInThread	?? throw new ArgumentNullException(nameof(CallInThread));
	private readonly Action<T, int, TResult?, Exception?> CallOnComplete= CallOnComplete?? throw new ArgumentNullException(nameof(CallOnComplete));
	private record struct ResultInfo(T ProcessObj, int Index, TResult? Result, Exception? Ex, bool IsComplete);

	//If MaxThreads is<1 then Max(Environment.ProcessorCount-MaxThreads, 1) is used. MaxThreads will not exceed Environment.ProcessorCount or SourceCount.
	//Source count is automatically filled in if <0 and is ICollection<T>. If source count is not given then too many worker threads may be created.
	public void Exec(IEnumerable<T> Source, int MaxThreads=-1, int SourceCount=-1)
	{
		if(Source==null)
			throw new ArgumentNullException(nameof(Source));

		//Auto fill in source count (fast path only; otherwise counting would enumerate Source).
		if(SourceCount<1 && Source is ICollection<T> Coll)
			SourceCount=Coll.Count;

		//Normalize MaxThreads
		//MaxThreads>0: take as-is
		//MaxThreads<=0: interpret as (ProcessorCount-MaxThreads)
		MaxThreads=Math.Clamp(
			Math.Min(
				MaxThreads>0 ? MaxThreads : Environment.ProcessorCount-MaxThreads,
				SourceCount>0 ? SourceCount : int.MaxValue
			),
			1, Environment.ProcessorCount
		);

		//Start workers
		using BlockingCollection<(T Obj, int Index)> WorkQueue=new(MaxThreads);
		using BlockingCollection<ResultInfo> ThreadResultQueue=[];
		Task[] Workers=new Task[MaxThreads];
		for(int i=0; i<MaxThreads; i++)
			Workers[i]=Task.Run(() => {
				try {
					foreach((T Obj, int Index) in WorkQueue.GetConsumingEnumerable()) {
						ResultInfo RI=new(Obj, Index, null, null, false);
						try {
							RI.Result=CallInThread(Obj, Index);
						} catch(Exception e) {
							RI.Ex=e;
						}
						ThreadResultQueue.Add(RI);
					}
				} finally {
					ThreadResultQueue.Add(new ResultInfo(default!, -1, null, null, true));
				}
			});

		//Store results to be processed later
		int NumThreadsWorking=0; //Number of processing work items currently being processed by workers
		int NumThreadsTotal=MaxThreads; //Number of worker tasks that have not exited yet
		Queue<ResultInfo> CurrentResults=[]; //Completed work results waiting to be passed to CallOnComplete
		void StoreResult(ResultInfo RI)
		{
			if(RI.IsComplete) //A worker has exited. Reduce the number of available workers.
				NumThreadsTotal--;
			else { //Result from completed worker job
				NumThreadsWorking--;
				CurrentResults.Enqueue(RI);
			}
		}

		//Create threads immediately when available and process results when all threads are filled
		bool AddingComplete=false;
		IEnumerator<(int Index, T Value)> Enum=Source.Entries.GetEnumerator();
		while(true) {
			//Exit condition: no workers left AND no buffered results to deliver
			if(NumThreadsTotal==0 && CurrentResults.Count==0)
				break;

			//If all needed workers are busy
			if(NumThreadsWorking>=NumThreadsTotal || AddingComplete)
				if(CurrentResults.Count==0) //Wait for a result when workers are full
					StoreResult(ThreadResultQueue.Take());
				else if(CurrentResults.TryDequeue(out ResultInfo Res)) //Run a completion callback if available
					Catcher.Run("Parallel CallOnComplete", () => CallOnComplete(Res.ProcessObj, Res.Index, Res.Ex==null ? Res.Result : null, Res.Ex));

			//Drain any available results so we can keep workers busy
			while(ThreadResultQueue.TryTake(out ResultInfo RI))
				StoreResult(RI);

			//Feed workers while work remains
			while(NumThreadsWorking<NumThreadsTotal && !AddingComplete) {
				try {
					//Test for enum completion
					if(!Enum.MoveNext()) {
						AddingComplete=true;
						WorkQueue.CompleteAdding();
						break;
					}
				} catch(Exception e) {
					//Handle errors from enumerator
					AddingComplete=true;
					WorkQueue.CompleteAdding();
					Catcher.OutputException("Parallel Source enumerator", e);
					break;
				}

				//Pass information to start back up a worker
				WorkQueue.Add((Enum.Current.Value, Enum.Current.Index));
				NumThreadsWorking++;
			}
		}

		//At this point all completion signals have been observed and buffered results have been delivered.
		//WaitAll is now cheap and protects against "worker outlives Exec" surprises (faulted workers, scheduler oddities, etc).
		Task.WaitAll(Workers);
	}
}

//Fixed-size background worker pool that processes queued jobs on dedicated threads and dispatches completion callbacks on the caller-controlled thread
public class BackgroundJobRunner<T, TResult>(
	Func<T, int, TResult>				CallInThread,
	Action<T, int, TResult?, Exception?>CallOnComplete,
	int									MaxThreads=-1	//If MaxThreads<1 then Environment.ProcessorCount-MaxThreads is used.
) : IDisposable where TResult : class {
	public readonly Func  <T, int, TResult				> CallInThread  = CallInThread  ?? throw new ArgumentNullException(nameof(CallInThread	));
	public readonly Action<T, int, TResult?, Exception?	> CallOnComplete= CallOnComplete?? throw new ArgumentNullException(nameof(CallOnComplete));

	private readonly ConcurrentQueue<WorkItem>				InputQueue	=[];
	private readonly SemaphoreSlim							InputSignal	=new(0);
	private readonly ConcurrentQueue<ResultInfo>			OutputQueue	=[];
	private readonly List<Thread>							Workers		=[];
	private readonly CancellationTokenSource				Cancellation=new();

	private volatile bool Initialized, Disposed, AddingCompleted;
	private readonly AtomicInt JobsCreated	 =new();
	private readonly AtomicInt JobsCompleted =new();
	private readonly AtomicInt JobsProcessing=new();
	public int GetJobsCreated	=> JobsCreated.Value;
	public int GetJobsCompleted => JobsCompleted.Value;
	public int GetJobsProcessing=> JobsProcessing.Value;
	public readonly int MaxThreads=Math.Clamp(MaxThreads>0 ? MaxThreads : Environment.ProcessorCount-MaxThreads, 0, Environment.ProcessorCount);

	private readonly record struct WorkItem(T ProcessObj, int Index);
	private record struct ResultInfo(T ProcessObj, int Index, TResult? Result, Exception? Ex);

	public void Init()
	{
		ThrowIfDisposed();
		if(Initialized)
			return;
		Initialized=true;

		for(int i=0; i<MaxThreads; i++)
		{
			Thread Thread = new(WorkerLoop) {
				IsBackground=true,
				Name=$"RunWorkers<{typeof(T).Name}, {typeof(TResult).Name}>[{i}]"
			};
			Workers.Add(Thread);
			Thread.Start();
		}
	}

	public void Add(T Item)
	{
		ThrowIfDisposed();
		if(!Initialized)	throw new InvalidOperationException("Init() must be called before Add()");
		if(AddingCompleted)	throw new InvalidOperationException("Cannot Add() after CompleteAdding()/Finish()");
		InputQueue.Enqueue(new WorkItem(Item, JobsCreated.IncrementVal()-1));
		_=InputSignal.Release();
	}

	public void CompleteAdding()
	{
		if(Disposed || AddingCompleted)
			return;
		AddingCompleted=true;
		_=InputSignal.Release(Workers.Count); //Wake all workers so they can observe completion and exit when queue drains.
	}

	private void WorkerLoop()
	{
		while (true)
		{
			//Wait for an item to process
			try { InputSignal.Wait(Cancellation.Token); }
			catch (OperationCanceledException) { break; }

			//If an item is not available
			if(!InputQueue.TryDequeue(out WorkItem Item))
				//Exit the worker when needed
				if(
					   Cancellation.IsCancellationRequested
					|| (AddingCompleted && InputQueue.IsEmpty && JobsProcessing.Value==0)
				)
					break;
				else //Otherwise go back to waiting for input
					continue;

			//Run the job
			JobsProcessing.Increment();
			ResultInfo RI=new(Item.ProcessObj, Item.Index, null, null);
			try					{ RI.Result=CallInThread(Item.ProcessObj, Item.Index); }
			catch(Exception e)	{ RI.Ex=e; }

			//Mark the job as complete
			OutputQueue.Enqueue(RI);
			JobsCompleted.Increment();
			JobsProcessing.Decrement();

			//If adding is complete and we just finished the last work, wake others so they can exit promptly.
			if(AddingCompleted && InputQueue.IsEmpty && JobsProcessing.Value==0)
				_=InputSignal.Release(Workers.Count);
		}
	}

	//Drain available results and invoke CallOnComplete for each. Intended to be called from the main thread
	public void ProcessResults()
	{
		ThrowIfDisposed();
		while(OutputQueue.TryDequeue(out var RI))
			Catcher.Run("Processing CallOnComplete", () => CallOnComplete(RI.ProcessObj, RI.Index, RI.Result, RI.Ex));
	}

	//Stop accepting new work, wait for all work to finish, drain completions, then stop workers.
	public void Finish()
	{
		//Mark as ready to finish jobs
		ThrowIfDisposed();
		if(!Initialized)
			return;
		CompleteAdding();

		//Wait for jobs to complete
		SpinWait Spin=new();
		while(
			   JobsCompleted.Value<JobsCreated.Value
			|| JobsProcessing.Value>0
			|| !OutputQueue.IsEmpty
			|| !InputQueue.IsEmpty
		) {
			ProcessResults();
			Spin.SpinOnce();
		}
		ProcessResults();

		//Wait for workers to exit
		try { _=InputSignal.Release(Workers.Count); } catch { }
		foreach(Thread Thread in Workers)
			try { Thread.Join(); } catch { }

		Dispose();
	}

	public void Dispose()
	{
		if(Disposed)
			return;
		Disposed=true;
		Cancellation.Cancel();

		//Wait for workers to exit and process results that are available
		try { _=InputSignal.Release(Workers.Count); } catch { }
		foreach(Thread Thread in Workers)
			try { Thread.Join(); } catch { }
		//ProcessResults();

		//Clear out the objects
		InputQueue.Clear();
		OutputQueue.Clear();
		InputSignal.Dispose();
		Cancellation.Dispose();
	}

	private void ThrowIfDisposed() => Misc.IFF(
		Disposed,
		() => throw new ObjectDisposedException(nameof(BackgroundJobRunner<,>))
	);
}