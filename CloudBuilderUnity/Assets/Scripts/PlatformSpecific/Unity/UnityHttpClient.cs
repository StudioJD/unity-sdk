// ------------------------------------------------------------------------------
//  <autogenerated>
//      This code was generated by a tool.
//      Mono Runtime Version: 4.0.30319.1
// 
//      Changes to this file may cause incorrect behavior and will be lost if 
//      the code is regenerated.
//  </autogenerated>
// ------------------------------------------------------------------------------
using System;
using UnityEngine;
using System.Collections;
using System.Net;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections.Generic;

namespace CloudBuilderLibrary
{
	public class UnityHttpClient: IHttpClient {
		#region IHttpClient implementation
		void IHttpClient.Run(HttpRequest request) {
			EnqueueRequest(request);
        }

		HttpResponse IHttpClient.RunSynchronously(HttpRequest request) {
			int retryCount = 0;
			HttpResponse[] responsePointer = new HttpResponse[1];
			synchronousRequestLock.Reset();
			// We'll use the asynchronous functions but set up a lock to get the response back
			ProcessRequest(request, delegate(RequestState state, HttpResponse response) {
				// Failed request
				if (response.ShouldBeRetried(state.OriginalRequest))  {
					// Will try again
					if (retryCount < timeBetweenRequestTries.Length) {
						CloudBuilder.Log(LogLevel.Warning, "[" + state.RequestId + "] Sync request failed, retrying in " + timeBetweenRequestTries[retryCount] + "ms.");
						Thread.Sleep(timeBetweenRequestTries[retryCount]);
						retryCount += 1;
						ChooseLoadBalancer();
						// Will call this delegate again upon finish
						ProcessRequest(request, state.FinishRequestOverride);
						return;
					}
					else {
						// Maximum failure count reached, will simply process the next request
						CloudBuilder.Log(LogLevel.Warning, "[" + state.RequestId + "] Sync request failed too many times, giving up.");
					}
				}
				// Done
				if (state.OriginalRequest.Callback != null) {
					state.OriginalRequest.Callback(response);
				}
				responsePointer[0] = response;
				synchronousRequestLock.Set();
			});
			synchronousRequestLock.WaitOne();
			return responsePointer[0];
		}

		bool IHttpClient.VerboseMode {
			get { return verboseMode; }
			set { verboseMode = value; }
		}
		#endregion

		#region Private
		/**
		 * Asynchronous request state.
		 */
		private class RequestState {
			// This class stores the State of the request.
			public const int BufferSize = 1024;
			public StringBuilder RequestData;
			public byte[] BufferRead;
			public int RequestId;
			public HttpRequest OriginalRequest;
			public HttpWebRequest Request;
			public HttpWebResponse Response;
			public Stream StreamResponse;
			public UnityHttpClient self;
			public Action<RequestState, HttpResponse> FinishRequestOverride;
			public RequestState(UnityHttpClient inst, HttpRequest originalReq, HttpWebRequest req) {
				self = inst;
				BufferRead = new byte[BufferSize];
				OriginalRequest = originalReq;
				RequestData = new StringBuilder("");
				Request = req;
				StreamResponse = null;
				RequestId = (self.requestCount += 1);
			}
		}

		private void ChooseLoadBalancer() {
			currentLoadBalancerId = random.Next(1, CloudBuilder.Clan.LoadBalancerCount + 1);
		}

		/** Enqueues a request to make it processed asynchronously. Will potentially wait for the other requests enqueued to finish. */
		private void EnqueueRequest(HttpRequest req) {
			// On the first time, choose a load balancer
			if (currentLoadBalancerId == -1) {
				ChooseLoadBalancer();
			}
			lock (this) {
				// Need to enqueue process?
				if (isProcessingRequest) {
					pendingRequests.Add(req);
					return;
				}
				isProcessingRequest = true;
			}
			// Or start immediately
			currentRequestTryCount = 0;
			ProcessRequest(req);
		}

		/** Called after an HTTP request has been processed in any way (error or failure). Decides what to do next. */
		private void FinishWithRequest(RequestState state, HttpResponse response) {
			// IDEA This function could probably be moved to another file with a little gymnastic…
			HttpRequest nextReq;
			// Avoid timeout to be triggered after that
			allDone.Set();
			// Prevent doing the next tasks
			if (state.FinishRequestOverride != null) {
				state.FinishRequestOverride(state, response);
				return;
			}
			// Has failed?
			if (response.ShouldBeRetried(state.OriginalRequest))  {
				// Will try again
				if (currentRequestTryCount < timeBetweenRequestTries.Length) {
					CloudBuilder.Log(LogLevel.Warning, "[" + state.RequestId + "] Request failed, retrying in " + timeBetweenRequestTries[currentRequestTryCount] + "ms.");
					Thread.Sleep(timeBetweenRequestTries[currentRequestTryCount]);
					currentRequestTryCount += 1;
					ChooseLoadBalancer();
					ProcessRequest(state.OriginalRequest);
					return;
				}
				// Maximum failure count reached, will simply process the next request
				CloudBuilder.Log(LogLevel.Warning, "[" + state.RequestId + "] Request failed too many times, giving up.");
				currentRequestTryCount = timeBetweenRequestTries.Length - 1;
			}
			else {
				// Success
				currentRequestTryCount = 0;
			}
			// Final result for this request
			if (state.OriginalRequest.Callback != null) {
				state.OriginalRequest.Callback(response);
			}
			
			// Process next request
			lock (this) {
				// Note: currently another request is only launched after synchronously processed by the callback. This behavior is slower but might be safer.
				if (pendingRequests.Count == 0) {
					isProcessingRequest = false;
					return;
				}
				nextReq = pendingRequests[0];
				pendingRequests.RemoveAt(0);
			}
			ProcessRequest(nextReq);
		}

		/** Got a network stream to write to. */
		private void GetRequestStreamCallback(IAsyncResult asynchronousResult) {
            RequestState state = asynchronousResult.AsyncState as RequestState;
			try {
                // End the operation
				Stream postStream = state.Request.EndGetRequestStream(asynchronousResult);
				// Convert the string into a byte array. 
				byte[] byteArray = Encoding.UTF8.GetBytes(state.OriginalRequest.BodyString);
				// Write to the request stream.
				postStream.Write(byteArray, 0, byteArray.Length);
				postStream.Close();
				// Start the asynchronous operation to get the response
	            state.Request.BeginGetResponse(new AsyncCallback(RespCallback), state);
			}
			catch (WebException e) {
				CloudBuilder.Log(LogLevel.Warning, "Failed to send data: " + e.Message + ", status=" + e.Status);
				if (e.Status != WebExceptionStatus.RequestCanceled) {
					FinishWithRequest(state, new HttpResponse(e));
				}
			}
        }
		        
        /** Prints the current request for user convenience. */
        private void LogRequest(RequestState state) {
			if (!verboseMode) { return; }

			StringBuilder sb = new StringBuilder();
			HttpWebRequest request = state.Request;
			sb.AppendLine("[" + state.RequestId + "] " + request.Method + "ing on " + request.RequestUri);
			sb.AppendLine("Headers:");
			foreach (string key in request.Headers) {
				sb.AppendLine("\t" + key + ": " + request.Headers[key]);
			}
			if (state.OriginalRequest.HasBody) {
				sb.AppendLine("Body: " + state.OriginalRequest.BodyString);
			}
			CloudBuilder.Log(sb.ToString());
		}

		/** Prints information about the response for debugging purposes. */
		private void LogResponse(RequestState state, HttpResponse response) {
			if (!verboseMode) { return; }

			StringBuilder sb = new StringBuilder();
			HttpWebRequest req = state.Request;
			sb.AppendLine("[" + state.RequestId + "] " + response.StatusCode + " on " + req.Method + "ed on " + req.RequestUri);
			sb.AppendLine("Recv. headers:");
			foreach (var pair in response.Headers) {
				sb.AppendLine("\t" + pair.Key + ": " + pair.Value);
			}
			if (response.HasBody) {
				sb.AppendLine("Body: " + response.BodyString);
			}
			CloudBuilder.Log(sb.ToString());
		}

		/** Processes a single request asynchronously. Will continue to FinishWithRequest in some way. */
		private void ProcessRequest(HttpRequest request, Action<RequestState, HttpResponse> bypassProcessNextRequest = null) {
			String url = request.Url.Replace("[id]", currentLoadBalancerId.ToString("00"));
			HttpWebRequest req = HttpWebRequest.Create(url) as HttpWebRequest;

			// Auto choose HTTP method
			req.Method = request.Method ?? (request.BodyString != null ? "POST" : "GET");
			req.UserAgent = CloudBuilder.Clan.UserAgent;
			foreach (var pair in request.Headers) {
				req.Headers[pair.Key] = pair.Value;
			}

			// Configure & perform the request
			RequestState state = new RequestState(this, request, req);
			state.FinishRequestOverride = bypassProcessNextRequest;
			allDone.Reset();
            if (request.BodyString != null) {
				req.BeginGetRequestStream(new AsyncCallback(GetRequestStreamCallback), state);
			}
			else {
				req.BeginGetResponse(new AsyncCallback(RespCallback), state);
			}
			LogRequest(state);
			// Setup timeout
			long timeout = CloudBuilder.Clan.HttpTimeoutMillis;
			ThreadPool.RegisterWaitForSingleObject(allDone, new WaitOrTimerCallback(TimeoutCallback), state, timeout, true);
        }

		/** Called when a response has been received by the HttpWebRequest. */
		private void RespCallback(IAsyncResult asynchronousResult) {  
			RequestState state = asynchronousResult.AsyncState as RequestState;
			try {
				// State of request is asynchronous.
				HttpWebRequest req = state.Request;
				state.Response = req.EndGetResponse(asynchronousResult) as HttpWebResponse;
				
				// Read the response into a Stream object.
				Stream responseStream = state.Response.GetResponseStream();
				state.StreamResponse = responseStream;
				// Begin reading the contents of the page
				responseStream.BeginRead(state.BufferRead, 0, RequestState.BufferSize, new AsyncCallback(ReadCallBack), state);
				return;
			}
			catch (WebException e) {
				// When there is a ProtocolError or such (HTTP code 4xx…), there is also a response associated, so read it anyway.
				state.Response = e.Response as HttpWebResponse;
				Stream responseStream = state.Response.GetResponseStream();
				state.StreamResponse = responseStream;
				responseStream.BeginRead(state.BufferRead, 0, RequestState.BufferSize, new AsyncCallback(ReadCallBack), state);
				return;
			}
			catch (Exception e) {
				CloudBuilder.Log(LogLevel.Warning, "Failed to get response: " + e.Message);
				FinishWithRequest(state, new HttpResponse(e));
			}
			if (state.Response != null) { state.Response.Close(); }
			allDone.Set();
		}

		/** Reads the response buffer little by little. */
		private void ReadCallBack(IAsyncResult asyncResult) {
			RequestState state = asyncResult.AsyncState as RequestState;
			try {
				Stream responseStream = state.StreamResponse;
				int read = responseStream.EndRead(asyncResult);
				// Read the HTML page and then print it to the console. 
				if (read > 0) {
					state.RequestData.Append(Encoding.UTF8.GetString(state.BufferRead, 0, read));
					responseStream.BeginRead(state.BufferRead, 0, RequestState.BufferSize, new AsyncCallback(ReadCallBack), state);
					return;
				}
				else {
					// Finished reading
					responseStream.Close();

					HttpResponse result = new HttpResponse();
					HttpWebResponse response = state.Response;
					result.StatusCode = (int) response.StatusCode;
					foreach (string key in response.Headers) {
						result.Headers[key] = response.Headers[key];
					}
					// Read the body
					result.BodyString = state.RequestData.ToString();
					// Logging
					LogResponse(state, result);
					FinishWithRequest(state, result);
				}
			}
			catch (Exception e) {
				CloudBuilder.Log(LogLevel.Warning, "Failed to read response: " + e.Message);
				FinishWithRequest(state, new HttpResponse(e));
			}
			allDone.Set();
		}

		/** Called upon timeout. */
		private static void TimeoutCallback(object state, bool timedOut) { 
			if (timedOut) {
				RequestState requestState = state as RequestState;
				if (requestState.Request != null) {
					requestState.Request.Abort();
				}
				HttpResponse response = new HttpResponse(new HttpTimeoutException());
				CloudBuilder.Log (LogLevel.Warning, "Request timed out");
				requestState.self.FinishWithRequest(requestState, response);
			}
		}

		// Request processing
		private ManualResetEvent allDone = new ManualResetEvent(false);
		private ManualResetEvent synchronousRequestLock = new ManualResetEvent(false);
		private bool isProcessingRequest = false;
		private List<HttpRequest> pendingRequests = new List<HttpRequest>();
		// Others
		private bool verboseMode;
		private int currentRequestTryCount = 0, currentLoadBalancerId = -1;
		private readonly int[] timeBetweenRequestTries = {1, 100, 1000, 1500, 2000, 3000, 4000, 6000, 8000};
		private System.Random random = new System.Random();
		private int requestCount = 0;
		#endregion
	}
}
