using System;
using System.Collections.Generic;
using System.Text;

namespace CloudBuilderLibrary {

	internal class HttpRequest {
		public enum Policy {
			AllErrors,
			NonpermanentErrors,
			Never,
		};

		public byte[] Body {
			get { return body; }
			set { body = value; }
		}
		public string BodyString {
			get { return Encoding.UTF8.GetString(body); }
			set { body = Encoding.UTF8.GetBytes(value); }
		}
		public Bundle BodyJson {
			set { BodyString = value.ToJson(); Headers["Content-Type"] = "application/json"; }
		}
		/**
		 * Set to perform the request immediately, regardless of a request already being run.
		 */
		public bool DoNotEnqueue;
		public bool HasBody {
			get { return Body != null; }
		}
		public Dictionary<String, String> Headers = new Dictionary<string, string>();
		public int LoadBalancerCount = 1;
		/**
		 * When not set (null), uses GET if no body is provided, or POST otherwise.
		 */
		public string Method;
		public Policy RetryPolicy = Policy.NonpermanentErrors;
		public int[] TimeBetweenTries = DefaultTimeBetweenTries;
		public string Url;
		public int TimeoutMillisec;
		public string UserAgent;

		// Please do not access this by yourself, this is only kept track of internally and will be ignored if set by you
		internal Action<HttpResponse> Callback;
		private byte[] body;
//		private static readonly int[] DefaultTimeBetweenTries = {1, 100, 1000, 1500, 2000, 3000, 4000, 6000, 8000};
		private static readonly int[] DefaultTimeBetweenTries = {};
	}
}
