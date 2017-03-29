
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace NDepCheck.WebServing {
    public class WebServer {
        private const string FILES_PREFIX = "/files/";
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
            Log.WriteInfo($"Listening on port {_port}");
            _listener.BeginGetContext(GetContextCallback, null);
            lock (_listener) {
                Monitor.Wait(_listener);
            }
        }

        public void Stop() {
            _listener.Stop();
            lock (_listener) {
                Monitor.PulseAll(_listener);
            }
        }

        public void GetContextCallback(IAsyncResult result) {
            HttpListenerContext context = _listener.EndGetContext(result);
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            var sbDebug = new StringBuilder();
            sbDebug.AppendLine($"HttpMethod:  {request.HttpMethod}");
            sbDebug.AppendLine($"Uri:         {request.Url.AbsoluteUri}");
            sbDebug.AppendLine($"LocalPath:   {request.Url.LocalPath}");
            foreach (string key in request.QueryString.Keys) {
                sbDebug.AppendLine($"Query:      {key} = {request.QueryString[key]}");
            }
            Log.WriteDebug(sbDebug.ToString());

            string responseString;
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
                        responseString = WrapAsHtmlBody(stringBuilderLogger.GetString());
                        // send errors to client
                    } else if (writtenMasterFiles.Count > 1) {
                        // send file selection HTML to client
                        var sb = new StringBuilder();
                        sb.AppendLine("<ol>");
                        foreach (var f in writtenMasterFiles) {
                            sb.AppendLine($@"<li><a href=""YYY/{f}"">{f}</a></li>");
                        }
                        sb.AppendLine("<ol>");
                        responseString = WrapAsHtmlBody(sb.ToString());
                    } else if (writtenMasterFiles.Count == 1) {
                        // send single file output
                        using (var sr = new StreamReader(writtenMasterFiles[0])) {
                            responseString = sr.ReadToEnd();
                        }
                    } else {
                        // send logger output to client
                        responseString = WrapAsHtmlBody(stringBuilderLogger.GetString());
                    }
                } finally {
                    Log.Logger = oldLogger;
                }

                _listener.BeginGetContext(GetContextCallback, null);
            } else if (request.Url.LocalPath.StartsWith(FILES_PREFIX)) {
                // Send file
                string localFileName = Path.Combine(_fileDirectory, request.Url.LocalPath.Substring(FILES_PREFIX.Length));
                using (var sr = new StreamReader(localFileName)) {
                    responseString = sr.ReadToEnd();
                }
            } else {
                string message = $"WebServer: URL cannot be handled - neither /run nor /files, but {request.Url}";
                Log.WriteWarning(message);
                responseString = WrapAsHtmlBody(message);
            }
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;

            using (Stream outputStream = response.OutputStream) {
                outputStream.Write(buffer, 0, buffer.Length);
            }
        }

        private string WrapAsHtmlBody(string s) {
            return $@"<!DOCTYPE HTML><html><body>{s.Replace("\r\n", "<br>\r\n")}</body></html>";
        }
    }
}

