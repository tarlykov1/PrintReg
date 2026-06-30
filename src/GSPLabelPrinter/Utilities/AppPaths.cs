namespace GSPLabelPrinter.Utilities;
public sealed class AppPaths
{
    public AppPaths(string root) => Root = Path.GetFullPath(root);
    public string Root { get; }
    public string ResolveInsideRoot(string relative)
    {
        if (Path.IsPathRooted(relative)) throw new InvalidOperationException("Разрешены только относительные пути внутри папки приложения.");
        var full = Path.GetFullPath(Path.Combine(Root, relative));
        if (!full.StartsWith(Root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Путь выходит за пределы папки приложения.");
        return full;
    }
}
