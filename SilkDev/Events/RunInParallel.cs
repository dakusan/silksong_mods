using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Mathf = UnityEngine.Mathf;
using Task = System.Threading.Tasks.Task;

namespace SilkDev.Events;

//Runs Source items through CallInThread on a max number of worker threads specified in Exec.
//CallOnComplete always runs on the thread that called Exec (the coordinator thread), only when all threads are busy.
//This is a bounded worker-pool with a single coordinator that:
// - feeds work items into WorkQueue
// - receives results from ThreadResultQueue
public class RunInParallel<T, TResult>(Func<T, int, TResult> CallInThread, Action<T, int, TResult?, Exception?> CallOnComplete) where TResult: class
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
		MaxThreads=Mathf.Min(
			MaxThreads>0 ? MaxThreads : Mathf.Max(Environment.ProcessorCount-MaxThreads, 1),
			Environment.ProcessorCount,
			SourceCount>0 ? SourceCount : int.MaxValue
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
		int NumThreadsWorking=0; //Number of in-flight work items currently being processed by workers
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