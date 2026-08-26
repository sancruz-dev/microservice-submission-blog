using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ContentSubmission.Application.Submissions.Mdx;

public sealed record MdxParseResult(FrontMatterData? FrontMatter, string? Body, IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0;
}

/// <summary>
/// Splits an uploaded .mdx file into its YAML frontmatter and body, the same
/// shape gray-matter produces for the blog itself (see
/// sancruzblog-nextjs/utils/mdx-utils.js). This only parses structure - it
/// does not judge whether the frontmatter's fields are complete or valid;
/// that's SubmissionService's job, so all applicable errors can be reported
/// together instead of one round-trip per mistake.
/// </summary>
public static class MdxDocumentParser
{
    private const string Delimiter = "---";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private const char ByteOrderMark = '﻿';

    public static MdxParseResult Parse(string rawContent)
    {
        var lines = rawContent.Replace("\r\n", "\n").TrimStart(ByteOrderMark).Split('\n');

        if (lines.Length == 0 || lines[0].Trim() != Delimiter)
        {
            return new MdxParseResult(null, null,
                ["Content must start with a YAML frontmatter block delimited by '---'."]);
        }

        var closingIndex = Array.FindIndex(lines, 1, line => line.Trim() == Delimiter);

        if (closingIndex == -1)
        {
            return new MdxParseResult(null, null,
                ["Frontmatter block is missing its closing '---'."]);
        }

        var yamlText = string.Join('\n', lines[1..closingIndex]);
        var body = string.Join('\n', lines[(closingIndex + 1)..]).Trim();

        try
        {
            var frontMatter = Deserializer.Deserialize<FrontMatterData?>(yamlText) ?? new FrontMatterData();
            return new MdxParseResult(frontMatter, body, []);
        }
        catch (YamlException ex)
        {
            return new MdxParseResult(null, null, [$"Frontmatter is not valid YAML: {ex.Message}"]);
        }
    }
}
