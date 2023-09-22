// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using Ethereum.Test.Base;
using NUnit.Framework;

namespace Ethereum.Blockchain.Test;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class EOFTests : GeneralStateTestBase
{
    // Uncomment when EOF tests are merged

    // [TestCaseSource(nameof(LoadTests))]
    // public void Test(GeneralStateTest test)
    // {
    //     Assert.True(RunTest(test).Pass);
    // }

    public static IEnumerable<GeneralStateTest> LoadTests()
    {
        var loader = new TestsSourceLoader(new LoadEipTestsStrategy(), "stEOF");
        return (IEnumerable<GeneralStateTest>)loader.LoadTests();
    }
}
