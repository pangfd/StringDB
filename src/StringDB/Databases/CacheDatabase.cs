﻿using JetBrains.Annotations;

using StringDB.LazyLoaders;

using System;
using System.Collections.Generic;
using System.Linq;

namespace StringDB.Databases
{
	/// <inheritdoc />
	/// <summary>
	/// Non-thread-safely caches results and loaded values from a database.
	/// This could be used in scenarios where you're repeatedly iterating
	/// over an IODatabase.
	/// </summary>
	/// <typeparam name="TKey">The type of key.</typeparam>
	/// <typeparam name="TValue">The type of value.</typeparam>
	[PublicAPI]
	public sealed class CacheDatabase<TKey, TValue>
		: BaseDatabase<TKey, TValue>, IDatabaseLayer<TKey, TValue>
	{
		[NotNull] private readonly List<KeyValuePair<TKey, CachedLoader<TValue>>> _cache;
		private readonly bool _disposeDatabase;

		/// <inheritdoc />
		[NotNull] public IDatabase<TKey, TValue> InnerDatabase { get; }

		/// <summary>
		/// Create a new <see cref="CacheDatabase{TKey,TValue}"/>.
		/// </summary>
		/// <param name="database">The database to cache.</param>
		public CacheDatabase
		(
			[NotNull] IDatabase<TKey, TValue> database,
			bool disposeDatabase = true
		)
			: this(database, EqualityComparer<TKey>.Default, disposeDatabase)
		{
		}

		/// <summary>
		/// Create a new <see cref="CacheDatabase{TKey,TValue}"/>.
		/// </summary>
		/// <param name="database">The database to cache.</param>
		/// <param name="comparer">The equality comparer for the key.</param>
		public CacheDatabase
		(
			[NotNull] IDatabase<TKey, TValue> database,
			[NotNull] IEqualityComparer<TKey> comparer,
			bool disposeDatabase = true
		)
			: base(comparer)
		{
			_cache = new List<KeyValuePair<TKey, CachedLoader<TValue>>>();
			InnerDatabase = database;
			_disposeDatabase = disposeDatabase;
		}

		/// <inheritdoc />
		public override void InsertRange(params KeyValuePair<TKey, TValue>[] items)

			// we can't add it to our cache otherwise it might change the order of things
			=> InnerDatabase.InsertRange(items);

		/// <inheritdoc />
		protected override IEnumerable<KeyValuePair<TKey, ILazyLoader<TValue>>> Evaluate()
		{
			// first enumerate to however much we have in memory
			for (var i = 0; i < _cache.Count; i++)
			{
				var current = _cache[i];
				yield return current.Value.ToKeyValuePair(current.Key);
			}

			// start reading and adding more as we go along
			foreach (var item in InnerDatabase.Skip(_cache.Count))
			{
				var currentCache = new KeyValuePair<TKey, CachedLoader<TValue>>
				(
					item.Key,
					new CachedLoader<TValue>(item.Value)
				);

				_cache.Add(currentCache);

				var current = currentCache;

				yield return current.Value.ToKeyValuePair(current.Key);
			}
		}

		/// <inheritdoc />
		public override void Dispose()
		{
			foreach (var item in _cache)
			{
				if (item.Key is IDisposable disposable)
				{
					disposable.Dispose();
				}

				item.Value.Dispose();
			}

			_cache.Clear();

			if (_disposeDatabase)
			{
				InnerDatabase.Dispose();
			}
		}
	}
}