﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Corax;
using Corax.Queries;
using FastTests.Voron;
using Sparrow.Server;
using Sparrow.Threading;
using Voron;
using Xunit;
using Xunit.Abstractions;

namespace FastTests.Corax
{
    public class BoostingQueryTest : StorageTest
    {
        private List<IndexSingleNumericalEntry<long, long>> longList = new();
        private const int IndexId = 0, Content1 = 1, Content2 = 2;

        public BoostingQueryTest(ITestOutputHelper output) : base(output) { }


        [Fact]
        public void SimpleBoosting()
        {
            PrepareData();            
            IndexEntries();         
            using var searcher = new IndexSearcher(Env);
            {
                var match = searcher.AllEntries();
                var boostedMatch = searcher.Boost(match, 10);

                Span<long> ids = stackalloc long[2048];                
                int read = boostedMatch.Fill(ids);
                ids = ids.Slice(0, read);

                Span<float> scores = stackalloc float[ids.Length];
                scores.Fill(1);
                boostedMatch.Score(ids, scores);

                for (int i = 0; i < scores.Length; i++)
                    Assert.Equal(10, scores[i]);
            }
        }

        [Fact]
        public void OrBoosting()
        {
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/1", Content1 = 1 });   //  2 * 10
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/11", Content1 = 0 });  //  2 
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/111", Content1 = 0 }); //  2
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/2", Content1 = 1 });   //  10            
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/4", Content1 = 1 });   //  10
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/3", Content1 = 2 });    

            IndexEntries();
            using var searcher = new IndexSearcher(Env);
            {
                var startWithMatch = searcher.StartWithQuery("Id", "list/1");
                var boostedStartWithMatch = searcher.Boost(startWithMatch, 2);
                var contentMatch = searcher.TermQuery("Content1", "1");
                var orMatch = searcher.Or(boostedStartWithMatch, contentMatch);
                var boostedOrMatch = searcher.Boost(orMatch, 10);

                Span<long> ids = stackalloc long[2048];
                int read = boostedOrMatch.Fill(ids);
                ids = ids.Slice(0, read);

                Span<float> scores = stackalloc float[ids.Length];
                scores.Fill(1);
                boostedOrMatch.Score(ids, scores);

                Assert.Equal(scores[0], 20);
                Assert.Equal(scores[1], 20);
                Assert.Equal(scores[2], 20);
                Assert.Equal(scores[3], 10);
                Assert.Equal(scores[4], 10);
            }
        }

        [Fact]
        public void OrderByBoosting()
        {
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/1", Content1 = 1 });   // 2 * 10
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/11", Content1 = 0 });  // 2 * 10
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/111", Content1 = 0 }); // 2 * 10
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/2", Content1 = 1 });   //     10
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/4", Content1 = 1 });   //     10
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/3", Content1 = 2 });   //      0

            IndexEntries();
            using var searcher = new IndexSearcher(Env);
            {
                var startWithMatch = searcher.StartWithQuery("Id", "list/1");
                var boostedStartWithMatch = searcher.Boost(startWithMatch, 2);
                var contentMatch = searcher.TermQuery("Content1", "1");
                var orMatch = searcher.Or(boostedStartWithMatch, contentMatch);
                var boostedOrMatch = searcher.Boost(orMatch, 10);
                var contentMatch2 = searcher.TermQuery("Content1", "2");
                var orMatch2 = searcher.Or(contentMatch2, boostedOrMatch);
                var sortedMatch = searcher.OrderBy(orMatch2, default(BoostingComparer));

                Span<long> ids = stackalloc long[2048];
                int read = sortedMatch.Fill(ids);
                ids = ids.Slice(0, read);

                List<string> sortedByCorax = new();
                for (int i = 0; i < read; ++i)
                    sortedByCorax.Add(searcher.GetIdentityFor(ids[i]));

                for (int i = 0; i < longList.Count; ++i)
                    Assert.Equal(longList[i].Id, sortedByCorax[i]);
            }
        }


        [Fact]
        public void OrderByBoostingTake4()
        {
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/1", Content1 = 1 });   // 2 * 10
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/11", Content1 = 0 });  // 2 * 10
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/111", Content1 = 0 }); // 2 * 10
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/2", Content1 = 1 });   //     10
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/4", Content1 = 1 });   //     10
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/3", Content1 = 2 });   //      0

            IndexEntries();
            using var searcher = new IndexSearcher(Env);
            {
                var startWithMatch = searcher.StartWithQuery("Id", "list/1");
                var boostedStartWithMatch = searcher.Boost(startWithMatch, 2);
                var contentMatch = searcher.TermQuery("Content1", "1");
                var orMatch = searcher.Or(boostedStartWithMatch, contentMatch);
                var boostedOrMatch = searcher.Boost(orMatch, 10);
                var contentMatch2 = searcher.TermQuery("Content1", "2");
                var orMatch2 = searcher.Or(contentMatch2, boostedOrMatch);
                var sortedMatch = searcher.OrderBy(orMatch2, default(BoostingComparer), take: 4);

                // TODO: Check what happens in OrderBy statements when the buffer is too small. 
                Span<long> ids = stackalloc long[1024];

                var read = sortedMatch.Fill(ids);

                List<string> sortedByCorax = new();
                for (int i = 0; i < read; ++i)
                    sortedByCorax.Add(searcher.GetIdentityFor(ids[i]));

                for (int i = 0; i < longList.Count; ++i)
                    Assert.Equal(longList[i].Id, sortedByCorax[i]);
            }
        }

        private static int CompareAscending(IndexSingleNumericalEntry<long, long> value1, IndexSingleNumericalEntry<long, long> value2)
        {
            return value1.Content1.CompareTo(value2.Content1);
        }

        [Fact]
        public void OrderByBoostingTermFrequency()
        {
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/1", Content1 = 0 });   // 1/2
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/2", Content1 = 0 });   // 1/2
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/3", Content1 = 1 });   // 1/4
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/4", Content1 = 1 });   // 1/4
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/5", Content1 = 1 });   // 1/4
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/6", Content1 = 1 });   // 1/4            

            IndexEntries();
            using var searcher = new IndexSearcher(Env);
            {
                var content0Match = searcher.TermQuery("Content1", "0");
                var boostedContent0 = searcher.Boost(content0Match, default(TermFrequencyScoreFunction));

                var content1Match = searcher.TermQuery("Content1", "1");
                var boostedContent1 = searcher.Boost(content1Match, default(TermFrequencyScoreFunction));

                var orMatch = searcher.Or(boostedContent0, boostedContent1);
                var boostedOrMatch = searcher.Boost(orMatch, 10);
                var sortedMatch = searcher.OrderByScore(boostedOrMatch);

                Span<long> ids = stackalloc long[1024];
                var read = sortedMatch.Fill(ids);

                List<string> sortedByCorax = new();
                for (int i = 0; i < read; ++i)
                    sortedByCorax.Add(searcher.GetIdentityFor(ids[i]));

                for (int i = 0; i < longList.Count; ++i)
                    Assert.Equal(longList[i].Id, sortedByCorax[i]);
            }
        }

        [Fact]
        public void OrderByBoostingOrBasedInQuery()
        {
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/1", Content1 = 0 });   // 1/2
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/2", Content1 = 0 });   // 1/2
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/3", Content1 = 1 });   // 1/4
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/4", Content1 = 1 });   // 1/4
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/5", Content1 = 1 });   // 1/4
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/6", Content1 = 1 });   // 1/4


            IndexEntries();
            using var searcher = new IndexSearcher(Env);
            {
                var query = searcher.InQuery("Content1", new List<string>() { "0", "1" }, default(TermFrequencyScoreFunction));

                Span<long> ids = stackalloc long[1024];
                var read = query.Fill(ids);

                List<string> sortedByCorax = new();
                for (int i = 0; i < read; ++i)
                    sortedByCorax.Add(searcher.GetIdentityFor(ids[i]));

                for (int i = 0; i < longList.Count; ++i)
                    Assert.Equal(longList[i].Id, sortedByCorax[i]);
            }
        }

        [Fact]
        public void OrderByBoostingMultiTermFrequency()
        {

            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/1", Content1 = 0 });   // 1            
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/3", Content1 = 2 });   // 1/3
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/4", Content1 = 3 });   // 1/4
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/2", Content1 = 1 });   // 1/2
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/22", Content1 = 1 });   // 1/2
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/33", Content1 = 2 });   // 1/3
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/44", Content1 = 3 });   // 1/4
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/333", Content1 = 2 });   // 1/3
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/444", Content1 = 3 });   // 1/4
            longList.Add(new IndexSingleNumericalEntry<long, long> { Id = $"list/4444", Content1 = 3 });   // 1/4

            IndexEntries();

            longList.Sort(CompareAscending);
            using var searcher = new IndexSearcher(Env);
            {                
                var query = MultiTermBoostingMatch<InTermProvider>.Create(searcher, 
                    new InTermProvider(searcher, "Content1", new List<string>() { "0", "1", "2", "3" }), 
                    default(TermFrequencyScoreFunction));
                var sortedMatch = searcher.OrderByScore(query);

                Span<long> ids = stackalloc long[1024];
                var read = sortedMatch.Fill(ids);

                List<long> sortedByCorax = new();
                for (int i = 0; i < read; ++i)
                {
                    searcher.GetReaderFor(ids[i]).Read(Content1, out long value);
                    sortedByCorax.Add(value);
                }                    

                for (int i = 0; i < longList.Count; ++i)
                    Assert.Equal(longList[i].Content1, sortedByCorax[i]);
            }
        }

        private void PrepareData(bool inverse = false)
        {
            for (int i = 0; i < 1000; ++i)
            {
                longList.Add(new IndexSingleNumericalEntry<long, long>
                {
                    Id = inverse ? $"list/1000-{i}" : $"list/{i}",
                    Content1 = i,
                    Content2 = inverse ? 1000 - i : i
                });
            }
        }

        private void IndexEntries()
        {
            using var bsc = new ByteStringContext(SharedMultipleUseFlag.None);
            Dictionary<Slice, int> knownFields = CreateKnownFields(bsc);

            const int bufferSize = 4096;
            using var _ = bsc.Allocate(bufferSize, out ByteString buffer);

            {
                using var indexWriter = new IndexWriter(Env);
                foreach (var entry in longList)
                {
                    var entryWriter = new IndexEntryWriter(buffer.ToSpan(), knownFields);
                    var data = CreateIndexEntry(ref entryWriter, entry);
                    indexWriter.Index(entry.Id, data, knownFields);
                }
                indexWriter.Commit();
            }
        }

        private Span<byte> CreateIndexEntry(ref IndexEntryWriter entryWriter, IndexSingleNumericalEntry<long, long> entry)
        {
            entryWriter.Write(IndexId, Encoding.UTF8.GetBytes(entry.Id));
            entryWriter.Write(Content1, Encoding.UTF8.GetBytes(entry.Content1.ToString()), entry.Content1, Convert.ToDouble(entry.Content1));
            entryWriter.Write(Content2, Encoding.UTF8.GetBytes(entry.Content2.ToString()), entry.Content2, Convert.ToDouble(entry.Content2));
            entryWriter.Finish(out var output);
            return output;
        }

        private Dictionary<Slice, int> CreateKnownFields(ByteStringContext bsc)
        {
            Slice.From(bsc, "Id", ByteStringType.Immutable, out Slice idSlice);
            Slice.From(bsc, "Content1", ByteStringType.Immutable, out Slice content1Slice);
            Slice.From(bsc, "Content2", ByteStringType.Immutable, out Slice content2Slice);

            return new Dictionary<Slice, int>
            {
                [idSlice] = IndexId,
                [content1Slice] = Content1,
                [content2Slice] = Content2,
            };
        }

        private class IndexSingleNumericalEntry<T1, T2>
        {
            public string Id { get; set; }
            public T1 Content1 { get; set; }
            public T2 Content2 { get; set; }
        }
    }
}