using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using WsqSharp;

namespace WSQSharp
{
    public static class MatcherNative
    {
        public const int MAX_MINUTIAE = 200;
        private static readonly string DLL_NAME;
        static MatcherNative()
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

        #region Minutiae Extraction

        [DllImport("nbis_wrapper", CallingConvention = CallingConvention.Cdecl)]
        public static extern int extract_minutiae(
            byte[] raw_data,
            int w,
            int h,
            int ppi,
            out int minutiae_count,
            [Out] int[] x_coords,
            [Out] int[] y_coords,
            [Out] int[] thetas,
            [Out] int[] qualities,
            int max_minutiae);

        [DllImport("nbis_wrapper", CallingConvention = CallingConvention.Cdecl)]
        public static extern int extract_minutiae_from_wsq(
            byte[] wsq_data,
            int wsq_len,
            out int minutiae_count,
            [Out] int[] x_coords,
            [Out] int[] y_coords,
            [Out] int[] thetas,
            [Out] int[] qualities,
            int max_minutiae);

        #endregion

        #region Bozorth3 Matching

        [DllImport("nbis_wrapper", CallingConvention = CallingConvention.Cdecl)]
        public static extern int bozorth_match(
            int probe_count,
            int[] probe_x,
            int[] probe_y,
            int[] probe_theta,
            int gallery_count,
            int[] gallery_x,
            int[] gallery_y,
            int[] gallery_theta);

        [DllImport("nbis_wrapper", CallingConvention = CallingConvention.Cdecl)]
        public static extern int match_fingerprints_raw(
            out int match_score,
            byte[] probe_data,
            int probe_w,
            int probe_h,
            int probe_ppi,
            byte[] gallery_data,
            int gallery_w,
            int gallery_h,
            int gallery_ppi);

        [DllImport("nbis_wrapper", CallingConvention = CallingConvention.Cdecl)]
        public static extern int match_fingerprints_wsq(
            out int match_score,
            byte[] probe_wsq,
            int probe_wsq_len,
            byte[] gallery_wsq,
            int gallery_wsq_len);
        #endregion
    }

    /// <summary>
    /// Represents a single minutia point
    /// </summary>
    public class Minutia
    {
        /// <summary>
        /// X coordinate (in pixels)
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Y coordinate (in pixels)
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Direction/orientation in degrees [0..360]
        /// </summary>
        public int Theta { get; set; }

        /// <summary>
        /// Quality/reliability score [0..100]
        /// </summary>
        public int Quality { get; set; }

        public override string ToString()
        {
            return $"Minutia(X={X}, Y={Y}, Theta={Theta}°, Quality={Quality})";
        }
    }

    /// <summary>
    /// Contains extracted minutiae data from a fingerprint
    /// </summary>
    public class MinutiaeData
    {
        /// <summary>
        /// Number of minutiae detected
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// List of detected minutiae points
        /// </summary>
        public List<Minutia> Minutiae { get; set; } = new List<Minutia>();

        /// <summary>
        /// Raw X coordinates array (for direct use with bozorth_match)
        /// </summary>
        public int[] XCoords { get; set; }

        /// <summary>
        /// Raw Y coordinates array (for direct use with bozorth_match)
        /// </summary>
        public int[] YCoords { get; set; }

        /// <summary>
        /// Raw theta/direction array (for direct use with bozorth_match)
        /// </summary>
        public int[] Thetas { get; set; }

        /// <summary>
        /// Raw quality array
        /// </summary>
        public int[] Qualities { get; set; }

        /// <summary>
        /// Average quality of all minutiae
        /// </summary>
        public double AverageQuality
        {
            get
            {
                if (Count == 0) return 0;
                double sum = 0;
                for (int i = 0; i < Count; i++)
                    sum += Qualities[i];
                return sum / Count;
            }
        }
    }


    /// <summary>
    /// High-level API for fingerprint minutiae extraction and matching
    /// </summary>
    public static class FingerprintMatcher
    {
        /// <summary>
        /// Default threshold for determining if two fingerprints match.
        /// Scores above this value indicate a likely match.
        /// </summary>
        public const int DefaultMatchThreshold = 40;

        #region Minutiae Extraction

        /// <summary>
        /// Extracts minutiae from a WSQ-encoded fingerprint image
        /// </summary>
        /// <param name="wsqData">WSQ image data</param>
        /// <returns>Extracted minutiae data</returns>
        /// <exception cref="NbisException">Thrown when extraction fails</exception>
        public static MinutiaeData ExtractMinutiae(byte[] wsqData)
        {
            if (wsqData == null || wsqData.Length == 0)
                throw new ArgumentNullException(nameof(wsqData));

            int[] xCoords = new int[MatcherNative.MAX_MINUTIAE];
            int[] yCoords = new int[MatcherNative.MAX_MINUTIAE];
            int[] thetas = new int[MatcherNative.MAX_MINUTIAE];
            int[] qualities = new int[MatcherNative.MAX_MINUTIAE];

            int result = MatcherNative.extract_minutiae_from_wsq(
                wsqData, wsqData.Length,
                out int count,
                xCoords, yCoords, thetas, qualities,
                MatcherNative.MAX_MINUTIAE);

            if (result != 0)
                throw new NbisException($"Failed to extract minutiae from WSQ. Error code: {result}", result);

            return BuildMinutiaeData(count, xCoords, yCoords, thetas, qualities);
        }

        /// <summary>
        /// Extracts minutiae from a raw grayscale fingerprint image
        /// </summary>
        /// <param name="rawData">Raw grayscale image data (8-bit)</param>
        /// <param name="width">Image width in pixels</param>
        /// <param name="height">Image height in pixels</param>
        /// <param name="ppi">Image resolution in pixels per inch (typically 500)</param>
        /// <returns>Extracted minutiae data</returns>
        /// <exception cref="NbisException">Thrown when extraction fails</exception>
        public static MinutiaeData ExtractMinutiae(byte[] rawData, int width, int height, int ppi = 500)
        {
            if (rawData == null || rawData.Length == 0)
                throw new ArgumentNullException(nameof(rawData));

            if (rawData.Length != width * height)
                throw new ArgumentException($"Raw data length ({rawData.Length}) does not match dimensions ({width}x{height}={width * height})");

            int[] xCoords = new int[MatcherNative.MAX_MINUTIAE];
            int[] yCoords = new int[MatcherNative.MAX_MINUTIAE];
            int[] thetas = new int[MatcherNative.MAX_MINUTIAE];
            int[] qualities = new int[MatcherNative.MAX_MINUTIAE];

            int result = MatcherNative.extract_minutiae(
                rawData, width, height, ppi,
                out int count,
                xCoords, yCoords, thetas, qualities,
                MatcherNative.MAX_MINUTIAE);

            if (result != 0)
                throw new NbisException($"Failed to extract minutiae from raw image. Error code: {result}", result);

            return BuildMinutiaeData(count, xCoords, yCoords, thetas, qualities);
        }

        /// <summary>
        /// Extracts minutiae from a WSQ file
        /// </summary>
        /// <param name="wsqFilePath">Path to the WSQ file</param>
        /// <returns>Extracted minutiae data</returns>
        public static MinutiaeData ExtractMinutiaeFromFile(string wsqFilePath)
        {
            if (!File.Exists(wsqFilePath))
                throw new FileNotFoundException("WSQ file not found", wsqFilePath);

            byte[] wsqData = File.ReadAllBytes(wsqFilePath);
            return ExtractMinutiae(wsqData);
        }

        private static MinutiaeData BuildMinutiaeData(int count, int[] xCoords, int[] yCoords, int[] thetas, int[] qualities)
        {
            var data = new MinutiaeData
            {
                Count = count,
                XCoords = new int[count],
                YCoords = new int[count],
                Thetas = new int[count],
                Qualities = new int[count]
            };

            Array.Copy(xCoords, data.XCoords, count);
            Array.Copy(yCoords, data.YCoords, count);
            Array.Copy(thetas, data.Thetas, count);
            Array.Copy(qualities, data.Qualities, count);

            for (int i = 0; i < count; i++)
            {
                data.Minutiae.Add(new Minutia
                {
                    X = xCoords[i],
                    Y = yCoords[i],
                    Theta = thetas[i],
                    Quality = qualities[i]
                });
            }

            return data;
        }

        #endregion

        #region Fingerprint Matching

        /// <summary>
        /// Compares two fingerprints using their extracted minutiae data.
        /// This is the most efficient method when comparing one probe against multiple gallery prints.
        /// </summary>
        /// <param name="probe">Minutiae data from the probe (query) fingerprint</param>
        /// <param name="gallery">Minutiae data from the gallery (enrolled) fingerprint</param>
        /// <returns>Match score (higher = more similar). Typical threshold: 40</returns>
        public static int Match(MinutiaeData probe, MinutiaeData gallery)
        {
            if (probe == null) throw new ArgumentNullException(nameof(probe));
            if (gallery == null) throw new ArgumentNullException(nameof(gallery));

            if (probe.Count == 0 || gallery.Count == 0)
                return 0;

            return MatcherNative.bozorth_match(
                probe.Count, probe.XCoords, probe.YCoords, probe.Thetas,
                gallery.Count, gallery.XCoords, gallery.YCoords, gallery.Thetas);
        }

        /// <summary>
        /// Compares two WSQ-encoded fingerprint images
        /// </summary>
        /// <param name="probeWsq">WSQ data of the probe fingerprint</param>
        /// <param name="galleryWsq">WSQ data of the gallery fingerprint</param>
        /// <returns>Match score (higher = more similar). Typical threshold: 40</returns>
        /// <exception cref="NbisException">Thrown when matching fails</exception>
        public static int MatchWsq(byte[] probeWsq, byte[] galleryWsq)
        {
            if (probeWsq == null || probeWsq.Length == 0)
                throw new ArgumentNullException(nameof(probeWsq));
            if (galleryWsq == null || galleryWsq.Length == 0)
                throw new ArgumentNullException(nameof(galleryWsq));

            int result = MatcherNative.match_fingerprints_wsq(
                out int matchScore,
                probeWsq, probeWsq.Length,
                galleryWsq, galleryWsq.Length);

            if (result != 0)
                throw new NbisException($"Failed to match WSQ fingerprints. Error code: {result}", result);

            return matchScore;
        }

        /// <summary>
        /// Compares two raw grayscale fingerprint images
        /// </summary>
        /// <param name="probeData">Raw grayscale data of the probe fingerprint</param>
        /// <param name="probeWidth">Probe image width</param>
        /// <param name="probeHeight">Probe image height</param>
        /// <param name="probePpi">Probe image resolution (PPI)</param>
        /// <param name="galleryData">Raw grayscale data of the gallery fingerprint</param>
        /// <param name="galleryWidth">Gallery image width</param>
        /// <param name="galleryHeight">Gallery image height</param>
        /// <param name="galleryPpi">Gallery image resolution (PPI)</param>
        /// <returns>Match score (higher = more similar). Typical threshold: 40</returns>
        /// <exception cref="NbisException">Thrown when matching fails</exception>
        public static int MatchRaw(
            byte[] probeData, int probeWidth, int probeHeight, int probePpi,
            byte[] galleryData, int galleryWidth, int galleryHeight, int galleryPpi)
        {
            if (probeData == null) throw new ArgumentNullException(nameof(probeData));
            if (galleryData == null) throw new ArgumentNullException(nameof(galleryData));

            int result = MatcherNative.match_fingerprints_raw(
                out int matchScore,
                probeData, probeWidth, probeHeight, probePpi,
                galleryData, galleryWidth, galleryHeight, galleryPpi);

            if (result != 0)
                throw new NbisException($"Failed to match raw fingerprints. Error code: {result}", result);

            return matchScore;
        }

        /// <summary>
        /// Compares two WSQ files
        /// </summary>
        /// <param name="probeFilePath">Path to the probe WSQ file</param>
        /// <param name="galleryFilePath">Path to the gallery WSQ file</param>
        /// <returns>Match score (higher = more similar). Typical threshold: 40</returns>
        public static int MatchFiles(string probeFilePath, string galleryFilePath)
        {
            if (!File.Exists(probeFilePath))
                throw new FileNotFoundException("Probe file not found", probeFilePath);
            if (!File.Exists(galleryFilePath))
                throw new FileNotFoundException("Gallery file not found", galleryFilePath);

            byte[] probeWsq = File.ReadAllBytes(probeFilePath);
            byte[] galleryWsq = File.ReadAllBytes(galleryFilePath);

            return MatchWsq(probeWsq, galleryWsq);
        }

        #endregion

        #region Verification (Boolean Match)

        /// <summary>
        /// Verifies if two fingerprints are from the same person
        /// </summary>
        /// <param name="probe">Minutiae data from the probe fingerprint</param>
        /// <param name="gallery">Minutiae data from the gallery fingerprint</param>
        /// <param name="threshold">Match threshold (default: 40)</param>
        /// <returns>True if fingerprints likely match</returns>
        public static bool Verify(MinutiaeData probe, MinutiaeData gallery, int threshold = DefaultMatchThreshold)
        {
            int score = Match(probe, gallery);
            return score >= threshold;
        }

        /// <summary>
        /// Verifies if two WSQ fingerprints are from the same person
        /// </summary>
        /// <param name="probeWsq">WSQ data of the probe fingerprint</param>
        /// <param name="galleryWsq">WSQ data of the gallery fingerprint</param>
        /// <param name="threshold">Match threshold (default: 40)</param>
        /// <returns>True if fingerprints likely match</returns>
        public static bool VerifyWsq(byte[] probeWsq, byte[] galleryWsq, int threshold = DefaultMatchThreshold)
        {
            int score = MatchWsq(probeWsq, galleryWsq);
            return score >= threshold;
        }

        /// <summary>
        /// Verifies if two WSQ files contain fingerprints from the same person
        /// </summary>
        /// <param name="probeFilePath">Path to the probe WSQ file</param>
        /// <param name="galleryFilePath">Path to the gallery WSQ file</param>
        /// <param name="threshold">Match threshold (default: 40)</param>
        /// <returns>True if fingerprints likely match</returns>
        public static bool VerifyFiles(string probeFilePath, string galleryFilePath, int threshold = DefaultMatchThreshold)
        {
            int score = MatchFiles(probeFilePath, galleryFilePath);
            return score >= threshold;
        }

        #endregion

        #region Match Result with Details

        /// <summary>
        /// Performs matching and returns detailed result
        /// </summary>
        /// <param name="probeWsq">WSQ data of the probe fingerprint</param>
        /// <param name="galleryWsq">WSQ data of the gallery fingerprint</param>
        /// <param name="threshold">Match threshold (default: 40)</param>
        /// <returns>Detailed match result</returns>
        public static MatchResult MatchWithDetails(byte[] probeWsq, byte[] galleryWsq, int threshold = DefaultMatchThreshold)
        {
            var probeMinutiae = ExtractMinutiae(probeWsq);
            var galleryMinutiae = ExtractMinutiae(galleryWsq);
            int score = Match(probeMinutiae, galleryMinutiae);

            return new MatchResult
            {
                Score = score,
                IsMatch = score >= threshold,
                Threshold = threshold,
                ProbeMinutiaeCount = probeMinutiae.Count,
                GalleryMinutiaeCount = galleryMinutiae.Count,
                ProbeAverageQuality = probeMinutiae.AverageQuality,
                GalleryAverageQuality = galleryMinutiae.AverageQuality
            };
        }

        #endregion
    }

    /// <summary>
    /// Detailed result of a fingerprint match operation
    /// </summary>
    public class MatchResult
    {
        /// <summary>
        /// Match score returned by Bozorth3 algorithm
        /// </summary>
        public int Score { get; set; }

        /// <summary>
        /// Whether the score exceeds the threshold
        /// </summary>
        public bool IsMatch { get; set; }

        /// <summary>
        /// Threshold used for the match decision
        /// </summary>
        public int Threshold { get; set; }

        /// <summary>
        /// Number of minutiae detected in the probe fingerprint
        /// </summary>
        public int ProbeMinutiaeCount { get; set; }

        /// <summary>
        /// Number of minutiae detected in the gallery fingerprint
        /// </summary>
        public int GalleryMinutiaeCount { get; set; }

        /// <summary>
        /// Average quality of probe minutiae
        /// </summary>
        public double ProbeAverageQuality { get; set; }

        /// <summary>
        /// Average quality of gallery minutiae
        /// </summary>
        public double GalleryAverageQuality { get; set; }

        public override string ToString()
        {
            return $"MatchResult: Score={Score}, IsMatch={IsMatch}, " +
                   $"ProbeMinutiae={ProbeMinutiaeCount}, GalleryMinutiae={GalleryMinutiaeCount}";
        }
    }

    /// <summary>
    /// Exception thrown by NBIS operations
    /// </summary>
    public class NbisException : Exception
    {
        public int ErrorCode { get; }

        public NbisException(string message, int errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }
    }

}
