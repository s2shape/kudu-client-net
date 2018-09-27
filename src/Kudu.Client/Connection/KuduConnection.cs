﻿using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client.Exceptions;
using Kudu.Client.Internal;
using Kudu.Client.Protocol.Rpc;
using ProtoBuf;

namespace Kudu.Client.Connection
{
    public class KuduConnection : IDisposable
    {
        private readonly IConnection _connection;
        private readonly PipeReader _input;
        private readonly PipeWriter _output;
        private readonly SemaphoreSlim _singleWriter;
        private readonly Dictionary<int, TaskCompletionSource<CallResponse>> _inflightMessages;
        private readonly Task _receiveTask;
        private readonly TaskCompletionSource<object> _inputCompletedTcs;

        private int _nextCallId;

        public KuduConnection(IConnection connection)
        {
            _connection = connection;
            _input = connection.Input;
            _output = connection.Output;
            _singleWriter = new SemaphoreSlim(1, 1);
            _inflightMessages = new Dictionary<int, TaskCompletionSource<CallResponse>>();
            _nextCallId = 0;

            _input.OnWriterCompleted(
                (Exception ex, object state) => ((KuduConnection)state).OnWriterCompleted(ex),
                state: this);

            _receiveTask = StartReceiveLoopAsync();
            _inputCompletedTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public async Task<CallResponse> SendReceiveAsync<TInput>(RequestHeader header, TInput body)
        {
            var tcs = new TaskCompletionSource<CallResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_inflightMessages)
            {
                // If an earlier component set CallId (such as the negotiator)
                // we don't want to overwrite it.
                if (header.CallId == 0)
                    header.CallId = _nextCallId++;

                _inflightMessages.Add(header.CallId, tcs);
            }
            await WriteAsync(header, body);
            return await tcs.Task;
        }

        public async ValueTask WriteAsync<TInput>(RequestHeader header, TInput body)
        {
            // TODO: Use PipeWriter once protobuf-net supports it.
            using (var stream = new RecyclableMemoryStream())
            {
                // Make space to write the length of the entire message.
                var length = stream.AsMemory(4);

                Serializer.SerializeWithLengthPrefix(stream, header, PrefixStyle.Base128);
                Serializer.SerializeWithLengthPrefix(stream, body, PrefixStyle.Base128);

                // Go back and write the length of the entire message, minus the 4
                // bytes we already allocated to store the length.
                BinaryPrimitives.WriteUInt32BigEndian(length.Span, (uint)stream.Length - 4);

                await WriteSynchronized(stream.ToMemory());
            }
        }

        private async ValueTask WriteSynchronized(ReadOnlyMemory<byte> source)
        {
            await _singleWriter.WaitAsync();
            try
            {
                await _output.WriteAsync(source);
            }
            finally
            {
                _singleWriter.Release();
            }
        }

        private async Task StartReceiveLoopAsync()
        {
            // TODO: Catch exceptions, and complete the reader.
            // TODO: Clean up parsing code.

            byte[] message = null;
            bool readInProgress = false;
            int remainingSize = 0;
            int written = 0;
            int msgSize = 0;

            while (true)
            {
                ReadResult result = await _input.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;

                if (result.IsCompleted || result.IsCanceled)
                    break;

                // TODO: Break some of these out into separate methods?
                if (readInProgress)
                {
                    // There's a read in progress (a message too large for one result).

                    var copyLength = Math.Min(remainingSize, buffer.Length);
                    var slice = buffer.Slice(0, copyLength);
                    slice.CopyTo(message.AsSpan(written));
                    written += (int)copyLength;
                    remainingSize -= (int)copyLength;

                    if (remainingSize == 0)
                    {
                        // Reached the end of the message.
                        ProcessResponse(CallResponse.FromMemory(message, msgSize));

                        message = null;
                        readInProgress = false;
                        written = 0;
                    }

                    buffer = buffer.Slice(copyLength);
                }

                while ((TryParseMessageSize(ref buffer, out var messageLength)))
                {
                    // TODO: Check messageLength.
                    message = ArrayPool<byte>.Shared.Rent(messageLength);
                    msgSize = messageLength;

                    // TODO: Catch exceptions, and return memory back to the pool.

                    // We got a message.
                    // Do we have this entire message?
                    if (buffer.Length >= messageLength)
                    {
                        // Yes!
                        var slice = buffer.Slice(0, messageLength);
                        slice.CopyTo(message);
                        ProcessResponse(CallResponse.FromMemory(message, messageLength));

                        message = null;
                        buffer = buffer.Slice(messageLength);
                    }
                    else
                    {
                        // We don't have the entire buffer. Take what we can.

                        buffer.CopyTo(message);
                        readInProgress = true;
                        remainingSize = messageLength - (int)buffer.Length;
                        written += (int)buffer.Length;
                        buffer = buffer.Slice(buffer.Length);
                    }
                }

                _input.AdvanceTo(buffer.Start, buffer.End);
            }

            _input.Complete();
        }

        private bool TryParseMessageSize(
            ref ReadOnlySequence<byte> input,
            out int totalMessageLength)
        {
            if (input.Length < 4)
            {
                // Not enough data for the header.
                totalMessageLength = default;
                return false;
            }

            if (input.First.Length >= 4)
            {
                // Already 4 bytes in the first segment.
                totalMessageLength = BinaryPrimitives.ReadInt32BigEndian(input.First.Span);
            }
            else
            {
                // Copy 4 bytes into a local span.
                Span<byte> local = stackalloc byte[4];
                input.Slice(0, 4).CopyTo(local);
                totalMessageLength = BinaryPrimitives.ReadInt32BigEndian(local);
            }

            input = input.Slice(4);
            return true;
        }

        private void ProcessResponse(CallResponse stream)
        {
            ResponseHeader header = stream.Header;
            TaskCompletionSource<CallResponse> tcs;

            lock (_inflightMessages)
            {
                // TODO: Check if the message actually exists.
                tcs = _inflightMessages[header.CallId];
                _inflightMessages.Remove(header.CallId);
            }

            if (header.IsError)
            {
                // TODO: Use stream.Memory directly once protobuf-net supports it.
                var ms = new MemoryStream(stream.Memory.ToArray());
                var error = Serializer.DeserializeWithLengthPrefix<ErrorStatusPB>(ms, PrefixStyle.Base128);
                var exception = new RpcException(error);

                tcs.SetException(exception);
            }
            else
            {
                tcs.SetResult(stream);
            }
        }

        private void OnWriterCompleted(Exception exception)
        {
            if (exception is null)
                Console.WriteLine($"KuduConnection [{_connection.ToString()}] closed");
            else
                Console.WriteLine($"KuduConnection [{_connection.ToString()}] unexpectedly terminated {exception.Message}");

            lock (_inflightMessages)
            {
                foreach (var inflightMessage in _inflightMessages.Values)
                {
                    if (exception is null)
                        // TODO: Should a separate exception be used, instead of canceling them?
                        inflightMessage.TrySetCanceled();
                    else
                        inflightMessage.TrySetException(exception);
                }

                _inflightMessages.Clear();
            }

            _inputCompletedTcs.SetResult(null);
        }

        public override string ToString() => _connection.ToString();

        public void Dispose()
        {
            // This connection is done writing, complete the writer.
            _output.Complete();

            // Wait for the reader to finish processing any data
            // left in its buffer.
            // TODO: Use C# 8 async dispose when it's available.
            _receiveTask.GetAwaiter().GetResult();

            _inputCompletedTcs.Task.GetAwaiter().GetResult();

            _singleWriter.Dispose();
            _connection.Dispose();
        }
    }
}