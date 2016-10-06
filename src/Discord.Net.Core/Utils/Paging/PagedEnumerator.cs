﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Discord
{
    internal class PagedAsyncEnumerable<T> : IAsyncEnumerable<IReadOnlyCollection<T>>
    {
        public int PageSize { get; }

        private readonly ulong? _start;
        private readonly int? _count;
        private readonly Func<PageInfo, CancellationToken, Task<IReadOnlyCollection<T>>> _getPage;
        private readonly Action<PageInfo, IReadOnlyCollection<T>> _nextPage;

        public PagedAsyncEnumerable(int pageSize, Func<PageInfo, CancellationToken, Task<IReadOnlyCollection<T>>> getPage, Action<PageInfo, IReadOnlyCollection<T>> nextPage = null,
            ulong? start = null, int? count = null)
        {
            PageSize = pageSize;
            _start = start;
            _count = count;

            _getPage = getPage;
            _nextPage = nextPage;
        }

        public IAsyncEnumerator<IReadOnlyCollection<T>> GetEnumerator() => new Enumerator(this);
        internal class Enumerator : IAsyncEnumerator<IReadOnlyCollection<T>>
        {
            private readonly PagedAsyncEnumerable<T> _source;
            private readonly PageInfo _info;

            public IReadOnlyCollection<T> Current { get; private set; }

            public Enumerator(PagedAsyncEnumerable<T> source)
            {
                _source = source;
                _info = new PageInfo(source._start, source._count, source.PageSize);
            }

            public async Task<bool> MoveNext(CancellationToken cancelToken)
            {
                if (_info.Remaining == 0)
                    return false;

                var data = await _source._getPage(_info, cancelToken);
                Current = new Page<T>(_info, data);

                _info.Page++;
                if (_info.Remaining != null)
                {
                    if (Current.Count >= _info.Remaining)
                        _info.Remaining = 0;
                    else
                        _info.Remaining -= Current.Count;
                }
                else
                {
                    if (Current.Count == 0)
                        _info.Remaining = 0;
                }
                _info.PageSize = _info.Remaining != null ? (int)Math.Min(_info.Remaining.Value, _source.PageSize) : _source.PageSize;

                if (_info.Remaining != 0)
                    _source?._nextPage(_info, data);

                return true;
            }
            
            public void Dispose() { Current = null; }
        }
    }
}