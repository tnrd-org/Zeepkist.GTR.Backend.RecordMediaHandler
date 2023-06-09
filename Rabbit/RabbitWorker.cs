﻿using System.Text;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TNRD.Zeepkist.GTR.DTOs.Rabbit;

namespace TNRD.Zeepkist.GTR.Backend.RecordMediaHandler.Rabbit;

internal class RabbitWorker : IHostedService
{
    private readonly RabbitOptions options;
    private readonly MediaQueue mediaQueue;
    private readonly IRabbitPublisher publisher;

    private IConnection connection = null!;
    private IModel channel = null!;

    public RabbitWorker(IOptions<RabbitOptions> options, MediaQueue mediaQueue, IRabbitPublisher publisher)
    {
        this.mediaQueue = mediaQueue;
        this.publisher = publisher;
        this.options = options.Value;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        ConnectionFactory factory = new ConnectionFactory()
        {
            HostName = options.Host,
            Port = options.Port,
            UserName = options.Username,
            Password = options.Password
        };

        connection = factory.CreateConnection();
        channel = connection.CreateModel();

        channel.ExchangeDeclare(exchange: "records", type: ExchangeType.Fanout);
        channel.ExchangeDeclare(exchange: "media", type: ExchangeType.Fanout);

        string? queueName = channel.QueueDeclare().QueueName;
        channel.QueueBind(queue: queueName,
            exchange: "media",
            routingKey: string.Empty);

        EventingBasicConsumer consumer = new EventingBasicConsumer(channel);
        consumer.Received += OnReceived;
        channel.BasicConsume(queue: queueName,
            autoAck: true,
            consumer: consumer);

        publisher.Initialize(channel);
        return Task.CompletedTask;
    }

    private void OnReceived(object? sender, BasicDeliverEventArgs e)
    {
        byte[] body = e.Body.ToArray();
        string message = Encoding.UTF8.GetString(body);
        UploadRecordMediaRequest request = JsonConvert.DeserializeObject<UploadRecordMediaRequest>(message)!;
        mediaQueue.AddToQueue(request);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        channel.Close();
        connection.Close();
        return Task.CompletedTask;
    }
}
