﻿using System;
using System.IO.Pipes;
using System.Threading.Tasks;
using Microsoft.Bot.Protocol.Payloads;
using Microsoft.Bot.Protocol.PayloadTransport;
using Microsoft.Bot.Protocol.Utilities;

namespace Microsoft.Bot.Protocol
{
    public class NamedPipeServer
    {
        private readonly string _baseName;
        private readonly RequestHandler _requestHandler;
        private readonly RequestManager _requestManager;
        private readonly IPayloadSender _sender;
        private readonly IPayloadReceiver _receiver;
        private readonly ProtocolAdapter _protocolAdapter;
        private readonly bool _autoReconnect;
        private bool _isDisconnecting = false;

        public NamedPipeServer(string baseName, RequestHandler requestHandler, bool autoReconnect = true)
        {
            _baseName = baseName;
            _requestHandler = requestHandler;
            _autoReconnect = autoReconnect;

            _requestManager = new RequestManager();

            _sender = new PayloadSender();
            _sender.Disconnected += OnConnectionDisconnected;
            _receiver = new PayloadReceiver();
            _receiver.Disconnected += OnConnectionDisconnected;

            _protocolAdapter = new ProtocolAdapter(_requestHandler, _requestManager, _sender, _receiver);
        }

        public async Task StartAsync()
        {
            var incomingPipeName = _baseName + NamedPipeTransport.ServerIncomingPath;
            var incomingServer = new NamedPipeServerStream(incomingPipeName, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.WriteThrough | PipeOptions.Asynchronous);
            await incomingServer.WaitForConnectionAsync().ConfigureAwait(false);

            var outgoingPipeName = _baseName + NamedPipeTransport.ServerOutgoingPath;
            var outgoingServer = new NamedPipeServerStream(outgoingPipeName, PipeDirection.Out, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.WriteThrough | PipeOptions.Asynchronous);
            await outgoingServer.WaitForConnectionAsync().ConfigureAwait(false);

            _sender.Connect(new NamedPipeTransport(outgoingServer));
            _receiver.Connect(new NamedPipeTransport(incomingServer));
        }

        public Task<ReceiveResponse> SendAsync(Request request)
        {
            return _protocolAdapter.SendRequestAsync(request);
        }

        public void Disconnect()
        {
            _sender.Disconnect();
            _receiver.Disconnect();
        }

        private void OnConnectionDisconnected(object sender, EventArgs e)
        {
            if (!_isDisconnecting)
            {
                _isDisconnecting = true;

                try
                {
                    if (_sender.IsConnected)
                    {
                        _sender.Disconnect();
                    }

                    if (_receiver.IsConnected)
                    {
                        _receiver.Disconnect();
                    }

                    if (_autoReconnect)
                    {
                        // Try to rerun the server connection 
                        Background.Run(StartAsync);
                    }
                }
                finally
                {
                    _isDisconnecting = false;
                }
            }
        }
    }
}
