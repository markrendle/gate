﻿using System;
using System.Collections.Generic;
using System.Text;
using Gate.Spooling;

namespace Gate.Helpers
{
    using ResultDelegate = Action< // result
        string, // status
        IDictionary<string, string>, // headers
        Func< // body
            Func< // next
                ArraySegment<byte>, // data
                Action, // continuation
                bool>, // async                    
            Action<Exception>, // error
            Action, // complete
            Action>>; // cancel

    public class Response
    {
        readonly ResultDelegate _result;
        readonly Spool _spool = new Spool(true);

        public Response(ResultDelegate result)
        {
            _result = result;

            Status = "200 OK";
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Encoding = Encoding.UTF8;
        }

        public string Status { get; set; }
        public IDictionary<string, string> Headers { get; set; }
        public Encoding Encoding { get; set; }

        string GetHeader(string name)
        {
            string value;
            return Headers.TryGetValue(name, out value) ? value : null;
        }

        void SetHeader(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                Headers.Remove(value);
            else
                Headers[name] = value;
        }

        public string ContentType
        {
            get { return GetHeader("Content-Type"); }
            set { SetHeader("Content-Type", value); }
        }

        public Response Write(string text)
        {
            // this could be more efficient if it spooled the immutable strings instead...
            var data = Encoding.GetBytes(text);
            _spool.Push(new ArraySegment<byte>(data), null);
            return this;
        }

        public void Finish()
        {
            Finish((fault, complete) => complete());
        }

        public void Finish(Action<Action<Exception>, Action> body)
        {
            _result(
                Status,
                Headers,
                (next, error, complete) =>
                {
                    // TODO - this is sloppy and barely works
                    var buffer = new byte[512];

                    body(error, _spool.PushComplete);

                    for (;;)
                    {
                        var count = new[] {0};
                        _spool.Pull(new ArraySegment<byte>(buffer), count, null);
                        if (count[0] == 0)
                            break;
                        next(new ArraySegment<byte>(buffer, 0, count[0]), null);
                    }

                    complete();

                    return () => { };
                });
        }
    }
}