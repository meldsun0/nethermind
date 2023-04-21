// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Linq;
using Antlr4.Runtime.Misc;
using Nethermind.Consensus.Validators;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Logging;
using Nethermind.Merge.Plugin.InvalidChainTracker;
using Nethermind.Specs;
using NSubstitute;
using NUnit.Framework;

namespace Nethermind.Merge.Plugin.Test;

public class InvalidBlockInterceptorTest
{
    private IBlockValidator _baseValidator;
    private IInvalidChainTracker _tracker;
    private InvalidBlockInterceptor _invalidBlockInterceptor;

    [SetUp]
    public void Setup()
    {
        _baseValidator = Substitute.For<IBlockValidator>();
        _tracker = Substitute.For<IInvalidChainTracker>();
        _invalidBlockInterceptor = new(
            _baseValidator,
            _tracker,
            NullLogManager.Instance);
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public void TestValidateSuggestedBlock(bool baseReturnValue, bool isInvalidBlockReported)
    {
        Block block = Build.A.Block.TestObject;
        _baseValidator.ValidateSuggestedBlock(block, Arg.Any<BlockHeader>()).Returns(baseReturnValue);
        _invalidBlockInterceptor.ValidateSuggestedBlock(block, Build.An.EmptyBlockHeader);

        _tracker.Received().SetChildParent(block.Hash, block.ParentHash);
        if (isInvalidBlockReported)
        {
            _tracker.Received().OnInvalidBlock(block.Hash, block.ParentHash);
        }
        else
        {
            _tracker.DidNotReceive().OnInvalidBlock(block.Hash, block.ParentHash);
        }
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public void TestValidateProcessedBlock(bool baseReturnValue, bool isInvalidBlockReported)
    {
        Block block = Build.A.Block.TestObject;
        Block suggestedBlock = Build.A.Block.WithExtraData(new byte[] { 1 }).TestObject;
        TxReceipt[] txs = { };
        _baseValidator.ValidateProcessedBlock(block, txs, suggestedBlock).Returns(baseReturnValue);
        _invalidBlockInterceptor.ValidateProcessedBlock(block, txs, suggestedBlock);

        _tracker.Received().SetChildParent(suggestedBlock.Hash, suggestedBlock.ParentHash);
        if (isInvalidBlockReported)
        {
            _tracker.Received().OnInvalidBlock(suggestedBlock.Hash, suggestedBlock.ParentHash);
        }
        else
        {
            _tracker.DidNotReceive().OnInvalidBlock(suggestedBlock.Hash, suggestedBlock.ParentHash);
        }
    }

    [Test]
    public void TestInvalidBlockhashShouldNotGetTracked()
    {
        Block block = Build.A.Block.TestObject;
        block.Header.StateRoot = Keccak.Zero;

        _baseValidator.ValidateSuggestedBlock(block, Build.An.EmptyBlockHeader).Returns(false);
        _invalidBlockInterceptor.ValidateSuggestedBlock(block, Build.An.EmptyBlockHeader);

        _tracker.DidNotReceive().SetChildParent(block.Hash, block.ParentHash);
        _tracker.DidNotReceive().OnInvalidBlock(block.Hash, block.ParentHash);
    }

    [Test]
    public void TestBlockWithNotMatchingTxShouldNotGetTracked()
    {
        Block block = Build.A.Block
            .WithTransactions(10, MainnetSpecProvider.Instance)
            .TestObject;

        block = new Block(block.Header, block.Body.WithChangedTransactions(
            block.Transactions.Take(9).ToArray()
        ));

        _baseValidator.ValidateSuggestedBlock(block, Build.An.EmptyBlockHeader).Returns(false);
        _invalidBlockInterceptor.ValidateSuggestedBlock(block, Build.An.EmptyBlockHeader);

        _tracker.DidNotReceive().SetChildParent(block.Hash, block.ParentHash);
        _tracker.DidNotReceive().OnInvalidBlock(block.Hash, block.ParentHash);
    }

    [Test]
    public void TestBlockWithIncorrectWithdrawalsShouldNotGetTracked()
    {
        Block block = Build.A.Block
            .WithWithdrawals(10)
            .TestObject;

        block = new Block(block.Header, block.Body.WithChangedWithdrawals(
            block.Withdrawals.Take(8).ToArray()
        ));

        _baseValidator.ValidateSuggestedBlock(block, Build.An.EmptyBlockHeader).Returns(false);
        _invalidBlockInterceptor.ValidateSuggestedBlock(block, Build.An.EmptyBlockHeader);

        _tracker.DidNotReceive().SetChildParent(block.Hash, block.ParentHash);
        _tracker.DidNotReceive().OnInvalidBlock(block.Hash, block.ParentHash);
    }

}
