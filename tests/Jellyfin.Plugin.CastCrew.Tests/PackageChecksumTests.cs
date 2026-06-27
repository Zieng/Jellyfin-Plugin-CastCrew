using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using Xunit;

namespace Jellyfin.Plugin.CastCrew.Tests;

/// <summary>
/// Validates that the packaging approach produces checksums compatible with
/// Jellyfin's InstallationManager verification.
/// </summary>
/// <remarks>
/// Jellyfin computes MD5 via BitConverter.ToString().Replace("-","") (uppercase hex)
/// and compares using StringComparison.OrdinalIgnoreCase. Our CI uses md5sum which
/// produces lowercase hex. This test verifies the two formats are compatible.
///
/// The root cause of checksum mismatch failures is when the manifest.json checksum
/// and the served zip file get out of sync (e.g., stale manifest from a previous
/// deployment while the zip is from a newer build).
/// </remarks>
public class PackageChecksumTests
{
    [Fact]
    public void Md5Checksum_LowercaseMatchesJellyfinUppercase_CaseInsensitive()
    {
        // Simulate a zip file
        byte[] zipContent;
        using (var ms = new MemoryStream())
        {
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("test.dll");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("dummy plugin content");
            }

            zipContent = ms.ToArray();
        }

        // Compute MD5 the way our CI does (md5sum → lowercase hex)
        string ciChecksum;
        using (var md5 = MD5.Create())
        {
            var hash = md5.ComputeHash(zipContent);
            ciChecksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        // Compute MD5 the way Jellyfin does (BitConverter.ToString → uppercase hex)
        string jellyfinChecksum;
        using (var md5 = MD5.Create())
        {
            var hash = md5.ComputeHash(zipContent);
            jellyfinChecksum = BitConverter.ToString(hash).Replace("-", "");
        }

        // Jellyfin uses OrdinalIgnoreCase comparison
        Assert.True(
            string.Equals(ciChecksum, jellyfinChecksum, StringComparison.OrdinalIgnoreCase),
            $"CI checksum '{ciChecksum}' must match Jellyfin checksum '{jellyfinChecksum}' " +
            $"under case-insensitive comparison.");

        // Verify format: CI produces lowercase, Jellyfin produces uppercase
        Assert.Equal(ciChecksum, ciChecksum.ToLowerInvariant());
        Assert.Equal(jellyfinChecksum, jellyfinChecksum.ToUpperInvariant());
        Assert.Equal(32, ciChecksum.Length);
    }

    [Fact]
    public void Md5Checksum_SameContent_ProducesSameHash_Regardless_Of_ComputeMethod()
    {
        var content = System.Text.Encoding.UTF8.GetBytes("test zip content for checksum verification");

        // Method 1: ComputeHash(byte[])
        string hash1;
        using (var md5 = MD5.Create())
        {
            hash1 = BitConverter.ToString(md5.ComputeHash(content)).Replace("-", "");
        }

        // Method 2: ComputeHash(Stream) — this is what Jellyfin uses
        string hash2;
        using (var md5 = MD5.Create())
        using (var stream = new MemoryStream(content))
        {
            hash2 = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "");
        }

        Assert.Equal(hash1, hash2);
    }
}
