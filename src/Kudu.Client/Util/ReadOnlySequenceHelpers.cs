using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Kudu.Client.Util
{
    public static class ReadOnlySequenceHelpers
    {
        public static Stream ToStream(this ReadOnlySequence<byte> sequence) {
            var stream = new MemoryStream();

            var enumerator = sequence.GetEnumerator();
            while (enumerator.MoveNext()) {
                var array = enumerator.Current.ToArray();
                stream.Write(array, 0, array.Length);
            }

            stream.Position = 0;
            return stream;
        }
    }
}
