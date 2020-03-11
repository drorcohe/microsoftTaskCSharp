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
        private HttpListener listener = new HttpListener();
        private List<Task> tasks = new List<Task>();
        private readonly ConcurrentDictionary<int, RequestHandler> _clientIdToRequestHandler;
        private readonly RequestHandler.HandlerType _type;


        private string HttpPrefix => $"{_type.ToString()}Window";

        private Server(RequestHandler.HandlerType type)
        {
            _type = type;
            _clientIdToRequestHandler = new ConcurrentDictionary<int, RequestHandler>();
        }


        private void HandleRequest(HttpListenerContext context)
        {
            Console.WriteLine($"{nameof(HandleRequest)}: start");
            Task.Delay(200).Wait();


            var request = context.Request;
            var clientIdFound = int.TryParse(request.QueryString["clientId"], out var clientId);

            if (clientIdFound)
            {
                var requestHandler =
                    _clientIdToRequestHandler.GetOrAdd(clientId, _ => RequestHandler.GetHandler(_type));
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
                if (listener.IsListening)
                {
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                    response.OutputStream.Close();
                }
                Thread.Sleep(1000);
            }


            Console.WriteLine($"{nameof(HandleRequest)}: end");
        }

        public void Close()
        {
            listener.Close();
            Task.WaitAll(tasks.ToArray());
        }

        private void Listen(CancellationToken token)
        {
            listener.Prefixes.Add("http://localhost:8080/" + HttpPrefix + "/");
            listener.Start();
            do
            {
                var context = listener.GetContext();
                tasks.Add(Task.Run(() => HandleRequest(context)));
            } while (!token.IsCancellationRequested);

        }


        private static void Main()
        {
            var tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;

            var staticServer = new Server(RequestHandler.HandlerType.Static);

            Task.Run(() => staticServer.Listen(token));

            var dynamicServer = new Server(RequestHandler.HandlerType.Dynamic);
            Task.Run(() => dynamicServer.Listen(token));

            Console.WriteLine("Press any key to stop");
            Console.ReadKey();
            tokenSource.Cancel();
            staticServer.Close();
            dynamicServer.Close();



            Console.WriteLine("Bye!");
        }
    }
}