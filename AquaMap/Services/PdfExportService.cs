using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AquaMap.Domain.Entities;
using Microsoft.Maui.Storage;

namespace AquaMap.Services
{
    public class PdfExportService
    {
        /// <summary>Writes the bulletin to the app cache (shareable, cleaned by the system) and returns its path.</summary>
        public Task<string> GenerateReservoirReportAsync(string reservoirName, IEnumerable<WaterAnalysis> history)
        {
            var report = WaterReport.Build(reservoirName, history, DateTime.Now);
            var filePath = Path.Combine(FileSystem.CacheDirectory, WaterReport.FileName(reservoirName, DateTime.Now));
#if ANDROID
            return Task.Run(() =>
            {
                WaterReportPdfWriter.Write(report, filePath);
                return filePath;
            });
#else
            return Task.FromException<string>(new PlatformNotSupportedException("O boletim em PDF só é gerado no Android."));
#endif
        }
    }
}
