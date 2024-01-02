// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Threading;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Verkle;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.Cache;
using Nethermind.Verkle.Tree.TrieNodes;
using Nethermind.Verkle.Tree.Utils;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree.TrieStore;

/// <summary>
///     Now the persisting and state access also have many issues, there are multiple cases to account for
///     STATE ACCESS
///     1. Just read the state for a particular stateRoot - no modifications allowed -> JsonRpc -> readOnlyChain
///     2. Move the entire state to the specific state root in the history - cannot be reverted -> can be mostly used for
///     reorgs.
///     3. Keep multiple version of changes on top of current working state then choose one of them to be committed -> can
///     be used when you receive multiple new_payload for one blockNumber but only know the correct state after FCU
///     This can be achieved with a WorldStateProvider -> (WorldState and ReadOnlyWorldState) interfaces (separate from
///     what we have right now)
///     Where you can ask WorldStateProvider to give you a object for your needs, you can call methods like
///     WorldStateProvider.ForSyncClient()
///     WorldStateProvider.ForSyncServer()
///     WorldStateProvider.ForBlockProcessing()
///     WorldStateProvider.ForBlockProduction()
///     WorldStateProvider.ForNewPayload()
///     WorldStateProvider.Reorg()
///     WorldStateProvider.ForkChoiceUpdate()
///     WorldStateProvider.ForJsonRpc()
///     WorldStateProvider.FinalizeState()
///     Now these interfaces would be specially designed to work for a specific interface.
///     Cases - NewPayload (A) NewPayload(B) FCU(A)
/// </summary>
public partial class VerkleStateStore
{
    private int _isFirst;

    // This method is called at the end of each block to flush the batch changes to the storage and generate forward and reverse diffs.
    // this should be called only once per block, right now it does not support multiple calls for the same block number.
    // if called multiple times, the full state would be fine - but it would corrupt the diffs and historical state will be lost
    // TODO: add capability to update the diffs instead of overwriting if Flush(long blockNumber)
    //   is called multiple times for the same block number, but do we even need this? also how to distinguish if it should be overwritten
    //   or just merged?

    // TODO: add a functionality where we only persist to rocksDb when we have a epoch finalization.
    // TODO: do we need to add another approach where we bulk insert into the db - or batching on epochs is fine?
    public void InsertBatch(long blockNumber, VerkleMemoryDb batch)
    {
        // TODO: create a sorted set here - we need it for verkleSync serving
        ReadOnlyVerkleMemoryDb cacheBatch = new()
        {
            InternalTable = batch.InternalTable,
            LeafTable = new LeafStoreSorted(batch.LeafTable, Bytes.Comparer)
        };

        if (blockNumber == 0)
        {
            if (_logger.IsDebug) _logger.Debug("Persisting the changes for block 0");
            PersistBlockChanges(batch.InternalTable, batch.LeafTable, Storage);
            InsertBatchCompletedV1?.Invoke(this, new InsertBatchCompletedV1(0, cacheBatch, null));
            Storage.GetInternalNode(RootNodeKey, out InternalNode? newRoot);
            StateRoot = newRoot?.Bytes ?? Hash256.Zero;
            PersistedStateRoot = StateRoot;
            LatestCommittedBlockNumber = LastPersistedBlockNumber = 0;
            StateRootToBlocks[StateRoot] = blockNumber;
        }
        else
        {
            if (!BlockCache.IsInitialized)
            {
                Storage.GetInternalNode(RootNodeKey, out InternalNode? newRoot);
                if (newRoot is null) _logger.Error("ERROR newRoot is null - this must be some kind of error");
                BlockCache.InitCache(blockNumber - 1, newRoot?.Bytes ?? Hash256.Zero);
            }

            Hash256? rootToCommit = GetStateRoot(batch.InternalTable);
            if (rootToCommit is null)
            {
                if (!(batch.InternalTable.Count == 0 && batch.LeafTable.Count == 0))
                    throw new StateFlushException(
                        $"Failed InsertBatch:{blockNumber}. StateRoot not found in the batch");
                rootToCommit = StateRoot;
            }


            var shouldPersistBlock = BlockCache.EnqueueAndReplaceIfFull(blockNumber,
                rootToCommit, cacheBatch, StateRoot, out StateInfo element);

            if (shouldPersistBlock)
            {
                ReadOnlyVerkleMemoryDb changesToPersist = element.StateDiff;
                var blockNumberToPersist = element.BlockNumber;
                if (_logger.IsDebug)
                    _logger.Debug(
                        $"BlockCache is full - got forwardDiff BlockNumber:{blockNumberToPersist} IN:{changesToPersist.InternalTable.Count} LN:{changesToPersist.LeafTable.Count}");
                Hash256 root = GetStateRoot(changesToPersist.InternalTable) ??
                               new Hash256(Storage.GetInternalNode(RootNodeKey)?.Bytes ??
                                           throw new ArgumentException());
                if (_logger.IsDebug) _logger.Debug($"StateRoot after persisting forwardDiff: {root}");

                PersistBlockChanges(changesToPersist.InternalTable, changesToPersist.LeafTable, Storage,
                    out VerkleMemoryDb reverseDiff);
                // TODO: handle this properly - while testing this is needed so that this does not fuck up other things
                try
                {
                    InsertBatchCompletedV1?.Invoke(this,
                        new InsertBatchCompletedV1(blockNumberToPersist, changesToPersist, reverseDiff));
                    InsertBatchCompletedV2?.Invoke(this,
                        new InsertBatchCompletedV2(blockNumberToPersist, reverseDiff.LeafTable));
                }
                catch (Exception e)
                {
                    _logger.Error("Error while persisting the history, not propagating forward", e);
                }

                PersistedStateRoot = root;
                LastPersistedBlockNumber = blockNumberToPersist;
            }

            StateRoot = rootToCommit;
            StateRootToBlocks[StateRoot] = LatestCommittedBlockNumber = blockNumber;
            if (_logger.IsDebug)
                _logger.Debug(
                    $"Completed Flush: PersistedStateRoot:{PersistedStateRoot} LastPersistedBlockNumber:{LastPersistedBlockNumber} LatestCommittedBlockNumber:{LatestCommittedBlockNumber} StateRoot:{StateRoot} blockNumber:{blockNumber}");
        }

        AnnounceReorgBoundaries();
    }

    private static void PersistBlockChanges(InternalStoreInterface internalStore, LeafStoreInterface leafStore,
        VerkleKeyValueDb storage, out VerkleMemoryDb reverseDiff)
    {
        // we should not have any null values in the Batch db - because deletion of values from verkle tree is not allowed
        // nullable values are allowed in MemoryStateDb only for reverse diffs.
        reverseDiff = new VerkleMemoryDb();

        foreach (KeyValuePair<byte[], byte[]?> entry in leafStore)
        {
            // in stateless tree - anything can be null
            // Debug.Assert(entry.Value is not null, "nullable value only for reverse diff");
            if (storage.GetLeaf(entry.Key, out var node)) reverseDiff.LeafTable[entry.Key] = node;
            else reverseDiff.LeafTable[entry.Key] = null;

            storage.SetLeaf(entry.Key, entry.Value);
        }

        foreach (KeyValuePair<byte[], InternalNode?> entry in internalStore)
        {
            // in stateless tree - anything can be null
            // Debug.Assert(entry.Value is not null, "nullable value only for reverse diff");
            if (storage.GetInternalNode(entry.Key, out InternalNode? node)) reverseDiff.InternalTable[entry.Key] = node;
            else reverseDiff.InternalTable[entry.Key] = null;

            storage.SetInternalNode(entry.Key, entry.Value);
        }

        storage.LeafDb.Flush();
        storage.InternalNodeDb.Flush();
    }

    private static void PersistBlockChanges(InternalStoreInterface internalStore, LeafStoreInterface leafStore,
        VerkleKeyValueDb storage)
    {
        foreach (KeyValuePair<byte[], byte[]?> entry in leafStore)
            storage.SetLeaf(entry.Key, entry.Value);

        foreach (KeyValuePair<byte[], InternalNode?> entry in internalStore)
            storage.SetInternalNode(entry.Key, entry.Value);

        storage.LeafDb.Flush();
        storage.InternalNodeDb.Flush();
    }

    private void AnnounceReorgBoundaries()
    {
        if (LatestCommittedBlockNumber < 1) return;

        var shouldAnnounceReorgBoundary = false;
        var isFirstCommit = Interlocked.Exchange(ref _isFirst, 1) == 0;
        if (isFirstCommit)
        {
            if (_logger.IsDebug)
                _logger.Debug(
                    $"Reached first commit - newest {LatestCommittedBlockNumber}, last persisted {LastPersistedBlockNumber}");
            // this is important when transitioning from fast sync
            // imagine that we transition at block 1200000
            // and then we close the app at 1200010
            // in such case we would try to continue at Head - 1200010
            // because head is loaded if there is no persistence checkpoint
            // so we need to force the persistence checkpoint
            var baseBlock = Math.Max(0, LatestCommittedBlockNumber - 1);
            LastPersistedBlockNumber = baseBlock;
            shouldAnnounceReorgBoundary = true;
        }
        else if (!_lastPersistedReachedReorgBoundary)
        {
            // even after we persist a block we do not really remember it as a safe checkpoint
            // until max reorgs blocks after
            if (LatestCommittedBlockNumber >= LastPersistedBlockNumber + BlockCacheSize)
                shouldAnnounceReorgBoundary = true;
        }

        if (shouldAnnounceReorgBoundary)
        {
            ReorgBoundaryReached?.Invoke(this, new ReorgBoundaryReached(LastPersistedBlockNumber));
            _lastPersistedReachedReorgBoundary = true;
        }
    }
}