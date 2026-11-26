using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DataParsers.Base.Helpers;

public static class LocalHelper
{
    private static readonly Regex RemoveInvalidRegex = new($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars()))}]");

    public static void TryClearDir(string dir, int? skipFiles = null)
    {
        try
        {
            if(!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                return;
            }

            var dirFiles = Directory.GetFiles(dir);
            if(skipFiles == null)
                foreach(var file in dirFiles)
                    File.Delete(file);
            else
                foreach(var file in dirFiles.OrderByDescending(file => file)
                            .Skip(skipFiles.Value))
                    File.Delete(file);
        }
        catch(UnauthorizedAccessException) { }
        catch(IOException) { }
        catch(Exception e)
        {
            throw new Exception($"Can't clear folder '{dir}'. Exception: {e}");
        }
    }

    /// <summary>
    /// Removes invalid characters from a local file name.
    /// </summary>
    /// <param name="fileName">Local file name; download methods use the naming strategy when omitted.</param>
    /// <returns>A safe local file name.</returns>
    public static string GetSafeFileName(string fileName)
    {
        return RemoveInvalidRegex.Replace(Path.GetFileName(fileName), "");
    }
}
