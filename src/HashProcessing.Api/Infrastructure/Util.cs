using System.Security.Cryptography;
using HashProcessing.Api.Core;

namespace HashProcessing.Api.Infrastructure;

internal static class Util
{
    internal static ushort EnsureDegreeOfParallelism(ushort degreeOfParallelism) =>
        degreeOfParallelism == 0
            ? (ushort)Environment.ProcessorCount
            : degreeOfParallelism;
    
        internal static ushort EnsureChannelCapacity(ushort requestedCapacity) 
            => Math.Clamp(requestedCapacity, (ushort)16, (ushort)4096);
        
        internal static Sha1Hash GenerateSha1()
        {
            Span<byte> randomBytes = stackalloc byte[32];
            Span<byte> hashBuffer = stackalloc byte[20];
            RandomNumberGenerator.Fill(randomBytes);
            SHA1.TryHashData(randomBytes, hashBuffer, out _);
            var hex = Convert.ToHexString(hashBuffer).ToLowerInvariant();
            return new Sha1Hash(hex);
        }
}