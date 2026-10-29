using System.Net;
using System.Net.Mail;
using System.Net.Mime;

namespace ServiceProviderAPI.Services;

/// <summary>
/// Builds the shared branded HTML shell used by every outbound yProHub email and
/// embeds the logo inline via a content-id (cid:) so it renders reliably in email
/// clients without depending on a publicly reachable image URL.
/// </summary>
public static class EmailTemplates
{
    public const string LogoCid = "yprohub-logo";
    private const string DefaultTagline = "This is an official message from yProHub.";

    /// <summary>
    /// Wraps inner HTML content in the branded yProHub shell (logo header + footer).
    /// </summary>
    public static string Wrap(string innerHtml, string? tagline = null)
    {
        var line = tagline ?? DefaultTagline;
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""></head>
<body style=""margin:0; padding:0; background-color:#f4f5f7; font-family:Segoe UI, Arial, sans-serif; color:#1a1a1a;"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f4f5f7; padding:24px 0;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""max-width:480px; background-color:#ffffff; border-radius:12px; overflow:hidden; border:1px solid #e5e7eb;"">
          <tr>
            <td align=""center"" style=""padding:32px 24px 8px 24px;"">
              <img src=""cid:{LogoCid}"" alt=""yProHub"" width=""64"" height=""64"" style=""display:block; border:0; outline:none;"" />
            </td>
          </tr>
          <tr>
            <td align=""center"" style=""padding:0 24px;"">
              <p style=""margin:4px 0 20px 0; font-size:13px; color:#6b7280;"">{line}</p>
            </td>
          </tr>
          <tr>
            <td style=""padding:0 24px 8px 24px; font-size:14px; color:#374151; line-height:1.6; text-align:left;"">
              {innerHtml}
            </td>
          </tr>
          <tr>
            <td style=""padding:0 24px;"">
              <hr style=""border:none; border-top:1px solid #e5e7eb; margin:16px 0 0 0;"" />
            </td>
          </tr>
          <tr>
            <td style=""padding:20px 24px 28px 24px;"">
              <p style=""margin:0; font-size:12px; color:#9ca3af;"">This is an automated message, please do not reply.<br/>&copy; {DateTime.UtcNow.Year} yProHub. All rights reserved.</p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
    }

    /// <summary>
    /// Converts a plain-text message (with newlines) into safe inline HTML,
    /// trimming the incidental indentation that verbatim string literals introduce.
    /// </summary>
    public static string TextToHtml(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var lines = text.Replace("\r\n", "\n").Split('\n')
                        .Select(l => WebUtility.HtmlEncode(l.Trim()));
        return string.Join("<br>", lines);
    }

    /// <summary>
    /// Builds an HTML <see cref="AlternateView"/> wrapped in the branded shell with the
    /// logo embedded inline. The caller adds it to the MailMessage's AlternateViews.
    /// </summary>
    public static AlternateView BuildBrandedView(string innerHtml, string? tagline = null)
    {
        var view = AlternateView.CreateAlternateViewFromString(
            Wrap(innerHtml, tagline), null, MediaTypeNames.Text.Html);

        var logoPath = Path.Combine(AppContext.BaseDirectory, "EmailAssets", "logo-512.png");
        if (File.Exists(logoPath))
        {
            var logo = new LinkedResource(logoPath, "image/png")
            {
                ContentId = LogoCid,
                TransferEncoding = TransferEncoding.Base64
            };
            logo.ContentType.Name = "logo-512.png";
            view.LinkedResources.Add(logo);
        }

        return view;
    }
}
