// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Db;
using Nethermind.Db.Rocks;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Verkle.Tree.Sync;
using Nethermind.Verkle.Tree.Utils;

namespace Nethermind.Verkle.Tree.Test;

public class TestSyncRangesInAHugeVerkleTree
{
    public static Random Random { get; } = new();
    public static int numKeys = 2000;
    private static string GetDbPathForTest()
    {
        string tempDir = Path.GetTempPath();
        string dbname = "VerkleTrie_TestID_" + TestContext.CurrentContext.Test.ID;
        return Path.Combine(tempDir, dbname);
    }

    private static IVerkleStore GetVerkleStoreForTest(DbMode dbMode)
    {
        IDbProvider provider;
        switch (dbMode)
        {
            case DbMode.MemDb:
                provider = VerkleDbFactory.InitDatabase(dbMode, null);
                break;
            case DbMode.PersistantDb:
                provider = VerkleDbFactory.InitDatabase(dbMode, GetDbPathForTest());
                break;
            case DbMode.ReadOnlyDb:
            default:
                throw new ArgumentOutOfRangeException(nameof(dbMode), dbMode, null);
        }

        return new VerkleStateStore(provider, LimboLogs.Instance);
    }

    private static VerkleTree GetVerkleTreeForTest(DbMode dbMode)
    {
        IDbProvider provider;
        switch (dbMode)
        {
            case DbMode.MemDb:
                provider = VerkleDbFactory.InitDatabase(dbMode, null);
                return new VerkleTree(provider, LimboLogs.Instance);
            case DbMode.PersistantDb:
                provider = VerkleDbFactory.InitDatabase(dbMode, GetDbPathForTest());
                return new VerkleTree(provider, LimboLogs.Instance);
            case DbMode.ReadOnlyDb:
            default:
                throw new ArgumentOutOfRangeException(nameof(dbMode), dbMode, null);
        }
    }

    [TearDown]
    public void CleanTestData()
    {
        string dbPath = GetDbPathForTest();
        if (Directory.Exists(dbPath))
        {
            Directory.Delete(dbPath, true);
        }
    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void GetSyncRangeForBigVerkleTree(DbMode dbMode)
    {
        const int pathPoolCount = 100_000;
        const int numBlocks = 200;
        const int leafPerBlock = 10;
        const int blockToGetIteratorFrom = 180;

        IVerkleStore store = TestItem.GetVerkleStore(dbMode);
        VerkleTree tree = new(store, LimboLogs.Instance);

        Pedersen[] pathPool = new Pedersen[pathPoolCount];
        SortedDictionary<Pedersen, byte[]> leafs = new();
        SortedDictionary<Pedersen, byte[]> leafsForSync = new();

        for (int i = 0; i < pathPoolCount; i++)
        {
            byte[] key = new byte[32];
            ((UInt256)i).ToBigEndian(key);
            Pedersen keccak = new Pedersen(key);
            pathPool[i] = keccak;
        }


        for (int leafIndex = 0; leafIndex < 10000; leafIndex++)
        {
            byte[] value = new byte[32];
            Random.NextBytes(value);
            Pedersen path = pathPool[Random.Next(pathPool.Length - 1)];
            tree.Insert(path, value);
            leafs[path] = value;
            leafsForSync[path] = value;
        }

        tree.Commit();
        tree.CommitTree(0);


        Pedersen stateRoot180 = Pedersen.Zero;
        for (int blockNumber = 1; blockNumber <= numBlocks; blockNumber++)
        {
            for (int accountIndex = 0; accountIndex < leafPerBlock; accountIndex++)
            {
                byte[] leafValue = new byte[32];

                Random.NextBytes(leafValue);
                Pedersen path = pathPool[Random.Next(pathPool.Length - 1)];

                if (leafs.ContainsKey(path))
                {
                    if (!(Random.NextSingle() > 0.5)) continue;
                    Console.WriteLine($"blockNumber:{blockNumber} uKey:{path} uValue:{leafValue.ToHexString()}");
                    tree.Insert(path, leafValue);
                    leafs[path] = leafValue;
                    if(blockToGetIteratorFrom >= blockNumber) leafsForSync[path] = leafValue;
                    Console.WriteLine("new values");
                }
                else
                {
                    Console.WriteLine($"blockNumber:{blockNumber} nKey:{path} nValue:{leafValue.ToHexString()}");
                    tree.Insert(path, leafValue);
                    leafs[path] = leafValue;
                    if(blockToGetIteratorFrom >= blockNumber) leafsForSync[path] = leafValue;
                }
            }

            tree.Commit();
            tree.CommitTree(blockNumber);
            if (blockNumber == blockToGetIteratorFrom) stateRoot180 = tree.StateRoot;
        }


        Pedersen[] keysArray = leafs.Keys.ToArray();
        int keyLength = keysArray.Length;
        using IEnumerator<KeyValuePair<byte[], byte[]>> rangeEnum =
            tree._verkleStateStore
                .GetLeafRangeIterator(
                keysArray[keyLength/4].Bytes,
                keysArray[(keyLength*2)/3].Bytes, 180)
                .GetEnumerator();


        while (rangeEnum.MoveNext())
        {
            Console.WriteLine($"Key:{rangeEnum.Current.Key.ToHexString()} AcValue:{rangeEnum.Current.Value.ToHexString()} ExValue:{leafsForSync[rangeEnum.Current.Key].ToHexString()}");
            Assert.That(rangeEnum.Current.Value.SequenceEqual(leafsForSync[rangeEnum.Current.Key]), Is.True);
        }

        using IEnumerator<PathWithSubTree> rangeEnumSized =
            tree._verkleStateStore
                .GetLeafRangeIterator(
                    keysArray[keyLength/4].StemAsSpan.ToArray(),
                    keysArray[(keyLength*2)/3].StemAsSpan.ToArray(), stateRoot180, 1000)
                .GetEnumerator();


        while (rangeEnumSized.MoveNext())
        {
            Console.WriteLine($"{rangeEnumSized.Current.Path}");
            // Console.WriteLine($"Key:{rangeEnum.Current.Key.ToHexString()} AcValue:{rangeEnum.Current.Value.ToHexString()} ExValue:{leafsForSync[rangeEnum.Current.Key].ToHexString()}");
            // Assert.That(rangeEnum.Current.Value.SequenceEqual(leafsForSync[rangeEnum.Current.Key]), Is.True);
        }


    }


    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void CreateHugeTree(DbMode dbMode)
    {
        long block = 0;
        VerkleTree tree = GetVerkleTreeForTest(dbMode);
        Dictionary<byte[], byte[]?> kvMap = new(Bytes.EqualityComparer);
        byte[] key = new byte[32];
        byte[] value = new byte[32];
        DateTime start = DateTime.Now;
        for (int i = 0; i < numKeys; i++)
        {
            Random.NextBytes(key);
            Random.NextBytes(value);
            kvMap[key.AsSpan().ToArray()] = value.AsSpan().ToArray();
            tree.Insert(key, value);
        }
        DateTime check1 = DateTime.Now;
        tree.Commit();
        tree.CommitTree(block++);
        DateTime check2 = DateTime.Now;
        Console.WriteLine($"{block} Insert: {(check1 - start).TotalMilliseconds}");
        Console.WriteLine($"{block} Flush: {(check2 - check1).TotalMilliseconds}");

        SortedSet<byte[]> keys = new(Bytes.Comparer);
        for (int i = 10; i < numKeys; i += 10)
        {
            DateTime check5 = DateTime.Now;
            Random.NextBytes(key);
            Random.NextBytes(value);
            for (int j = (i-10); j < i; j += 1)
            {
                Random.NextBytes(key);
                Random.NextBytes(value);
                kvMap[key.AsSpan().ToArray()] = value.AsSpan().ToArray();
                tree.Insert(key, value);
                keys.Add(key.AsSpan().ToArray());
            }
            DateTime check3 = DateTime.Now;
            tree.Commit();
            tree.CommitTree(block++);
            DateTime check4 = DateTime.Now;
            Console.WriteLine($"{block} Insert: {(check3 - check5).TotalMilliseconds}");
            Console.WriteLine($"{block} Flush: {(check4 - check3).TotalMilliseconds}");
        }
        DateTime check6 = DateTime.Now;
        Console.WriteLine($"Loop Time: {(check6 - check2).TotalMilliseconds}");
        Console.WriteLine($"Total Time: {(check6 - start).TotalMilliseconds}");


        byte[][] keysArray = keys.ToArray();
        using IEnumerator<KeyValuePair<byte[], byte[]>> rangeEnum =
            tree._verkleStateStore.GetLeafRangeIterator(keysArray[30], keysArray[90], 180).GetEnumerator();

        while (rangeEnum.MoveNext())
        {
            Console.WriteLine($"Key:{rangeEnum.Current.Key.ToHexString()} Value:{rangeEnum.Current.Value.ToHexString()}");
            Assert.That(rangeEnum.Current.Value.SequenceEqual(kvMap[rangeEnum.Current.Key]), Is.True);
        }
    }
}
