// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Synchronization.VerkleSync
{
    public enum AddRangeResult
    {
        OK,
        MissingRootHashInProofs,
        DifferentRootHash,
        ExpiredRootHash
    }
}