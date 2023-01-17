// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Diagnostics;
using System.Xml.XPath;

namespace Nethermind.Trie
{
    internal static class TrieNodeFactory
    {
        public static TrieNode CreateBranch()
        {
            TrieNode node = new(NodeType.Branch);
            return node;
        }

        public static TrieNode CreateBranch(Span<byte> pathToNode)
        {
            TrieNode node = new(NodeType.Branch);
            node.PathToNode = pathToNode.ToArray();
            return node;
        }

        public static TrieNode CreateLeaf(HexPrefix key, byte[]? value)
        {
            Debug.Assert(
                key.IsLeaf,
                $"{nameof(NodeType.Leaf)} should always be created with a leaf {nameof(HexPrefix)}");

            TrieNode node = new(NodeType.Leaf);
            node.Key = key;
            node.Value = value;
            return node;
        }

        public static TrieNode CreateLeaf(HexPrefix key, byte[]? value, Span<byte> pathToNode)
        {
            Debug.Assert(
                key.IsLeaf,
                $"{nameof(NodeType.Leaf)} should always be created with a leaf {nameof(HexPrefix)}");

            TrieNode node = new(NodeType.Leaf);
            node.Key = key;
            node.Value = value;
            node.PathToNode = pathToNode.ToArray();
            if (node.Path.Length + node.PathToNode.Length != 64)
                throw new Exception("what?");
            return node;
        }

        public static TrieNode CreateExtension(HexPrefix key)
        {
            TrieNode node = new(NodeType.Extension);
            node.Key = key;
            return node;
        }

        public static TrieNode CreateExtension(HexPrefix key, Span<byte> pathToNode)
        {
            TrieNode node = new(NodeType.Extension);
            node.Key = key;
            node.PathToNode = pathToNode.ToArray();
            return node;
        }

        public static TrieNode CreateExtension(HexPrefix key, TrieNode child)
        {
            Debug.Assert(
                key.IsExtension,
                $"{nameof(NodeType.Extension)} should always be created with an extension {nameof(HexPrefix)}");

            TrieNode node = new(NodeType.Extension);
            node.SetChild(0, child);
            node.Key = key;
            return node;
        }

        public static TrieNode CreateExtension(HexPrefix key, TrieNode child, Span<byte> pathToNode)
        {
            Debug.Assert(
                key.IsExtension,
                $"{nameof(NodeType.Extension)} should always be created with an extension {nameof(HexPrefix)}");

            TrieNode node = new(NodeType.Extension);
            node.SetChild(0, child);
            node.Key = key;
            node.PathToNode = pathToNode.ToArray();
            return node;
        }
    }
}
