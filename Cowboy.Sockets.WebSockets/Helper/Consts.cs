﻿using System.Text;

namespace Cowboy.Sockets.WebSockets
{
    internal class Consts
    {
        internal static readonly byte[] HeaderTerminator = Encoding.UTF8.GetBytes("\r\n\r\n");

        internal static readonly string[] WebSocketSchemes = new string[] { "ws", "wss" };

        internal const string HeaderLineFormat = "{0}: {1}";

        internal const string HttpStatusCodeName = "HttpStatusCode";
        internal const string HttpStatusCodeDescription = "HttpStatusCodeDescription";
        internal const string HttpGetMethodName = "GET";
        internal const string HttpVersionName = "HTTP";

        internal const string SecWebSocketKeyGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        internal const string WebSocketUpgradeToken = "websocket";
        internal const string WebSocketConnectionToken = "Upgrade";

        internal const string HttpVersion = "1.1";
        internal const string WebSocketVersion = "13";
    }
}
