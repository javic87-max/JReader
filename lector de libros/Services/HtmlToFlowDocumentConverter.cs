using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;

namespace lector_de_libros.Services;

/// <summary>
/// Converts the XHTML content of an EPUB chapter into WPF FlowDocument blocks.
/// Heading paragraphs get Tag = heading level (1-6), used later to rescale them when the user changes font size.
/// </summary>
public static class HtmlToFlowDocumentConverter
{
    private static readonly Dictionary<string, string> NamedEntityReplacements = new(StringComparer.OrdinalIgnoreCase)
    {
        ["&nbsp;"] = "&#160;",
        ["&mdash;"] = "&#8212;",
        ["&ndash;"] = "&#8211;",
        ["&hellip;"] = "&#8230;",
        ["&ldquo;"] = "&#8220;",
        ["&rdquo;"] = "&#8221;",
        ["&lsquo;"] = "&#8216;",
        ["&rsquo;"] = "&#8217;",
        ["&copy;"] = "&#169;",
        ["&reg;"] = "&#174;",
        ["&trade;"] = "&#8482;",
        ["&deg;"] = "&#176;",
        ["&times;"] = "&#215;",
        ["&laquo;"] = "&#171;",
        ["&raquo;"] = "&#187;",
    };

    public static Block? AppendChapter(FlowDocument document, string xhtmlContent, double baseFontSize)
    {
        XElement? root = TryParse(xhtmlContent);
        List<Block> blocks;

        if (root is null)
        {
            blocks = [new Paragraph(new Run(CollapseWhitespace(StripTags(xhtmlContent))))];
        }
        else
        {
            XElement body = root.Descendants()
                .FirstOrDefault(e => e.Name.LocalName.Equals("body", StringComparison.OrdinalIgnoreCase)) ?? root;
            blocks = ConvertBlocks(body, baseFontSize).ToList();
            if (blocks.Count == 0)
            {
                Paragraph fallback = new();
                AppendInlines(body, fallback.Inlines);
                if (fallback.Inlines.Count > 0)
                {
                    blocks.Add(fallback);
                }
            }
        }

        Block? firstBlock = null;
        foreach (Block block in blocks)
        {
            document.Blocks.Add(block);
            firstBlock ??= block;
        }
        return firstBlock;
    }

    private static IEnumerable<Block> ConvertBlocks(XElement parent, double baseFontSize)
    {
        foreach (XElement child in parent.Elements())
        {
            string tag = child.Name.LocalName.ToLowerInvariant();

            if (tag is "script" or "style" or "head" or "svg")
            {
                continue;
            }

            if (tag.Length == 2 && tag[0] == 'h' && char.IsDigit(tag[1]) &&
                HeadingStyle.LevelFontMultiplier.TryGetValue(tag[1] - '0', out double multiplier))
            {
                Paragraph paragraph = new()
                {
                    FontWeight = FontWeights.Bold,
                    FontSize = baseFontSize * multiplier,
                    Tag = tag[1] - '0',
                };
                AppendInlines(child, paragraph.Inlines);
                if (paragraph.Inlines.Count > 0)
                {
                    yield return paragraph;
                }
            }
            else if (tag is "p" or "blockquote" or "li")
            {
                Paragraph paragraph = new();
                if (tag == "blockquote")
                {
                    paragraph.Margin = new Thickness(24, 6, 0, 6);
                }
                else if (tag == "li")
                {
                    paragraph.Margin = new Thickness(12, 0, 0, 0);
                    paragraph.Inlines.Add(new Run("• "));
                }
                AppendInlines(child, paragraph.Inlines);
                if (paragraph.Inlines.Count > 0)
                {
                    yield return paragraph;
                }
            }
            else if (tag == "img")
            {
                string alt = (string?)child.Attribute("alt") ?? "imagen";
                yield return new Paragraph(new Run($"[Imagen: {alt}]")) { FontStyle = FontStyles.Italic };
            }
            else
            {
                // div/section/article/header/footer/ul/ol/body/etc: no visual block of their own, recurse into children
                foreach (Block nested in ConvertBlocks(child, baseFontSize))
                {
                    yield return nested;
                }
            }
        }
    }

    private static void AppendInlines(XElement element, InlineCollection inlines)
    {
        foreach (XNode node in element.Nodes())
        {
            if (node is XText textNode)
            {
                string text = CollapseWhitespace(textNode.Value);
                if (text.Length > 0)
                {
                    inlines.Add(new Run(text));
                }
            }
            else if (node is XElement el)
            {
                string tag = el.Name.LocalName.ToLowerInvariant();
                switch (tag)
                {
                    case "br":
                        inlines.Add(new LineBreak());
                        break;
                    case "em":
                    case "i":
                        Italic italic = new();
                        AppendInlines(el, italic.Inlines);
                        if (italic.Inlines.Count > 0)
                        {
                            inlines.Add(italic);
                        }
                        break;
                    case "strong":
                    case "b":
                        Bold bold = new();
                        AppendInlines(el, bold.Inlines);
                        if (bold.Inlines.Count > 0)
                        {
                            inlines.Add(bold);
                        }
                        break;
                    case "img":
                        string alt = (string?)el.Attribute("alt") ?? "imagen";
                        inlines.Add(new Run($"[Imagen: {alt}]"));
                        break;
                    case "script" or "style":
                        break;
                    default:
                        AppendInlines(el, inlines);
                        break;
                }
            }
        }
    }

    private static string CollapseWhitespace(string text) => Regex.Replace(text, @"\s+", " ");

    private static string StripTags(string html) => Regex.Replace(html, "<[^>]+>", " ");

    private static XElement? TryParse(string xhtmlContent)
    {
        string cleaned = StripPrologAndDoctype(NormalizeEntities(xhtmlContent));
        try
        {
            return XElement.Parse(cleaned);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static string NormalizeEntities(string xhtml)
    {
        foreach ((string name, string numeric) in NamedEntityReplacements)
        {
            xhtml = xhtml.Replace(name, numeric, StringComparison.OrdinalIgnoreCase);
        }
        return xhtml;
    }

    private static string StripPrologAndDoctype(string xhtml)
    {
        xhtml = Regex.Replace(xhtml, @"<\?xml[^>]*\?>", string.Empty, RegexOptions.IgnoreCase);
        xhtml = Regex.Replace(xhtml, @"<!DOCTYPE[^>]*>", string.Empty, RegexOptions.IgnoreCase);
        return xhtml;
    }
}
