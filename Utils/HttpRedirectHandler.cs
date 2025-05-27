using System;
using System.Collections.Generic; // Add this for Dictionary
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClioDataMigrator.Utils
{
    public class HttpRedirectHandler : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly string _redirectUri;
        private CancellationTokenSource _cancellationTokenSource;

        private string HtmlEncodeSimple(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }

        public string state { get; private set; }

        public string AuthorizationCode { get; private set; }

        public bool CodeReceived => !string.IsNullOrEmpty(AuthorizationCode);

        public HttpRedirectHandler(string redirectUri)
        {
            if (
                string.IsNullOrEmpty(redirectUri)
                || !Uri.TryCreate(redirectUri, UriKind.Absolute, out Uri uri)
                || (uri.Scheme != "http" && uri.Scheme != "https")
                || (uri.Host != "localhost" && uri.Host != "127.0.0.1")
            )
            {
                throw new ArgumentException(
                    "Invalid redirect URI. Must be a valid http:// or https:// loopback address (127.0.0.1) and include a port.",
                    nameof(redirectUri)
                );
            }

            _redirectUri = redirectUri;
            _listener = new HttpListener();

            // Create a listener prefix that will match all paths on this host and port
            var parsedUri = new Uri(_redirectUri);
            string baseListenPrefix = $"{parsedUri.Scheme}://{parsedUri.Host}:{parsedUri.Port}/";
            _listener.Prefixes.Add(baseListenPrefix);

            Console.WriteLine($"HTTP listener will listen for requests at: {baseListenPrefix}");
            Console.WriteLine($"Expected callback URL pattern: {_redirectUri}");

            _cancellationTokenSource = new CancellationTokenSource();
            AuthorizationCode = null;
        }

        public async Task StartListeningAsync()
        {
            if (_listener.IsListening)
            {
                Console.WriteLine("Listener is already running.");
                return;
            }

            try
            {
                _listener.Start();
                Console.WriteLine($"HTTP listener started");

                while (
                    _listener.IsListening
                    && !_cancellationTokenSource.IsCancellationRequested
                    && !CodeReceived
                )
                {
                    var contextTask = _listener.GetContextAsync();

                    var completedTask = await Task.WhenAny(
                        contextTask,
                        Task.Delay(-1, _cancellationTokenSource.Token)
                    );

                    if (completedTask == contextTask)
                    {
                        var context = contextTask.Result;
                        ProcessRequest(context);
                    }
                }
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine($"HTTP Listener error: {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("HTTP listener operation cancelled.");
            }
            finally
            {
                StopListening();
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            Console.WriteLine($"Received request: {request.HttpMethod} {request.Url}");
            Console.WriteLine($"Query string: {request.Url.Query}");

            // Check if the request URL contains query parameters
            if (request.Url.Query.Length > 1)
            {
                var queryString = request.Url.Query.TrimStart('?');
                var queryDictionary = new Dictionary<string, string>();

                foreach (var pair in queryString.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var keyValue = pair.Split('=');
                    if (keyValue.Length == 2)
                    {
                        var key = keyValue[0];
                        var value = Uri.UnescapeDataString(keyValue[1]);
                        queryDictionary[key] = value;
                    }
                } // Get the authorization code and state, ensuring they're properly URL-decoded
                string code = null;
                if (queryDictionary.ContainsKey("code"))
                {
                    code = queryDictionary["code"];
                    // Make sure the authorization code is properly decoded
                    try
                    {
                        code = Uri.UnescapeDataString(code);
                    }
                    catch (Exception)
                    {
                        // Keep the original code if unescaping fails
                    }
                }

                state = null;
                if (queryDictionary.ContainsKey("state"))
                {
                    state = queryDictionary["state"];
                    // Make sure the state is properly decoded
                    try
                    {
                        state = Uri.UnescapeDataString(state);
                    }
                    catch (Exception)
                    {
                        // Keep the original state if unescaping fails
                    }
                }

                if (!string.IsNullOrEmpty(code))
                {
                    AuthorizationCode = code;
                    Console.WriteLine($"Authorization code captured: {code}");
                    Console.WriteLine($"State parameter: {state}");

                    // Send a simple success response back to the browser
                    string responseString =
                        "<html><body><h1>Authorization Successful!</h1><p>You can now close this window.</p></body></html>";
                    byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    response.ContentType = "text/html";
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                    response.OutputStream.Close();

                    // Stop listening once the code is captured
                    StopListening();
                }
                else
                {
                    string error = queryDictionary.TryGetValue("error", out string value)
                        ? value
                        : null;
                    Console.WriteLine($"Redirect received, but no code found. Error: {error}");
                    string responseString =
                        $"<html><body><h1>Authorization Failed</h1><p>Error: {HtmlEncodeSimple(error ?? "Unknown error")}</p></body></html>";
                    byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    response.ContentType = "text/html";
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                    response.OutputStream.Close();
                }
            }
            else
            {
                string responseString =
                    "<html><body><h1>Waiting for Authorization</h1><p>This is the OAuth redirect listener. Please complete the authentication process in the browser.</p></body></html>";
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                response.ContentType = "text/html";
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
        }

        public void StopListening()
        {
            if (_listener.IsListening)
            {
                _listener.Stop();
                Console.WriteLine("HTTP listener stopped.");
                _cancellationTokenSource?.Cancel();
            }
        }

        public void Dispose()
        {
            StopListening();
            _cancellationTokenSource?.Dispose();
        }
    }
}
