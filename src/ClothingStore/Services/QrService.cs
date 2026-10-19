using System.Security.Cryptography;
using QRCoder;

namespace ClothingStore.Services;

public static class QrService
{
    public static string NewToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    public static byte[] Png(string text, int pixelsPerModule = 10)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(pixelsPerModule, new byte[] { 0x18, 0x36, 0x30 }, new byte[] { 0xFF, 0xFF, 0xFF });
    }
}
