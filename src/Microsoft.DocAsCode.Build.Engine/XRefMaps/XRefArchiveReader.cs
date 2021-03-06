﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.DocAsCode.Utility;

namespace Microsoft.DocAsCode.Build.Engine
{
    public class XRefArchiveReader : XRefRedirectionReader, IDisposable
    {
        #region Fields
        private readonly LruList<Tuple<string, XRefMap>> _lru;
        private readonly XRefArchive _archive;
        #endregion

        #region Ctors

        public XRefArchiveReader(XRefArchive archive)
            : base(XRefArchive.MajorFileName, new HashSet<string>(archive.Entries))
        {
            if (archive == null)
            {
                throw new ArgumentNullException(nameof(archive));
            }
            _archive = archive;
            _lru = LruList<Tuple<string, XRefMap>>.Create(0x10, comparer: new TupleComparer());
        }

        #endregion

        protected override XRefMap GetMap(string name)
        {
            Tuple<string, XRefMap> tuple;
            if (_lru.TryFind(t => t.Item1 == name, out tuple))
            {
                return tuple.Item2;
            }
            var result = _archive.Get(name);
            _lru.Access(Tuple.Create(name, result));
            return result;
        }

        #region IDisposable Members

        public void Dispose()
        {
            _archive.Dispose();
        }

        #endregion

        #region MyRegion

        private sealed class TupleComparer : EqualityComparer<Tuple<string, XRefMap>>
        {
            public override bool Equals(Tuple<string, XRefMap> x, Tuple<string, XRefMap> y)
            {
                return string.Equals(x.Item1, y.Item1);
            }

            public override int GetHashCode(Tuple<string, XRefMap> obj)
            {
                return obj.Item1.GetHashCode();
            }
        }
        #endregion
    }
}
