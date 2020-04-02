namespace KafkaFlow.Consumers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Confluent.Kafka;
    using KafkaFlow.Configuration;

    internal class ConsumerWorkerPool : IConsumerWorkerPool
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ConsumerConfiguration configuration;
        private readonly ILogHandler logHandler;
        private readonly IMiddlewareExecutor middlewareExecutor;
        private readonly Factory<IDistributionStrategy> distributionStrategyFactory;

        private readonly List<IConsumerWorker> workers = new List<IConsumerWorker>();

        private IDistributionStrategy distributionStrategy;

        private OffsetManager offsetManager;

        public ConsumerWorkerPool(
            IServiceProvider serviceProvider,
            ConsumerConfiguration configuration,
            ILogHandler logHandler,
            IMiddlewareExecutor middlewareExecutor,
            Factory<IDistributionStrategy> distributionStrategyFactory)
        {
            this.serviceProvider = serviceProvider;
            this.configuration = configuration;
            this.logHandler = logHandler;
            this.middlewareExecutor = middlewareExecutor;
            this.distributionStrategyFactory = distributionStrategyFactory;
        }

        public async Task StartAsync(
            IConsumer<byte[], byte[]> consumer,
            IReadOnlyCollection<TopicPartition> partitions,
            CancellationToken stopCancellationToken = default)
        {
            this.offsetManager = new OffsetManager(consumer, partitions);
            var workersCount = this.configuration.WorkersCount;

            await Task.WhenAll(
                    Enumerable
                        .Range(0, workersCount)
                        .Select(
                            workerId =>
                            {
                                var worker = new ConsumerWorker(
                                    consumer,
                                    workerId,
                                    this.configuration,
                                    this.offsetManager,
                                    this.logHandler,
                                    this.middlewareExecutor);

                                this.workers.Add(worker);

                                return worker.StartAsync(stopCancellationToken);
                            }))
                .ConfigureAwait(false);

            this.distributionStrategy = this.distributionStrategyFactory(this.serviceProvider);
            this.distributionStrategy.Init(this.workers.AsReadOnly());
        }

        public async Task StopAsync()
        {
            await Task.WhenAll(this.workers.Select(x => x.StopAsync())).ConfigureAwait(false);

            this.workers.Clear();
        }

        public async Task EnqueueAsync(ConsumeResult<byte[], byte[]> message)
        {
            this.offsetManager.InitializeOffsetIfNeeded(message.TopicPartitionOffset);

            var worker = (IConsumerWorker) await this.distributionStrategy
                .GetWorkerAsync(message.Key)
                .ConfigureAwait(false);

            await worker
                .EnqueueAsync(message)
                .ConfigureAwait(false);
        }
    }
}
