// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethermind.Core;
using Nethermind.Core.Caching;
using Nethermind.Core.Extensions;
using Nethermind.Db;

namespace Nethermind.Trie
{
    public static class KeyValueStoreWithBatchingExtensions
    {
        public static CachingStore Cached(this IKeyValueStoreWithBatching @this, int maxCapacity)
        {
            return new CachingStore(@this, maxCapacity);
        }
    }

    public class CachingStore : IKeyValueStoreWithBatching
    {
        private readonly IKeyValueStoreWithBatching _wrappedStore;

        public CachingStore(IKeyValueStoreWithBatching wrappedStore, int maxCapacity)
        {
            _wrappedStore = wrappedStore ?? throw new ArgumentNullException(nameof(wrappedStore));
            _cache = new SpanLruCache<byte, byte[]>(maxCapacity, 0, "RLP Cache", Bytes.SpanEqualityComparer);
        }

        public bool PreferWriteByArray => true;
        private readonly SpanLruCache<byte, byte[]> _cache;

        public byte[]? this[ReadOnlySpan<byte> key]
        {
            get
            {
                return Get(key);
            }
            set
            {
                Set(key, value);
            }
        }

        public byte[]? Get(ReadOnlySpan<byte> key, ReadFlags flags = ReadFlags.None)
        {
            if ((flags & ReadFlags.HintCacheMiss) == ReadFlags.HintCacheMiss)
            {
                return _wrappedStore.Get(key, flags);
            }

            if (!_cache.TryGet(key, out byte[] value))
            {
                value = _wrappedStore.Get(key, flags);
                _cache.Set(key, value);
            }
            else
            {
                // TODO: a hack assuming that we cache only one thing, accepted unanimously by Lukasz, Marek, and Tomasz
                Pruning.Metrics.LoadedFromRlpCacheNodesCount++;
            }

            return value;
        }

        public void Set(ReadOnlySpan<byte> key, byte[]? value, WriteFlags flags = WriteFlags.None)
        {
            _cache.Set(key, value);
            _wrappedStore.Set(key, value, flags);
        }


        public IWriteBatch StartWriteBatch() => _wrappedStore.StartWriteBatch();

        public void PersistCache(IKeyValueStore pruningContext)
        {
            KeyValuePair<byte[], byte[]>[] clone = _cache.ToArray();
            Task.Run(() =>
            {
                foreach (KeyValuePair<byte[], byte[]> kvp in clone)
                {
                    pruningContext[kvp.Key] = kvp.Value;
                }
            });
        }

        public void DeleteByRange(Span<byte> startKey, Span<byte> endKey)
        {
            _wrappedStore.DeleteByRange(startKey, endKey);
        }
    }

    public class CachingDb : IDb
    {
        private readonly IDb _wrappedDb;

        public CachingDb(IDb wrappedDb, int maxCapacity)
        {
            _wrappedDb = wrappedDb ?? throw new ArgumentNullException(nameof(_wrappedDb));
            _cache = new SpanLruCache<byte, byte[]>(maxCapacity, 0, "RLP Cache", Bytes.SpanEqualityComparer);
        }

        public bool PreferWriteByArray => true;
        private readonly SpanLruCache<byte, byte[]> _cache;

        public byte[]? this[ReadOnlySpan<byte> key]
        {
            get
            {
                return Get(key);
            }
            set
            {
                Set(key, value);
            }
        }

        public byte[]? Get(ReadOnlySpan<byte> key, ReadFlags flags = ReadFlags.None)
        {
            if ((flags & ReadFlags.HintCacheMiss) == ReadFlags.HintCacheMiss)
            {
                return _wrappedDb.Get(key, flags);
            }

            if (!_cache.TryGet(key, out byte[] value))
            {
                value = _wrappedDb.Get(key, flags);
                _cache.Set(key, value);
            }
            else
            {
                // TODO: a hack assuming that we cache only one thing, accepted unanimously by Lukasz, Marek, and Tomasz
                Pruning.Metrics.LoadedFromRlpCacheNodesCount++;
            }

            return value;
        }

        public void Set(ReadOnlySpan<byte> key, byte[]? value, WriteFlags flags = WriteFlags.None)
        {
            _cache.Set(key, value);
            _wrappedDb.Set(key, value, flags);
        }

        public IWriteBatch StartWriteBatch() => _wrappedDb.StartWriteBatch();

        public void DeleteByRange(Span<byte> startKey, Span<byte> endKey)
        {
            //should put the effort to iterate over cache?
            _cache.Clear();
            _wrappedDb.DeleteByRange(startKey, endKey);
        }

        public void Dispose() => _wrappedDb.Dispose();

        public string Name => _wrappedDb.Name;

        public KeyValuePair<byte[], byte[]?>[] this[byte[][] keys] => _wrappedDb[keys];

        public IEnumerable<KeyValuePair<byte[], byte[]?>> GetAll(bool ordered = false) => _wrappedDb.GetAll(ordered);

        public IEnumerable<byte[]> GetAllValues(bool ordered = false) => _wrappedDb.GetAllValues(ordered);
    }
}
