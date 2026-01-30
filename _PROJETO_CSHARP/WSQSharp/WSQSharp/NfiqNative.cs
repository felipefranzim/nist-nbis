using System;
using System.IO;
using System.Runtime.InteropServices;

namespace WsqSharp
{
    public static class NfiqNative
    {
        private static readonly string DLL_NAME;
        static NfiqNative()
        {
            // Define o nome da DLL com base na arquitetura
            if (Environment.Is64BitProcess)
            {
                DLL_NAME = Path.Combine("x64", "wsq_nfiq_wrapper.dll");
            }
            else
            {
                DLL_NAME = Path.Combine("x86", "wsq_nfiq_wrapper.dll");
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

        /// <summary>
        /// Calcula NFIQ de pixels raw grayscale
        /// </summary>
        [DllImport("wsq_nfiq_wrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nfiq_compute_quality(
            out int nfiq_score,
            byte[] idata,
            int w,
            int h,
            int ppi);

        /// <summary>
        /// Versão simplificada - retorna o score diretamente
        /// </summary>
        [DllImport("wsq_nfiq_wrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nfiq_get_score(
            byte[] idata,
            int w,
            int h,
            int ppi);

        /// <summary>
        /// Calcula NFIQ de um WSQ em memória
        /// </summary>
        [DllImport("wsq_nfiq_wrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int nfiq_from_wsq_data(
            out int nfiq_score,
            byte[] wsq_data,
            int wsq_len);
    }

    public static class NfiqHelper
    {
        /// <summary>
        /// Calcula a qualidade NFIQ de uma imagem grayscale
        /// </summary>
        /// <returns>Score de 1 (melhor) a 5 (pior), ou -1 se erro</returns>
        public static int GetQuality(byte[] grayscalePixels, int width, int height, int ppi = 500)
        {
            return NfiqNative.nfiq_get_score(grayscalePixels, width, height, ppi);
        }

        /// <summary>
        /// Calcula a qualidade NFIQ de um arquivo WSQ
        /// </summary>
        /// <returns>Score de 1 (melhor) a 5 (pior)</returns>
        public static int GetQualityFromWsq(byte[] wsqData)
        {
            int score;
            int result = NfiqNative.nfiq_from_wsq_data(out score, wsqData, wsqData.Length);

            if (result != 0)
                throw new Exception($"Erro ao calcular NFIQ. Código: {result}");

            return score;
        }

        /// <summary>
        /// Retorna descrição textual do score NFIQ
        /// </summary>
        public static string GetQualityDescription(int nfiqScore)
        {
            switch (nfiqScore)
            {
                case 1: return "Excelente";
                case 2: return "Muito Boa";
                case 3: return "Boa";
                case 4: return "Regular";
                case 5: return "Ruim";
                default: return "Desconhecido";
            }
        }
    }
}