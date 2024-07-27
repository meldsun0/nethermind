// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;

namespace Nethermind.Network.Discovery.Kademlia;

/// Note, a tree based kademlia will likely change this significantly.
public interface INodeHashProvider<in TNode, in TContentKey>
{
    ValueHash256 GetHash(TNode node);
    ValueHash256 GetHash(TContentKey key);
}
