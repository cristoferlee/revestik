using System.Text;
using Revestik.Api.Services.GmailIntegration;

namespace Revestik.Api.Tests.ElectronicDocuments;

public sealed class GmailMimePartWalkerTests
{
    [Fact]
    public void FindXmlAttachments_TraversesNestedMimeParts_AndIgnoresOtherFiles()
    {
        var root = new GmailMessagePartDto
        {
            Parts =
            [
                new GmailMessagePartDto
                {
                    FileName = "factura.pdf",
                    Body = new GmailMessagePartBodyDto
                    {
                        AttachmentId = "pdf-1",
                        Size = 123
                    }
                },
                new GmailMessagePartDto
                {
                    MimeType = "multipart/alternative",
                    Parts =
                    [
                        new GmailMessagePartDto
                        {
                            PartId = "2.1",
                            FileName = "Factura.XML",
                            Body = new GmailMessagePartBodyDto
                            {
                                AttachmentId = "xml-1",
                                Size = 456
                            }
                        }
                    ]
                }
            ]
        };

        var result = GmailMimePartWalker.FindXmlAttachments(root);

        var item = Assert.Single(result);
        Assert.Equal("Factura.XML", item.FileName);
        Assert.Equal("xml-1", item.Identity);
        Assert.Equal(456, item.DeclaredSize);
    }

    [Fact]
    public void FindTextBodies_TraversesAlternativeParts_AndReturnsPlainAndHtml()
    {
        var plain = Encode("Voucher plain text");
        var html = Encode("<html><body>Voucher HTML</body></html>");

        var root = new GmailMessagePartDto
        {
            MimeType = "multipart/alternative",
            Parts =
            [
                new GmailMessagePartDto
                {
                    PartId = "1",
                    MimeType = "text/plain",
                    Body = new GmailMessagePartBodyDto
                    {
                        Data = plain,
                        Size = 18
                    }
                },
                new GmailMessagePartDto
                {
                    PartId = "2",
                    MimeType = "text/html",
                    Body = new GmailMessagePartBodyDto
                    {
                        Data = html,
                        Size = 38
                    }
                }
            ]
        };

        var result = GmailMimePartWalker.FindTextBodies(root);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.MimeType == "text/plain" && x.Identity == "part:1");
        Assert.Contains(result, x => x.MimeType == "text/html" && x.Identity == "part:2");
    }

    [Fact]
    public void GetHeader_IsCaseInsensitive()
    {
        var root = new GmailMessagePartDto
        {
            Headers =
            [
                new GmailMessageHeaderDto { Name = "From", Value = "Banco Nacional <example@bncr.fi>" },
                new GmailMessageHeaderDto { Name = "Subject", Value = "Voucher Digital" }
            ]
        };

        Assert.Equal(
            "Voucher Digital",
            GmailMimePartWalker.GetHeader(root, "subject"));
    }

    [Fact]
    public void DecodeBase64Url_DecodesGmailPayload()
    {
        var bytes = Encoding.UTF8.GetBytes("<xml>ok</xml>");
        var base64Url = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var decoded = GmailMimePartWalker.DecodeBase64Url(base64Url);

        Assert.Equal(bytes, decoded);
    }

    [Fact]
    public void DecodeBase64UrlUtf8_DecodesTextBody()
    {
        var encoded = Encode("Hola voucher");

        var decoded = GmailMimePartWalker.DecodeBase64UrlUtf8(encoded);

        Assert.Equal("Hola voucher", decoded);
    }

    private static string Encode(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
