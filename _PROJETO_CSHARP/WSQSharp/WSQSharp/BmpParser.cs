using System;
using System.IO;
using System.Runtime.InteropServices;

namespace WsqSharp
{
    /// <summary>
    /// Informações de uma imagem raw grayscale
    /// </summary>
    public class RawImageData
    {
        public byte[] Pixels { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Ppi { get; set; }
    }

    /// <summary>
    /// Parser de BMP compatível com .NET Standard 2.0
    /// </summary>
    public static class BmpParser
    {
        /// <summary>
        /// Lê um arquivo BMP e extrai os dados grayscale
        /// </summary>
        public static RawImageData ReadBmpAsGrayscale(string filePath)
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            return ParseBmpAsGrayscale(fileData);
        }

        /// <summary>
        /// Parseia bytes de um BMP e extrai os dados grayscale
        /// </summary>
        public static RawImageData ParseBmpAsGrayscale(byte[] bmpData)
        {
            if (bmpData == null || bmpData.Length < 54)
                throw new ArgumentException("Dados BMP inválidos");

            // Verificar assinatura BMP ("BM")
            if (bmpData[0] != 0x42 || bmpData[1] != 0x4D)
                throw new ArgumentException("Arquivo não é um BMP válido");

            // Ler header BMP
            int dataOffset = BitConverter.ToInt32(bmpData, 10);
            int width = BitConverter.ToInt32(bmpData, 18);
            int height = BitConverter.ToInt32(bmpData, 22);
            int bitsPerPixel = BitConverter.ToInt16(bmpData, 28);
            int compression = BitConverter.ToInt32(bmpData, 30);

            // Resolução (pixels por metro → converter para PPI)
            int xPelsPerMeter = BitConverter.ToInt32(bmpData, 38);
            int yPelsPerMeter = BitConverter.ToInt32(bmpData, 42);
            int ppi = (int)Math.Round(xPelsPerMeter / 39.3701); // metros para polegadas
            if (ppi < 100) ppi = 500; // default para impressões digitais

            // BMP pode ter altura negativa (top-down)
            bool topDown = height < 0;
            height = Math.Abs(height);

            if (compression != 0 && compression != 3)
                throw new NotSupportedException($"Compressão BMP não suportada: {compression}");

            // Calcular stride (linhas são alinhadas em 4 bytes)
            int bytesPerPixel = bitsPerPixel / 8;
            int stride = ((width * bytesPerPixel) + 3) & ~3;

            byte[] grayscale = new byte[width * height];

            for (int y = 0; y < height; y++)
            {
                // BMP armazena de baixo para cima (a menos que seja top-down)
                int srcY = topDown ? y : (height - 1 - y);
                int srcOffset = dataOffset + (srcY * stride);

                for (int x = 0; x < width; x++)
                {
                    int pixelOffset = srcOffset + (x * bytesPerPixel);
                    byte gray;

                    switch (bitsPerPixel)
                    {
                        case 8:
                            // Já é grayscale (ou indexed, assumimos grayscale)
                            gray = bmpData[pixelOffset];
                            break;

                        case 24:
                            // BGR → Grayscale
                            byte b = bmpData[pixelOffset];
                            byte g = bmpData[pixelOffset + 1];
                            byte r = bmpData[pixelOffset + 2];
                            gray = (byte)(0.299 * r + 0.587 * g + 0.114 * b);
                            break;

                        case 32:
                            // BGRA → Grayscale
                            byte b32 = bmpData[pixelOffset];
                            byte g32 = bmpData[pixelOffset + 1];
                            byte r32 = bmpData[pixelOffset + 2];
                            // Alpha ignorado
                            gray = (byte)(0.299 * r32 + 0.587 * g32 + 0.114 * b32);
                            break;

                        default:
                            throw new NotSupportedException($"Bits por pixel não suportado: {bitsPerPixel}");
                    }

                    grayscale[y * width + x] = gray;
                }
            }

            return new RawImageData
            {
                Pixels = grayscale,
                Width = width,
                Height = height,
                Ppi = ppi
            };
        }

        /// <summary>
        /// Cria um BMP grayscale 8-bit a partir de dados raw
        /// </summary>
        public static byte[] CreateGrayscaleBmp(byte[] pixels, int width, int height, int ppi = 500)
        {
            // Calcular stride (alinhado em 4 bytes)
            int stride = (width + 3) & ~3;
            int padding = stride - width;
            int imageSize = stride * height;

            // Paleta grayscale: 256 cores × 4 bytes (BGRA)
            int paletteSize = 256 * 4;

            // Tamanho total do arquivo
            int headerSize = 14;      // BITMAPFILEHEADER
            int infoHeaderSize = 40;  // BITMAPINFOHEADER
            int dataOffset = headerSize + infoHeaderSize + paletteSize;
            int fileSize = dataOffset + imageSize;

            byte[] bmp = new byte[fileSize];

            // BITMAPFILEHEADER (14 bytes)
            bmp[0] = 0x42; // 'B'
            bmp[1] = 0x4D; // 'M'
            WriteInt32(bmp, 2, fileSize);
            WriteInt32(bmp, 10, dataOffset);

            // BITMAPINFOHEADER (40 bytes)
            WriteInt32(bmp, 14, infoHeaderSize);
            WriteInt32(bmp, 18, width);
            WriteInt32(bmp, 22, height);
            WriteInt16(bmp, 26, 1);       // planes
            WriteInt16(bmp, 28, 8);       // bits per pixel
            WriteInt32(bmp, 30, 0);       // compression (none)
            WriteInt32(bmp, 34, imageSize);

            // Resolução em pixels por metro
            int pelsPerMeter = (int)Math.Round(ppi * 39.3701);
            WriteInt32(bmp, 38, pelsPerMeter); // X
            WriteInt32(bmp, 42, pelsPerMeter); // Y

            WriteInt32(bmp, 46, 256);     // colors used
            WriteInt32(bmp, 50, 256);     // important colors

            // Paleta grayscale (256 entradas BGRA)
            int paletteOffset = headerSize + infoHeaderSize;
            for (int i = 0; i < 256; i++)
            {
                int offset = paletteOffset + (i * 4);
                bmp[offset] = (byte)i;     // B
                bmp[offset + 1] = (byte)i; // G
                bmp[offset + 2] = (byte)i; // R
                bmp[offset + 3] = 0;       // A (reserved)
            }

            // Dados da imagem (de baixo para cima)
            for (int y = 0; y < height; y++)
            {
                int srcY = height - 1 - y; // BMP é bottom-up
                int dstOffset = dataOffset + (y * stride);

                for (int x = 0; x < width; x++)
                {
                    bmp[dstOffset + x] = pixels[srcY * width + x];
                }
                // Padding já é zero (array inicializado com zeros)
            }

            return bmp;
        }

        private static void WriteInt16(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }
    }
}