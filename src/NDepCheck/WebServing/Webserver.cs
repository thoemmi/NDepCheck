using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace NDepCheck.WebServing {
    public class WebServer {
        private const string FILES_PREFIX = "/files/";
        private readonly Program _program;
        private readonly string _fullFileDirectory;
        private readonly short _port;
        private readonly HttpListener _listener = new HttpListener();

        public WebServer(Program program, string port, string fileDirectory) {
            if (!short.TryParse(port, out _port) || _port < 1024) {
                throw new ArgumentException("Port must be a number in the range 1024...32767");
            }
            _program = program;
            _fullFileDirectory = Path.GetFullPath(fileDirectory);
        }

        public void Start() {
            _listener.Prefixes.Add($"http://*:{_port}/");
            _listener.Start();
            Log.WriteInfo($"Listening on port {_port}");
            _listener.BeginGetContext(GetContextCallback, null);
        }

        public void Stop() {
            _listener.Stop();
        }

        public void GetContextCallback(IAsyncResult result) {
            HttpListenerContext context = _listener.EndGetContext(result);
            HttpListenerRequest request = context.Request;
            try {
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
                    _listener.BeginGetContext(GetContextCallback, null);

                    var args = new List<string>();
                    foreach (string key in request.QueryString.AllKeys.OrderBy(k => k)) {
                        args.Add(request.QueryString[key]);
                    }

                    ILogger oldLogger = Log.Logger;
                    try {
                        var stringBuilderLogger = new StringBuilderLogger();
                        Log.Logger = stringBuilderLogger;

                        var writtenMasterFiles = new List<string>();
                        // Each call runs with its own environment=GlobalContext; this is necessary
                        // so that each one can set its own defines.

                        string previousCurrentDirectory = Environment.CurrentDirectory;
                        int runResult;
                        try {
                            Environment.CurrentDirectory = _fullFileDirectory;
                            runResult = _program.Run(args.ToArray(), new string[0], new GlobalContext(), writtenMasterFiles,
                                logCommands: true);
                        } finally {
                            Environment.CurrentDirectory = previousCurrentDirectory;
                        }

                        if (runResult != Program.OK_RESULT) {
                            stringBuilderLogger.WriteWarning($"Run had result {runResult} - see problems above");
                        }

                        if (stringBuilderLogger.HasWarningsOrErrors) {
                            responseString = WrapAsHtmlBody(stringBuilderLogger.GetString());
                            // send errors to client
                        } else if (writtenMasterFiles.Count > 1) {
                            // send file selection HTML to client - UNTESTED
                            var sb = new StringBuilder();
                            sb.AppendLine("<ol>");
                            foreach (var f in writtenMasterFiles) {
                                sb.AppendLine($@"<li><a href=""{FILES_PREFIX}{f}"">{f}</a></li>");
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

                    Log.WriteDebug($"Response: '{responseString}'");

                    SendStringReponse(responseString, response);
                } else if (request.Url.LocalPath.StartsWith(FILES_PREFIX)) {
                    _listener.BeginGetContext(GetContextCallback, null);

                    // Send file
                    string localFileName = Path.Combine(_fullFileDirectory,
                        request.Url.LocalPath.Substring(FILES_PREFIX.Length));

                    using (var sr = new StreamReader(localFileName)) {
                        response.ContentLength64 = sr.BaseStream.Length;
                        sr.BaseStream.CopyTo(response.OutputStream);
                    }
                } else {
                    _listener.BeginGetContext(GetContextCallback, null);

                    string message = $"WebServer: URL cannot be handled - neither /run nor /files, but {request.Url}";
                    Log.WriteWarning(message);
                    responseString = WrapAsHtmlBody(message);

                    SendStringReponse(responseString, response);
                }
            } catch (Exception ex) {
                Log.WriteError($"Cannot handle web request {request.Url}; reason: {ex.GetType().Name} '{ex.Message}'");
            }
        }

        private static void SendStringReponse(string responseString, HttpListenerResponse response) {
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;

            using (Stream outputStream = response.OutputStream) {
                outputStream.Write(buffer, 0, buffer.Length);
            }
        }

        private string WrapAsHtmlBody(string s) {
            return $@"<!DOCTYPE HTML><html><body>{s.Replace(Environment.NewLine, "<br>" + Environment.NewLine)}</body></html>";
        }
    }
}
