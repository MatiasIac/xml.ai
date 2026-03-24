using System.Xml;
using System.Xml.XPath;

namespace XmlAI;

readonly record struct ColoredLine(string Text, ConsoleColor Color = ConsoleColor.White);

class Program
{
    private const string Prompt = "> ";

    static void Main(string[] args)
    {
        var filePath = ParseFilePath(args);
        if (filePath == null)
        {
            Console.Error.WriteLine("Usage: xmlai -f <xmlfile>");
            Environment.Exit(1);
            return;
        }

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Error: File '{filePath}' not found.");
            Environment.Exit(1);
            return;
        }

        XmlDocument doc;
        try
        {
            doc = new XmlDocument();
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            using var reader = System.Xml.XmlReader.Create(filePath, settings);
            doc.Load(reader);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading XML: {ex.Message}");
            Environment.Exit(1);
            return;
        }

        var suggestions = XPathSuggestionEngine.BuildSuggestions(doc);
        var inputReader = new ConsoleInput(new XPathAutoComplete(suggestions));

        if (ConsoleLayout.IsSupported)
        {
            RunPinnedConsole(doc, filePath, suggestions, inputReader);
            return;
        }

        RunStandardConsole(doc, filePath, suggestions, inputReader);
    }

    static List<ColoredLine> BuildLogoLines()
    {
        return
        [
            new(@"$$\   $$\ $$\      $$\ $$\                $$\ ", ConsoleColor.Cyan),
            new(@"$$ |  $$ |$$$\    $$$ |$$ |               \__|", ConsoleColor.Cyan),
            new(@"\$$\ $$  |$$$$\  $$$$ |$$ |      $$$$$$\  $$\ ", ConsoleColor.Cyan),
            new(@" \$$$$  / $$\$$\$$ $$ |$$ |      \____$$\ $$ |", ConsoleColor.Magenta),
            new(@" $$  $$<  $$ \$$$  $$ |$$ |      $$$$$$$ |$$ |", ConsoleColor.Magenta),
            new(@"$$  /\$$\ $$ |\$  /$$ |$$ |     $$  __$$ |$$ |", ConsoleColor.Magenta),
            new(@"$$ /  $$ |$$ | \_/ $$ |$$$$$$$$\\$$$$$$$ |$$ |", ConsoleColor.Yellow),
            new(@"\__|  \__|\__|     \__|\________|\_______|\__|", ConsoleColor.Yellow),
            new(string.Empty, ConsoleColor.White),
        ];
    }

    static void RunPinnedConsole(XmlDocument doc, string filePath, IReadOnlyList<string> suggestions, ConsoleInput inputReader)
    {
        var layout = new ConsoleLayout(filePath, doc.DocumentElement?.Name, suggestions.Count);
        var resultLines = BuildLogoLines();
        resultLines.AddRange(BuildWelcomeLines(filePath, doc.DocumentElement?.Name, suggestions));
        string? lastCommand = null;

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.CursorVisible = true;
            Console.ResetColor();
            Console.Clear();
            Console.WriteLine("Goodbye!");
            Environment.Exit(0);
        };

        while (true)
        {
            layout.Render(resultLines, lastCommand);

            var input = inputReader.Read(Prompt);
            if (input == null)
                return;

            if (string.IsNullOrWhiteSpace(input))
                continue;

            lastCommand = input;
            resultLines = ExecuteInput(doc, filePath, suggestions, input);
        }
    }

    static void RunStandardConsole(XmlDocument doc, string filePath, IReadOnlyList<string> suggestions, ConsoleInput inputReader)
    {
        foreach (var line in BuildLogoLines())
            WriteColoredLine(line);

        foreach (var line in BuildWelcomeLines(filePath, doc.DocumentElement?.Name, suggestions))
            WriteColoredLine(line);

        WriteColoredLine(new("Type an XPath query and press Enter. Press Ctrl+C to exit.\n", ConsoleColor.DarkGray));

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.ResetColor();
            Console.WriteLine("\nGoodbye!");
            Environment.Exit(0);
        };

        while (true)
        {
            Console.ResetColor();
            var input = inputReader.Read(Prompt);
            if (input == null)
                return;

            if (string.IsNullOrWhiteSpace(input))
                continue;

            foreach (var line in ExecuteInput(doc, filePath, suggestions, input))
                WriteColoredLine(line);

            Console.WriteLine();
        }
    }

    static void WriteColoredLine(ColoredLine line)
    {
        Console.ForegroundColor = line.Color;
        Console.WriteLine(line.Text);
        Console.ResetColor();
    }

    static string? ParseFilePath(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-f")
                return args[i + 1];
        }

        return null;
    }

    static List<ColoredLine> BuildWelcomeLines(string filePath, string? rootElementName, IReadOnlyList<string> suggestions)
    {
        return
        [
            new($"Loaded: {filePath}", ConsoleColor.DarkGray),
            new($"Root element: <{rootElementName ?? "unknown"}>", ConsoleColor.DarkGray),
            new($"Found {suggestions.Count} XPath suggestions.", ConsoleColor.DarkGray),
            new("Commands:", ConsoleColor.DarkGray),
            new("  --help         Show the startup information and commands list.", ConsoleColor.DarkGray),
            new("  --suggestions   Show all generated XPath suggestions.", ConsoleColor.DarkGray),
            new("  --tree         Show the XML structure of the loaded file.", ConsoleColor.DarkGray),
            new("Run an XPath query to display results here.", ConsoleColor.DarkGray)
        ];
    }

    static List<ColoredLine> ExecuteInput(XmlDocument doc, string filePath, IReadOnlyList<string> suggestions, string input)
    {
        var command = input.Trim();
        if (command.Equals("--help", StringComparison.OrdinalIgnoreCase))
            return BuildWelcomeLines(filePath, doc.DocumentElement?.Name, suggestions);

        if (command.Equals("--suggestions", StringComparison.OrdinalIgnoreCase))
        {
            return BuildSuggestionLines(suggestions);
        }

        if (command.Equals("--tree", StringComparison.OrdinalIgnoreCase))
            return BuildTreeLines(doc);

        return ExecuteXPath(doc, input);
    }

    static List<ColoredLine> BuildSuggestionLines(IReadOnlyList<string> suggestions)
    {
        if (suggestions.Count == 0)
            return [new("(no XPath suggestions were generated)", ConsoleColor.White)];

        var lines = new List<ColoredLine>
        {
            new($"--- {suggestions.Count} XPath suggestion(s) ---", ConsoleColor.White)
        };

        foreach (var s in suggestions)
            lines.Add(new(s, ConsoleColor.White));

        return lines;
    }

    static List<ColoredLine> BuildTreeLines(XmlDocument doc)
    {
        if (doc.DocumentElement == null)
            return [new("(empty XML document)", ConsoleColor.White)];

        var structure = XmlTreeNode.FromElement(doc.DocumentElement);
        var rawLines = new List<string>
        {
            "XML structure:"
        };

        structure.AppendRootLines(rawLines);
        return rawLines.Select(l => new ColoredLine(l, ConsoleColor.White)).ToList();
    }

    static List<ColoredLine> ExecuteXPath(XmlDocument doc, string input)
    {
        try
        {
            var nodes = doc.SelectNodes(input);
            if (nodes == null || nodes.Count == 0)
                return [new("(no results)", ConsoleColor.White)];

            var lines = new List<ColoredLine>
            {
                new($"--- {nodes.Count} result(s) ---", ConsoleColor.White)
            };

            for (int i = 0; i < nodes.Count; i++)
                lines.Add(new(FormatNode(nodes[i]!), ConsoleColor.White));

            return lines;
        }
        catch (XPathException ex)
        {
            return [new($"XPath error: {ex.Message}", ConsoleColor.Red)];
        }
    }

    static string FormatNode(XmlNode node)
    {
        return node.NodeType switch
        {
            XmlNodeType.Element => node.OuterXml,
            XmlNodeType.Attribute => $"@{node.Name}=\"{node.Value}\"",
            XmlNodeType.Text => node.Value ?? string.Empty,
            XmlNodeType.CDATA => node.Value ?? string.Empty,
            _ => node.OuterXml
        };
    }
}

sealed class ConsoleInput
{
    private readonly XPathAutoComplete _autoComplete;
    private readonly List<string> _history = [];

    public ConsoleInput(XPathAutoComplete autoComplete)
    {
        _autoComplete = autoComplete;
    }

    public string? Read(string prompt)
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            Console.Write(prompt);
            return Console.ReadLine();
        }

        return ReadInteractive(prompt);
    }

    private string ReadInteractive(string prompt)
    {
        var buffer = new List<char>();
        var cursor = 0;
        var historyIndex = _history.Count;
        string savedInput = string.Empty;
        string? tabPrefix = null;
        string[] tabMatches = [];
        var tabMatchIndex = -1;
        var row = Console.CursorTop;
        var windowStart = 0;

        RenderBuffer();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            var resetTabState = true;

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    var input = new string([.. buffer]);
                    if (!string.IsNullOrWhiteSpace(input) &&
                        (_history.Count == 0 || !string.Equals(_history[^1], input, StringComparison.Ordinal)))
                    {
                        _history.Add(input);
                    }

                    return input;

                case ConsoleKey.Backspace:
                    if (cursor > 0)
                    {
                        buffer.RemoveAt(cursor - 1);
                        cursor--;
                    }
                    break;

                case ConsoleKey.Delete:
                    if (cursor < buffer.Count)
                        buffer.RemoveAt(cursor);
                    break;

                case ConsoleKey.LeftArrow:
                    if (cursor > 0)
                        cursor--;
                    break;

                case ConsoleKey.RightArrow:
                    if (cursor < buffer.Count)
                        cursor++;
                    break;

                case ConsoleKey.Home:
                    cursor = 0;
                    break;

                case ConsoleKey.End:
                    cursor = buffer.Count;
                    break;

                case ConsoleKey.UpArrow:
                    if (_history.Count == 0)
                        break;

                    if (historyIndex == _history.Count)
                        savedInput = new string([.. buffer]);

                    if (historyIndex > 0)
                        historyIndex--;

                    ReplaceBuffer(_history[historyIndex]);
                    break;

                case ConsoleKey.DownArrow:
                    if (_history.Count == 0)
                        break;

                    if (historyIndex < _history.Count - 1)
                    {
                        historyIndex++;
                        ReplaceBuffer(_history[historyIndex]);
                    }
                    else
                    {
                        historyIndex = _history.Count;
                        ReplaceBuffer(savedInput);
                    }
                    break;

                case ConsoleKey.Tab:
                    resetTabState = false;
                    var prefix = new string([.. buffer]);
                    if (!string.Equals(tabPrefix, prefix, StringComparison.Ordinal))
                    {
                        tabPrefix = prefix;
                        tabMatches = _autoComplete.GetSuggestions(prefix);
                        tabMatchIndex = -1;
                    }

                    if (tabMatches.Length > 0)
                    {
                        tabMatchIndex = (tabMatchIndex + 1) % tabMatches.Length;
                        ReplaceBuffer(tabMatches[tabMatchIndex]);
                    }
                    break;

                case ConsoleKey.Escape:
                    historyIndex = _history.Count;
                    savedInput = string.Empty;
                    ReplaceBuffer(string.Empty);
                    break;

                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        if (historyIndex != _history.Count)
                        {
                            historyIndex = _history.Count;
                            savedInput = string.Empty;
                        }

                        buffer.Insert(cursor, key.KeyChar);
                        cursor++;
                    }
                    break;
            }

            if (resetTabState)
            {
                tabPrefix = null;
                tabMatches = [];
                tabMatchIndex = -1;
            }

            EnsureWindowVisible();
            RenderBuffer();
        }

        void ReplaceBuffer(string text)
        {
            buffer.Clear();
            buffer.AddRange(text);
            cursor = buffer.Count;
            EnsureWindowVisible();
        }

        void EnsureWindowVisible()
        {
            var visibleWidth = GetVisibleWidth(prompt);

            if (cursor < windowStart)
                windowStart = cursor;
            else if (cursor > windowStart + visibleWidth)
                windowStart = cursor - visibleWidth;

            var maxWindowStart = Math.Max(0, buffer.Count - visibleWidth);
            if (windowStart > maxWindowStart)
                windowStart = maxWindowStart;
        }

        void RenderBuffer()
        {
            var visibleWidth = GetVisibleWidth(prompt);
            var visibleLength = Math.Min(visibleWidth, Math.Max(0, buffer.Count - windowStart));
            var visibleText = visibleLength > 0
                ? new string([.. buffer.Skip(windowStart).Take(visibleLength)])
                : string.Empty;

            Console.SetCursorPosition(0, row);
            Console.Write(prompt);
            Console.Write(visibleText.PadRight(visibleWidth + 1));
            Console.SetCursorPosition(Math.Min(prompt.Length + (cursor - windowStart), Console.WindowWidth - 1), row);
        }
    }

    private static int GetVisibleWidth(string prompt)
    {
        return Math.Max(1, Console.WindowWidth - prompt.Length - 1);
    }
}

sealed class XPathAutoComplete
{
    private readonly List<string> _suggestions;

    public XPathAutoComplete(List<string> suggestions)
    {
        _suggestions = suggestions;
    }

    public string[] GetSuggestions(string text)
    {
        if (string.IsNullOrEmpty(text))
            return _suggestions.ToArray();

        return _suggestions
            .Where(s => s.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}

sealed class ConsoleLayout
{
    private readonly string _filePath;
    private readonly string? _rootElementName;
    private readonly int _suggestionCount;

    public static bool IsSupported =>
        Environment.UserInteractive &&
        !Console.IsInputRedirected &&
        !Console.IsOutputRedirected;

    public ConsoleLayout(string filePath, string? rootElementName, int suggestionCount)
    {
        _filePath = filePath;
        _rootElementName = rootElementName;
        _suggestionCount = suggestionCount;
    }

    public void Render(IReadOnlyList<ColoredLine> resultLines, string? lastCommand)
    {
        var metrics = GetMetrics();

        Console.CursorVisible = false;
        Console.Clear();

        WriteRows(0, metrics.ResultHeight, PrepareResultRows(resultLines, metrics.Width, metrics.ResultHeight), metrics.Width);
        WriteRows(metrics.CommandTop, metrics.InfoHeight, BuildCommandRows(lastCommand), metrics.Width, fromEnd: true);
        WriteRow(metrics.PromptRow, new(string.Empty, ConsoleColor.White), metrics.Width);

        Console.ResetColor();
        Console.SetCursorPosition(0, metrics.PromptRow);
        Console.CursorVisible = true;
    }

    private List<ColoredLine> BuildCommandRows(string? lastCommand)
    {
        return
        [
            new("Command pane: results stay above | TAB autocomplete | Ctrl+C exit", ConsoleColor.DarkGray),
            new($"File: {Path.GetFileName(_filePath)} | Root: <{_rootElementName ?? "unknown"}> | Suggestions: {_suggestionCount}", ConsoleColor.DarkGray),
            new($"Last command: {(string.IsNullOrWhiteSpace(lastCommand) ? "(none yet)" : lastCommand)}", ConsoleColor.DarkGray)
        ];
    }

    private static List<ColoredLine> PrepareResultRows(IReadOnlyList<ColoredLine> lines, int width, int maxRows)
    {
        if (maxRows <= 0)
            return [];

        var wrapped = WrapLines(lines, width);
        if (wrapped.Count <= maxRows)
            return wrapped;

        if (maxRows == 1)
            return [new($"... ({wrapped.Count - 1} more line(s))", ConsoleColor.DarkGray)];

        var visibleRows = wrapped.Take(maxRows - 1).ToList();
        visibleRows.Add(new($"... ({wrapped.Count - (maxRows - 1)} more line(s))", ConsoleColor.DarkGray));
        return visibleRows;
    }

    private static List<ColoredLine> WrapLines(IReadOnlyList<ColoredLine> lines, int width)
    {
        var wrapped = new List<ColoredLine>();
        var safeWidth = Math.Max(1, width);

        foreach (var line in lines)
        {
            var normalized = (line.Text ?? string.Empty).Replace("\r", string.Empty).Replace("\t", "    ");
            var segments = normalized.Split('\n');

            foreach (var segment in segments)
            {
                if (segment.Length == 0)
                {
                    wrapped.Add(new(string.Empty, line.Color));
                    continue;
                }

                for (int start = 0; start < segment.Length; start += safeWidth)
                {
                    var length = Math.Min(safeWidth, segment.Length - start);
                    wrapped.Add(new(segment.Substring(start, length), line.Color));
                }
            }
        }

        return wrapped;
    }

    private static void WriteRows(int startRow, int maxRows, IReadOnlyList<ColoredLine> lines, int width, bool fromEnd = false)
    {
        if (maxRows <= 0)
            return;

        IReadOnlyList<ColoredLine> visibleRows = lines;
        if (lines.Count > maxRows)
        {
            visibleRows = fromEnd
                ? lines.Skip(lines.Count - maxRows).ToList()
                : lines.Take(maxRows).ToList();
        }

        for (int i = 0; i < maxRows; i++)
        {
            var line = i < visibleRows.Count ? visibleRows[i] : new(string.Empty, ConsoleColor.White);
            WriteRow(startRow + i, line, width);
        }
    }

    private static void WriteRow(int row, ColoredLine line, int width)
    {
        if (row < 0 || row >= Console.WindowHeight)
            return;

        var visibleText = line.Text.Length > width ? line.Text[..width] : line.Text;
        Console.SetCursorPosition(0, row);
        Console.ForegroundColor = line.Color;
        Console.Write(visibleText.PadRight(width));
    }

    private static LayoutMetrics GetMetrics()
    {
        var height = Math.Max(2, Console.WindowHeight);
        var width = Math.Max(1, Console.WindowWidth);
        var commandHeight = Math.Min(height - 1, Math.Max(2, (int)Math.Ceiling(height / 5d)));
        var commandTop = height - commandHeight;
        var promptRow = Math.Min(height - 1, commandTop + commandHeight - 1);

        if (promptRow == height - 1 && commandHeight > 1)
            promptRow--;

        if (promptRow < commandTop)
            promptRow = commandTop;

        return new LayoutMetrics(
            width,
            height,
            commandTop,
            commandTop,
            promptRow,
            Math.Max(0, promptRow - commandTop));
    }

    private readonly record struct LayoutMetrics(
        int Width,
        int Height,
        int CommandTop,
        int ResultHeight,
        int PromptRow,
        int InfoHeight);
}

static class XPathSuggestionEngine
{
    public static List<string> BuildSuggestions(XmlDocument doc)
    {
        var paths = new HashSet<string>();

        if (doc.DocumentElement == null)
            return new List<string>();

        CollectPaths(doc.DocumentElement, "", paths);

        var suggestions = new List<string>(paths);

        // Add useful shorthand patterns with //
        var shorthand = new HashSet<string>();
        foreach (var path in paths)
        {
            var parts = path.TrimStart('/').Split('/');
            if (parts.Length >= 2)
            {
                // Add //elementName for quick access
                var last = parts[^1];
                if (!last.StartsWith("@"))
                    shorthand.Add($"//{last}");
                else
                    shorthand.Add($"//{parts[^2]}/{last}");
            }
        }
        suggestions.AddRange(shorthand);

        // Add text() variants for leaf elements
        var textVariants = new HashSet<string>();
        foreach (var path in paths)
        {
            if (!path.Contains("@"))
                textVariants.Add($"{path}/text()");
        }
        suggestions.AddRange(textVariants);

        suggestions.Sort(StringComparer.OrdinalIgnoreCase);
        return suggestions.Distinct().ToList();
    }

    private static void CollectPaths(XmlNode node, string currentPath, HashSet<string> paths)
    {
        if (node.NodeType != XmlNodeType.Element)
            return;

        var path = $"{currentPath}/{node.Name}";
        paths.Add(path);

        // Add attribute paths
        if (node.Attributes != null)
        {
            foreach (XmlAttribute attr in node.Attributes)
                paths.Add($"{path}/@{attr.Name}");
        }

        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType == XmlNodeType.Element)
                CollectPaths(child, path, paths);
        }
    }
}

sealed class XmlTreeNode
{
    private readonly SortedSet<string> _attributes = new(StringComparer.OrdinalIgnoreCase);
    private readonly SortedDictionary<string, XmlTreeNode> _children = new(StringComparer.OrdinalIgnoreCase);

    private XmlTreeNode(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public bool HasTextValue { get; private set; }

    public static XmlTreeNode FromElement(XmlElement element)
    {
        var node = new XmlTreeNode(element.Name);

        if (element.Attributes != null)
        {
            foreach (XmlAttribute attribute in element.Attributes)
                node._attributes.Add(attribute.Name);
        }

        foreach (XmlNode child in element.ChildNodes)
        {
            switch (child.NodeType)
            {
                case XmlNodeType.Element:
                    var childElement = (XmlElement)child;
                    if (!node._children.TryGetValue(childElement.Name, out var existingChild))
                    {
                        existingChild = new XmlTreeNode(childElement.Name);
                        node._children.Add(childElement.Name, existingChild);
                    }

                    existingChild.MergeFrom(FromElement(childElement));
                    break;

                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                    if (!string.IsNullOrWhiteSpace(child.Value))
                        node.HasTextValue = true;
                    break;
            }
        }

        return node;
    }

    public void AppendRootLines(List<string> lines)
    {
        lines.Add($"<{Name}>");
        AppendChildren(lines, prefix: string.Empty);
    }

    public void AppendLines(List<string> lines, string prefix, bool isLast)
    {
        var connector = isLast ? "\\-- " : "+-- ";
        lines.Add($"{prefix}{connector}<{Name}>");

        var childPrefix = prefix + (isLast ? "    " : "|   ");
        AppendChildren(lines, childPrefix);
    }

    private void MergeFrom(XmlTreeNode other)
    {
        _attributes.UnionWith(other._attributes);
        HasTextValue |= other.HasTextValue;

        foreach (var child in other._children.Values)
        {
            if (!_children.TryGetValue(child.Name, out var existingChild))
            {
                existingChild = new XmlTreeNode(child.Name);
                _children.Add(child.Name, existingChild);
            }

            existingChild.MergeFrom(child);
        }
    }

    private void AppendChildren(List<string> lines, string prefix)
    {
        var children = new List<(string Label, XmlTreeNode? Node)>();

        foreach (var attribute in _attributes)
            children.Add(($"@{attribute}", null));

        foreach (var child in _children.Values)
            children.Add((child.Name, child));

        if (HasTextValue)
            children.Add(("text()", null));

        for (int i = 0; i < children.Count; i++)
        {
            var (label, node) = children[i];
            var isLast = i == children.Count - 1;
            var connector = isLast ? "\\-- " : "+-- ";

            if (node == null)
            {
                lines.Add($"{prefix}{connector}{label}");
                continue;
            }

            node.AppendLines(lines, prefix, isLast);
        }
    }
}
