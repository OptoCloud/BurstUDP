using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BurstUDP
{
    internal static class EnumeratorExtensions
    {
        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, ulong batchSize)
        {
            using (var enumerator = source.GetEnumerator())
                while (enumerator.MoveNext())
                    yield return YieldBatchElements(enumerator, batchSize - 1);
        }

        private static IEnumerable<T> YieldBatchElements<T>(IEnumerator<T> source, ulong batchSize)
        {
            yield return source.Current;
            for (ulong i = 0; i < batchSize && source.MoveNext(); i++)
                yield return source.Current;
        }

        public delegate Task AsyncTask<T>(T item);

        /// <summary>
        /// Runs all items in the enumerable in parallel as tasks, and awaits them all to finish.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        public static Task ForEachAsync<T>(this IEnumerable<T> source, AsyncTask<T> func)
        {
            // Start async funcs on all tasks at the same time (.ToArray() is really important, because it finishes all the iterations)
            return Task.WhenAll(source.Select(p => func(p)).ToArray());
        }
    }
}
