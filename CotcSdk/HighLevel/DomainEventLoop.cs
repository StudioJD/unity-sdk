using System;
using System.Threading;

namespace CotcSdk {

	/// @ingroup data_classes
	/// <summary>
	/// Delegate called when receiving a message on a #CotcSdk.DomainEventLoop.</summary>
	/// <param name="sender">Domain loop that triggered the event.</param>
	/// <param name="e">Description of the received event.</param>
	public delegate void EventLoopHandler(DomainEventLoop sender, EventLoopArgs e);

	/// @ingroup data_classes
	/// <summary>
	/// Arguments of the EventLoopArgs.ReceivedEvent event. You can use `args.Message.ToJson()` to
	/// obtain more information.
	/// </summary>
	public class EventLoopArgs {
		/// <summary>Message received.</summary>
		public Bundle Message {
			get; private set;
		}

		internal EventLoopArgs(Bundle message) {
			Message = message;
		}
	}

	/// @ingroup main_classes
	/// <summary>
	/// This class is responsible for polling the server waiting for new events.
	/// You should instantiate one and manage its lifecycle as the state of the application changes.
	/// 
	/// A loop is typically managed through the Gamer.StartEventLoop method (loops are always running as an authenticated
	/// gamer) and should be started once the gamer is logged in, and stopped at logout. The loop is automatically paused
	/// by the system when the user leaves the application, and automatically restarted as well.
	/// 
	/// @code{.cs}
	/// DomainEventLoop loop;
	/// 
	/// void Login() {
	///     Cloud.LoginAnonymous()
	///     .Then(gamer => {
	///         loop = gamer.StartEventLoop();
	///         loop.ReceivedEvent += ReceivedEvent;
	///     });
	/// }
	/// 
	/// void Logout() {
	///     loop.Stop();
	/// }
	/// 
	/// void ReceivedEvent(DomainEventLoop sender, EventLoopArgs e) {
	///     Debug.Log("Received event of type " + e.Message.Type + ": " + e.Message.ToJson());
	/// } @endcode
	/// </summary>
	public sealed class DomainEventLoop {
		/// <summary>
		/// You need valid credentials in order to instantiate this class. Use Cloud.Login* methods for that purpose.
		/// Once the object is created, you need to start the thread, please look at the other methods available.
		/// </summary>
		/// <param name="gamer">The gamer object received from a login or similar function.</param>
		/// <param name="domain">The domain on which to listen for events. Note that you may create multiple event loops,
		///     especially if you are using multiple domains. The default domain, that you should use unless you are
		///     explicitly using multiple domains, is the private domain.</param>
		/// <param name="gamer">Gamer, used to authenticate (receive events related to the said gamer).</param>
		/// <param name="domain">Domain on which to listen for events.</param>
		/// <param name="iterationDuration">Sets a custom timeout in seconds for the long polling event loop. Should be used
		///     with care and set to a high value (at least 60). Defaults to 590 (~10 min).</param>
		public DomainEventLoop(Gamer gamer, String domain = Common.PrivateDomain, int iterationDuration = 590) {
			Domain = domain;
			Gamer = gamer;
			LoopIterationDuration = iterationDuration * 1000;
		}

		/// <summary>The domain on which this loop is listening.</summary>
		public String Domain {
			get; private set;
		}

		public Gamer Gamer { get; private set; }

		/// <summary>This event is raised when an event is received.</summary>
		public event EventLoopHandler ReceivedEvent {
			add { receivedEvent += value; }
			remove { receivedEvent -= value; }
		}
		private EventLoopHandler receivedEvent;

		/// <summary>Starts the thread. Call this upon initialization.</summary>
		public DomainEventLoop Start() {
			if (Stopped) throw new InvalidOperationException("Never restart a loop that was stopped");
			if (AlreadyStarted) return this;
			AlreadyStarted = true;
			// Allow for automatic housekeeping
			Cotc.RunningEventLoops.Add(this);
			new Thread(new ThreadStart(this.Run)).Start();
			return this;
		}

		/// <summary>
		/// Will stop the event thread. Might take some time until the current request finishes.
		/// You should not use this object for other purposes later on. In particular, do not start it again.
		/// </summary>
		public DomainEventLoop Stop() {
			Stopped = true;
			Resume();
			// Stop and exit cleanly
			if (CurrentRequest != null) {
				Managers.HttpClient.Abort(CurrentRequest);
				CurrentRequest = null;
			}
			Cotc.RunningEventLoops.Remove(this);
			return this;
		}

		/// <summary>Suspends the event thread.</summary>
		public DomainEventLoop Suspend() {
			Paused = true;
			if (CurrentRequest != null) {
				Managers.HttpClient.Abort(CurrentRequest);
				CurrentRequest = null;
			}
			return this;
		}

		/// <summary>Resumes a suspended event thread.</summary>
		public DomainEventLoop Resume() {
			if (Paused) {
				SynchronousRequestLock.Set();
				Paused = false;
			}
			return this;
		}

		#region Private
		private void ProcessEvent(HttpResponse res) {
			try {
				EventLoopArgs args = new EventLoopArgs(res.BodyJson);
				if (receivedEvent != null) receivedEvent(this, args);
				Cotc.NotifyReceivedMessage(this, args);
			}
			catch (Exception e) {
				Common.LogError("Exception in the event chain: " + e.ToString());
			}
		}

		private void Run() {
			int delay = LoopIterationDuration;
			string messageToAcknowledge = null;
			bool lastResultPositive = true;

			while (!Stopped) {
				if (!lastResultPositive) {
					// Last time failed, wait a bit to avoid bombing the Internet.
					Thread.Sleep(PopEventDelayThreadHold);
				}

				UrlBuilder url = new UrlBuilder("/v1/gamer/event");
				url.Path(Domain).QueryParam("timeout", delay);
				if (messageToAcknowledge != null) {
					url.QueryParam("ack", messageToAcknowledge);
				}

				CurrentRequest = Gamer.MakeHttpRequest(url);
				CurrentRequest.RetryPolicy = HttpRequest.Policy.NonpermanentErrors;
				CurrentRequest.TimeoutMillisec = delay + 30000;
				CurrentRequest.DoNotEnqueue = true;

				Managers.HttpClient.Run(CurrentRequest, (HttpResponse res) => {
					CurrentRequest = null;
					try {
						lastResultPositive = true;
						if (res.StatusCode == 200) {
							messageToAcknowledge = res.BodyJson["id"];
							ProcessEvent(res);
						}
						else if (res.StatusCode != 204) {
							lastResultPositive = false;
							// Non retriable error -> kill ourselves
							if (res.StatusCode >= 400 && res.StatusCode < 500) {
								Stopped = true;
							}
						}
					}
					catch (Exception e) {
						Common.LogError("Exception happened in pop event loop: " + e.ToString());
					}
					SynchronousRequestLock.Set();
				});

				// Wait for request (synchronous)
				SynchronousRequestLock.WaitOne();

				// Wait if suspended
				if (Paused) {
					SynchronousRequestLock.WaitOne();
					lastResultPositive = true;
				}
			}
			Common.Log("Finished pop event thread " + Thread.CurrentThread.ManagedThreadId);
		}

		private AutoResetEvent SynchronousRequestLock = new AutoResetEvent(false);
		private HttpRequest CurrentRequest;
		private bool Stopped = false, AlreadyStarted = false, Paused = false;
		private int LoopIterationDuration;
		private const int PopEventDelayThreadHold = 20000;
		#endregion
	}
}
