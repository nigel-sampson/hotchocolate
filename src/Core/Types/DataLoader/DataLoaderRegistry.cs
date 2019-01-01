using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using GreenDonut;
using HotChocolate.Execution;

namespace HotChocolate.DataLoader
{
    public class DataLoaderRegistry
        : IDataLoaderRegistry
        , IBatchOperation
    {
        private readonly ConcurrentDictionary<string, Func<IDataLoader>> _factories
            = new ConcurrentDictionary<string, Func<IDataLoader>>();

        private readonly ConcurrentDictionary<string, IDataLoader> _instances
            = new ConcurrentDictionary<string, IDataLoader>();

        private readonly IServiceProvider _services;

        public DataLoaderRegistry(IServiceProvider services)
        {
            _services = services
                ?? throw new ArgumentNullException(nameof(services));
        }

        public event EventHandler<EventArgs> BatchSizeIncreased;

        public int BatchSize => _instances.Count;

        public Task InvokeAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public bool Register<T>(string key, Func<IServiceProvider, T> factory)
            where T : IDataLoader
        {
            if (string.IsNullOrEmpty(key))
            {
                // TODO : Resources
                throw new ArgumentException(
                    "The DataLoader key cannot be null or empty.",
                    nameof(key));
            }

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            return _factories.TryAdd(key, () => factory(_services));
        }

        public bool TryGet<T>(string key, out T dataLoader)
            where T : IDataLoader
        {
            if (string.IsNullOrEmpty(key))
            {
                // TODO : Resources
                throw new ArgumentException(
                    "The DataLoader key cannot be null or empty.",
                    nameof(key));
            }

            if (!_instances.TryGetValue(key, out IDataLoader loader)
                && _factories.TryGetValue(key, out Func<IDataLoader> factory))
            {
                lock (factory)
                {
                    if (!_instances.TryGetValue(key, out loader))
                    {
                        loader = _instances.GetOrAdd(key, k =>
                        {
                            IDataLoader instance = factory();
                            // TODO : register event
                            return instance;
                        });
                        SendBatchSizeIncreasedEvent();
                    }
                }
            }

            if (loader is T l)
            {
                dataLoader = l;
                return true;
            }

            dataLoader = default(T);
            return false;
        }

        private void SendBatchSizeIncreasedEvent()
        {
            if (BatchSizeIncreased != null)
            {
                BatchSizeIncreased(this, EventArgs.Empty);
            }
        }
    }
}