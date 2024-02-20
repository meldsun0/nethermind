// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Core;
using Nethermind.Core.Collections;
using Nethermind.Core.Crypto;

namespace Nethermind.Trie
{
    public interface ITreeVisitor
    {
        /// <summary>
        /// Specify that this is a full table scan and should optimize for it.
        /// </summary>
        public bool IsFullDbScan { get; }

        ReadFlags ExtraReadFlag => ReadFlags.None;

        bool ShouldVisit(Hash256 nodeHash);

        void VisitTree(Hash256 rootHash, TrieVisitContext trieVisitContext);

        void VisitMissingNode(Hash256 nodeHash, TrieVisitContext trieVisitContext);

        void VisitBranch(TrieNode node, TrieVisitContext trieVisitContext);

        void VisitExtension(TrieNode node, TrieVisitContext trieVisitContext);

        void VisitLeaf(TrieNode node, TrieVisitContext trieVisitContext, ReadOnlySpan<byte> value);

        void VisitCode(Hash256 codeHash, TrieVisitContext trieVisitContext);
    }

    public interface ITreeVisitor<TNodeContext>
        where TNodeContext : struct, INodeContext<TNodeContext>
    {
        /// <summary>
        /// Specify that this is a full table scan and should optimize for it.
        /// </summary>
        public bool IsFullDbScan { get; }

        ReadFlags ExtraReadFlag => ReadFlags.None;

        bool ShouldVisit(in TNodeContext nodeContext, Hash256 nextNode);

        void VisitTree(in TNodeContext nodeContext, Hash256 rootHash, TrieVisitContext trieVisitContext);

        void VisitMissingNode(in TNodeContext nodeContext, Hash256 nodeHash, TrieVisitContext trieVisitContext);

        void VisitBranch(in TNodeContext nodeContext, TrieNode node, TrieVisitContext trieVisitContext);

        void VisitExtension(in TNodeContext nodeContext, TrieNode node, TrieVisitContext trieVisitContext);

        void VisitLeaf(in TNodeContext nodeContext, TrieNode node, TrieVisitContext trieVisitContext,
            ReadOnlySpan<byte> value);

        void VisitCode(in TNodeContext nodeContext, Hash256 codeHash, TrieVisitContext trieVisitContext);
    }

    public class ContextNotAwareTreeVisitor : ITreeVisitor<EmptyContext>
    {
        private readonly ITreeVisitor _wrapped;

        public ContextNotAwareTreeVisitor(ITreeVisitor wrapped)
        {
            _wrapped = wrapped;
        }

        public bool IsFullDbScan => _wrapped.IsFullDbScan;

        public ReadFlags ExtraReadFlag => _wrapped.ExtraReadFlag;

        public bool ShouldVisit(in EmptyContext nodeContext, Hash256 nextNode) => _wrapped.ShouldVisit(nextNode);

        public void VisitTree(in EmptyContext nodeContext, Hash256 rootHash, TrieVisitContext trieVisitContext)
        {
            _wrapped.VisitTree(rootHash, trieVisitContext);
        }

        public void VisitMissingNode(in EmptyContext nodeContext, Hash256 nodeHash, TrieVisitContext trieVisitContext)
        {
            _wrapped.VisitMissingNode(nodeHash, trieVisitContext);
        }

        public void VisitBranch(in EmptyContext nodeContext, TrieNode node, TrieVisitContext trieVisitContext)
        {
            _wrapped.VisitBranch(node, trieVisitContext);
        }

        public void VisitExtension(in EmptyContext nodeContext, TrieNode node, TrieVisitContext trieVisitContext)
        {
            _wrapped.VisitExtension(node, trieVisitContext);
        }

        public void VisitLeaf(in EmptyContext nodeContext, TrieNode node, TrieVisitContext trieVisitContext,
            ReadOnlySpan<byte> value)
        {
            _wrapped.VisitLeaf(node, trieVisitContext, value);
        }

        public void VisitCode(in EmptyContext nodeContext, Hash256 codeHash, TrieVisitContext trieVisitContext)
        {
            _wrapped.VisitCode(codeHash, trieVisitContext);
        }
    }

    public interface INodeContext<out TNodeContext>
        // The context needs to be the struct so that it's passed nicely via in and returned from the methods.
        where TNodeContext : struct, INodeContext<TNodeContext>
    {
        TNodeContext Add(ReadOnlySpan<byte> nibblePath);

        TNodeContext Add(byte nibble);
    }

    public readonly struct EmptyContext : INodeContext<EmptyContext>
    {
        public EmptyContext Add(ReadOnlySpan<byte> nibblePath) => this;
        public EmptyContext Add(byte nibble) => this;
    }

    public struct TreePathContext : INodeContext<TreePathContext>
    {
        public TreePath Path = TreePath.Empty;

        public TreePathContext()
        {
        }

        public TreePathContext Add(ReadOnlySpan<byte> nibblePath)
        {
            return new TreePathContext()
            {
                Path = Path.Append(nibblePath)
            };
        }

        public TreePathContext Add(byte nibble)
        {
            return new TreePathContext()
            {
                Path = Path.Append(nibble)
            };
        }
    }

    public struct ConventionalContext(int level, int? branchChildIndex) : INodeContext<ConventionalContext>
    {
        public int Level { get; } = level;
        public int? BranchChildIndex { get; } = branchChildIndex;

        public ConventionalContext Add(ReadOnlySpan<byte> nibblePath)
        {
            return new ConventionalContext(Level + 1, null);
        }

        public ConventionalContext Add(byte nibble)
        {
            return new ConventionalContext(Level + 1, nibble);
        }
    }

}
