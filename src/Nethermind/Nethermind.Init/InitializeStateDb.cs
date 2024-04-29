// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Api;
using Nethermind.Blockchain;
using Nethermind.Blockchain.FullPruning;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Db;
using Nethermind.Db.FullPruning;
using Nethermind.Init.Steps;
using Nethermind.JsonRpc.Converters;
using Nethermind.Logging;
using Nethermind.Serialization.Json;
using Nethermind.State;
using Nethermind.State.Witnesses;
using Nethermind.Synchronization.Trie;
using Nethermind.Synchronization.Witness;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.TreeStore;
using IPersistenceStrategy = Nethermind.Trie.Pruning.IPersistenceStrategy;

namespace Nethermind.Init;

[RunnerStepDependencies(typeof(InitializePlugins), typeof(InitializeBlockTree), typeof(SetupKeyStore))]
public class InitializeStateDb : IStep
{
    private readonly INethermindApi _api;
    private ILogger _logger;

    public InitializeStateDb(INethermindApi api)
    {
        _api = api;
    }

    public Task Execute(CancellationToken cancellationToken)
    {
        InitBlockTraceDumper();

        (IApiWithStores getApi, IApiWithBlockchain setApi) = _api.ForBlockchain;

        if (getApi.ChainSpec is null) throw new StepDependencyException(nameof(getApi.ChainSpec));
        if (getApi.DbProvider is null) throw new StepDependencyException(nameof(getApi.DbProvider));
        if (getApi.SpecProvider is null) throw new StepDependencyException(nameof(getApi.SpecProvider));
        if (getApi.BlockTree is null) throw new StepDependencyException(nameof(getApi.BlockTree));

        _logger = getApi.LogManager.GetClassLogger();
        ISyncConfig syncConfig = getApi.Config<ISyncConfig>();
        IPruningConfig pruningConfig = getApi.Config<IPruningConfig>();
        IInitConfig initConfig = getApi.Config<IInitConfig>();

        _api.NodeStorageFactory.DetectCurrentKeySchemeFrom(getApi.DbProvider.StateDb);

        syncConfig.SnapServingEnabled |= syncConfig.SnapServingEnabled is null
            && _api.NodeStorageFactory.CurrentKeyScheme is INodeStorage.KeyScheme.HalfPath or null
            && initConfig.StateDbKeyScheme != INodeStorage.KeyScheme.Hash;

        if (_api.NodeStorageFactory.CurrentKeyScheme is INodeStorage.KeyScheme.Hash
            || initConfig.StateDbKeyScheme == INodeStorage.KeyScheme.Hash)
        {
            // Special case in case its using hashdb, use a slightly different database configuration.
            if (_api.DbProvider?.StateDb is ITunableDb tunableDb) tunableDb.Tune(ITunableDb.TuneType.HashDb);
        }

        if (syncConfig.SnapServingEnabled == true && pruningConfig.PruningBoundary < 128)
        {
            if (_logger.IsWarn) _logger.Warn($"Snap serving enabled, but {nameof(pruningConfig.PruningBoundary)} is less than 128. Setting to 128.");
            pruningConfig.PruningBoundary = 128;
        }

        if (pruningConfig.PruningBoundary < 64)
        {
            if (_logger.IsWarn) _logger.Warn($"Prunig boundary must be at least 64. Setting to 64.");
            pruningConfig.PruningBoundary = 64;
        }

        if (syncConfig.DownloadReceiptsInFastSync && !syncConfig.DownloadBodiesInFastSync)
        {
            if (_logger.IsWarn) _logger.Warn($"{nameof(syncConfig.DownloadReceiptsInFastSync)} is selected but {nameof(syncConfig.DownloadBodiesInFastSync)} - enabling bodies to support receipts download.");
            syncConfig.DownloadBodiesInFastSync = true;
        }

        IWitnessCollector witnessCollector;
        if (syncConfig.WitnessProtocolEnabled)
        {
            WitnessCollector witnessCollectorImpl = new(getApi.DbProvider.WitnessDb, _api.LogManager);
            witnessCollector = setApi.WitnessCollector = witnessCollectorImpl;
            setApi.WitnessRepository = witnessCollectorImpl.WithPruning(getApi.BlockTree!, getApi.LogManager);
        }
        else
        {
            witnessCollector = setApi.WitnessCollector = NullWitnessCollector.Instance;
            setApi.WitnessRepository = NullWitnessCollector.Instance;
        }

        IKeyValueStore codeDb = getApi.DbProvider.CodeDb
            .WitnessedBy(witnessCollector);
        IKeyValueStoreWithBatching stateWitnessedBy = getApi.DbProvider.StateDb.WitnessedBy(witnessCollector);
        IPersistenceStrategy persistenceStrategy;
        IPruningStrategy pruningStrategy;
        if (pruningConfig.Mode.IsMemory())
        {
            persistenceStrategy = Persist.IfBlockOlderThan(pruningConfig.PersistenceInterval); // TODO: this should be based on time
            if (pruningConfig.Mode.IsFull())
            {
                PruningTriggerPersistenceStrategy triggerPersistenceStrategy = new((IFullPruningDb)getApi.DbProvider!.StateDb, getApi.BlockTree!, getApi.LogManager);
                getApi.DisposeStack.Push(triggerPersistenceStrategy);
                persistenceStrategy = persistenceStrategy.Or(triggerPersistenceStrategy);
            }

            pruningStrategy = Prune
                .WhenCacheReaches(pruningConfig.CacheMb.MB())
                // Use of ratio, as the effectiveness highly correlate with the amount of keys per snapshot save which
                // depends on CacheMb. 0.05 is the minimum where it can keep track the whole snapshot.. most of the time.
                .TrackingPastKeys((int)(pruningConfig.CacheMb.MB() * pruningConfig.TrackedPastKeyCountMemoryRatio / 48))
                .KeepingLastNState(pruningConfig.PruningBoundary);
        }
        else
        {
            pruningStrategy = No.Pruning;
            persistenceStrategy = Persist.EveryBlock;
        }

        INodeStorage mainNodeStorage = _api.NodeStorageFactory.WrapKeyValueStore(stateWitnessedBy);
        IWorldState worldState;
        IWorldStateManager stateManager;
        TrieStore? trieStore = null;
        if (!getApi.SpecProvider.GenesisSpec.IsVerkleTreeEipEnabled)
        {
            trieStore = syncConfig.TrieHealing
                ? new HealingTrieStore(
                    mainNodeStorage,
                    pruningStrategy,
                    persistenceStrategy,
                    getApi.LogManager)
                : new TrieStore(
                    mainNodeStorage,
                    pruningStrategy,
                    persistenceStrategy,
                    getApi.LogManager);

            // TODO: Needed by node serving. Probably should use `StateReader` instead.
            setApi.TrieStore = trieStore;

            worldState = syncConfig.TrieHealing
                ? new HealingWorldState(
                    trieStore,
                    codeDb,
                    getApi.LogManager)
                : new WorldState(
                    trieStore,
                    codeDb,
                    getApi.LogManager);

            stateManager = setApi.WorldStateManager = new WorldStateManager(
                worldState,
                trieStore,
                getApi.DbProvider,
                getApi.LogManager);
            getApi.DisposeStack.Push(trieStore);
        }
        else
        {
            if (initConfig.StatelessProcessingEnabled)
            {
                IVerkleTreeStore verkleTreeStore;
                setApi.VerkleTreeStore = verkleTreeStore = new NullVerkleTreeStore();
                worldState = new VerkleWorldState(new VerkleStateTree(verkleTreeStore, getApi.LogManager), codeDb, getApi.LogManager);
                stateManager = setApi.WorldStateManager = new VerkleWorldStateManager(
                    worldState,
                    verkleTreeStore,
                    getApi.DbProvider,
                    getApi.LogManager);
            }
            else
            {
                VerkleTreeStore<VerkleSyncCache> verkleTreeStore;
                setApi.VerkleTreeStore = verkleTreeStore = new VerkleTreeStore<VerkleSyncCache>(getApi.DbProvider, getApi.LogManager);
                setApi.VerkleArchiveStore = new(verkleTreeStore, getApi.DbProvider, getApi.LogManager);
                worldState = new VerkleWorldState(new VerkleStateTree(verkleTreeStore, getApi.LogManager), codeDb, getApi.LogManager);
                stateManager = setApi.WorldStateManager = new VerkleWorldStateManager(
                    worldState,
                    verkleTreeStore,
                    getApi.DbProvider,
                    getApi.LogManager);
            }
        }

        // TODO: Don't forget this
        TrieStoreBoundaryWatcher trieStoreBoundaryWatcher = new(stateManager, _api.BlockTree!, _api.LogManager);
        getApi.DisposeStack.Push(trieStoreBoundaryWatcher);

        stateManager.GlobalChainHeadProvider = new ChainHeadReadOnlyStateProvider(getApi.BlockTree, stateManager.GlobalStateReader);

        worldState.StateRoot = getApi.BlockTree!.Head?.StateRoot ?? Keccak.EmptyTreeHash;

        if (_api.Config<IInitConfig>().DiagnosticMode == DiagnosticMode.VerifyTrie)
        {
            Task.Run(() =>
            {
                try
                {
                    _logger!.Info("Collecting trie stats and verifying that no nodes are missing...");
                    Hash256 stateRoot = getApi.BlockTree!.Head?.StateRoot ?? Keccak.EmptyTreeHash;
                    TrieStats stats = stateManager.GlobalStateReader.CollectStats(stateRoot, getApi.DbProvider.CodeDb, _api.LogManager);
                    _logger.Info($"Starting from {getApi.BlockTree.Head?.Number} {getApi.BlockTree.Head?.StateRoot}{Environment.NewLine}" + stats);
                }
                catch (Exception ex)
                {
                    _logger!.Error(ex.ToString());
                }
            });
        }

        // Init state if we need system calls before actual processing starts
        if (getApi.BlockTree!.Head?.StateRoot is not null)
        {
            worldState.StateRoot = getApi.BlockTree.Head.StateRoot;
        }

        if (!getApi.SpecProvider.GenesisSpec.IsVerkleTreeEipEnabled) InitializeFullPruning(pruningConfig, initConfig, _api, stateManager.GlobalStateReader, mainNodeStorage, trieStore!);

        return Task.CompletedTask;
    }

    private static void InitBlockTraceDumper()
    {
        EthereumJsonSerializer.AddConverter(new TxReceiptConverter());
    }

    private static void InitializeFullPruning(
        IPruningConfig pruningConfig,
        IInitConfig initConfig,
        INethermindApi api,
        IStateReader stateReader,
        INodeStorage mainNodeStorage,
        IPruningTrieStore trieStore)
    {
        IPruningTrigger? CreateAutomaticTrigger(string dbPath)
        {
            long threshold = pruningConfig.FullPruningThresholdMb.MB();

            switch (pruningConfig.FullPruningTrigger)
            {
                case FullPruningTrigger.StateDbSize:
                    return new PathSizePruningTrigger(dbPath, threshold, api.TimerFactory, api.FileSystem);
                case FullPruningTrigger.VolumeFreeSpace:
                    return new DiskFreeSpacePruningTrigger(dbPath, threshold, api.TimerFactory, api.FileSystem);
                default:
                    return null;
            }
        }

        if (pruningConfig.Mode.IsFull())
        {
            IDb stateDb = api.DbProvider!.StateDb;
            if (stateDb is IFullPruningDb fullPruningDb)
            {
                string pruningDbPath = fullPruningDb.GetPath(initConfig.BaseDbPath);
                IPruningTrigger? pruningTrigger = CreateAutomaticTrigger(pruningDbPath);
                if (pruningTrigger is not null)
                {
                    api.PruningTrigger.Add(pruningTrigger);
                }

                IDriveInfo? drive = api.FileSystem.GetDriveInfos(pruningDbPath).FirstOrDefault();
                FullPruner pruner = new(
                    fullPruningDb,
                    api.NodeStorageFactory,
                    mainNodeStorage,
                    api.PruningTrigger,
                    pruningConfig,
                    api.BlockTree!,
                    stateReader,
                    api.ProcessExit!,
                    ChainSizes.CreateChainSizeInfo(api.ChainSpec.ChainId),
                    drive,
                    trieStore,
                    api.LogManager);
                api.DisposeStack.Push(pruner);
            }
        }
    }
}
