/*
 * Copyright (c) 2018 Demerzel Solutions Limited
 * This file is part of the Nethermind library.
 *
 * The Nethermind library is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * The Nethermind library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Core;
using Nethermind.Core.Logging;
using Nethermind.Mining;

namespace Nethermind.Clique
{
    public class CliqueSealEngine : ISealEngine
    {
        private readonly Clique _clique;
        private readonly ILogger _logger;

        public CliqueSealEngine(Clique clique, ILogManager logManager)
        {
            _logger = logManager?.GetClassLogger() ?? throw new ArgumentNullException(nameof(logManager));
            _clique = clique;
        }

        public BigInteger MinGasPrice { get; set; } = 0;

        public async Task<Block> MineAsync(Block processed, CancellationToken cancellationToken)
        {
            Block block = await MineAsync(cancellationToken, processed, null).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger.Error($"{nameof(MineAsync)} failed", t.Exception);
                    return null;
                }

                return t.Result;
            }, cancellationToken);

            if (block == null)
            {
                throw new SealEngineException($"{nameof(MineAsync)} failed");
            }

            return block;
        }

        public bool ValidateParams(Block parent, BlockHeader header)
        {
            return _clique.VerifyHeader(header);
        }

        public bool ValidateSeal(BlockHeader header)
        {
            return _clique.VerifySeal(header);
        }

        public bool IsMining { get; set; }

        private async Task<Block> MineAsync(CancellationToken cancellationToken, Block processed, ulong? startNonce)
        {
            if (processed.Header.TransactionsRoot == null ||
                processed.Header.StateRoot == null ||
                processed.Header.ReceiptsRoot == null ||
                processed.Header.OmmersHash == null ||
                processed.Header.Bloom == null ||
                processed.Header.ExtraData == null)
            {
                throw new InvalidOperationException($"Requested to mine an invalid block {processed.Header}");
            }

            Task<Block> miningTask = Task.Factory.StartNew(() => Mine(processed), cancellationToken);
            await miningTask.ContinueWith(
                t =>
                {
                    if (t.IsCompleted)
                    {
                        t.Result.Header.Hash = BlockHeader.CalculateHash(t.Result.Header);
                    }
                }, cancellationToken, TaskContinuationOptions.NotOnFaulted, TaskScheduler.Default);

            return await miningTask;
        }

        private Block Mine(Block block)
        {
            return _clique.Mine(block);
        }
    }
}