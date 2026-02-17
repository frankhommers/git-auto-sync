namespace GitAutoSync.Core;

public static class FileUtils
{
  public static FileSystemInfo GetFileOrDirectory(string path)
  {
    if (Directory.Exists(path))
    {
      return new DirectoryInfo(path);
    }

    return new FileInfo(path);
  }

  public static bool IsDescendantOfDirectory(DirectoryInfo directoryInfo, FileSystemInfo descendant)
  {
    directoryInfo = new DirectoryInfo(GetExactPathName(directoryInfo.FullName));
    DirectoryInfo? d = null;
    if (descendant is DirectoryInfo descendantDir)
    {
      d = new DirectoryInfo(GetExactPathName(descendantDir.FullName));
    }
    else if (descendant is FileInfo descendantFile)
    {
      d = new DirectoryInfo(GetExactPathName(descendantFile.DirectoryName!));
    }

    do
    {
      if (GetExactPathName(d!.FullName).Equals(directoryInfo.FullName, StringComparison.Ordinal))
      {
        return true;
      }

      d = d.Parent;
    } while (d != null);

    return false;
  }

  public static string GetExactPathName(string pathName)
  {
    if (!(File.Exists(pathName) || Directory.Exists(pathName)))
    {
      return pathName;
    }

    DirectoryInfo di = new(pathName);
    if (di.Parent != null)
    {
      FileSystemInfo[] fileSystemInfos = di.Parent.GetFileSystemInfos(di.Name);
      if (fileSystemInfos.Length > 1)
      {
        throw new InvalidOperationException();
      }

      return Path.Combine(
        GetExactPathName(di.Parent.FullName),
        fileSystemInfos[0].Name);
    }

    if (di.Name.EndsWith(":\\"))
    {
      return di.Name.ToUpper(); // Windows Drive Letter
    }

    return di.Name;
  }
}