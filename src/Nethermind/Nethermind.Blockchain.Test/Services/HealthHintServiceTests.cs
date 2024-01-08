// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using Nethermind.Blockchain.Services;
using Nethermind.Core;
using Nethermind.Specs.ChainSpecStyle;
using NUnit.Framework;

namespace Nethermind.Blockchain.Test.Services
{
    public class HealthHintServiceTests
    {
        [Test, Timeout(Timeout.MaxTestTime)]
        public void GetBlockProcessorAndProducerIntervalHint_returns_expected_result(
            [ValueSource(nameof(BlockProcessorIntervalHintTestCases))]
            BlockProcessorIntervalHint test)
        {
            IHealthHintService healthHintService = new HealthHintService(test.ChainSpec);
            ulong? actualProcessing = healthHintService.MaxSecondsIntervalForProcessingBlocksHint();
            ulong? actualProducing = healthHintService.MaxSecondsIntervalForProducingBlocksHint();
            Assert.That(actualProcessing, Is.EqualTo(test.ExpectedProcessingHint));
            Assert.That(actualProducing, Is.EqualTo(test.ExpectedProducingHint));
        }

        public class BlockProcessorIntervalHint
        {
            public required ChainSpec ChainSpec { get; init; }
            public ulong? ExpectedProcessingHint { get; init; }
            public ulong? ExpectedProducingHint { get => null; }

            public override string ToString() =>
                $"ExpectedProcessingHint: {ExpectedProcessingHint}, ExpectedProducingHint: {ExpectedProducingHint}";
        }

        public static IEnumerable<BlockProcessorIntervalHint> BlockProcessorIntervalHintTestCases
        {
            get
            {
                yield return new BlockProcessorIntervalHint
                {
                    ChainSpec = new ChainSpec { }
                };
                yield return new BlockProcessorIntervalHint
                {
                    ChainSpec = new ChainSpec {  },
                    ExpectedProcessingHint = 180
                };
                yield return new BlockProcessorIntervalHint
                {
                    ChainSpec = new ChainSpec {  }
                };
                yield return new BlockProcessorIntervalHint
                {
                    ChainSpec = new ChainSpec { }
                };
            }
        }
    }
}
