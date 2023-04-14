// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Threading;
using Nethermind.Core.Specs;

namespace Nethermind.Specs.Forks
{
    public class Cancun : Shanghai
    {
        private static IReleaseSpec _instance;

        protected Cancun()
        {
            Name = "Cancun";
            IsEip1153Enabled = true;
            IsEip4844Enabled = true;

            IsEip3540Enabled= true;
            IsEip3670Enabled = true;
            IsEip4200Enabled = true;
            IsEip4750Enabled = true;
        }

        public new static IReleaseSpec Instance => LazyInitializer.EnsureInitialized(ref _instance, () => new Cancun());
    }
}
