﻿/*
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

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Encoding;
using Nethermind.Dirichlet.Numerics;
using Nethermind.Evm.Tracing;

namespace Nethermind.Blockchain
{
    public interface ITracer
    {
        GethLikeTxTrace Trace(Keccak txHash);
        GethLikeTxTrace Trace(long blockNumber, Transaction transaction);
        GethLikeTxTrace Trace(long blockNumber, int txIndex);
        GethLikeTxTrace Trace(Keccak blockHash, int txIndex);
        GethLikeTxTrace[] TraceBlock(Keccak blockHash);
        GethLikeTxTrace[] TraceBlock(long blockNumber);
        
        GethLikeTxTrace[] TraceBlock(Rlp blockRlp);
        ParityLikeTxTrace ParityTrace(Keccak txHash, ParityTraceTypes parityTraceTypes);
        ParityLikeTxTrace[] ParityTraceBlock(Keccak blockHash, ParityTraceTypes parityTraceTypes);
        ParityLikeTxTrace[] ParityTraceBlock(long blockNumber, ParityTraceTypes parityTraceTypes);
    }
}