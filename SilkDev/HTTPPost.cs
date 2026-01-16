using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using PostDict = System.Collections.Generic.Dictionary<string, string>;

namespace SilkDev;

//TODO: Add streaming supporting: Multipart.Add(new StreamContent(fileStream), "file", fileName); Multipart.Last().Headers.ContentType=new MediaTypeHeaderValue("application/octet-stream");

/*
Sends an HTTP POST request with progress reporting.
Automatically chooses the most efficient encoding based on payload size:
  - application/x-www-form-urlencoded for small key/value bodies
  - multipart/form-data for larger bodies
Features:
  • Upload progress callback (bytes sent, total bytes)
  • Accurate total size when known (urlencoded), deterministic estimate otherwise (multipart)
  • Supports cancellation
  • Allows HttpClient injection or internal ownership
Notes:
  • Multipart totals are estimates unless Content-Length is explicitly known
  • Starts after call to Start()
*/
public class HTTPPost : IDisposable
{
	public static int SendDelay=0; //The millisecond delay between each async write (for testing)

	private System.Threading.CancellationTokenSource CancelToken=new();
	private readonly HttpClient Client;
	private readonly bool OwnsClient;

	public delegate void ProgressHandler(long Sent, long Total, bool TotalIsEstimate);
	public readonly ProgressHandler? ProgressCallback;
	public Task<string?>? Job { get; private set; }
	public readonly string URL;
	public bool IsJobRunning { get; private set; }
	public bool IsDisposed { get; private set; }
	private readonly object LockObject=new();

	public long AmntSent { get; private set; } = 0;
	public long Len { get; private set; } = 0; //Contains an estimate if !ActualAmountKnown
	public bool ActualAmountKnown { get; private set; } = false;

	public HTTPPost(string URL, ProgressHandler? ProgressCallback, HttpClient? Client=null)
	{
		if(string.IsNullOrWhiteSpace(URL))
			throw new ArgumentException("Cannot be empty", nameof(URL));

		(this.URL, this.ProgressCallback)=(URL, ProgressCallback);
		OwnsClient=(Client==null);
		this.Client=(Client ?? new HttpClient());
	}

	//Future proofing so streaming support can be added
	public Task<string?> Start(PostDict? Body=null)
	{
		bool Virgin=(Body?.Count is not >0);
		if(Virgin)
			throw new ArgumentException("Cannot be empty", nameof(Body));

		//Only allow 1 job to run at once
		lock(LockObject) {
			if(IsDisposed)
				throw new InvalidOperationException("Object is already disposed");
			else if(IsJobRunning)
				throw new InvalidOperationException("Job is already running");
			IsJobRunning=true;

			return Job=Task.Run(() => Send(Body!));
		}
	}

	private void ProgressFunc(long Sent, long? Total)
	{
		if(!ActualAmountKnown && Total!=null)
			(ActualAmountKnown, Len)=(true, Total.Value);
		ProgressCallback?.Invoke(AmntSent=Sent, Math.Max(Len, Sent), !ActualAmountKnown);
	}

	private const int MaxByteLengthForUrlEncode=8192; //Arbitrary size to switch from FormUrlEncoded to MultiPartFormData
	private async Task<string?> Send(PostDict Body) //Returns null when cancelled or the string from the server or the error message if the send fails
	{
		//Reset everything once the job is complete
		using TypedDisposer<int> MarkWhenComplete=new(0, _ => {
			lock(LockObject) {
				if(IsDisposed)
					return;
				CancelToken.Dispose();
				CancelToken=new();
				Job=null;
				AmntSent=Len=0;
				IsJobRunning=ActualAmountKnown=false;
			}
		});

		int RunningSize=0;
		using ProgressHttpContent Content=
			Body.Any(KV => (RunningSize+=NumUTF8Bytes(KV.Key)+NumUTF8Bytes(KV.Value))>MaxByteLengthForUrlEncode) ?
				  new ProgressHttpContent_MultiPartFormData	(Body, ProgressFunc)
				: new ProgressHttpContent_FormUrlEncoded	(Body, ProgressFunc);
		ActualAmountKnown=Content.GetContentsSize(out long TheLen);
		Len=TheLen;

		try {
			using HttpResponseMessage Response=await Client.PostAsync(URL, Content, CancelToken.Token).ConfigureAwait(false);
			// response.EnsureSuccessStatusCode();
			string Result=await Response.Content.ReadAsStringAsync().ConfigureAwait(false);
			Log.Debug($"HTTP post sent {AmntSent} bytes");
			return Result;
		} catch(OperationCanceledException) when(CancelToken.IsCancellationRequested) {
			return null;
		} catch(Exception e) {
			return e.Message;
		}
	}

	public void Cancel()
	{
		lock(LockObject)
			if(!IsDisposed && IsJobRunning)
				CancelAction();
	}

	private void CancelAction()
	{
		CancelToken.Cancel();
		Log.Info("HTTP send cancelled: "+URL);
	}

	public void Dispose()
	{
		Task<string?>? JobToWait=null;

		lock(LockObject) {
			if(IsDisposed)
				return;
			IsDisposed=true;
			if(IsJobRunning)
				JobToWait=Job;
		}
		if(JobToWait!=null) {
			CancelAction();
			try { _=JobToWait?.Wait(TimeSpan.FromSeconds(1)); } catch { }
		}

		CancelToken.Dispose();
		if(OwnsClient)
			Client.Dispose();
	}

	private abstract class ProgressHttpContent : HttpContent
	{
		public delegate void InternalProgressHandler(long Sent, long? Total);
		public readonly HttpContent FormContent;
		protected readonly InternalProgressHandler ProgressFunc;
		protected readonly int BufferSize;
		protected long EstimatedTotal=0;
		protected long? RealTotal;

		protected ProgressHttpContent(HttpContent FormContent, InternalProgressHandler ProgressFunc, int BufferSize)
		{
			(this.FormContent, this.ProgressFunc, this.BufferSize)=(FormContent, ProgressFunc, BufferSize);

			//Copy headers (this preserves multipart boundary Content-Type)
			foreach(var Head in FormContent.Headers)
				_=Headers.TryAddWithoutValidation(Head.Key, Head.Value);
		}

		protected override async Task SerializeToStreamAsync(Stream Stream, TransportContext? Context)
		{
			await using Stream InnerStream=await FormContent.ReadAsStreamAsync().ConfigureAwait(false);
			var Buffer=new byte[BufferSize];
			long Sent=0;
			int AmountRead;
			while((AmountRead=await InnerStream.ReadAsync(Buffer, 0, Buffer.Length).ConfigureAwait(false))>0) {
				await Stream.WriteAsync(Buffer, 0, AmountRead).ConfigureAwait(false);
				ProgressFunc(Sent+=AmountRead, RealTotal);
				if(SendDelay>0)
					await Task.Delay(SendDelay).ConfigureAwait(false);
			}
		}

		public virtual bool GetContentsSize(out long Length)
		{
			Length=RealTotal ?? EstimatedTotal;
			return RealTotal!=null;
		}

		protected override bool TryComputeLength(out long Length)
		{
			Length=FormContent.Headers.ContentLength ?? 0;
			return FormContent.Headers.ContentLength!=null;
		}

		protected override void Dispose(bool Disposing)
		{
			if(Disposing)
				FormContent.Dispose();
			base.Dispose(Disposing);
		}
	}

	private sealed class ProgressHttpContent_MultiPartFormData : ProgressHttpContent
	{
		private const string ContentDispositionLine="Content-Disposition: form-data; name=\"\"";
		private const string ContentTypeLine="Content-Type: text/plain; charset=utf-8";
		public readonly string BoundaryStr;
		private static string CreateBoundary() => "---------------------------" + Guid.NewGuid().ToString("N");

		public ProgressHttpContent_MultiPartFormData(PostDict PostContent, InternalProgressHandler ProgressFunc, int BufferSize=64*1024)
			: this(PostContent, ProgressFunc, BufferSize, CreateBoundary()) { }
		private ProgressHttpContent_MultiPartFormData(PostDict PostContent, InternalProgressHandler ProgressFunc, int BufferSize, string BoundaryString)
			: base(new MultipartFormDataContent(BoundaryString), ProgressFunc, BufferSize)
		{
			MultipartFormDataContent FormContent=(MultipartFormDataContent)this.FormContent;
			foreach((string Key, string Value) in PostContent)
				FormContent.Add(new StringContent(Value ?? ""), Key);

			//Get the estimated send size
			int BoundaryLength=(BoundaryStr=BoundaryString).Length;
			const int CRLF=2;
			EstimatedTotal=PostContent.Aggregate(
				(BoundaryLength, Total:2+BoundaryLength+2+CRLF),				//Final boundary line: --{boundary}--
				static (Accum, KVP) => (Accum.BoundaryLength, Accum.Total+
					ContentDispositionLine.Length+CRLF+NumUTF8Bytes(KVP.Key)+	//Content Disposition line include variable name
					ContentTypeLine.Length+CRLF+								//Content type line
					CRLF+														//Blank line between headers and body
					NumUTF8Bytes(KVP.Value)+CRLF+								//Body Value+blank line
					2+Accum.BoundaryLength+CRLF									//Boundary line: --{boundary}
			)).Total;
			RealTotal=FormContent.Headers.ContentLength;
		}
	}

	private sealed class ProgressHttpContent_FormUrlEncoded : ProgressHttpContent
	{
		public ProgressHttpContent_FormUrlEncoded(PostDict PostContent, InternalProgressHandler ProgressFunc, int BufferSize=64*1024)
			: base(new FormUrlEncodedContent(PostContent), ProgressFunc, BufferSize) =>
				FormContent.Headers.ContentLength=RealTotal=EstimatedTotal=
					   FormContent.Headers.ContentLength
					?? FormContent.ReadAsByteArrayAsync().GetAwaiter().GetResult().LongLength;
		protected override bool TryComputeLength(out long Length) => (Length=RealTotal!.Value)>=0 || true;
	}

	protected static int NumUTF8Bytes(string? Str) => System.Text.Encoding.UTF8.GetByteCount(Str ?? "");
}