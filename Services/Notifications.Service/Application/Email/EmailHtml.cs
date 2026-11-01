using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Notifications.Service.Application.Email;

// A simple branded HTML version of a plain-text email: paragraphs, links and the first link as a button.
public static partial class EmailHtml
{
    [GeneratedRegex(@"^https?://\S+$")]
    private static partial Regex LinkLine();

    [GeneratedRegex(@"https?://[^\s<]+")]
    private static partial Regex InlineLink();

    public static string FromText(string subject, string text, string buttonLabel = "Open CropConnect")
    {
        var body = new StringBuilder();
        var buttonDone = false;

        foreach (var paragraph in text.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = paragraph.Trim();

            if (LinkLine().IsMatch(trimmed))
            {
                var href = WebUtility.HtmlEncode(trimmed);

                if (!buttonDone)
                {
                    body.Append($"""<p style="margin:24px 0"><a href="{href}" style="background:#16722F;color:#ffffff;text-decoration:none;padding:12px 24px;border-radius:24px;font-weight:600;display:inline-block">{WebUtility.HtmlEncode(buttonLabel)}</a></p>""");
                    body.Append($"""<p style="font-size:13px;color:#56615A;word-break:break-all">Or open: <a href="{href}" style="color:#16722F">{href}</a></p>""");
                    buttonDone = true;
                }
                else
                {
                    body.Append($"""<p><a href="{href}" style="color:#16722F">{href}</a></p>""");
                }

                continue;
            }

            var encoded = InlineLink().Replace(WebUtility.HtmlEncode(trimmed), m => $"""<a href="{m.Value}" style="color:#16722F">{m.Value}</a>""");
            body.Append($"<p>{encoded.Replace("\n", "<br>")}</p>");
        }

        return $"""
            <!doctype html>
            <html lang="en"><head><meta charset="utf-8"><title>{WebUtility.HtmlEncode(subject)}</title></head>
            <body style="margin:0;background:#F4F6F3;font-family:Arial,Helvetica,sans-serif;color:#1E2621">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr><td align="center" style="padding:24px 12px">
            <table role="presentation" width="100%" style="max-width:560px;background:#ffffff;border-radius:12px" cellpadding="0" cellspacing="0">
            <tr><td style="padding:20px 28px;border-bottom:1px solid #CBD3CD;font-size:20px;font-weight:700;color:#16722F">CropConnect</td></tr>
            <tr><td style="padding:8px 28px 24px;font-size:15px;line-height:1.5">{body}</td></tr>
            </table>
            <p style="font-size:12px;color:#56615A">CropConnect · local farms, direct to you</p>
            </td></tr></table>
            </body></html>
            """;
    }
}
