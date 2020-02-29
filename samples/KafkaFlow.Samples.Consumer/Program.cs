﻿namespace KafkaFlow.Samples.Consumer
{
    using System.Threading;
    using KafkaFlow.Extensions;
    using KafkaFlow.Samples.Common;
    using KafkaFlow.Serializer.ProtoBuf;
    using KafkaFlow.TypedHandler;
    using Microsoft.Extensions.DependencyInjection;

    class Program
    {
        static void Main(string[] args)
        {
            var services = new ServiceCollection();

            services.AddKafka(
                kafka => kafka
                    .UseLogHandler<ConsoleLogHandler>()
                    .AddCluster(
                        cluster => cluster
                            .WithBrokers(new[] { "localhost:9092" })
                            .AddConsumer(
                                consumer => consumer
                                    .Topic("test-topic")
                                    .WithGroupId("print-console-handler")
                                    .WithBufferSize(100)
                                    .WithWorkersCount(10)
                                    .UseMiddleware<MessageTypeResolverMiddleware>()
                                    .UseSerializer<ProtobufMessageSerializer>()
                                    .UseTypedHandlers(handlers =>
                                        handlers
                                            .WithHandlerLifetime(ServiceLifetime.Singleton)
                                            .AddHandler<PrintConsoleHandler>())
                            )
                    )
            );

            var provider = services.BuildServiceProvider();

            var bus = provider.UseKafka();

            bus.StartAsync().GetAwaiter().GetResult();

            while (true)
            {
                Thread.Sleep(10000);
            }
        }
    }
}
