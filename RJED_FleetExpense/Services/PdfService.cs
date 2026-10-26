using RJED_FleetExpense.Services;
using System;
using System.IO;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;

namespace RJED_FleetExpense.Services
{
    public class PdfService
    {
        public byte[] GenerateExpenseReport()
        {
            using var memoryStream = new MemoryStream();

            using (var writer = new PdfWriter(memoryStream))
            using (var pdf = new PdfDocument(writer))
            using (var document = new Document(pdf))
            {
                document.Add(
                    new Paragraph("RJED FLEET EXPENSE")
                        .SetFontSize(20)
                        .SetTextAlignment(TextAlignment.CENTER)
                );

                document.Add(
                    new Paragraph("Expense Report")
                        .SetFontSize(15)
                        .SetTextAlignment(TextAlignment.CENTER)
                );

                document.Add(
                    new Paragraph(
                        $"Generated: {DateTime.Now:MMMM dd, yyyy}"
                    )
                );

                var table = new Table(
                    UnitValue.CreatePercentArray(
                        new float[] { 2, 2, 2, 2 }
                    )
                ).UseAllAvailableWidth();

                table.AddHeaderCell("Expense Type");
                table.AddHeaderCell("Amount");
                table.AddHeaderCell("Date");
                table.AddHeaderCell("Status");

                table.AddCell("Fuel");
                table.AddCell("1500.00");
                table.AddCell("October 10, 2026");
                table.AddCell("Approved");

                document.Add(table);
            }

            return memoryStream.ToArray();
        }
    }
}
