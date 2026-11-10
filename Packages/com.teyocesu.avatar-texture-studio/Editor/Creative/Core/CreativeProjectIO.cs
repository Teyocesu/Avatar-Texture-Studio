using System;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using AvatarTextureStudio.Infrastructure;

namespace AvatarTextureStudio.Creative.Core
{
    internal static class CreativeProjectIO
    {
        static readonly byte[] Magic = Encoding.ASCII.GetBytes("ATSCRV01");
        const long MaxDecoded = 512L * 1024 * 1024;
        public static void Save(string path, CreativeDocument document, bool overwrite)
        {
            document.Validate();
            long size = (long)document.Baseline.Width * document.Baseline.Height * 4;
            foreach (var region in document.Regions) size += region.Mask.ByteCount;
            if (size > MaxDecoded - 1024 * 1024) throw new InvalidDataException("Creative document exceeds the 512 MiB decoded limit.");
            SafeFileIO.Write(path, stream =>
            {
                stream.Write(Magic, 0, Magic.Length);
                using (var header = new BinaryWriter(stream, Encoding.UTF8, true)) { header.Write(1); header.Write(0L); }
                long payloadStart = stream.Position;
                using (var gzip = new GZipStream(stream, CompressionLevel.Optimal, true))
                using (var w = new BinaryWriter(gzip, Encoding.UTF8, true))
                {
                    var image = document.Baseline; var b = document.Binding;
                    w.Write(image.Width); w.Write(image.Height); w.Write(image.IsSRGB);
                    Text(w, b.Renderer); Text(w, b.Material); Text(w, b.Property); Text(w, b.Source);
                    w.Write(b.Slot); w.Write(b.Channel); Vector(w, b.Scale); Vector(w, b.Offset); w.Write((int)b.WrapU); w.Write((int)b.WrapV);
                    w.Write(document.Zoom); Vector(w, document.Pan); w.Write(document.ActiveRegion);
                    w.Write(image.CopyPixels()); w.Write(document.Regions.Count);
                    foreach (var r in document.Regions)
                    {
                        Text(w, r.Name); w.Write(r.Enabled); w.Write(r.FacePreset); w.Write(r.Hue); w.Write(r.Chroma); w.Write(r.Strength); w.Write(r.Mask.CopyBits());
                    }
                }
                long end = stream.Position;
                stream.Position = Magic.Length + sizeof(int);
                using (var header = new BinaryWriter(stream, Encoding.UTF8, true)) header.Write(end - payloadStart);
                stream.Position = end;
            }, overwrite);
        }
        public static CreativeDocument Load(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var header = new BinaryReader(stream, Encoding.UTF8, true))
            {
                for (int i = 0; i < Magic.Length; i++) if (header.ReadByte() != Magic[i]) throw new InvalidDataException("Not an ATSCRV01 creative document.");
                if (header.ReadInt32() != 1) throw new InvalidDataException("Unsupported creative document version; use a compatible Studio version.");
                long compressedBytes = header.ReadInt64();
                if (compressedBytes < 18 || compressedBytes > MaxDecoded + 4 * 1024 * 1024 || compressedBytes != stream.Length - stream.Position)
                    throw new InvalidDataException("Creative payload length is invalid or incomplete. Current editing state was retained.");
                using (var gzip = new GZipStream(stream, CompressionMode.Decompress, true))
                using (var bounded = new BoundedInput(gzip, MaxDecoded))
                using (var r = new BinaryReader(bounded, Encoding.UTF8, true))
                {
                    int width = r.ReadInt32(), height = r.ReadInt32(); bool srgb = r.ReadBoolean();
                    if (width < 1 || height < 1 || width > 8192 || height > 8192) throw new InvalidDataException("Creative dimensions must be 1–8192.");
                    var b = new CreativeBinding { Renderer = Text(r), Material = Text(r), Property = Text(r), Source = Text(r), Slot = r.ReadInt32(), Channel = r.ReadInt32(),
                        Scale = Vector(r), Offset = Vector(r), WrapU = (UvWrap)r.ReadInt32(), WrapV = (UvWrap)r.ReadInt32() };
                    var d = new CreativeDocument { Binding = b, Zoom = r.ReadSingle(), Pan = Vector(r), ActiveRegion = r.ReadInt32() };
                    d.Baseline = new ColorImage(width, height, srgb, Bytes(r, checked(width * height * 4)));
                    int count = r.ReadInt32();
                    if (count < 0 || count > 32) throw new InvalidDataException("Creative projects support at most 32 regions.");
                    for (int i = 0; i < count; i++)
                        d.Regions.Add(new ColorRegion { Name = Text(r), Enabled = r.ReadBoolean(), FacePreset = r.ReadBoolean(), Hue = r.ReadSingle(), Chroma = r.ReadSingle(), Strength = r.ReadSingle(),
                            Mask = new RegionMask(width, height, Bytes(r, checked((width * height + 7) / 8))) });
                    if (bounded.ReadByte() != -1) throw new InvalidDataException("Unexpected trailing creative data.");
                    d.Validate(); return d;
                }
            }
        }
        static byte[] Bytes(BinaryReader reader, int count)
        {
            byte[] bytes = reader.ReadBytes(count);
            if (bytes.Length != count) throw new EndOfStreamException("Creative document is incomplete; current editing state was retained.");
            return bytes;
        }
        static void Text(BinaryWriter writer, string text) { var bytes = Encoding.UTF8.GetBytes(text); writer.Write(bytes.Length); writer.Write(bytes); }
        static string Text(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > 16384) throw new InvalidDataException("Creative text field exceeds its bound.");
            return new UTF8Encoding(false, true).GetString(Bytes(reader, count));
        }
        static void Vector(BinaryWriter w, Vector2 v) { w.Write(v.X); w.Write(v.Y); }
        static Vector2 Vector(BinaryReader r) => new Vector2(r.ReadSingle(), r.ReadSingle());

        sealed class BoundedInput : Stream
        {
            readonly Stream input; readonly long maximum; long read;
            public BoundedInput(Stream input, long maximum) { this.input = input; this.maximum = maximum; }
            public override int Read(byte[] buffer, int offset, int count)
            {
                int n = input.Read(buffer, offset, (int)Math.Min(count, maximum - read + 1)); read += n;
                if (read > maximum) throw new InvalidDataException("Creative data exceeds the 512 MiB decoded limit."); return n;
            }
            public override int ReadByte() { byte[] b = new byte[1]; return Read(b, 0, 1) == 0 ? -1 : b[0]; }
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => read; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
