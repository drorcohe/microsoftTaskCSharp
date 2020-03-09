using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace AoratoExercise
{
    internal class Server
    {
        private readonly ConcurrentDictionary<int, RequestHandler> _clientIdToRequestHandler;
        private readonly RequestHandler.HandlerType _type;
        private int _openRequests = 0;

        private Server(RequestHandler.HandlerType type)
        {
            _type = type;
            _clientIdToRequestHandler = new ConcurrentDictionary<int, RequestHandler>();
        }


        private void HandleRequest(IAsyncResult result)
        {
            Console.WriteLine($"{nameof(HandleRequest)}: start");
            _openRequests++;
            Task.Delay(200).Wait();
            var listener = (HttpListener)result.AsyncState;
            if (!listener.IsListening)
            {
                Console.WriteLine($"listener is closed. returns");
                _openRequests--;
                return;
            }


            var context = listener.EndGetContext(result);
            var request = context.Request;
            var clientIdFound = int.TryParse(request.QueryString["clientId"], out var clientId);

            if (clientIdFound)
            {
                var requestHandler = _clientIdToRequestHandler.GetOrAdd(clientId, _ => RequestHandler.GetHandler(_type));
                var timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                bool isRequestValid;
                lock (requestHandler)
                {
                    isRequestValid = requestHandler.ProcessRequest(timestamp);
                }

                var response = context.Response;
                response.StatusCode = (int)(isRequestValid ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
                Console.WriteLine($"{nameof(_type)}={_type} " +
                                  $"{nameof(timestamp)}={timestamp} " +
                                  $"{nameof(clientId)}={clientId}" +
                                  $"{nameof(context.Response.StatusCode)}={context.Response.StatusCode}");
                var buffer = System.Text.Encoding.UTF8.GetBytes($"<HTML><BODY> {response.StatusCode} </BODY></HTML>");
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }

            _openRequests--;
            Console.WriteLine($"{nameof(HandleRequest)}: end");

        }



        private void Listen(CancellationToken token)
        {

            var listener = new HttpListener();
            var prefix = $"{_type.ToString()}Window";
            var prefixes = new List<String> { "http://localhost:8080/" + prefix + "/" };
            prefixes.ForEach(s => listener.Prefixes.Add(s));
            listener.Start();
            do
            {

                var requestCallback = new AsyncCallback(HandleRequest);
                var context = listener.BeginGetContext(requestCallback, listener);
                context.AsyncWaitHandle.WaitOne(500, true);

            } while (!token.IsCancellationRequested);

            listener.Stop();
            Task.Delay(1000).Wait();
            while (_openRequests > 0)
            {
                Task.Delay(1000).Wait();
            }
            listener.Close();
        }




        private static void Main(string[] args)
        {
            var tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;

            var staticServer = new Server(RequestHandler.HandlerType.Static);
            var listeningTask1 = Task.Run(() => staticServer.Listen(token));

            var dynamicServer = new Server(RequestHandler.HandlerType.Dynamic);
            var listeningTask2 = Task.Run(() => dynamicServer.Listen(token));

            Console.WriteLine("Press any key to stop");
            Console.ReadKey();

            tokenSource.Cancel();
            listeningTask1.Wait();
            listeningTask2.Wait();
            // listeningTask2.Wait();
            Console.WriteLine("Bye!");
        }
    }
}
