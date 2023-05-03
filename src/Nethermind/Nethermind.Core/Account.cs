// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Core
{
    public class Account
    {
        public static Account TotallyEmpty = new();

        private readonly Keccak? _codeHash;
        private readonly Keccak? _storageRoot;

        public Account(UInt256 balance)
        {
            Balance = balance;
            Nonce = default;
            _codeHash = null;
            _storageRoot = null;
        }

        private Account()
        {
            Balance = default;
            Nonce = default;
            _codeHash = null;
            _storageRoot = null;
        }

        public Account(in UInt256 nonce, in UInt256 balance, Keccak storageRoot, Keccak codeHash)
        {
            Nonce = nonce;
            Balance = balance;
            _storageRoot = storageRoot == Keccak.EmptyTreeHash ? null : storageRoot;
            _codeHash = codeHash == Keccak.OfAnEmptyString ? null : codeHash;
        }

        private Account(Account account, Keccak? storageRoot)
        {
            Nonce = account.Nonce;
            Balance = account.Balance;
            _storageRoot = storageRoot == Keccak.EmptyTreeHash ? null : storageRoot;
            _codeHash = account._codeHash;
        }

        private Account(Keccak? codeHash, Account account)
        {
            Nonce = account.Nonce;
            Balance = account.Balance;
            _storageRoot = account._storageRoot;
            _codeHash = codeHash == Keccak.OfAnEmptyString ? null : codeHash;
        }

        private Account(Account account, in UInt256 nonce, in UInt256 balance)
        {
            Nonce = nonce;
            Balance = balance;
            _storageRoot = account._storageRoot;
            _codeHash = account._codeHash;
        }

        public bool HasCode => _codeHash is not null;

        public bool HasStorage => _storageRoot is not null;

        public UInt256 Nonce { get; }
        public UInt256 Balance { get; }
        public Keccak StorageRoot => _storageRoot ?? Keccak.EmptyTreeHash;
        public Keccak CodeHash => _codeHash ?? Keccak.OfAnEmptyString;
        public bool IsTotallyEmpty => _storageRoot is null && IsEmpty;
        public bool IsEmpty => _codeHash is null && Balance.IsZero && Nonce.IsZero;
        public bool IsContract => _codeHash is not null;

        public Account WithChangedBalance(in UInt256 newBalance)
        {
            return new(this, Nonce, newBalance);
        }

        public Account WithChangedNonce(in UInt256 newNonce)
        {
            return new(this, newNonce, Balance);
        }

        public Account WithChangedStorageRoot(Keccak newStorageRoot)
        {
            return new(this, newStorageRoot);
        }

        public Account WithChangedCodeHash(Keccak newCodeHash)
        {
            return new(newCodeHash, this);
        }
    }
}
