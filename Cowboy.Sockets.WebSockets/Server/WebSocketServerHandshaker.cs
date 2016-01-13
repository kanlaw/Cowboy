﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Cowboy.Buffer;
using Cowboy.Logging;

namespace Cowboy.Sockets.WebSockets
{
    internal class WebSocketServerHandshaker
    {
        private static readonly ILog _log = Logger.Get<WebSocketServerHandshaker>();

        internal static bool HandleOpenningHandshakeRequest(AsyncWebSocketSession session, byte[] buffer, int offset, int count,
            out string secWebSocketKey,
            out string path,
            out string query)
        {
            BufferValidator.ValidateBuffer(buffer, offset, count, "buffer");

            var request = Encoding.UTF8.GetString(buffer, offset, count);
#if DEBUG
            _log.DebugFormat("{0}{1}", Environment.NewLine, request);
#endif
            // GET /chat HTTP/1.1
            // Host: server.example.com
            // Upgrade: websocket
            // Connection: Upgrade
            // Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==
            // Origin: http://example.com
            // Sec-WebSocket-Protocol: chat, superchat
            // Sec-WebSocket-Version: 13
            var headers = ParseOpenningHandshakeRequestHeaders(request);

            // An HTTP/1.1 or higher GET request, including a "Request-URI"
            // [RFC2616] that should be interpreted as a /resource name/
            // defined in Section 3 (or an absolute HTTP/HTTPS URI containing the /resource name/).
            // A |Host| header field containing the server's authority.
            if (!headers.ContainsKey(Consts.HttpGetMethodName))
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of get method.", session.RemoteEndPoint));
            if (!headers.ContainsKey(HttpKnownHeaderNames.Host))
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of host authority.", session.RemoteEndPoint));
            string uriString = string.Format("ws://{0}{1}", headers[HttpKnownHeaderNames.Host], headers[Consts.HttpGetMethodName]);
            Uri requestUri = null;
            if (!Uri.TryCreate(uriString, UriKind.RelativeOrAbsolute, out requestUri))
            {
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to invalid requested resource name.", session.RemoteEndPoint));
            }
            path = requestUri.AbsolutePath;
            query = requestUri.Query;

            // A |Connection| header field that includes the token "Upgrade",
            // treated as an ASCII case-insensitive value.
            if (!headers.ContainsKey(HttpKnownHeaderNames.Connection))
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of connection header item.", session.RemoteEndPoint));
            if (headers[HttpKnownHeaderNames.Connection].ToLowerInvariant() != Consts.WebSocketConnectionToken.ToLowerInvariant())
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to invalid connection header item value [{1}].",
                    session.RemoteEndPoint, headers[HttpKnownHeaderNames.Connection]));

            // An |Upgrade| header field containing the value "websocket",
            // treated as an ASCII case-insensitive value.
            if (!headers.ContainsKey(HttpKnownHeaderNames.Upgrade))
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of upgrade header item.", session.RemoteEndPoint));
            if (headers[HttpKnownHeaderNames.Upgrade].ToLowerInvariant() != Consts.WebSocketUpgradeToken.ToLowerInvariant())
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to invalid upgrade header item value [{1}].",
                    session.RemoteEndPoint, headers[HttpKnownHeaderNames.Upgrade]));

            // A |Sec-WebSocket-Key| header field with a base64-encoded (see
            // Section 4 of [RFC4648]) value that, when decoded, is 16 bytes in length.
            if (!headers.ContainsKey(HttpKnownHeaderNames.SecWebSocketKey))
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of Sec-WebSocket-Key header item.", session.RemoteEndPoint));
            if (string.IsNullOrWhiteSpace(headers[HttpKnownHeaderNames.SecWebSocketKey]))
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to invalid Sec-WebSocket-Key header item value [{1}].",
                    session.RemoteEndPoint, headers[HttpKnownHeaderNames.SecWebSocketKey]));
            secWebSocketKey = headers[HttpKnownHeaderNames.SecWebSocketKey];

            // A |Sec-WebSocket-Version| header field, with a value of 13.
            if (!headers.ContainsKey(HttpKnownHeaderNames.SecWebSocketVersion))
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to lack of Sec-WebSocket-Version header item.", session.RemoteEndPoint));
            if (headers[HttpKnownHeaderNames.SecWebSocketVersion].ToLowerInvariant() != Consts.WebSocketVersion.ToLowerInvariant())
                throw new WebSocketHandshakeException(string.Format(
                    "Handshake with remote [{0}] failed due to invalid Sec-WebSocket-Version header item value [{1}].",
                    session.RemoteEndPoint, headers[HttpKnownHeaderNames.SecWebSocketVersion]));

            // Optionally, a |Sec-WebSocket-Protocol| header field, with a list
            // of values indicating which protocols the client would like to
            // speak, ordered by preference.

            // Optionally, a |Sec-WebSocket-Extensions| header field, with a
            // list of values indicating which extensions the client would like
            // to speak.  The interpretation of this header field is discussed in Section 9.1.

            // Optionally, an |Origin| header field.  This header field is sent
            // by all browser clients.  A connection attempt lacking this
            // header field SHOULD NOT be interpreted as coming from a browser client.

            // Optionally, other header fields, such as those used to send
            // cookies or request authentication to a server.  Unknown header
            // fields are ignored, as per [RFC2616].

            return true;
        }

        internal static byte[] CreateOpenningHandshakeResponse(AsyncWebSocketSession session, string secWebSocketKey)
        {
            var sb = new StringBuilder();

            // A Status-Line with a 101 response code as per RFC 2616
            // [RFC2616].  Such a response could look like "HTTP/1.1 101
            // Switching Protocols".
            sb.AppendFormatWithCrCf("HTTP/{0} {1} {2}",
                Consts.HttpVersion,
                (int)HttpStatusCode.SwitchingProtocols,
                @"Switching Protocols");

            // An |Upgrade| header field with value "websocket" as per RFC2616 [RFC2616].
            sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.Upgrade, Consts.WebSocketUpgradeToken);

            // A |Connection| header field with value "Upgrade".
            sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.Connection, Consts.WebSocketConnectionToken);

            // A |Sec-WebSocket-Accept| header field.  The value of this
            // header field is constructed by concatenating /key/, defined
            // above in step 4 in Section 4.2.2, with the string "258EAFA5-
            // E914-47DA-95CA-C5AB0DC85B11", taking the SHA-1 hash of this
            // concatenated value to obtain a 20-byte value and base64-
            // encoding (see Section 4 of [RFC4648]) this 20-byte hash.
            var secWebSocketAccept = GetSecWebSocketAcceptString(secWebSocketKey);
            sb.AppendFormatWithCrCf(Consts.HeaderLineFormat, HttpKnownHeaderNames.SecWebSocketAccept, secWebSocketAccept);

            // Optionally, a |Sec-WebSocket-Protocol| header field, with a
            // value /subprotocol/ as defined in step 4 in Section 4.2.2.

            // Optionally, a |Sec-WebSocket-Extensions| header field, with a
            // value /extensions/ as defined in step 4 in Section 4.2.2.  If
            // multiple extensions are to be used, they can all be listed in
            // a single |Sec-WebSocket-Extensions| header field or split
            // between multiple instances of the |Sec-WebSocket-Extensions| header field.

            sb.AppendWithCrCf();

            // HTTP/1.1 101 Switching Protocols
            // Upgrade: websocket
            // Connection: Upgrade
            // Sec-WebSocket-Accept: 1tGBmA9p0DQDgmFll6P0/UcVS/E=
            // Sec-WebSocket-Protocol: chat
            var response = sb.ToString();
#if DEBUG
            _log.DebugFormat("{0}{1}", Environment.NewLine, response);
#endif
            return Encoding.UTF8.GetBytes(response);
        }

        private static Dictionary<string, string> ParseOpenningHandshakeRequestHeaders(string request)
        {
            var headers = new Dictionary<string, string>();

            var lines = request.Split(new char[] { '\r', '\n' }).Where(l => l.Length > 0);
            foreach (var line in lines)
            {
                // GET /chat HTTP/1.1
                if (line.StartsWith(Consts.HttpGetMethodName))
                {
                    var segements = line.Split(' ');
                    if (segements.Length > 1)
                    {
                        headers.Add(Consts.HttpGetMethodName, segements[1]);

                        if (segements.Length > 2)
                        {
                            var versions = segements[2].Split('/');
                            if (versions.Length > 1)
                            {
                                headers.Add(Consts.HttpVersionName, versions[1]);
                            }
                        }
                    }
                }
                else
                {
                    foreach (var item in HttpKnownHeaderNames.All)
                    {
                        if (line.StartsWith(item + ":"))
                        {
                            var index = line.IndexOf(':');
                            if (index != -1)
                            {
                                var value = line.Substring(index + 1);
                                headers.Add(item, value.Trim());
                            }
                        }
                    }
                }
            }

            return headers;
        }

        private static string GetSecWebSocketAcceptString(string secWebSocketKey)
        {
            string retVal;

            using (SHA1 sha1 = SHA1.Create())
            {
                string acceptString = string.Concat(secWebSocketKey, Consts.SecWebSocketKeyGuid);
                byte[] toHash = Encoding.UTF8.GetBytes(acceptString);
                retVal = Convert.ToBase64String(sha1.ComputeHash(toHash));
            }

            return retVal;
        }
    }
}
