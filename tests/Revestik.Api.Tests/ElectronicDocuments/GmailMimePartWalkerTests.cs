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
    public void DecodeBase64Url_DecodesGmailPayload()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("<xml>ok</xml>");
        var base64Url = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var decoded = GmailMimePartWalker.DecodeBase64Url(base64Url);

        Assert.Equal(bytes, decoded);
    }
}
