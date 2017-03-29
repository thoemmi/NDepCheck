
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace NDepCheck.WebServing {
    public class WebServer {
        private readonly Program _program;
        private readonly GlobalContext _globalContext;
        private readonly string _fileDirectory;
        private readonly short _port;
        private readonly HttpListener _listener = new HttpListener();

        public WebServer(Program program, GlobalContext globalContext, string port, string fileDirectory) {
            if (!short.TryParse(port, out _port) || _port <= 0) {
                throw new ArgumentException("Port must be a number in the range 1...32767");
            }
            _program = program;
            _globalContext = globalContext;
            _fileDirectory = fileDirectory;
        }

        public void Start() {
            _listener.Prefixes.Add($"http://*:{_port}/");
            _listener.Start();
            Log.WriteInfo("Listening, _____hit enter to stop");
            _listener.BeginGetContext(GetContextCallback, null);
            Console.ReadLine();
            _listener.Stop();
        }

        public void GetContextCallback(IAsyncResult result) {
            HttpListenerContext context = _listener.EndGetContext(result);
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            var sb = new StringBuilder();

            sb.AppendLine();
            sb.AppendLine($"HttpMethod:  {request.HttpMethod}");
            sb.AppendLine($"Uri:         {request.Url.AbsoluteUri}");
            sb.AppendLine($"LocalPath:   {request.Url.LocalPath}");
            foreach (string key in request.QueryString.Keys) {
                sb.AppendLine($"Query:      {key} = {request.QueryString[key]}");
            }
            sb.AppendLine();

            if (request.Url.LocalPath.EndsWith("/run")) {
                var args = new List<string>();
                foreach (string key in request.QueryString.AllKeys.OrderBy(k => k)) {
                    args.Add(request.QueryString[key]);
                }

                ILogger oldLogger = Log.Logger;
                try {
                    var stringBuilderLogger = new StringBuilderLogger();
                    Log.Logger = stringBuilderLogger;

                    var writtenMasterFiles = new List<string>();
                    _program.Run(args.ToArray(), _globalContext, writtenMasterFiles);

                    if (stringBuilderLogger.HasWarningsOrErrors) {
                        // send errors to client
                    } else if (writtenMasterFiles.Count > 1) {
                        // send file selection HTML to client
                    } else if (writtenMasterFiles.Count > 1) {
                        // send single file output
                    } else {
                        // send logger output to client
                    }
                } finally {
                    Log.Logger = oldLogger;
                }

                string responseString = sb.ToString();
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;

                using (Stream outputStream = response.OutputStream) {
                    outputStream.Write(buffer, 0, buffer.Length);
                }
                _listener.BeginGetContext(GetContextCallback, null);
            } else if (request.Url.LocalPath.Contains("/files/")) {
                // Send file
            } else {
                Log.WriteWarning($"WebServer: URL cannot be handled - neither /run nor /files, but {request.Url}");
            }
        }
    }
}

