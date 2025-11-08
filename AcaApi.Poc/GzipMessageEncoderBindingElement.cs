using System;
using System.IO;
using System.IO.Compression;
using System.ServiceModel.Channels;

namespace AcaApi.Poc
{
    public sealed class GzipMessageEncoderBindingElement : MessageEncodingBindingElement
    {
        private readonly MessageEncodingBindingElement _innerBindingElement;

        public GzipMessageEncoderBindingElement(MessageEncodingBindingElement innerBindingElement)
        {
            _innerBindingElement = innerBindingElement ?? throw new ArgumentNullException(nameof(innerBindingElement));
        }

        public override MessageVersion MessageVersion
        {
            get => _innerBindingElement.MessageVersion;
            set => _innerBindingElement.MessageVersion = value;
        }

        public override MessageEncoderFactory CreateMessageEncoderFactory()
            => new GzipMessageEncoderFactory(_innerBindingElement.CreateMessageEncoderFactory());

        public override BindingElement Clone()
            => new GzipMessageEncoderBindingElement((MessageEncodingBindingElement)_innerBindingElement.Clone());

        public override T GetProperty<T>(BindingContext context) => _innerBindingElement.GetProperty<T>(context);

        public override bool CanBuildChannelFactory<TChannel>(BindingContext context)
            => _innerBindingElement.CanBuildChannelFactory<TChannel>(context);

        public override IChannelFactory<TChannel> BuildChannelFactory<TChannel>(BindingContext context)
            => _innerBindingElement.BuildChannelFactory<TChannel>(context);
    }

    public sealed class GzipMessageEncoderFactory : MessageEncoderFactory
    {
        private readonly MessageEncoderFactory _innerFactory;

        public GzipMessageEncoderFactory(MessageEncoderFactory innerFactory)
        {
            _innerFactory = innerFactory ?? throw new ArgumentNullException(nameof(innerFactory));
        }

        public override MessageEncoder Encoder => new GzipMessageEncoder(_innerFactory.Encoder);

        public override MessageVersion MessageVersion => _innerFactory.MessageVersion;
    }

    public sealed class GzipMessageEncoder : MessageEncoder
    {
        private readonly MessageEncoder _innerEncoder;

        public GzipMessageEncoder(MessageEncoder innerEncoder)
        {
            _innerEncoder = innerEncoder ?? throw new ArgumentNullException(nameof(innerEncoder));
        }

        public override string ContentType => _innerEncoder.ContentType;

        public override string MediaType => _innerEncoder.MediaType;

        public override MessageVersion MessageVersion => _innerEncoder.MessageVersion;

        public override Message ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager, string contentType)
        {
            var decompressedBuffer = DecompressBuffer(buffer, bufferManager);
            return _innerEncoder.ReadMessage(decompressedBuffer, bufferManager, contentType);
        }

        public override Message ReadMessage(Stream stream, int maxSizeOfHeaders, string contentType)
        {
            using var decompressedStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
            return _innerEncoder.ReadMessage(decompressedStream, maxSizeOfHeaders, contentType);
        }

        public override ArraySegment<byte> WriteMessage(Message message, int maxMessageSize, BufferManager bufferManager, int messageOffset)
        {
            var buffer = _innerEncoder.WriteMessage(message, maxMessageSize, bufferManager, 0);
            return CompressBuffer(buffer, bufferManager, messageOffset);
        }

        public override void WriteMessage(Message message, Stream stream)
        {
            using var gzipStream = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true);
            _innerEncoder.WriteMessage(message, gzipStream);
            gzipStream.Flush();
        }

        private static ArraySegment<byte> CompressBuffer(ArraySegment<byte> buffer, BufferManager bufferManager, int messageOffset)
        {
            using var memoryStream = new MemoryStream();
            using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, leaveOpen: true))
            {
                gzipStream.Write(buffer.Array!, buffer.Offset, buffer.Count);
                gzipStream.Flush();
            }

            var compressedBytes = memoryStream.ToArray();
            var compressedBuffer = bufferManager.TakeBuffer(compressedBytes.Length + messageOffset);
            Array.Copy(compressedBytes, 0, compressedBuffer, messageOffset, compressedBytes.Length);

            bufferManager.ReturnBuffer(buffer.Array!);
            return new ArraySegment<byte>(compressedBuffer, messageOffset, compressedBytes.Length);
        }

        private static ArraySegment<byte> DecompressBuffer(ArraySegment<byte> buffer, BufferManager bufferManager)
        {
            using var memoryStream = new MemoryStream(buffer.Array!, buffer.Offset, buffer.Count);
            using var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress, leaveOpen: true);
            using var decompressedStream = new MemoryStream();
            gzipStream.CopyTo(decompressedStream);
            decompressedStream.Flush();

            var decompressedBytes = decompressedStream.ToArray();
            var decompressedBuffer = bufferManager.TakeBuffer(decompressedBytes.Length);
            Array.Copy(decompressedBytes, 0, decompressedBuffer, 0, decompressedBytes.Length);

            bufferManager.ReturnBuffer(buffer.Array!);
            return new ArraySegment<byte>(decompressedBuffer, 0, decompressedBytes.Length);
        }
    }
}
