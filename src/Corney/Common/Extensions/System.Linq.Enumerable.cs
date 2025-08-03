using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Corney.Common.Extensions
{
    public static class EnumerableExtensions
    {
        /// <summary>
        ///     Executes function for each sequence item
        /// </summary>
        /// <typeparam name="TSource">Sequence</typeparam>
        /// <param name="source">Function to execute</param>
        /// <returns />
        [DebuggerStepThrough]
        public static void ForEach<TSource>(this IEnumerable<TSource> source, Action<TSource> action)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            if (action == null)
                throw new ArgumentNullException("action");
            foreach (TSource source1 in source)
                action(source1);
        }

        /// <summary>
        ///     Tries to cast each item to the specified type. If it fails,  it just ignores the item
        /// </summary>
        /// <typeparam name="T" />
        /// <param name="source" />
        /// <returns />
        public static IEnumerable<T> CastSilentlyTo<T>(this IEnumerable source) where T : class
        {
            T res = default(T);
            foreach (object obj in source)
            {
                res = obj as T;
                if (res != null)
                    yield return res;
            }
        }

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>
            (this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            var seenKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source)
        {
            return new HashSet<T>(source);
        }

        public static IEnumerable<List<T>> Partition<T>(this IList<T> source, int size)
        {
            for (int i = 0; i < Math.Ceiling(source.Count / (double)size); i++)
                yield return new List<T>(source.Skip(size * i).Take(size));
        }

        public static IEnumerable<IList<T>> Partition<T>(this IEnumerable<T> source, int size)
        {
            var partition = new T[size];
            var count = 0;

            foreach (var t in source)
            {
                partition[count] = t;
                count++;

                if (count == size)
                {
                    yield return partition;
                   partition = new T[size];
                   count = 0;
                }
            }

            if (count > 0)
            {
                Array.Resize(ref partition, count);
                yield return partition;
            }
        }

        public static async Task<TResult[]> ForEachWithResulAsync<T, TResult>(this IList<T> source, int degreeOfParallelism, Func<T, Task<TResult>> body)
        {
            var lists = await Task.WhenAll<List<TResult>>(
                Partitioner.Create(source).GetPartitions(degreeOfParallelism)
                    .Select(partition => Task.Run<List<TResult>>(async () =>
                    {
                        var list = new List<TResult>();
                        using (partition)
                        {
                            while (partition.MoveNext())
                            {
                                list.Add(await body(partition.Current));    
                            }
                        }
                        return list;
                    })));
            return lists.SelectMany(list => list).ToArray();
        }
        public static Task ForEachAsync<T>(this IEnumerable<T> source, int degreeOfParallelism, Func<T, Task> body)
        {
            var partitions = Partitioner.Create(source).GetPartitions(degreeOfParallelism);
            var tasks = partitions.Select(async partition =>
            {
                using (partition)
                    while (partition.MoveNext())
                        await body(partition.Current);
            });
            return Task.WhenAll(tasks);
        }
        public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> collection, int n)
        {
            if (collection == null)
                throw new ArgumentNullException("collection");
            if (n < 0)
                throw new ArgumentOutOfRangeException("n", "n must be 0 or greater");

            LinkedList<T> temp = new LinkedList<T>();

            foreach (var value in collection)
            {
                temp.AddLast(value);
                if (temp.Count > n)
                    temp.RemoveFirst();
            }

            return temp;
        }
    }
}