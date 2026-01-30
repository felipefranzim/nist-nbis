// See https://aka.ms/new-console-template for more information
using System.Drawing;
using System.Drawing.Imaging;
using WsqSharp;

void ConvertPngToBmp(string pngPath, string bmpPath)
{
    // Load the PNG file into an Image object
    using (Image image = Image.FromFile(pngPath))
    {
        // Save the image in the BMP format
        image.Save(bmpPath, ImageFormat.Bmp);
    }
}

 ConvertPngToBmp(@"C:\\Users\\operador\\Downloads\\ENC_ Impressões digitais neonatal\\nomatch2.png", @"C:\\Users\\operador\\Downloads\\ENC_ Impressões digitais neonatal\\nomatch2.bmp");


var wsq = WsqConverter.ConvertBmpToWsq(File.ReadAllBytes(@"C:\\Users\\operador\\Downloads\\ENC_ Impressões digitais neonatal\\nomatch2.bmp"));
File.WriteAllBytes(@"fingerprint.wsq", wsq);

var teste = File.ReadAllBytes("C:\\Users\\operador\\Downloads\\OneDrive_2025-10-06\\Digitais NeoNatal\\medio esquerdo.wsq");
int score;
int result = NfiqNative.nfiq_from_wsq_data(out score, teste, teste.Length);
Console.WriteLine($"Result: {result}, Score: {score}");

var nfiq = NfiqHelper.GetQualityFromWsq(wsq);

Console.WriteLine($"Qualidade NFIQ: {nfiq}");
Console.ReadKey();

// WSQ para BMP (arquivos)
//WsqConverter.ConvertWsqFileToBmpFile(@"fingerprint.wsq", @"output.bmp");

// Trabalhando com byte[]
//byte[] bmpBytes = File.ReadAllBytes(@"fingerprint.bmp");
//byte[] wsqBytes = WsqConverter.ConvertBmpToWsq(bmpBytes);

// Ou diretamente com pixels raw
//byte[] grayscalePixels = GetPixelsFromSomewhere();
//byte[] wsq = WsqConverter.EncodeRawToWsq(grayscalePixels, 512, 512, ppi: 500, bitrate: 0.75f);