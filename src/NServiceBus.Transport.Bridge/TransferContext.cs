﻿using NServiceBus.Transport;

class TransferContext
{
    public TransferContext(
        string sourceTransport,
        string proxyEndpointName,
        QueueAddress proxyQueueAddress,
        MessageContext messageToTransfer,
        bool passTransportTransaction)
    {
        SourceTransport = sourceTransport;
        ProxyEndpointName = proxyEndpointName;
        ProxyQueueAddress = proxyQueueAddress;
        MessageToTransfer = messageToTransfer;
        PassTransportTransaction = passTransportTransaction;
    }

    public string SourceTransport { get; }
    public string ProxyEndpointName { get; }
    public QueueAddress ProxyQueueAddress { get; }
    public MessageContext MessageToTransfer { get; }
    public bool PassTransportTransaction { get; }
}