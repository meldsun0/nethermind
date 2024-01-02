// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree.Utils;

public class InsertBatchCompletedV1 : EventArgs
{
    public InsertBatchCompletedV1(long blockNumber, ReadOnlyVerkleMemoryDb forwardDiff, VerkleMemoryDb? reverseDiff)
    {
        BlockNumber = blockNumber;
        ReverseDiff = reverseDiff;
        ForwardDiff = forwardDiff;
    }

    public VerkleMemoryDb? ReverseDiff { get; }
    public ReadOnlyVerkleMemoryDb ForwardDiff { get; }
    public long BlockNumber { get; }
}

public class InsertBatchCompletedV2 : EventArgs
{
    public InsertBatchCompletedV2(long blockNumber, LeafStoreInterface leafTable)
    {
        BlockNumber = blockNumber;
        LeafTable = leafTable;
    }

    public LeafStoreInterface LeafTable { get; }
    public long BlockNumber { get; }
}