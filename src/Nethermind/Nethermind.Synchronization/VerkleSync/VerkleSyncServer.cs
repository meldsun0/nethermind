// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Nethermind.Core.Crypto;
using Nethermind.Core.Verkle;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Tree;
using Nethermind.Verkle.Tree.Sync;
using Nethermind.Verkle.Tree.TreeStore;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Synchronization.VerkleSync;

public class VerkleSyncServer(IVerkleTreeStore treeStore, ILogManager logManager)
{
    private readonly IVerkleTreeStore _store = treeStore ?? throw new ArgumentNullException(nameof(treeStore));
    private readonly ILogManager _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
    private readonly ILogger _logger = logManager.GetClassLogger();

    private const long HardResponseByteLimit = 2000000;
    private const int HardResponseNodeLimit = 10000;

    public (List<PathWithSubTree>, VerkleProof?) GetSubTreeRanges(Hash256 rootHash, Stem startingStem, Stem? limitStem, long byteLimit)
    {
        var watch = Stopwatch.StartNew();
        var nodes = _store.GetLeafRangeIterator(startingStem, limitStem ?? Stem.MaxValue, rootHash, byteLimit).ToList();
        watch.Stop();

        _logger.Info($"VerkleSyncServer - GetSubTreeRanges - RH:{rootHash} S:{startingStem} L:{limitStem} Bytes:{byteLimit}");
        _logger.Info($"VerkleSyncServer - GetSubTreeRanges - Count - {nodes.Count} time: {watch.Elapsed}");

        if (nodes.Count == 0) return (nodes, null);

        VerkleTree tree = new(_store, _logManager);

        watch = Stopwatch.StartNew();
        VerkleProof vProof = tree.CreateVerkleRangeProof(startingStem.Bytes, nodes[^1].Path.Bytes, out _, rootHash);
        watch.Stop();

        _logger.Info($"VerkleSyncServer - GetSubTreeRanges - Proof Generated time: {watch.Elapsed}");
        // TestIsGeneratedProofValid(vProof, rootPoint, startingStem, nodes.ToArray());
        return (nodes, vProof);
    }

    private void TestIsGeneratedProofValid(VerkleProof vProof, Banderwagon rootPoint, Stem startingStem, PathWithSubTree[] nodes)
    {
        VerkleTreeStore<PersistEveryBlock>? stateStore = new(new MemColumnsDb<VerkleDbColumns>(), new MemDb(), LimboLogs.Instance);
        VerkleTree localTree = new VerkleTree(stateStore, LimboLogs.Instance);
        var isCorrect = localTree.CreateStatelessTreeFromRange(vProof, rootPoint, startingStem, nodes[^1].Path, nodes);
        _logger.Info(!isCorrect
            ? $"GetSubTreeRanges: Generated proof is INVALID"
            : $"GetSubTreeRanges: Generated proof is VALID");
    }
}
