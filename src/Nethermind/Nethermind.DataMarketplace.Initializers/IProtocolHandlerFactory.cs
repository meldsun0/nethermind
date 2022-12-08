// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Network.P2P;
using Nethermind.Network.P2P.ProtocolHandlers;

namespace Nethermind.DataMarketplace.Initializers
{
    public interface IProtocolHandlerFactory
    {
        IProtocolHandler Create(ISession session);
    }
}
