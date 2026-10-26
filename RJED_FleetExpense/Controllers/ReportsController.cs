
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RJED_FleetExpense.Services;

namespace RJED_FleetExpense.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly PdfService _pdfService;

        public ReportsController(PdfService pdfService)
        {
            _pdfService = pdfService;
        }

        [HttpGet]
        public IActionResult ExportExpensesPdf()
        {
            byte[] pdfBytes = _pdfService.GenerateExpenseReport();

            return File(
                pdfBytes,
                "application/pdf",
                $"RJED_Expense_Report_{DateTime.Now:yyyyMMdd}.pdf"
            );
        }
    }
}
