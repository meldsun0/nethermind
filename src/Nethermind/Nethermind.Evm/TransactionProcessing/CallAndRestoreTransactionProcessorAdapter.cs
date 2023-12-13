// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only
//
using Nethermind.Core;
using Nethermind.Evm.Tracing;
using Nethermind.State;

namespace Nethermind.Evm.TransactionProcessing
{
    public class CallAndRestoreTransactionProcessorAdapter : ITransactionProcessorAdapter
    {
        private readonly ITransactionProcessor _transactionProcessor;

        public CallAndRestoreTransactionProcessorAdapter(ITransactionProcessor transactionProcessor)
        {
            _transactionProcessor = transactionProcessor;
        }

        public void Execute(Transaction transaction, BlockExecutionContext blkCtx, ITxTracer txTracer) =>
            _transactionProcessor.CallAndRestore(transaction, blkCtx, txTracer);

        public ITransactionProcessorAdapter WithNewStateProvider(IWorldState worldState)
        {
            return new CallAndRestoreTransactionProcessorAdapter(_transactionProcessor.WithNewStateProvider(worldState));
        }
    }
}
