using System;
using System.Text;
using System.Collections.Generic;

namespace CloudBuilderLibrary
{
	public sealed partial class Clan {

		/**
		 * Executes a "ping" request to the server. Allows to know whether the server is currently working as expected.
		 * You should hardly ever need this.
		 * @param done callback invoked when the request has finished, either successfully or not. The boolean value inside is not important.
		 */
		public void Ping(ResultHandler<bool> done) {
			HttpRequest req = MakeUnauthenticatedHttpRequest("/v1/ping");
			Common.RunHandledRequest(req, done, (HttpResponse response) => {
				Common.InvokeHandler(done, true);
			});
		}

		#region Internal HTTP helpers
		internal HttpRequest MakeUnauthenticatedHttpRequest(string path) {
			HttpRequest result = new HttpRequest();
			if (path.StartsWith("/")) {
				result.Url = Server + path;
			}
			else {
				result.Url = path;
			}
			result.LoadBalancerCount = LoadBalancerCount;
			result.Headers["x-apikey"] = ApiKey;
			result.Headers["x-sdkversion"] = SdkVersion;
			result.Headers["x-apisecret"] = ApiSecret;
			result.TimeoutMillisec = HttpTimeoutMillis;
			result.UserAgent = UserAgent;
			return result;
		}
		#endregion

		#region Private
		internal Clan(string apiKey, string apiSecret, string environment, bool httpVerbose, int httpTimeout) {
			this.ApiKey = apiKey;
			this.ApiSecret = apiSecret;
			this.Server = environment;
			LoadBalancerCount = 2;
			Managers.HttpClient.VerboseMode = httpVerbose;
			HttpTimeoutMillis = httpTimeout * 1000;
			UserAgent = String.Format(Common.UserAgent, Managers.SystemFunctions.GetOsName(), Common.SdkVersion);
		}
		#endregion

		#region Members
		private const string SdkVersion = "1";
		private string ApiKey, ApiSecret, Server;
		private int HttpTimeoutMillis;
		public int LoadBalancerCount {
			get; private set;
		}
		public string UserAgent {
			get; private set;
		}
		#endregion
	}
}
