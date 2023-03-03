using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace DataParser.HtmlParser;

/// <summary>
///     required System.IO.Compression, System.IO.Compression.FileSystem
/// </summary>
public static class ZipParser
{
    public const string ZipExtension = ".zip";
    private const string ZipTempFolder = "tempUnzip";
    private static readonly string UnzipSourcesFolder = Path.Combine(ZipTempFolder, "zipSources");
    private static readonly string UnzipFolder = Path.Combine(ZipTempFolder, "unzip");

    public static IEnumerable<string> GetFiles(byte[] data)
    {
        using var stream = new MemoryStream(data);
        return GetFiles(stream);
    }

    public static IEnumerable<string> GetFiles(Stream stream)
    {
        try
        {
            if(Directory.Exists(ZipTempFolder))
                Directory.Delete(ZipTempFolder, true);
        }
        catch(IOException)
        {
            //ignored
        }

        Directory.CreateDirectory(ZipTempFolder);
        Directory.CreateDirectory(UnzipSourcesFolder);
        Directory.CreateDirectory(UnzipFolder);

        var zipFile = Path.Combine(UnzipSourcesFolder, Guid.NewGuid().ToString());
        using(var fileStream = new FileStream(zipFile, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.CopyTo(fileStream);
            fileStream.Flush(true);
        }

        ZipFile.ExtractToDirectory(zipFile, UnzipFolder, Encoding.UTF8);
        return Directory.EnumerateFiles(UnzipFolder, "*", SearchOption.AllDirectories);
    }

    public static bool IsArchiveValid(FileInfo file)
    {
        using var fileStream = file.OpenRead();
        return IsArchiveValid(fileStream);
    }

    public static bool IsArchiveValid(byte[] data)
    {
        var stream = new MemoryStream(data);
        return IsArchiveValid(stream);
    }

    public static bool IsArchiveValid(Stream stream, bool leaveOpen = false)
    {
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen);
            foreach(var entry in archive.Entries)
            {
                using var entryStream = entry.Open();
            }

            return true;
        }
        catch(Exception)
        {
            return false;
        }
    }
}
