```text
$$\   $$\ $$\      $$\ $$\                $$\ 
$$ |  $$ |$$$\    $$$ |$$ |               \__|
\$$\ $$  |$$$$\  $$$$ |$$ |      $$$$$$\  $$\ 
 \$$$$  / $$\$$\$$ $$ |$$ |      \____$$\ $$ |
 $$  $$<  $$ \$$$  $$ |$$ |      $$$$$$$ |$$ |
$$  /\$$\ $$ |\$  /$$ |$$ |     $$  __$$ |$$ |
$$ /  $$ |$$ | \_/ $$ |$$$$$$$$\\$$$$$$$ |$$ |
\__|  \__|\__|     \__|\________|\_______|\__|
```

`xml.ai` is a .NET console application for exploring XML files with XPath in an interactive prompt.

It loads an XML document, generates XPath suggestions, and lets you run XPath queries repeatedly with:
- tab-based autocomplete
- command history (up/down arrows)
- built-in helper commands for tree and suggestions

## What It Does

- Loads an XML file passed with `-f <path>`.
- Builds XPath suggestions from element paths, attribute paths, shorthand (`//...`), and `text()` variants.
- Starts an interactive REPL where each line is either a built-in command or an XPath expression.

Built-in commands:
- `--help` shows startup/help information
- `--suggestions` prints all generated XPath suggestions
- `--tree` prints a structural tree of the loaded XML

## Requirements

- .NET SDK 10 (`net10.0`)

## Compile

From repository root:

```powershell
dotnet build .\xmlai.sln
```

Once compiled:

```powershell
xmlai -f <xmlfile>
```

If the file does not exist, it exits with an error message.

## Example Queries

After launch, try:

```text
--tree
--suggestions
--help
```

If using the example `books.xml` file:

```text
//author
```

## Interaction Notes

- `Tab` cycles autocomplete suggestions.
- `Up`/`Down` browse command history.
- `Esc` clears the current input line.
- `Ctrl+C` exits cleanly.

When running in a redirected/non-interactive context, it reads from standard input line-by-line until EOF.

## Security Note

XML is loaded with DTD processing disabled (`DtdProcessing = Prohibit`) and `XmlResolver = null`.
