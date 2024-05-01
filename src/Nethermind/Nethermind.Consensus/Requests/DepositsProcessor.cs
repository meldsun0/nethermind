// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using Nethermind.Core;
using Nethermind.Core.ConsensusRequests;
using Nethermind.Core.Specs;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Consensus.Requests;

public class DepositsProcessor : IDepositsProcessor
{
    public List<Deposit>? ProcessDeposits(Block block, TxReceipt[] receipts, IReleaseSpec spec)
    {
        if (!spec.DepositsEnabled)
            return null;

        List<Deposit> depositList = [];

        for (int i = 0; i < block.Transactions.Length; i++)
        {
            LogEntry[]? logEntries = receipts[i].Logs;
            if (logEntries != null)
                foreach (LogEntry? log in logEntries)
                {
                    if (log != null && log.LoggersAddress == spec.DepositContractAddress)
                    {
                        var depositDecoder = new DepositDecoder();
                        Deposit? deposit = depositDecoder.Decode(new RlpStream(log.Data));
                        depositList.Add(deposit);
                    }
                }
        }

        return depositList;
    }
}