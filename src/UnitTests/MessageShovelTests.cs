﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NServiceBus;
using NServiceBus.Faults;
using NServiceBus.Raw;
using NServiceBus.Transport;
using NUnit.Framework;

public class MessageShovelTests
{
    [Test]
    public async Task Should_transform_reply_to_address()
    {
        var transferDetails = await Transfer(replyToAddress: "SendingEndpointReplyAddress@MyMachine");

        Assert.AreEqual("SendingEndpointReplyAddress", transferDetails.OutgoingOperation.Message.Headers[Headers.ReplyToAddress]);
    }

    [Test]
    public async Task Should_transform_failed_queue_header()
    {
        var transferDetails = await Transfer(failedQueueAddress: "error@MyMachine");

        Assert.AreEqual("error", transferDetails.OutgoingOperation.Message.Headers[FaultsHeaderKeys.FailedQ]);
    }

    [Test]
    public async Task Should_transform_retry_ack_queue_header()
    {
        var transferDetails = await Transfer(retryAckQueueAddress: "error@MyMachine");

        Assert.AreEqual("error", transferDetails.OutgoingOperation.Message.Headers["ServiceControl.Retry.AcknowledgementQueue"]);
    }

    [Test]
    public async Task Should_not_transform_retry_ack_header_for_audited_message()
    {
        var transferDetails = await Transfer(retryAckQueueAddress: "error@MyMachine", isAuditMessage: true);

        Assert.AreEqual("error@MyMachine", transferDetails.OutgoingOperation.Message.Headers["ServiceControl.Retry.AcknowledgementQueue"]);
    }

    [Test]
    public async Task Should_handle_send_only_endpoints()
    {
        //send only endpoints doesn't attach a reply to address
        var transferDetails = await Transfer(replyToAddress: null);

        Assert.NotNull(transferDetails.OutgoingOperation);
    }


    [Test]
    public async Task Should_attach_transfer_header()
    {
        var transferDetails = await Transfer();

        Assert.AreEqual("SourceTransport->TargetTransport", transferDetails.OutgoingOperation.Message.Headers[BridgeHeaders.Transfer]);
    }

    [Test]
    public async Task Should_pass_transport_transaction_if_specified()
    {
        var transportTransaction = new TransportTransaction();

        var transferWithoutTransaction = await Transfer(
            transportTransaction: transportTransaction,
            passTransportTransaction: false);

        Assert.AreNotSame(transportTransaction, transferWithoutTransaction.TransportTransaction);

        var transferWithTransaction = await Transfer(
             transportTransaction: transportTransaction,
             passTransportTransaction: true);

        Assert.AreSame(transportTransaction, transferWithTransaction.TransportTransaction);
    }

    [Test]
    public async Task Should_dispatch_message_to_configured_address()
    {
        var targetEndpointAddress = "target@some-machine";

        var transferDetails = await Transfer(targetAddress: targetEndpointAddress);

        Assert.AreEqual(targetEndpointAddress, transferDetails.OutgoingOperation.Destination);
    }

    static async Task<TransferDetails> Transfer(
        string targetAddress = null,
        string replyToAddress = null,
        string failedQueueAddress = null,
        string retryAckQueueAddress = null,
        bool isAuditMessage = false,
        TransportTransaction transportTransaction = null,
        bool passTransportTransaction = false)
    {
        var logger = new NullLogger<MessageShovel>();
        var headers = new Dictionary<string, string>();

        if (string.IsNullOrEmpty(targetAddress))
        {
            targetAddress = "TargetAddress";
        }

        if (!string.IsNullOrEmpty(replyToAddress))
        {
            headers.Add(Headers.ReplyToAddress, replyToAddress);
        }

        if (!string.IsNullOrEmpty(failedQueueAddress))
        {
            headers.Add(FaultsHeaderKeys.FailedQ, failedQueueAddress);
        }

        if (!string.IsNullOrEmpty(retryAckQueueAddress))
        {
            headers.Add("ServiceControl.Retry.UniqueMessageId", "some-id");
            headers.Add("ServiceControl.Retry.AcknowledgementQueue", retryAckQueueAddress);
        }

        if (isAuditMessage)
        {
            headers.Add(Headers.ProcessingEnded, DateTime.UtcNow.ToString());
        }

        var targetEndpoint = new BridgeEndpoint("TargetEndpoint", targetAddress);
        var dispatcherRegistry = new FakeTargetEndpointRegistry("TargetTransport", targetEndpoint);
        var shovel = new MessageShovel(logger, dispatcherRegistry);
        var messageContext = new MessageContext(
            "some-id",
            headers,
            ReadOnlyMemory<byte>.Empty,
            transportTransaction ?? new TransportTransaction(),
            "SourceEndpointAddress",
            new NServiceBus.Extensibility.ContextBag());

        var transferContext = new TransferContext(
            "SourceTransport",
            "SourceEndpoint",
            messageContext,
            passTransportTransaction);

        await shovel.TransferMessage(transferContext, CancellationToken.None);

        return dispatcherRegistry.TransferDetails;
    }

    class FakeRawEndpoint : IStoppableRawEndpoint, IRawEndpoint
    {
        public FakeRawEndpoint(string endpointName)
        {
            EndpointName = endpointName;
        }

        public string TransportAddress => EndpointName;

        public string EndpointName { get; }

        public ISubscriptionManager SubscriptionManager => null;

        public TransferDetails TransferDetails { get; private set; }

        public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken cancellationToken = default)
        {
            TransferDetails = new TransferDetails
            {
                OutgoingOperation = outgoingMessages.UnicastTransportOperations.Single(),
                TransportTransaction = transaction
            };

            return Task.CompletedTask;
        }

        public Task Stop(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public string ToTransportAddress(QueueAddress logicalAddress) => logicalAddress.ToString();
    }

    class FakeTargetEndpointRegistry : IEndpointRegistry
    {
        public FakeTargetEndpointRegistry(string targetTransport, BridgeEndpoint targetEndpoint)
        {
            this.targetTransport = targetTransport;
            this.targetEndpoint = targetEndpoint;
            rawEndpoint = new FakeRawEndpoint(targetEndpoint.Name);
        }

        public TargetEndpointDispatcher GetTargetEndpointDispatcher(string sourceEndpointName)
        {
            return new TargetEndpointDispatcher(targetTransport, rawEndpoint, targetEndpoint.QueueAddress);
        }

        public string TranslateToTargetAddress(string sourceAddress)
        {
            return sourceAddress.Split('@').First();
        }

        public string GetEndpointAddress(string endpointName) => throw new NotImplementedException();

        readonly string targetTransport;
        readonly BridgeEndpoint targetEndpoint;
        readonly FakeRawEndpoint rawEndpoint;

        public TransferDetails TransferDetails => rawEndpoint.TransferDetails;
    }

    public class TransferDetails
    {
        public UnicastTransportOperation OutgoingOperation { get; set; }
        public TransportTransaction TransportTransaction { get; set; }
    }
}