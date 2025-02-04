﻿using System;
using RabbitMQ.Client;

public class ConnectionHelper
{
    static Lazy<string> connectionString = new Lazy<string>(() =>
    {
        var connectionString = Environment.GetEnvironmentVariable("RabbitMQTransport_ConnectionString") ?? "host=localhost";

        return connectionString;
    });

    static Lazy<ConnectionFactory> connectionFactory = new Lazy<ConnectionFactory>(() =>
    {
        var connectionStringParser = new RabbitMqConnectionStringParser(ConnectionString);

        var factory = new ConnectionFactory
        {
            AutomaticRecoveryEnabled = true,
            UseBackgroundThreadsForIO = true,
            HostName = connectionStringParser.HostName,
            UserName = connectionStringParser.UserName ?? "guest",
            Password = connectionStringParser.Password ?? "guest",
        };

        if (!string.IsNullOrEmpty(connectionStringParser.VirtualHost))
        {
            factory.VirtualHost = connectionStringParser.VirtualHost;
        }

        if (connectionStringParser.Port.HasValue)
        {
            factory.Port = connectionStringParser.Port.Value;
        }

        return factory;
    });

    public static string ConnectionString => connectionString.Value;

    public static ConnectionFactory ConnectionFactory => connectionFactory.Value;
}