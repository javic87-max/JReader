using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace lector_de_libros.Services;

/// <summary>
/// Converts the XHTML content of an EPUB chapter into plain text appended to a StringBuilder.
/// The reading view is a plain read-only TextBox (see MainWindow.xaml for why), so there's no rich
/// formatting to produce here - just readable, well-separated paragraphs.
/// </summary>
public static class HtmlToPlainTextConverter
{
    private static readonly HashSet<string> HeadingTags = ["h1", "h2", "h3", "h4", "h5", "h6"];

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

    public static void AppendChapter(StringBuilder text, string xhtmlContent)
    {
        XElement? root = TryParse(xhtmlContent);
        if (root is null)
        {
            AppendParagraph(text, CollapseWhitespace(StripTags(xhtmlContent)));
            return;
        }

        XElement body = root.Descendants()
            .FirstOrDefault(e => e.Name.LocalName.Equals("body", StringComparison.OrdinalIgnoreCase)) ?? root;
        AppendBlocks(text, body);
    }

    private static void AppendBlocks(StringBuilder text, XElement parent)
    {
        foreach (XElement child in parent.Elements())
        {
            string tag = child.Name.LocalName.ToLowerInvariant();

            if (tag is "script" or "style" or "head" or "svg")
            {
                continue;
            }

            if (HeadingTags.Contains(tag) || tag is "p" or "blockquote")
            {
                AppendParagraph(text, CollapseWhitespace(GetInlineText(child)));
            }
            else if (tag == "li")
            {
                AppendParagraph(text, "• " + CollapseWhitespace(GetInlineText(child)));
            }
            else if (tag == "img")
            {
                string alt = (string?)child.Attribute("alt") ?? "imagen";
                AppendParagraph(text, $"[Imagen: {alt}]");
            }
            else
            {
                // div/section/article/header/footer/ul/ol/body/etc: no paragraph of their own, recurse.
                AppendBlocks(text, child);
            }
        }
    }

    private static void AppendParagraph(StringBuilder text, string paragraphText)
    {
        if (paragraphText.Length == 0)
        {
            return;
        }
        if (text.Length > 0)
        {
            text.Append("\r\n\r\n");
        }
        text.Append(paragraphText);
    }

    private static string GetInlineText(XElement element)
    {
        StringBuilder sb = new();
        AppendInlineText(element, sb);
        return sb.ToString();
    }

    private static void AppendInlineText(XElement element, StringBuilder sb)
    {
        foreach (XNode node in element.Nodes())
        {
            if (node is XText textNode)
            {
                sb.Append(textNode.Value);
            }
            else if (node is XElement el)
            {
                string tag = el.Name.LocalName.ToLowerInvariant();
                switch (tag)
                {
                    case "br":
                        sb.Append(' ');
                        break;
                    case "img":
                        string alt = (string?)el.Attribute("alt") ?? "imagen";
                        sb.Append($"[Imagen: {alt}]");
                        break;
                    case "script" or "style":
                        break;
                    default:
                        AppendInlineText(el, sb);
                        break;
                }
            }
        }
    }

    private static string CollapseWhitespace(string text) => Regex.Replace(text, @"\s+", " ").Trim();

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
