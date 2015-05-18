using System;
using System.Collections.Generic;

namespace CloudBuilderLibrary {
	internal class HttpResponse {
		public string BodyString {
			get { return Body; }
			set { Body = value; CachedBundle = null; }
		}
		public Bundle BodyJson {
			get { CachedBundle = CachedBundle?? Bundle.FromJson(Body); return CachedBundle; }
		}
		public Exception Exception;
		public bool HasBody {
			get { return Body != null; }
		}
		public bool HasFailed {
			get { return Exception != null; }
		}
		public Dictionary<String, String> Headers = new Dictionary<string, string>();
		/** Returns whether this response is in an error state that should be retried according to the request configuration. */
		public bool ShouldBeRetried(HttpRequest request) {
			switch (request.RetryPolicy) {
			case HttpRequest.Policy.NonpermanentErrors:
				return HasFailed || StatusCode < 100 || (StatusCode >= 300 && StatusCode < 400) || StatusCode >= 500;
			case HttpRequest.Policy.AllErrors:
				return HasFailed || StatusCode < 100 || StatusCode >= 300;
			case HttpRequest.Policy.Never:
			default:
				return false;
			}
		}
		public int StatusCode;

		public HttpResponse() {}
		public HttpResponse(Exception e) { Exception = e; }

		private string Body;
		private Bundle CachedBundle;
	}
}

