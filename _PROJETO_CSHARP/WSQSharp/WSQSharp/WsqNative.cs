using System;
using System.IO;
using System.Runtime.InteropServices;

namespace WsqSharp
{
    public static class WsqNative
    {
        private static readonly string DLL_NAME;
        static WsqNative()
        {
            // Define o nome da DLL com base na arquitetura
            if (Environment.Is64BitProcess)
            {
                DLL_NAME = Path.Combine("x64", "nbis_wrapper.dll");
            }
            else
            {
                DLL_NAME = Path.Combine("x86", "nbis_wrapper.dll");
            }

            // Carrega a DLL manualmente
            LoadNativeLibrary();
        }

        private static void LoadNativeLibrary()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dllPath = Path.Combine(baseDir, DLL_NAME);

            if (!File.Exists(dllPath))
            {
                throw new FileNotFoundException($"DLL nativa não encontrada: {dllPath}");
            }

            // Carrega a DLL explicitamente
            IntPtr handle = LoadLibrary(dllPath);
            if (handle == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new Exception($"Falha ao carregar DLL: {dllPath}. Código de erro: {errorCode}");
            }
        }

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);


        [DllImport("nbis_wrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int wsq_encode_wrapper(
            out IntPtr odata,
            out int olen,
            float r_bitrate,
            byte[] idata,
            int w,
            int h,
            int d,
            int ppi,
            string comment_text);

        [DllImport("nbis_wrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int wsq_decode_wrapper(
            out IntPtr odata,
            out int ow,
            out int oh,
            out int od,
            out int oppi,
            out int lossyflag,
            byte[] idata,
            int ilen);

        [DllImport("nbis_wrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void wsq_free(IntPtr data);
    }

    public static class WsqConverter
    {
        /// <summary>
        /// Converte dados raw grayscale para WSQ
        /// </summary>
        public static byte[] EncodeRawToWsq(byte[] grayscalePixels, int width, int height, int ppi = 500, float bitrate = 0.75f)
        {
            if (grayscalePixels == null)
                throw new ArgumentNullException(nameof(grayscalePixels));

            if (grayscalePixels.Length != width * height)
                throw new ArgumentException($"Tamanho dos pixels ({grayscalePixels.Length}) não corresponde a {width}x{height}");

            int result = WsqNative.wsq_encode_wrapper(
                out IntPtr outputPtr,
                out int outputLen,
                bitrate,
                grayscalePixels,
                width,
                height,
                8,
                ppi,
                null);

            if (result != 0)
                throw new Exception($"Erro ao codificar WSQ. Código: {result}");

            try
            {
                byte[] wsqData = new byte[outputLen];
                Marshal.Copy(outputPtr, wsqData, 0, outputLen);
                return wsqData;
            }
            finally
            {
                WsqNative.wsq_free(outputPtr);
            }
        }

        /// <summary>
        /// Decodifica WSQ para dados raw grayscale
        /// </summary>
        public static RawImageData DecodeWsqToRaw(byte[] wsqData)
        {
            if (wsqData == null)
                throw new ArgumentNullException(nameof(wsqData));

            int result = WsqNative.wsq_decode_wrapper(
                out IntPtr outputPtr,
                out int width,
                out int height,
                out int depth,
                out int ppi,
                out int lossyFlag,
                wsqData,
                wsqData.Length);

            if (result != 0)
                throw new Exception($"Erro ao decodificar WSQ. Código: {result}");

            try
            {
                byte[] pixels = new byte[width * height];
                Marshal.Copy(outputPtr, pixels, 0, pixels.Length);

                return new RawImageData
                {
                    Pixels = pixels,
                    Width = width,
                    Height = height,
                    Ppi = ppi
                };
            }
            finally
            {
                WsqNative.wsq_free(outputPtr);
            }
        }

        /// <summary>
        /// Converte BMP (bytes) para WSQ
        /// </summary>
        public static byte[] ConvertBmpToWsq(byte[] bmpData, float bitrate = 0.75f)
        {
            RawImageData raw = BmpParser.ParseBmpAsGrayscale(bmpData);
            return EncodeRawToWsq(raw.Pixels, raw.Width, raw.Height, raw.Ppi, bitrate);
        }

        /// <summary>
        /// Converte WSQ para BMP (bytes)
        /// </summary>
        public static byte[] ConvertWsqToBmp(byte[] wsqData)
        {
            RawImageData raw = DecodeWsqToRaw(wsqData);
            return BmpParser.CreateGrayscaleBmp(raw.Pixels, raw.Width, raw.Height, raw.Ppi);
        }

        /// <summary>
        /// Converte arquivo BMP para arquivo WSQ
        /// </summary>
        public static void ConvertBmpFileToWsqFile(string bmpPath, string wsqPath, float bitrate = 0.75f)
        {
            byte[] bmpData = File.ReadAllBytes(bmpPath);
            byte[] wsqData = ConvertBmpToWsq(bmpData, bitrate);
            File.WriteAllBytes(wsqPath, wsqData);
        }

        /// <summary>
        /// Converte arquivo WSQ para arquivo BMP
        /// </summary>
        public static void ConvertWsqFileToBmpFile(string wsqPath, string bmpPath)
        {
            byte[] wsqData = File.ReadAllBytes(wsqPath);
            byte[] bmpData = ConvertWsqToBmp(wsqData);
            File.WriteAllBytes(bmpPath, bmpData);
        }
    }
}