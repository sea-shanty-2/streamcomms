using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using BroadcastCommunication.Packet;
using Fleck;
using J3QQ4;
using Newtonsoft.Json;

namespace BroadcastCommunication.Sockets
{
    public class WebSocketServer : Fleck.WebSocketServer, IWebSocketServer
    {
        private readonly IDictionary<string, Polarity> _emojiPolarityMap;
        private readonly IDictionary<IWebSocketConnection, WebSocketClient> _clientMap;

        public WebSocketServer(string location, bool supportDualStack = true, bool useSsl = false, string certFile = null) : base(useSsl ? $"wss://{location}" : $"ws://{location}", supportDualStack)
        {
            if (useSsl && !string.IsNullOrWhiteSpace(certFile))
            {
                Certificate = new X509Certificate2(certFile);
            }
            
            _clientMap = new ConcurrentDictionary<IWebSocketConnection, WebSocketClient>();
            _emojiPolarityMap = new Dictionary<string, Polarity>
            {
                { Emoji.Fire, Polarity.Positive},
                { Emoji.Joy, Polarity.Positive},
                { Emoji.Thumbsup, Polarity.Positive},
                { Emoji.Heart, Polarity.Positive},
                { Emoji.Eggplant, Polarity.Positive},
                { Emoji.Angry, Polarity.Negative},
                { Emoji.Thumbsdown, Polarity.Negative},
            };
        }

        public void Start()
        {
            base.Start(socket =>
            {
                socket.OnClose = () => ConnectionClosed(socket);
                socket.OnOpen = () => ConnectionOpened(socket);
            });
        }

        public void Broadcast(string channel, IPacket packet, ISet<IWebSocketClient> excludedClients)
        {
            var serialized = JsonConvert.SerializeObject(packet);
            
            foreach (var (socket, _) in _clientMap.Where(item => !excludedClients.Contains(item.Value) && item.Value.Channel.Equals(channel)))
                socket.Send(serialized);
        }

        public bool IsEmojiAllowed(string emoji)
        {
            return _emojiPolarityMap.ContainsKey(emoji);
        }

        public Polarity GetEmojiPolarity(string emoji)
        {
            if (!IsEmojiAllowed(emoji))
                throw new EmojiNotAllowedException(emoji);

            return _emojiPolarityMap[emoji];
        }

        private void ConnectionOpened(IWebSocketConnection socket)
        {
            var client = new WebSocketClient(this);
            socket.OnMessage = client.HandleMessage;
            _clientMap[socket] = client;
        }

        private void ConnectionClosed(IWebSocketConnection socket)
        {
            if (_clientMap.ContainsKey(socket))
                _clientMap.Remove(socket);
        }
    }
}